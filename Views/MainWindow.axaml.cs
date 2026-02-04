#pragma warning disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using CSharpAlgorithms;
using CSharpAlgorithms.Audio;
using CSharpAlgorithms.Files;
using CSharpAlgorithms.GUI;
using CSharpAlgorithms.Interfaces;
using CSharpAlgorithms.UUID;
using PortAudioSharp;

namespace VirtualSoundboard.Views;

public partial class MainWindow : Window
{
    private static BleepPlayer bleepPlayer = new BleepPlayer();

    private static Dictionary<string, AudioClip> audioClipCache = [];
    //private static Dictionary<string, AudioDevice> deviceDic = [];
    private static Dictionary<string, AudioPlayer> audioPlayerDic = [];

    public MainWindow()
    {
        InitializeComponent();

        //new GCNotifier();

        //AudioDevice audioDevice = AudioDevice.CreateNewDevice();
        //AudioClip clip = AudioClip.FromMP3File("Audio Clips/Vine Boom Sound Effect.mp3").GetAwaiter().GetResult();
        //AudioPlayer player = new AudioPlayer(clip);
        //audioDevice.AddPlayer(player);
        //player.Play();

        DirectoryInfo audioClipDir = new DirectoryInfo("Audio Clips");
        LoadDirectory(audioClipDir);

        Closed += OnExit;

        void LoadDirectory(DirectoryInfo dir)
        {
            SoundBoard.Children.Clear();

            if (dir != audioClipDir)
            {
                Button previousDirButton = new Button
                {
                    Content = "<-",
                    ContextMenu = new ContextMenu(),
                    Margin = new Avalonia.Thickness(5),
                    Padding = new Avalonia.Thickness(10, 5),
                };
                previousDirButton.Click += (sender, e) => LoadDirectory(dir.Parent);

                SoundBoard.Children.Add(previousDirButton);
            }

            DirectoryInfo[] subDirs = dir.GetDirectories();
            FileInfo[] files = dir.GetFiles();

            foreach (DirectoryInfo subDir in subDirs)
            {
                Button dirButton = new Button
                {
                    Content = subDir.Name,
                    ContextMenu = new ContextMenu(),
                    Margin = new Avalonia.Thickness(5),
                    Padding = new Avalonia.Thickness(10, 5),
                };
                dirButton.Click += (sender, e) => LoadDirectory(subDir);

                SoundBoard.Children.Add(dirButton);
            }

            foreach (FileInfo file in files)
            {
                StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
                Label label = new Label { Content = file.Name, Margin = new Avalonia.Thickness(5), VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(label);

                Button playPauseButton = new Button
                {
                    Content = "▶",
                    ContextMenu = new ContextMenu(),
                    Margin = new Avalonia.Thickness(5),
                    Padding = new Avalonia.Thickness(10, 5),
                };
                panel.Children.Add(playPauseButton);

                playPauseButton.Click += async (sender, e) =>
                {
                    string soundId = file.FullName;

                    if (!audioClipCache.ContainsKey(soundId))
                        audioClipCache[soundId] = await AudioClip.FromMP3File(soundId);

                    AudioClip clip = audioClipCache[soundId];

                    CSharpAlgorithms.Debug.Print(AudioDevice.ActiveDevices);
                    foreach (AudioDevice device in DictionaryUtils.GetValues(AudioDevice.ActiveDevices))
                    {

                        string playerId = $"{device.Info.name} - {clip.Name}";
                        if (audioPlayerDic.TryGetValue(playerId, out AudioPlayer player))
                        {
                            player.Play();
                        }
                        else
                        {
                            AudioPlayer newPlayer = new AudioPlayer(clip);
                            newPlayer.OnStateChanged += (string state) =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                {
                                    if (state == "play")
                                        playPauseButton.Content = Text.PAUSE_SYMBOL;
                                    else if (state == "pause" || state == "end")
                                        playPauseButton.Content = Text.PLAY_SYMBOL;
                                    else
                                        playPauseButton.Content = "!";
                                });
                            };
                            audioPlayerDic[playerId] = newPlayer;

                            device.AddPlayer(newPlayer);
                            newPlayer.Play();
                        }
                    }
                };

                SoundBoard.Children.Add(panel);
            }
        }

        AudioDevice a = AddNewDevice("default");
        //AudioDevice b = AddNewDevice("USB Audio Device: - (hw:1,0)");

        //b.inputDevices.Add(a);
    }

    AudioDevice AddNewDevice(string name = "")
    {
        AudioDevice device = AudioDevice.GetDevice(name);

        ComboBox nameDropdown = new ComboBox()
        {
            SelectedValue = device.Info.name,
            ItemsSource = AudioUtils.GetDeviceNames(),
        };

        DeviceParent.Children.Add(new StackPanel
        {
            Children =
            {
                nameDropdown
            }
        });

        return device;
    }

    void AddNewDevice()
    {
        string uuid = UUIDUtils.CreateUUID();

        ComboBox deviceNameDropdown = new ComboBox();
        GUIUutils.Setup(deviceNameDropdown, AudioUtils.GetInputDeviceNames(), "default", null, (string newDeviceName) =>
        {

        });

        Slider volumeSlider = new Slider { Minimum = 0, Maximum = 1, Value = 1, Width = 100, Margin = new Avalonia.Thickness(2) };
        volumeSlider.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {

            }
        };

        CheckBox muteCheckbox = new CheckBox { Content = "Mute", Margin = new Avalonia.Thickness(2) };
        muteCheckbox.PropertyChanged += (sender, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
            {

            }
        };
    }

    private void OnExit(object? sender, EventArgs e)
    {
        //SaveState();
    }

    private void SaveState()
    {
        throw new NotImplementedException();
    }

    private void LoadState()
    {
        throw new NotImplementedException();
    }
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.FileNames)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        // optional cleanup
    }


    private void OnDrop(object? sender, DragEventArgs e)
    {
        Console.WriteLine("Drop fired!");
        if (e.Data.Contains(DataFormats.FileNames))
        {
            foreach (var f in e.Data.GetFileNames() ?? Array.Empty<string>())
                Console.WriteLine($"Dropped: {f}");
        }
    }
}

