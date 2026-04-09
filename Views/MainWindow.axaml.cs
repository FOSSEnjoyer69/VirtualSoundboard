#pragma warning disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using CSharpAlgorithms;
using CSharpAlgorithms.Audio;
using CSharpAlgorithms.Collection;
using CSharpAlgorithms.GC;
using CSharpAlgorithms.Interfaces;
using CSharpAlgorithms.Networking;
using Debug = CSharpAlgorithms.Debug;
using SoundFlow.Abstracts;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Providers;
using SoundFlow.Structs;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace VirtualSoundboard.Views;

public partial class MainWindow : Window
{
    private static BleepPlayer bleepPlayer = new BleepPlayer();

    static DirectoryInfo audioClipDir = new DirectoryInfo("Audio Clips");

    SoundBoard m_soundboard;
    SoundboardWebuiServer soundboardWebuiServer;

    MiniAudioEngine AudioEngine = new MiniAudioEngine();

    Dictionary<string, AudioCaptureDevice> captureDeviceDic = [];
    Dictionary<string, AudioPlaybackDevice> playbackDeviceDic = [];
    Dictionary<string, FullDuplexDevice> duplexDeviceDic = [];
    Dictionary<string, AudioBridge> audioBridgeDic = [];

    readonly DeviceConfig deviceConfig;

    private readonly IBrush normalSoundBoardParentBackground,
                            dragSoundbaordParentBackground = Brushes.LightBlue;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        SetupDeviceMenu(AudioEngine);

        //Ensure 'Audio Clips' directory exists
        if (!audioClipDir.Exists)
            audioClipDir.Create();

        m_soundboard = new SoundBoard(audioClipDir, SoundBoard, playbackDeviceDic, AudioEngine);

        soundboardWebuiServer = new SoundboardWebuiServer(m_soundboard);
        // soundboardWebuiServer.Start();

        LoadState();

        normalSoundBoardParentBackground = SoundboardParent.Background;

        Closed += OnExit;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        SaveState();
    }

    private void SaveState()
    {
        // AudioDeviceData.Save("devices.json");
    }

    private bool LoadState()
    {
        // AudioDeviceData[] deviceDatas = AudioDeviceData.Load();
        // if (deviceDatas.Length == 0)
        //     return false;

        // foreach (AudioDeviceData deviceData in deviceDatas)
        // {


        //     // device.SetInputVolume(deviceData.InputVolume);
        //     // device.SetOutputVolume(deviceData.OutputVolume);
        //     // device.IsMuted = deviceData.IsMuted;

        //     // AddNewDevice(device);
        // }


        return true;
    }

    private void SetupDeviceMenu(AudioEngine engine)
    {
        engine.UpdateAudioDevicesInfo();

        foreach (DeviceInfo deviceInfo in engine.CaptureDevices)
        {
            CreateDevicePanel(deviceInfo, CaptureDevicesParent, type: "capture");
        }

        foreach (DeviceInfo deviceInfo in engine.PlaybackDevices)
        {
            CreateDevicePanel(deviceInfo, PlaybackDevicesParent, type: "playback");
        }
    }

    private void CreateDevicePanel(DeviceInfo deviceInfo, StackPanel DeviceParent, string type = "capture")
    {
        StackPanel devicePanel = new StackPanel
        {
            Name = $"{deviceInfo.Name}:{type}",
            Classes = { "AudioDevice" },
            Margin = new Avalonia.Thickness(5),
        };

        TextBlock nameText = new TextBlock
        {
            Text = deviceInfo.Name,
        };
        devicePanel.Children.Add(nameText);

        switch (type)
        {
            case "capture":
                foreach (DeviceInfo playbackDeviceInfo in AudioEngine.PlaybackDevices)
                {
                    CheckBox sendAudioCheck = new CheckBox
                    {
                        Content = $"Send audio to {playbackDeviceInfo.Name}",
                        Margin = new Avalonia.Thickness(5),
                        Padding = new Avalonia.Thickness(10, 5),
                    };

                    sendAudioCheck.Click += (_, _) =>
                    {
                        AudioBridge bridge = null!;
                        string name = AudioBridge.GetName(deviceInfo, playbackDeviceInfo);

                        if (audioBridgeDic.TryGetValue(name, out bridge) == false)
                        {
                            AudioCaptureDevice captureDevice = GetAudioCaptureDevice(deviceInfo);
                            AudioPlaybackDevice playbackDevice = GetAudioPlaybackDevice(playbackDeviceInfo);

                            bridge = new AudioBridge(captureDevice, playbackDevice, AudioEngine);
                            audioBridgeDic[name] = bridge;
                        }

                        if (sendAudioCheck.IsChecked.Value)
                            bridge.Play();
                        else
                            bridge.Stop();


                    };

                    devicePanel.Children.Add(sendAudioCheck);
                }
                break;
            case "playback":
                AudioPlaybackDevice playbackDevice = GetAudioPlaybackDevice(deviceInfo);
                playbackDevice.Start();
                break;
        }

        // Add more controls for volume, mute, etc.

        DeviceParent.Children.Add(devicePanel);
    }

    private FullDuplexDevice GetFullDuplexDevice(DeviceInfo playbackDevice, DeviceInfo captureDevice)
    {
        FullDuplexDevice device = null!;
        string name = $"{captureDevice.Name}:{playbackDevice.Name}";

        if (duplexDeviceDic.TryGetValue(name, out device))
            return device;

        device = AudioEngine.InitializeFullDuplexDevice(playbackDevice, captureDevice, AudioFormat.DvdHq);
        duplexDeviceDic[name] = device;

        return device;
    }

    private AudioCaptureDevice GetAudioCaptureDevice(DeviceInfo captureDevice)
    {
        AudioCaptureDevice device = null!;
        string name = captureDevice.Name;

        if (captureDeviceDic.TryGetValue(name, out device))
            return device;

        device = AudioEngine.InitializeCaptureDevice(captureDevice, AudioFormat.DvdHq);
        captureDeviceDic[name] = device;

        return device;
    }

    private AudioPlaybackDevice GetAudioPlaybackDevice(DeviceInfo playbackDevice)
    {
        AudioPlaybackDevice device = null!;
        string name = playbackDevice.Name;

        if (playbackDeviceDic.TryGetValue(name, out device))
            return device;

        device = AudioEngine.InitializePlaybackDevice(playbackDevice, AudioFormat.DvdHq);
        playbackDeviceDic[name] = device;

        return device;
    }
}
