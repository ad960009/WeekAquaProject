using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WeekAquaWPF.Models;
using WeekAquaWPF.Protocol;
using WeekAquaWPF.Services;

namespace WeekAquaWPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly BleService _bleService;
        private BleDeviceInfo? _selectedDevice;
        private bool _isScanning;
        private bool _isConnected;
        private string _connectionStatusText = "Disconnected";
        private double _powerKwh;

        // Manual Live Spectrum
        private double _redPercent = 50;
        private double _greenPercent = 50;
        private double _bluePercent = 50;
        private double _whitePercent = 50;
        private double _fanSpeedPercent = 50;
        private bool _autoSendLiveSpectrum = false; // Default unchecked

        private double _uvPercent = 0;
        private bool _hasUvChannel = false;

        private AppSettingsData _appSettings;

        public ObservableCollection<BleDeviceInfo> DiscoveredDevices { get; } = new ObservableCollection<BleDeviceInfo>();
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        public ObservableCollection<RampPointSlot> RampSlots { get; } = new ObservableCollection<RampPointSlot>();

        public BleDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
                HasUvChannel = _selectedDevice?.HasUvChannel ?? false;
                string modelCode = _selectedDevice?.ModelCode ?? string.Empty;
                foreach (var slot in RampSlots)
                {
                    slot.ModelCode = modelCode;
                }
                if (_selectedDevice != null)
                {
                    LoadDeviceConfig(_selectedDevice.MacAddress);
                }
            }
        }

        public bool HasUvChannel
        {
            get => _hasUvChannel;
            set { _hasUvChannel = value; OnPropertyChanged(); }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set { _connectionStatusText = value; OnPropertyChanged(); }
        }

        public double PowerKwh
        {
            get => _powerKwh;
            set { _powerKwh = value; OnPropertyChanged(); }
        }

        public string CurrentModelCode => SelectedDevice?.ModelCode ?? string.Empty;

        public double TotalPowerPercent => WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, UvPercent, CurrentModelCode);

        // Live Spectrum Colors with Max Power Limit (100%) Guard
        public double RedPercent
        {
            get => _redPercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(value, GreenPercent, BluePercent, WhitePercent, UvPercent, CurrentModelCode);
                if (total > 100.0)
                {
                    _redPercent = Math.Max(0, _redPercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). Red clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _redPercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(RedByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public double GreenPercent
        {
            get => _greenPercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, value, BluePercent, WhitePercent, UvPercent, CurrentModelCode);
                if (total > 100.0)
                {
                    _greenPercent = Math.Max(0, _greenPercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). Green clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _greenPercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(GreenByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public double BluePercent
        {
            get => _bluePercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, value, WhitePercent, UvPercent, CurrentModelCode);
                if (total > 100.0)
                {
                    _bluePercent = Math.Max(0, _bluePercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). Blue clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _bluePercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlueByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public double WhitePercent
        {
            get => _whitePercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, value, UvPercent, CurrentModelCode);
                if (total > 100.0)
                {
                    _whitePercent = Math.Max(0, _whitePercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). White clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _whitePercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(WhiteByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public double UvPercent
        {
            get => _uvPercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, value, CurrentModelCode);
                if (total > 100.0)
                {
                    _uvPercent = Math.Max(0, _uvPercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). UV clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _uvPercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(UvByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public byte UvByte => WeekAquaProtocol.PercentToByte(UvPercent);

        public double FanSpeedPercent
        {
            get => _fanSpeedPercent;
            set
            {
                _fanSpeedPercent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FanByte));
                if (_autoSendLiveSpectrum) SendFanSpeed();
            }
        }

        public bool AutoSendLiveSpectrum
        {
            get => _autoSendLiveSpectrum;
            set { _autoSendLiveSpectrum = value; OnPropertyChanged(); }
        }

        public byte RedByte => WeekAquaProtocol.PercentToByte(RedPercent);
        public byte GreenByte => WeekAquaProtocol.PercentToByte(GreenPercent);
        public byte BlueByte => WeekAquaProtocol.PercentToByte(BluePercent);
        public byte WhiteByte => WeekAquaProtocol.PercentToByte(WhitePercent);
        public byte FanByte => WeekAquaProtocol.PercentToByte(FanSpeedPercent);

        // Sunrise & Sunset Mode Properties
        private TimeSpan _sunriseStartTime = new TimeSpan(8, 0, 0);
        private TimeSpan _sunriseEndTime = new TimeSpan(18, 0, 0);
        private int _sunriseRampIndex = 2; // Default 1h (Index 2)

        public TimeSpan SunriseStartTime
        {
            get => _sunriseStartTime;
            set { _sunriseStartTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(SunriseStartStr)); }
        }

        public TimeSpan SunriseEndTime
        {
            get => _sunriseEndTime;
            set { _sunriseEndTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(SunriseEndStr)); }
        }

        public string SunriseStartStr
        {
            get => _sunriseStartTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out TimeSpan ts)) SunriseStartTime = ts; }
        }

        public string SunriseEndStr
        {
            get => _sunriseEndTime.ToString(@"hh\:mm");
            set { if (TimeSpan.TryParse(value, out TimeSpan ts)) SunriseEndTime = ts; }
        }

        public int SunriseRampIndex
        {
            get => _sunriseRampIndex;
            set { _sunriseRampIndex = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendLiveSpectrumCommand { get; }
        public ICommand SendFanSpeedCommand { get; }
        public ICommand SyncRtcTimeCommand { get; }
        public ICommand SelectModeCommand { get; }
        public ICommand ApplyPresetSpectrumCommand { get; }
        public ICommand SendSunriseSunsetCommand { get; }
        public ICommand SyncAllRampSlotsCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel()
        {
            _bleService = new BleService();
            _bleService.DeviceDiscovered += OnDeviceDiscovered;
            _bleService.ConnectionStateChanged += OnConnectionStateChanged;
            _bleService.DataReceived += OnDataReceived;
            _bleService.LogMessage += OnBleLogMessage;

            ScanCommand = new RelayCommand(StartScan);
            ConnectCommand = new RelayCommand(ConnectToSelectedDevice, () => SelectedDevice != null && !IsConnected);
            DisconnectCommand = new RelayCommand(DisconnectDevice, () => IsConnected);
            SendLiveSpectrumCommand = new RelayCommand(SendLiveSpectrum);
            SendFanSpeedCommand = new RelayCommand(SendFanSpeed);
            SyncRtcTimeCommand = new RelayCommand(SyncRtcTime);
            SelectModeCommand = new RelayCommand(param => SelectMode(param));
            ApplyPresetSpectrumCommand = new RelayCommand(param => ApplyPresetSpectrum(param));
            SendSunriseSunsetCommand = new RelayCommand(SendSunriseSunset);
            SyncAllRampSlotsCommand = new RelayCommand(SyncAllRampSlots);
            ClearLogCommand = new RelayCommand(ClearLog);

            _appSettings = SettingsManager.LoadSettings();

            InitializeRampSlots();
        }

        private void SendSunriseSunset()
        {
            byte[] packet = WeekAquaProtocol.BuildSunriseSunsetPacket(
                (byte)SunriseStartTime.Hours,
                (byte)SunriseStartTime.Minutes,
                (byte)SunriseEndTime.Hours,
                (byte)SunriseEndTime.Minutes,
                (byte)SunriseRampIndex,
                enabled: true
            );

            _bleService.EnqueueWritePacket(packet);
            string[] rampLabels = { "0h", "0.5h", "1h", "1.5h", "2h", "2.5h" };
            string label = SunriseRampIndex >= 0 && SunriseRampIndex < rampLabels.Length ? rampLabels[SunriseRampIndex] : $"{SunriseRampIndex}";
            AddLog(LogDirection.Info, $"Enqueued Sunrise/Sunset packet ({SunriseStartStr} ~ {SunriseEndStr}, Ramp: {label}).");
            SaveCurrentDeviceConfig();
        }

        private void ApplyPresetSpectrum(object? presetName)
        {
            if (presetName == null) return;
            string key = presetName.ToString() ?? "";
            (double R, double G, double B, double W) preset = key switch
            {
                "GreenGrass" => WeekAquaProtocol.Presets.GreenGrass,
                "RedGrass" => WeekAquaProtocol.Presets.RedGrass,
                "FishMixed" => WeekAquaProtocol.Presets.FishMixed,
                "CoralMarine" => WeekAquaProtocol.Presets.CoralMarine,
                "AlgaeMax" => WeekAquaProtocol.Presets.AlgaeMax,
                _ => (50, 50, 50, 50)
            };

            // 1. Apply to Live Manual Spectrum Controls
            RedPercent = preset.R;
            GreenPercent = preset.G;
            BluePercent = preset.B;
            WhitePercent = preset.W;

            // 2. Naturally scale preset color ratio across 12 Ramp Schedule Slots according to daily photoperiod intensity curve
            double[] dailyIntensityCurve = new double[]
            {
                0.20, // Slot 1: 🌅 Sunrise Start
                0.55, // Slot 2: 🌄 Morning Ramp Up
                0.85, // Slot 3: ☀️ Late Morning
                1.00, // Slot 4: ☀️ Noon Peak Sunlight
                1.00, // Slot 5: ☀️ Afternoon Peak Sunlight
                0.85, // Slot 6: 🌤️ Mid-Afternoon Ramp Down
                0.65, // Slot 7: 🌇 Sunset Start
                0.40, // Slot 8: 🌆 Golden Hour
                0.25, // Slot 9: 🌆 Twilight Dusk
                0.15, // Slot 10: 🌙 Moonlight Glow
                0.05, // Slot 11: 🌌 Dim Night Slope
                0.00  // Slot 12: 🌑 Total Darkness
            };

            for (int i = 0; i < RampSlots.Count && i < dailyIntensityCurve.Length; i++)
            {
                double factor = dailyIntensityCurve[i];
                var slot = RampSlots[i];
                slot.RedPercent = Math.Round(preset.R * factor, 1);
                slot.GreenPercent = Math.Round(preset.G * factor, 1);
                slot.BluePercent = Math.Round(preset.B * factor, 1);
                slot.WhitePercent = Math.Round(preset.W * factor, 1);
                slot.Validate();
            }

            if (IsConnected)
            {
                SendLiveSpectrum();
            }

            AddLog(LogDirection.Info, $"Applied '{key}' preset spectrum to Live control and 12 schedule slots naturally.");
            SaveCurrentDeviceConfig();
        }

        private void InitializeRampSlots()
        {
            RampSlots.Clear();

            // Natural 12-slot Sunrise/Sunset Cycle (All slots strictly Total Power <= 100.0% max limit)
            var defaultSlots = new (TimeSpan Start, TimeSpan End, double R, double G, double B, double W)[]
            {
                (new TimeSpan(8, 0, 0),   new TimeSpan(9, 0, 0),   20, 10, 15, 10), // 1: 🌅 Early Sunrise (20.9% Power)
                (new TimeSpan(9, 0, 0),   new TimeSpan(10, 0, 0),  45, 35, 40, 30), // 2: 🌄 Morning Ramp Up (56.4% Power)
                (new TimeSpan(10, 0, 0),  new TimeSpan(11, 30, 0), 65, 60, 65, 50), // 3: ☀️ Late Morning (89.9% Power)
                (new TimeSpan(11, 30, 0), new TimeSpan(13, 30, 0), 70, 65, 70, 55), // 4: ☀️ Noon Peak Sunlight (97.1% Power)
                (new TimeSpan(13, 30, 0), new TimeSpan(15, 30, 0), 70, 65, 70, 55), // 5: ☀️ Afternoon Peak (97.1% Power)
                (new TimeSpan(15, 30, 0), new TimeSpan(17, 0, 0),  65, 60, 65, 50), // 6: 🌤️ Mid-Afternoon Ramp Down (89.9% Power)
                (new TimeSpan(17, 0, 0),  new TimeSpan(18, 30, 0), 50, 40, 50, 35), // 7: 🌇 Sunset Start (66.3% Power)
                (new TimeSpan(18, 30, 0), new TimeSpan(19, 30, 0), 40, 20, 30, 15), // 8: 🌆 Golden Hour (41.4% Power)
                (new TimeSpan(19, 30, 0), new TimeSpan(20, 30, 0), 25, 10, 35, 5),  // 9: 🌆 Twilight Dusk (32.9% Power)
                (new TimeSpan(20, 30, 0), new TimeSpan(21, 30, 0), 5,  0,  30, 0),  // 10: 🌙 Moonlight Glow (17.9% Power)
                (new TimeSpan(21, 30, 0), new TimeSpan(22, 30, 0), 0,  0,  10, 0),  // 11: 🌌 Dim Night Slope (5.3% Power)
                (new TimeSpan(22, 30, 0), new TimeSpan(23, 59, 0), 0,  0,  0,  0)   // 12: 🌑 Total Darkness (0.0% Power)
            };

            for (int i = 0; i < defaultSlots.Length; i++)
            {
                var s = defaultSlots[i];
                var slot = new RampPointSlot
                {
                    PointId = i + 1,
                    IsEnabled = false, // All slots unchecked by default as requested
                    StartTime = s.Start,
                    EndTime = s.End,
                    RedPercent = s.R,
                    GreenPercent = s.G,
                    BluePercent = s.B,
                    WhitePercent = s.W
                };
                slot.Validate();
                RampSlots.Add(slot);
            }
        }

        public void LoadDeviceConfig(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress)) return;

            if (_appSettings.Devices.TryGetValue(macAddress, out var config))
            {
                _redPercent = config.RedPercent;
                _greenPercent = config.GreenPercent;
                _bluePercent = config.BluePercent;
                _whitePercent = config.WhitePercent;
                _fanSpeedPercent = config.FanSpeedPercent;
                _autoSendLiveSpectrum = config.AutoSendLiveSpectrum;
                SunriseStartStr = config.SunriseStartStr;
                SunriseEndStr = config.SunriseEndStr;
                SunriseRampIndex = config.SunriseRampIndex;

                OnPropertyChanged(nameof(RedPercent));
                OnPropertyChanged(nameof(GreenPercent));
                OnPropertyChanged(nameof(BluePercent));
                OnPropertyChanged(nameof(WhitePercent));
                OnPropertyChanged(nameof(FanSpeedPercent));
                OnPropertyChanged(nameof(AutoSendLiveSpectrum));
                OnPropertyChanged(nameof(RedByte));
                OnPropertyChanged(nameof(GreenByte));
                OnPropertyChanged(nameof(BlueByte));
                OnPropertyChanged(nameof(WhiteByte));
                OnPropertyChanged(nameof(FanByte));

                if (config.RampSlots != null && config.RampSlots.Count > 0)
                {
                    foreach (var slotConfig in config.RampSlots)
                    {
                        var target = RampSlots.FirstOrDefault(s => s.PointId == slotConfig.PointId);
                        if (target != null)
                        {
                            target.IsEnabled = slotConfig.IsEnabled;
                            target.StartTimeStr = slotConfig.StartTimeStr;
                            target.EndTimeStr = slotConfig.EndTimeStr;
                            target.RedPercent = slotConfig.RedPercent;
                            target.GreenPercent = slotConfig.GreenPercent;
                            target.BluePercent = slotConfig.BluePercent;
                            target.WhitePercent = slotConfig.WhitePercent;
                        }
                    }
                }

                AddLog(LogDirection.Info, $"Loaded saved JSON settings for device [{macAddress}].");
            }
        }

        public void SaveCurrentDeviceConfig()
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(SelectedDevice.MacAddress)) return;

            var config = new DeviceConfig
            {
                MacAddress = SelectedDevice.MacAddress,
                DeviceName = SelectedDevice.Name,
                RedPercent = RedPercent,
                GreenPercent = GreenPercent,
                BluePercent = BluePercent,
                WhitePercent = WhitePercent,
                FanSpeedPercent = FanSpeedPercent,
                AutoSendLiveSpectrum = AutoSendLiveSpectrum,
                SunriseStartStr = SunriseStartStr,
                SunriseEndStr = SunriseEndStr,
                SunriseRampIndex = SunriseRampIndex
            };

            foreach (var slot in RampSlots)
            {
                config.RampSlots.Add(new SlotConfig
                {
                    PointId = slot.PointId,
                    IsEnabled = slot.IsEnabled,
                    StartTimeStr = slot.StartTimeStr,
                    EndTimeStr = slot.EndTimeStr,
                    RedPercent = slot.RedPercent,
                    GreenPercent = slot.GreenPercent,
                    BluePercent = slot.BluePercent,
                    WhitePercent = slot.WhitePercent
                });
            }

            _appSettings.Devices[SelectedDevice.MacAddress] = config;
            _appSettings.LastConnectedMac = SelectedDevice.MacAddress;
            SettingsManager.SaveSettings(_appSettings);

            AddLog(LogDirection.Info, $"Saved settings to JSON for device [{SelectedDevice.MacAddress}].");
        }

        private void StartScan()
        {
            DiscoveredDevices.Clear();
            IsScanning = true;
            _bleService.StartScan();
        }

        private async void ConnectToSelectedDevice()
        {
            if (SelectedDevice == null) return;
            IsScanning = false;
            ConnectionStatusText = "Connecting...";
            await _bleService.ConnectAsync(SelectedDevice.BluetoothAddress);
        }

        private void DisconnectDevice()
        {
            _bleService.Disconnect();
        }

        private void OnSpectrumChanged()
        {
            if (AutoSendLiveSpectrum && IsConnected)
            {
                SendLiveSpectrum();
            }
        }

        private void SendLiveSpectrum()
        {
            byte[] packet = WeekAquaProtocol.BuildLiveSpectrumPacket(RedByte, GreenByte, BlueByte, WhiteByte);
            _bleService.EnqueueWritePacket(packet);
        }

        private void SendFanSpeed()
        {
            byte[] packet = WeekAquaProtocol.BuildFanSpeedPacket(FanByte);
            _bleService.EnqueueWritePacket(packet);
        }

        private void SyncRtcTime()
        {
            byte[] packet = WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now);
            _bleService.EnqueueWritePacket(packet);
            AddLog(LogDirection.Info, "Enqueued RTC Time Sync packet.");
        }

        private void SelectMode(object? modeParam)
        {
            if (modeParam != null && int.TryParse(modeParam.ToString(), out int modeId))
            {
                byte[] packet = WeekAquaProtocol.BuildModePacket(modeId);
                _bleService.EnqueueWritePacket(packet);
                AddLog(LogDirection.Info, $"Enqueued Mode {modeId} packet.");
            }
        }

        private void SyncAllRampSlots()
        {
            // Option 3: Batch Validation Check before transmission
            var invalidSlots = RampSlots.Where(s => s.HasErrors).ToList();
            if (invalidSlots.Any())
            {
                var errorSummary = string.Join("\n", invalidSlots.Select(s => $"• Slot #{s.PointId}: {s.FirstErrorMessage}"));
                AddLog(LogDirection.Error, $"Cannot send schedule! Validation errors found in {invalidSlots.Count} slot(s).");
                System.Windows.MessageBox.Show(
                    $"Cannot send schedule due to validation errors:\n\n{errorSummary}\n\nPlease correct the errors and try again.",
                    "Schedule Validation Warning",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            int enqueuedCount = 0;
            foreach (var slot in RampSlots)
            {
                // 1. Time Range Packet (FEF1 ~ FEFC)
                byte[] timePacket = WeekAquaProtocol.BuildRampTimePacket(
                    slot.PointId,
                    (byte)slot.StartTime.Hours,
                    (byte)slot.StartTime.Minutes,
                    (byte)slot.EndTime.Hours,
                    (byte)slot.EndTime.Minutes,
                    slot.IsEnabled
                );
                _bleService.EnqueueWritePacket(timePacket);

                // 2. Spectrum Packet (FBF1 ~ FBFC)
                if (slot.IsEnabled)
                {
                    byte[] spectrumPacket = WeekAquaProtocol.BuildRampSpectrumPacket(
                        slot.PointId,
                        slot.RedByte,
                        slot.GreenByte,
                        slot.BlueByte,
                        slot.WhiteByte
                    );
                    _bleService.EnqueueWritePacket(spectrumPacket);
                    enqueuedCount += 2;
                }
                else
                {
                    enqueuedCount += 1;
                }
            }

            AddLog(LogDirection.Info, $"Enqueued {enqueuedCount} schedule slot packets (Processing with 500ms queue delay).");
        }

        private void ClearLog()
        {
            LogEntries.Clear();
        }

        private void OnDeviceDiscovered(BleDeviceInfo device)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var d in DiscoveredDevices)
                {
                    if (d.BluetoothAddress == device.BluetoothAddress)
                    {
                        d.Rssi = device.Rssi;
                        return;
                    }
                }
                DiscoveredDevices.Add(device);
            });
        }

        private void OnConnectionStateChanged(bool connected, string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsConnected = connected;
                ConnectionStatusText = message;
                CommandManager.InvalidateRequerySuggested();
            });
        }

        private void OnDataReceived(byte[] data)
        {
            double kwh = WeekAquaProtocol.ParsePowerData(data);
            if (kwh > 0.0)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    PowerKwh = kwh;
                });
            }
        }

        private void OnBleLogMessage(LogDirection direction, string msg, byte[]? data)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                AddLog(direction, msg, data);
            });
        }

        private void AddLog(LogDirection direction, string message, byte[]? data = null)
        {
            string hexData = data != null ? WeekAquaProtocol.BytesToHexString(data, " ") : string.Empty;
            LogEntries.Insert(0, new LogEntry
            {
                Timestamp = DateTime.Now,
                Direction = direction,
                Message = message,
                HexData = hexData
            });

            // Keep log count under 200
            while (LogEntries.Count > 200)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _bleService.Dispose();
        }
    }
}
