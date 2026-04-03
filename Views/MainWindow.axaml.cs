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

namespace VirtualSoundboard.Views;

public partial class MainWindow : Window
{
    private static BleepPlayer bleepPlayer = new BleepPlayer();

    static DirectoryInfo audioClipDir = new DirectoryInfo("Audio Clips");

    SoundBoard m_soundboard;
    SoundboardWebuiServer soundboardWebuiServer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        new GCNotifyer()
        {
            keepAlive = true,
        };

        //Ensure 'Audio Clips' directory exists
        if (!audioClipDir.Exists)
            audioClipDir.Create();


        m_soundboard = new SoundBoard(SoundBoard, audioClipDir);

        soundboardWebuiServer = new SoundboardWebuiServer(m_soundboard);
        soundboardWebuiServer.Start();

        LoadState();
        UpdateNewDeviceDropdown();

        Closed += OnExit;

        AddDeviceButton.Click += (sender, e) => AddNewDevice(NewDeviceDropdown.SelectedItem as string);
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

        if (AudioDevice.GetDevice(name, out AudioDevice device))
        {
            AddNewDevice(device);
        }
    }
    void AddNewDevice(AudioDevice device)
    {
        m_soundboard.outputAudioDevices.Add(device);

        ComboBox nameDropdown = new ComboBox()
        {
            Name = "name",
        };

        StackPanel optionsPanel = new StackPanel { Orientation = Orientation.Vertical };
        StackPanel inputOptionsPanel = new StackPanel
        {
            Name = "input-options-panel",
            Orientation = Orientation.Vertical
        };

        StackPanel outputOptionsPanel = new StackPanel
        {
            Name = "output-options-panel",
            Orientation = Orientation.Vertical
        };

        optionsPanel.Children.Add(inputOptionsPanel);
        optionsPanel.Children.Add(outputOptionsPanel);

        CheckBox enableInput = new CheckBox
        {
            Name = "enable-input",
            Content = "Enable Input",
            Margin = new Avalonia.Thickness(5),
        };

        inputOptionsPanel.Children.Add(enableInput);

        Slider inputVolumeSlider = new Slider
        {
            Name = "input-volume-slider",
            Minimum = 0,
            Maximum = 10,
            Margin = new Avalonia.Thickness(5)
        };
        inputOptionsPanel.Children.Add(inputVolumeSlider);

        CheckBox enableOutput = new CheckBox
        {
            Name = "enable-output",
            Content = "Enable Output",
            Margin = new Avalonia.Thickness(5),
        };
        outputOptionsPanel.Children.Add(enableOutput);

        StackPanel outputDevicesPanel = new StackPanel
        {
            Name = "output-devices-panel",
            Orientation = Orientation.Vertical
        };
        outputOptionsPanel.Children.Add(outputDevicesPanel);

        StackPanel devicePanel = new StackPanel
        {
            Name = device.Info.name,
            Classes = { "AudioDevice" },
            Children =
            {
                nameDropdown,
                optionsPanel,
            }
        };

        NameScope scope = new NameScope();
        NameScope.SetNameScope(devicePanel, scope);

        scope.Register("name", nameDropdown);
        scope.Register("input-options-panel", inputOptionsPanel);
        scope.Register("output-options-panel", outputOptionsPanel);
        scope.Register("enable-input", enableInput);
        scope.Register("enable-output", enableOutput);
        scope.Register("input-volume-slider", inputVolumeSlider);
        scope.Register("output-devices-panel", outputDevicesPanel);

        DeviceParent.Children.Add(devicePanel);
        SetDevice(devicePanel, device);
    }

    bool SetDevice(StackPanel devicePanel, AudioDevice device)
    {
        if (device is null)
        {
            Debug.WriteErrorLine("Device is null");
            return false;
        }

        ComboBox nameDropdown = devicePanel.FindControl<ComboBox>("name");
        nameDropdown.SelectedValue = device.Info.name;
        nameDropdown.ItemsSource = AudioUtils.GetDeviceNames();

        nameDropdown.SelectionChanged += (sender, e) =>
        {
            if (nameDropdown.SelectedValue is string selectedName)
            {
                device.SetDevice(selectedName);
                nameDropdown.SelectedValue = device.Info.name;
            }
        };

        StackPanel inputOptionsPanel = devicePanel.FindControl<StackPanel>("input-options-panel");
        StackPanel outputOptionsPanel = devicePanel.FindControl<StackPanel>("output-options-panel");

        CheckBox enableInput = devicePanel.FindControl<CheckBox>("enable-input");
        CheckBox enableOutput = devicePanel.FindControl<CheckBox>("enable-output");

        enableInput.IsChecked = device.IsInputMuted == false;
        enableOutput.IsChecked = device.IsOutputMuted == false;

        enableOutput.PropertyChanged += (sender, e) =>
        {
            device.IsOutputMuted = !enableOutput.IsChecked.GetValueOrDefault();
        };

        Slider inputVolumeSlider = devicePanel.FindControl<Slider>("input-volume-slider");
        inputVolumeSlider.Value = device.InputVolume;
        inputVolumeSlider.PropertyChanged += (sender, e) =>
        {
            device.SetInputVolume((float)inputVolumeSlider.Value);
        };

        StackPanel outputDevicesPanel = devicePanel.FindControl<StackPanel>("output-devices-panel");
        outputDevicesPanel.Children.Clear();

        UpdateDevices();

        return true;
    }

    private void UpdateDevices()
    {
        StackPanel[] devicePanels = DeviceParent.Children.OfType<StackPanel>().ToArray();
        foreach (StackPanel devicePanel in devicePanels)
        {
            StackPanel outputDevicesPanel = devicePanel.FindControl<StackPanel>("output-devices-panel");
            outputDevicesPanel.Children.Clear();

            foreach (AudioDevice device in AudioDevice.ActiveDevices.Values)
            {
                CheckBox sendAudioCheck = new CheckBox
                {
                    Content = $"Send input to {device.Info.name}",
                    Margin = new Avalonia.Thickness(5),
                    Padding = new Avalonia.Thickness(10, 5),
                };

                outputDevicesPanel.Children.Add(sendAudioCheck);

                sendAudioCheck.IsCheckedChanged += (sender, e) =>
                {
                    bool enable = sendAudioCheck.IsChecked.GetValueOrDefault();

                    if (device == AudioDevice.ActiveDevices[devicePanel.Name])
                    {
                        device.MonitorOwnInput = enable;
                    }
                    else
                    {
                        if (enable)
                            AudioDevice.ConnectDevices(AudioDevice.ActiveDevices[devicePanel.Name], device);
                        else
                            AudioDevice.DisconnectDevices(AudioDevice.ActiveDevices[devicePanel.Name], device);
                    }
                };
            }
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        SaveState();
    }

    private void SaveState()
    {
        AudioDeviceData.Save("devices.json");
    }

    private bool LoadState()
    {
        AudioDeviceData[] deviceDatas = AudioDeviceData.Load();
        if (deviceDatas.Length == 0)
            return false;

        foreach (AudioDeviceData deviceData in deviceDatas)
        {
            if (!AudioDevice.GetDevice(deviceData.Name, out AudioDevice device))
                continue;

            device.SetInputVolume(deviceData.InputVolume);
            device.SetOutputVolume(deviceData.OutputVolume);
            device.IsMuted = deviceData.IsMuted;

            AddNewDevice(device);
        }

        return true;
    }
}

