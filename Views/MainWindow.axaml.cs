#pragma warning disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using PortAudioSharp;

namespace VirtualSoundboard.Views;

public partial class MainWindow : Window
{
    private static BleepPlayer bleepPlayer = new BleepPlayer();

    private static Dictionary<string, AudioClip> audioClipCache = [];
    private static Dictionary<string, AudioPlayer> audioPlayerDic = [];

    static DirectoryInfo audioClipDir = new DirectoryInfo("Audio Clips");

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        new GCNotifyer()
        {
            keepAlive = true,
        };

        LoadDirectory(audioClipDir);
        UpdateNewDeviceDropdown();

        Closed += OnExit;

        //AddNewDevice("default");

        AddDeviceButton.Click += (sender, e) => AddNewDevice(NewDeviceDropdown.SelectedItem as string);
    }

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
                foreach (AudioDevice device in DictionaryUtils.GetValues(AudioDevice.ActiveDevices))
                {
                    AudioClip clip = null;

                    string soundId = $"{file.Name}:{device.Info.defaultSampleRate}";
                    if (audioClipCache.ContainsKey(soundId))
                        clip = audioClipCache[soundId];
                    else
                    {
                        clip = await AudioClip.FromMP3File(file.FullName, (int)device.Info.defaultSampleRate);
                        audioClipCache[soundId] = clip;
                    }


                    string playerId = $"{device.Info.name} - {clip.Name}";
                    if (audioPlayerDic.TryGetValue(playerId, out AudioPlayer player))
                    {
                        if (player.IsPlaying)
                        {
                            player.Pause();
                        }
                        else
                        {
                            Console.WriteLine($"playing {playerId}");
                            player.Play();
                        }
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

                        device.audioPlayers.Add(newPlayer);
                        newPlayer.Play();
                    }
                }
            };

            SoundBoard.Children.Add(panel);
        }
    }

    void UpdateNewDeviceDropdown()
    {
        string[] unusedDeviceNames = AudioDevice.GetNamesOfUnusedDevices();
        NewDeviceDropdown.ItemsSource = unusedDeviceNames;
        if (unusedDeviceNames.Length > 0)
            NewDeviceDropdown.SelectedIndex = 0;
    }

    void AddAllDevices()
    {
        string[] unusedDeviceNames = AudioDevice.GetNamesOfUnusedDevices();
        foreach (string name in unusedDeviceNames)
            AddNewDevice(name);
    }
    void AddNewDevice(string name = "")
    {
        if (string.IsNullOrEmpty(name))
            return;

        if (AudioDevice.ActiveDevices.ContainsKey(name))
            return;

        UpdateNewDeviceDropdown();

        if(AudioDevice.GetDevice(name, out AudioDevice device));
            AddNewDevice(device);
    }
    void AddNewDevice(AudioDevice device)
    {
        ComboBox nameDropdown = new ComboBox()
        {
            SelectedValue = device.Info.name,
            ItemsSource = AudioUtils.GetDeviceNames(),
        };

        nameDropdown.SelectionChanged += (sender, e) =>
        {
            if (nameDropdown.SelectedValue is string selectedName)
            {
                if(!device.SetDevice(selectedName));
                {
                    nameDropdown.SelectedValue = device.Info.name;
                }
            }
        };

        StackPanel optionsPanel = new StackPanel { Orientation = Orientation.Vertical };

        if (device.InputChannelCount > 0)
        {
            StackPanel inputOptionsPanel = new StackPanel { Orientation = Orientation.Vertical };
            optionsPanel.Children.Add(inputOptionsPanel);

            CheckBox enableInput = new CheckBox 
            { 
                Content = "Enable Input", 
                Margin = new Avalonia.Thickness(5), 
                IsChecked = device.IsInputMuted == false
            };

            inputOptionsPanel.Children.Add(enableInput);

            Slider inputVolumeSlider = new Slider 
            { 
                Minimum = 0, 
                Maximum = 10, 
                Value = device.InputVolume, 
                Margin = new Avalonia.Thickness(5) 
            };
            inputOptionsPanel.Children.Add(inputVolumeSlider);

            inputVolumeSlider.PropertyChanged += (sender, e) =>
            {
                device.SetInputVolume((float)inputVolumeSlider.Value);
            };
        }

        if (device.OutputChannelCount > 0)
        {
            StackPanel outputOptionsPanel = new StackPanel { Orientation = Orientation.Vertical };
            optionsPanel.Children.Add(outputOptionsPanel);

            CheckBox enableOutput = new CheckBox 
            { 
                Content = "Enable Output", 
                Margin = new Avalonia.Thickness(5), 
                IsChecked = device.IsOutputMuted == false 
            };
            enableOutput.PropertyChanged += (sender, e) =>
            {
                device.IsOutputMuted = !enableOutput.IsChecked.GetValueOrDefault();
            };
            outputOptionsPanel.Children.Add(enableOutput);

            if (device.InputChannelCount > 0)
            {
                CheckBox playbackInput = new CheckBox 
                { 
                    Content = "Playback Input", 
                    Margin = new Avalonia.Thickness(5), 
                    IsChecked = device.IsPlayingInput
                };
                playbackInput.PropertyChanged += (sender, e) =>
                {
                    device.IsPlayingInput = playbackInput.IsChecked.GetValueOrDefault();
                };

                outputOptionsPanel.Children.Add(playbackInput);
            }
        }

        ListBox inputDeviceListBox = new ListBox { Margin = new Avalonia.Thickness(5) };

        DeviceParent.Children.Add(new StackPanel
        {
            Children =
            {
                nameDropdown,
                optionsPanel,
                inputDeviceListBox
            }
        });
    }

    private void OnExit(object? sender, EventArgs e)
    {
        SaveState();
    }

    private void SaveState()
    {
        AudioDeviceData[] deviceDatas = AudioDeviceData.Get();
        AudioDeviceData.Save("devices.json", deviceDatas);
    }

    private bool LoadState()
    {
        AudioDeviceData[] deviceDatas = AudioDeviceData.Load();
        if (deviceDatas.Length == 0)
            return false;

        foreach (AudioDeviceData deviceData in deviceDatas)
        {
            if(!AudioDevice.GetDevice(deviceData.Name, out AudioDevice device))
                continue;

            device.SetInputVolume(deviceData.InputVolume);
            device.SetOutputVolume(deviceData.OutputVolume);
            device.IsMuted = deviceData.IsMuted;

            AddNewDevice(device);
        }

        return true;
    }
}

