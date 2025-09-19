#pragma warning disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using CSharpAlgorithms.Audio;
using CSharpAlgorithms.Files;
using CSharpAlgorithms.GUI;
using CSharpAlgorithms.Interfaces;
using CSharpAlgorithms.UUID;

namespace VirtualSoundboard.Views;

public partial class MainWindow : Window
{
    private static BleepPlayer bleepPlayer = new BleepPlayer();
    private static AudioMixer mixer = new AudioMixer();

    private static Dictionary<string, AudioPlayer> audioPlayerCacheDic = [];

    private static DispatcherTimer updateTimer;
    private static Stopwatch stopwatch;
    private static long lastTicks;


    public MainWindow()
    {
        InitializeComponent();

        AddInputDeviceButton.Click += (sender, e) => AddNewInputDevice();
        AddOutputDeviceButton.Click += (sender, e) => AddOutputDevice();

        GUIUutils.SetupButtonWrapPanel(SoundBoard, FileUtils.GetFileNames("Audio Clips"), async (string name) =>
        {
            Console.WriteLine($"Playing {name}");

            if (audioPlayerCacheDic.TryGetValue(name, out AudioPlayer player))
            {
                player.Play();
            }
            else
            {
                AudioClip clip = await AudioClip.FromMP3FileAsync($"Audio Clips/{name}");
                AudioPlayer newPlayer = new AudioPlayer(clip);
                audioPlayerCacheDic[name] = newPlayer;
                mixer.iReadMix.Add(newPlayer);
                newPlayer.Play();
            }
        });

        LoadState();

        Closed += OnExit;
    }

    void AddNewInputDevice()
    {
        string uuid = UUIDUtils.CreateUUID();

        ComboBox deviceNameDropdown = new ComboBox();
        GUIUutils.Setup(deviceNameDropdown, AudioUtils.GetInputDeviceNames(), "default", null, (string newDeviceName) =>
        {
            if (mixer.inputDevices.TryGetValue(uuid, out AudioInputDevice device))
                device.SetInput(newDeviceName);
        });

        Button removeButton = new Button { Content = "X", Margin = new Avalonia.Thickness(2), Padding = new Avalonia.Thickness(10, 5) };
        removeButton.Click += (sender, e) =>
        {
            if (mixer.inputDevices.TryGetValue(uuid, out AudioInputDevice device))
            {
                mixer.inputDevices.Remove(uuid);
                InputDeviceParent.Children.Remove((Control)removeButton.Parent.Parent);
                device.Dispose();
            }
        };

        Slider volumeSlider = new Slider { Minimum = 0, Maximum = 1, Value = 1, Width = 100, Margin = new Avalonia.Thickness(2) };
        volumeSlider.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                if (mixer.inputDevices.TryGetValue(uuid, out AudioInputDevice device))
                    device.Volume = (float)volumeSlider.Value;
            }
        };

        CheckBox muteCheckbox = new CheckBox { Content = "Mute", Margin = new Avalonia.Thickness(2) };
        muteCheckbox.PropertyChanged += (sender, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
            {
                if (mixer.inputDevices.TryGetValue(uuid, out AudioInputDevice device))
                    device.IsMuted = muteCheckbox.IsChecked ?? false;
            }
        };

        InputDeviceParent.Children.Add(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        deviceNameDropdown,
                        removeButton
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        volumeSlider,
                        muteCheckbox
                    },
                }
            }
        });

        AudioInputDevice device = new AudioInputDevice("default");

        mixer.inputDevices[uuid] = device;
    }

    void AddOutputDevice()
    {
        string uuid = UUIDUtils.CreateUUID();

        ComboBox deviceNameDropdown = new ComboBox();
        GUIUutils.Setup(deviceNameDropdown, AudioUtils.GetOutputDeviceNames(), "default", null, (string newDeviceName) =>
        {
            if (mixer.outputDevices.TryGetValue(uuid, out AudioOutputDevice device))
                device.SetOutput(newDeviceName);
        });

        Button removeButton = new Button { Content = "X", Margin = new Avalonia.Thickness(2), Padding = new Avalonia.Thickness(10, 5) };
        removeButton.Click += (sender, e) =>
        {
            if (mixer.outputDevices.TryGetValue(uuid, out AudioOutputDevice device))
            {
                mixer.outputDevices.Remove(uuid);
                OutputDeviceParent.Children.Remove((Control)removeButton.Parent.Parent);
                device.Dispose();
            }
        };

        Slider volumeSlider = new Slider { Minimum = 0, Maximum = 1, Value = 1, Width = 100, Margin = new Avalonia.Thickness(2) };
        volumeSlider.PropertyChanged += (sender, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                if (mixer.outputDevices.TryGetValue(uuid, out AudioOutputDevice device))
                    device.Volume = (float)volumeSlider.Value;
            }
        };

        CheckBox muteCheckbox = new CheckBox { Content = "Mute", Margin = new Avalonia.Thickness(2) };
        muteCheckbox.PropertyChanged += (sender, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
            {
                if (mixer.outputDevices.TryGetValue(uuid, out AudioOutputDevice device))
                    device.IsMuted = muteCheckbox.IsChecked ?? false;
            }
        };

        OutputDeviceParent.Children.Add(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        deviceNameDropdown,
                        removeButton
                    },
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        volumeSlider,
                        muteCheckbox
                    },
                }
            }
        });

        AudioOutputDevice device = new AudioOutputDevice("default", mixer);
        mixer.outputDevices[uuid] = device;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        SaveState();
    }

    private void SaveState()
    {
        AudioDeviceData.Save("devices.json", AudioDeviceData.FromMixer(mixer));
    }

    private void LoadState()
    {
        AudioDeviceData[] deviceDatas = AudioDeviceData.Load("devices.json");

        foreach (AudioDeviceData deviceData in deviceDatas)
        {
            if (deviceData.IsInput)
            {
                AddNewInputDevice();

                if (mixer.inputDevices.TryGetValue(DictionaryUtils.GetLastKey(mixer.inputDevices), out AudioInputDevice device))
                {
                    device.SetInput(deviceData.Name);
                    device.Volume = deviceData.Volume;
                    device.IsMuted = deviceData.IsMuted;
                }
            }
            else
            {
                AddOutputDevice();

                if (mixer.outputDevices.TryGetValue(DictionaryUtils.GetLastKey(mixer.outputDevices), out AudioOutputDevice device))
                {
                    device.SetOutput(deviceData.Name);
                    device.Volume = deviceData.Volume;
                    device.IsMuted = deviceData.IsMuted;
                }
            }
        }
    }
}

