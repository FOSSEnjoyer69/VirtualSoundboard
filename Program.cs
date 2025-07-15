namespace VirtualSoundboard;

using HttpMultipartParser;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpAlgorithms;
using CSharpAlgorithms.Audio;

public class Program
{
    private static Server m_server = new Server();

    private static AudioMixer audioMixer = new AudioMixer();
    private static AudioOutputDevice audioOutputDevice = new AudioOutputDevice(audioMixer.AudioBuffer);

    public static Dictionary<string, AudioPlayer> audioClipDic = new();

    public static async Task Main(string[] args)
    {
        m_server.AddIndexPage("WebUi/main.html");
        m_server.LinkFile("WebUi/style.css", "style.css");
        m_server.LinkFile("WebUi/script.js", "script.js");
        m_server.LinkFile("WebUi/bleep-button.js", "bleep-button.js");

        m_server.AddCustomPath("/api/input-devices", GetInputDevices);
        m_server.AddCustomPath("/api/output-devices", GetOutputDevices);
        m_server.AddCustomPath("/api/set-input-device", SetInputDevice);
        m_server.AddCustomPath("/api/set-output-device", SetOutputDevice);
        m_server.AddCustomPath("/api/play-beep", PlayBeep);
        m_server.AddCustomPath("/api/upload-sound", UploadSound);
        m_server.AddCustomPath("/api/play-sound", PlaySound);
        m_server.AddCustomPath("/api/get-sound-ids", GetSounds);

        await m_server.Start(port: 40605, open: false);
    }

    static async Task GetInputDevices(HttpListenerRequest request, HttpListenerResponse response)
    {
        string[] names = AudioUtils.GetInputDeviceNames();
        string json = JsonSerializer.Serialize(names);
        await Responses.SendJSONResponse(response, json);
    }

    static async Task GetOutputDevices(HttpListenerRequest request, HttpListenerResponse response)
    {
        string[] names = AudioUtils.GetOutputDeviceNames();
        string json = JsonSerializer.Serialize(names);
        await Responses.SendJSONResponse(response, json);
    }

    static async Task SetInputDevice(HttpListenerRequest request, HttpListenerResponse response)
    {
        StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        string? deviceName = json.RootElement.GetProperty("device").GetString();

        Console.WriteLine($"Setting input device to: {deviceName}");
    }

    static async Task SetOutputDevice(HttpListenerRequest request, HttpListenerResponse response)
    {
        StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        string? deviceName = json.RootElement.GetProperty("device").GetString();

        if (deviceName is not null)
        {
            //audioMixer.OutputDevice.SetOutput(AudioUtils.GetDeviceIndex(deviceName));
            Console.WriteLine($"Setting output device to: {deviceName}");
        }
    }

    static async Task PlayBeep(HttpListenerRequest request, HttpListenerResponse response)
    {
        StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        bool playBeep = json.RootElement.GetProperty("play").GetBoolean();
        //audioMixer.playBleep = playBeep;

        string reponseString = playBeep ? "Beep started" : "Beep stopped";

        await Responses.SendTextRespone(response, reponseString);
    }

    static async Task UploadSound(HttpListenerRequest request, HttpListenerResponse response)
    {
        // Only accept POST requests
        if (request.HttpMethod != "POST")
        {
            await Responses.SendNotPostRespone(response);
            return;
        }

        // Check that the request contains a body
        if (!request.HasEntityBody)
        {
            await Responses.SendNoEntityBodyRespone(response);
            return;
        }

        try
        {
            // Verify that the Content-Type is multipart/form-data
            if (string.IsNullOrEmpty(request.ContentType) ||
                !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                byte[] badReqBuffer = Encoding.UTF8.GetBytes("Invalid content type.");
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.ContentLength64 = badReqBuffer.Length;
                await response.OutputStream.WriteAsync(badReqBuffer, 0, badReqBuffer.Length);
                response.OutputStream.Close();
                return;
            }

            // Read the request body into a MemoryStream
            using var memoryStream = new MemoryStream();
            await request.InputStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Parse the multipart form-data request using HttpMultipartParser
            var parser = MultipartFormDataParser.Parse(memoryStream);

            // Assuming that the file is in a field named "file"
            var filePart = parser.Files.FirstOrDefault();
            if (filePart == null)
            {
                byte[] notFoundBuffer = Encoding.UTF8.GetBytes("No file found in the request.");
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.ContentLength64 = notFoundBuffer.Length;
                await response.OutputStream.WriteAsync(notFoundBuffer, 0, notFoundBuffer.Length);
                response.OutputStream.Close();
                return;
            }

            // Retrieve the file name and the file data
            string fileName = $"Audio Clips/{filePart.FileName}";
            using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            await filePart.Data.CopyToAsync(fileStream);

            FFMPegInterface.Resample(fileName, (int)audioOutputDevice.Info.defaultSampleRate);

            // Send a successful response
            byte[] buffer = Encoding.UTF8.GetBytes("Sound uploaded!");
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            // Log the error and send an error response
            Console.Error.WriteLine("Error during file upload: " + ex.Message);
            byte[] errorBuffer = Encoding.UTF8.GetBytes("Internal Server Error");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentLength64 = errorBuffer.Length;
            await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length);
            response.OutputStream.Close();
        }
    }

    static async Task GetSounds(HttpListenerRequest requst, HttpListenerResponse response)
    {
        string[] soundFilePaths = Directory.GetFiles("Audio Clips");
        for (int i = 0; i < soundFilePaths.Length; i++)
        {
            soundFilePaths[i] = soundFilePaths[i].Replace("Audio Clips/", "");
        }

        string json = JsonSerializer.Serialize(soundFilePaths);
        await Responses.SendJSONResponse(response, json);
    }

    static async Task PlaySound(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.HttpMethod != "POST")
        {
            response.StatusCode = (int)HttpStatusCode.MethodNotAllowed; // 405
            response.AddHeader("Allow", "POST");
            using var writer = new StreamWriter(response.OutputStream);
            writer.Write("Method Not Allowed");
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string soundName = await reader.ReadToEndAsync();

        string soundFile = $"Audio Clips/{soundName}";

        try
        {
            Console.WriteLine((int)audioMixer.OutputDevice.Info.defaultSampleRate);

            AudioClip audioClip = AudioClip.FromMP3File(soundFile, (int)audioMixer.OutputDevice.Info.defaultSampleRate);
            AudioPlayer audioPlayer = new AudioPlayer(audioClip, audioOutputDevice);
            audioPlayer.Play();

            byte[] buffer = Encoding.UTF8.GetBytes($"Sound played: {soundFile}");
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            byte[] buffer = Encoding.UTF8.GetBytes("Error playing sound.");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        finally
        {
            response.OutputStream.Close();
        }
    }
}
