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
                if (IsConnected && _selectedDevice != null && value != null && value != _selectedDevice)
                {
                    AddLog(LogDirection.Warning, $"Cannot switch device while connected. Please disconnect from '{_selectedDevice.Name}' first.");
                    OnPropertyChanged(nameof(SelectedDevice));
                    return;
                }

                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged();
                    string modelCode = _selectedDevice?.ModelCode ?? string.Empty;
                    Is4ChannelRgbUv = _selectedDevice?.Is4ChannelRgbUv ?? false;
                    HasUvChannel = _selectedDevice?.HasUvChannel ?? false;
                    Has6Channel = _selectedDevice?.Has6Channel ?? false;
                    OnPropertyChanged(nameof(CurrentModelCode));
                    OnPropertyChanged(nameof(Channel4Name));
                    OnPropertyChanged(nameof(Channel4Color));
                    OnPropertyChanged(nameof(Channel4Header));
                    OnPropertyChanged(nameof(Channel5Name));
                    OnPropertyChanged(nameof(Channel5Color));
                    OnPropertyChanged(nameof(Channel6Name));
                    OnPropertyChanged(nameof(Channel6Color));
                    OnPropertyChanged(nameof(ScheduleChannel4Header));
                    OnPropertyChanged(nameof(ScheduleChannel5Header));
                    OnPropertyChanged(nameof(ScheduleChannel6Header));
                    OnPropertyChanged(nameof(ScheduleSlotLimit));
                    OnPropertyChanged(nameof(ScheduleSlotLimitDescription));
                    OnPropertyChanged(nameof(AutoScheduleButtonText));
                    foreach (var slot in RampSlots)
                    {
                        slot.ModelCode = modelCode;
                        slot.IsUvEnabled = HasUvChannel;
                        slot.IsVioletEnabled = Has6Channel;
                    }
                    if (_selectedDevice != null)
                    {
                        LoadDeviceConfig(_selectedDevice.MacAddress);
                    }
                }
            }
        }

        public int ScheduleSlotLimit => SelectedDevice?.MaxScheduleSlots ?? 8;
        public string ScheduleSlotLimitDescription => SelectedDevice?.ScheduleSlotLimitDescription ?? "8 Slots (1~8)";
        public string AutoScheduleButtonText => $"⚡ Auto-Calculate {ScheduleSlotLimit}-Slot Schedule Times";

        private bool _is4ChannelRgbUv = false;

        public bool Is4ChannelRgbUv
        {
            get => _is4ChannelRgbUv;
            set
            {
                _is4ChannelRgbUv = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Channel4Name));
                OnPropertyChanged(nameof(Channel4Color));
                OnPropertyChanged(nameof(Channel4Header));
                OnPropertyChanged(nameof(Channel5Name));
                OnPropertyChanged(nameof(Channel5Color));
                OnPropertyChanged(nameof(ScheduleChannel4Header));
                OnPropertyChanged(nameof(ScheduleChannel5Header));
            }
        }

        public string Channel4Name => SelectedDevice?.Channel4Label ?? (Is4ChannelRgbUv ? "UV (Ultraviolet)" : "White");
        public string Channel4Color => SelectedDevice?.Channel4Color ?? (Is4ChannelRgbUv ? "#C084FC" : "#F4F4F5");
        public string Channel4Header => Is4ChannelRgbUv ? "UV Channel (395nm)" : "White Channel";

        public string Channel5Name => SelectedDevice?.Channel5Label ?? (Is4ChannelRgbUv ? "White (W)" : "UV/UVA");
        public string Channel5Color => SelectedDevice?.Channel5Color ?? (HasUvChannel ? "#8B5CF6" : "#71717A");

        public string Channel6Name => SelectedDevice?.Channel6Label ?? "Violet";
        public string Channel6Color => SelectedDevice?.Channel6Color ?? (Has6Channel ? "#EC4899" : "#71717A");

        public string ScheduleChannel4Header => Is4ChannelRgbUv ? "UV %" : "White %";
        public string ScheduleChannel5Header => Is4ChannelRgbUv ? "White %" : "UV/UVA %";
        public string ScheduleChannel6Header => "Violet/UV2 %";

        public bool HasUvChannel
        {
            get => _hasUvChannel;
            set
            {
                _hasUvChannel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Channel5Color));
                OnPropertyChanged(nameof(ScheduleChannel4Header));
                OnPropertyChanged(nameof(ScheduleChannel5Header));
                if (!_hasUvChannel) UvPercent = 0.0;
                foreach (var slot in RampSlots)
                {
                    slot.IsUvEnabled = _hasUvChannel;
                }
            }
        }

        private bool _has6Channel = false;

        public bool Has6Channel
        {
            get => _has6Channel;
            set
            {
                _has6Channel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Channel6Color));
                if (!_has6Channel) VioletPercent = 0.0;
                foreach (var slot in RampSlots)
                {
                    slot.IsVioletEnabled = _has6Channel;
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set { _isScanning = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotConnected));
                    OnPropertyChanged(nameof(CanSelectDevice));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsNotConnected => !IsConnected;
        public bool CanSelectDevice => !IsConnected;

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

        public double TotalPowerPercent => WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, UvPercent, VioletPercent, CurrentModelCode);

        // Live Spectrum Colors with Max Power Limit (100%) Guard
        public double RedPercent
        {
            get => _redPercent;
            set
            {
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(value, GreenPercent, BluePercent, WhitePercent, UvPercent, VioletPercent, CurrentModelCode);
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
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, value, BluePercent, WhitePercent, UvPercent, VioletPercent, CurrentModelCode);
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
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, value, WhitePercent, UvPercent, VioletPercent, CurrentModelCode);
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
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, value, UvPercent, VioletPercent, CurrentModelCode);
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
                double total = WeekAquaProtocol.CalculateTotalPowerPercent(RedPercent, GreenPercent, BluePercent, WhitePercent, value, VioletPercent, CurrentModelCode);
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

        private double _violetPercent = 0.0;

        public double VioletPercent
        {
            get => _violetPercent;
            set
            {
                if (!_has6Channel)
                {
                    _violetPercent = 0.0;
                }
                else if (TotalPowerPercent - _violetPercent + value > 100.0)
                {
                    _violetPercent = Math.Max(0, _violetPercent);
                    AddLog(LogDirection.Warning, $"Total power limit exceeded (100%). Violet/UV2 clamped for model [{CurrentModelCode}].");
                }
                else
                {
                    _violetPercent = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(VioletByte));
                OnPropertyChanged(nameof(TotalPowerPercent));
                OnSpectrumChanged();
            }
        }

        public byte VioletByte => WeekAquaProtocol.PercentToByte(VioletPercent);

        public void SetLiveSpectrum(double r, double g, double b, double w, double uv = 0.0, double violet = 0.0)
        {
            var norm = WeekAquaProtocol.NormalizeSpectrumToMaxPower(
                r,
                g,
                b,
                w,
                HasUvChannel ? uv : 0.0,
                Has6Channel ? violet : 0.0,
                CurrentModelCode
            );

            _redPercent = norm.R;
            _greenPercent = norm.G;
            _bluePercent = norm.B;
            _whitePercent = norm.W;
            _uvPercent = HasUvChannel ? norm.UV : 0.0;
            _violetPercent = Has6Channel ? norm.Violet : 0.0;

            OnPropertyChanged(nameof(RedPercent));
            OnPropertyChanged(nameof(GreenPercent));
            OnPropertyChanged(nameof(BluePercent));
            OnPropertyChanged(nameof(WhitePercent));
            OnPropertyChanged(nameof(UvPercent));
            OnPropertyChanged(nameof(VioletPercent));
            OnPropertyChanged(nameof(RedByte));
            OnPropertyChanged(nameof(GreenByte));
            OnPropertyChanged(nameof(BlueByte));
            OnPropertyChanged(nameof(WhiteByte));
            OnPropertyChanged(nameof(UvByte));
            OnPropertyChanged(nameof(VioletByte));
            OnPropertyChanged(nameof(TotalPowerPercent));
            OnSpectrumChanged();
        }

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
        private string _sunriseStartStr = "08:00";
        private string _sunriseEndStr = "18:00";
        private int _sunriseRampIndex = 2; // Default 1.0 Hour ramp

        public TimeSpan SunriseStartTime
        {
            get => _sunriseStartTime;
            set
            {
                _sunriseStartTime = new TimeSpan(value.Hours % 24, value.Minutes, 0);
                _sunriseStartStr = WeekAquaProtocol.FormatTimeString(_sunriseStartTime);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SunriseStartStr));
            }
        }

        public TimeSpan SunriseEndTime
        {
            get => _sunriseEndTime;
            set
            {
                _sunriseEndTime = new TimeSpan(value.Hours % 24, value.Minutes, 0);
                _sunriseEndStr = WeekAquaProtocol.FormatTimeString(_sunriseEndTime);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SunriseEndStr));
            }
        }

        public string SunriseStartStr
        {
            get => _sunriseStartStr;
            set
            {
                _sunriseStartStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    _sunriseStartTime = ts;
                    OnPropertyChanged(nameof(SunriseStartTime));
                }
            }
        }

        public string SunriseEndStr
        {
            get => _sunriseEndStr;
            set
            {
                _sunriseEndStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    _sunriseEndTime = ts;
                    OnPropertyChanged(nameof(SunriseEndTime));
                }
            }
        }

        public int SunriseRampIndex
        {
            get => _sunriseRampIndex;
            set { _sunriseRampIndex = value; OnPropertyChanged(); }
        }

        // Schedule Editor Quick Auto-Calculator Properties
        private TimeSpan _scheduleSunriseTime = new TimeSpan(8, 0, 0);
        private TimeSpan _scheduleSunsetTime = new TimeSpan(20, 0, 0);
        private string _scheduleSunriseStr = "08:00";
        private string _scheduleSunsetStr = "20:00";
        private bool _keepMoonlight = true;

        public bool KeepMoonlight
        {
            get => _keepMoonlight;
            set
            {
                _keepMoonlight = value;
                OnPropertyChanged();
                SaveCurrentDeviceConfig();
            }
        }

        public TimeSpan ScheduleSunriseTime
        {
            get => _scheduleSunriseTime;
            set
            {
                _scheduleSunriseTime = (value.TotalHours == 24.0) ? TimeSpan.Zero : new TimeSpan(value.Hours % 24, value.Minutes, 0);
                _scheduleSunriseStr = WeekAquaProtocol.FormatTimeString(_scheduleSunriseTime);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScheduleSunriseStr));
            }
        }

        public TimeSpan ScheduleSunsetTime
        {
            get => _scheduleSunsetTime;
            set
            {
                if (value.TotalHours == 24.0)
                    _scheduleSunsetTime = TimeSpan.FromHours(24);
                else
                    _scheduleSunsetTime = new TimeSpan(value.Hours % 24, value.Minutes, 0);
                _scheduleSunsetStr = WeekAquaProtocol.FormatTimeString(_scheduleSunsetTime);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScheduleSunsetStr));
            }
        }

        public string ScheduleSunriseStr
        {
            get => _scheduleSunriseStr;
            set
            {
                _scheduleSunriseStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    if (ts.TotalHours == 24.0) ts = TimeSpan.Zero;
                    _scheduleSunriseTime = ts;
                    _scheduleSunriseStr = WeekAquaProtocol.FormatTimeString(ts);
                    OnPropertyChanged(nameof(ScheduleSunriseTime));
                    OnPropertyChanged(nameof(ScheduleSunriseStr));
                }
                SaveCurrentDeviceConfig();
            }
        }

        public string ScheduleSunsetStr
        {
            get => _scheduleSunsetStr;
            set
            {
                _scheduleSunsetStr = value;
                OnPropertyChanged();
                if (WeekAquaProtocol.TryParseTimeString(value, out TimeSpan ts))
                {
                    if (ts == TimeSpan.Zero && _scheduleSunriseTime > TimeSpan.Zero)
                    {
                        ts = TimeSpan.FromHours(24);
                    }
                    _scheduleSunsetTime = ts;
                    _scheduleSunsetStr = WeekAquaProtocol.FormatTimeString(ts);
                    OnPropertyChanged(nameof(ScheduleSunsetTime));
                    OnPropertyChanged(nameof(ScheduleSunsetStr));
                }
                SaveCurrentDeviceConfig();
            }
        }

        // Commands
        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendLiveSpectrumCommand { get; }
        public ICommand SendFanSpeedCommand { get; }
        public ICommand SyncRtcTimeCommand { get; }
        public ICommand SyncRtcCommand => SyncRtcTimeCommand;
        public ICommand SelectModeCommand { get; }
        public ICommand ApplyPresetSpectrumCommand { get; }
        public ICommand ApplyPresetCommand => ApplyPresetSpectrumCommand;
        public ICommand SendSunriseSunsetCommand { get; }
        public ICommand SyncAllRampSlotsCommand { get; }
        public ICommand ApplyAutoScheduleTimesCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel()
        {
            _bleService = new BleService();
            _bleService.DeviceDiscovered += OnDeviceDiscovered;
            _bleService.ConnectionStateChanged += OnConnectionStateChanged;
            _bleService.DataReceived += OnDataReceived;
            _bleService.LogMessage += OnBleLogMessage;

            ScanCommand = new RelayCommand(StartScan, () => !IsConnected);
            ConnectCommand = new RelayCommand(ConnectToSelectedDevice, () => SelectedDevice != null && !IsConnected);
            DisconnectCommand = new RelayCommand(DisconnectDevice, () => IsConnected);
            SendLiveSpectrumCommand = new RelayCommand(() => SendLiveSpectrum(false));
            SendFanSpeedCommand = new RelayCommand(SendFanSpeed);
            SyncRtcTimeCommand = new RelayCommand(SyncRtcTime);
            SelectModeCommand = new RelayCommand(param => SelectMode(param));
            ApplyPresetSpectrumCommand = new RelayCommand(param => ApplyPresetSpectrum(param));
            SendSunriseSunsetCommand = new RelayCommand(SendSunriseSunset);
            SyncAllRampSlotsCommand = new RelayCommand(SyncAllRampSlots);
            ApplyAutoScheduleTimesCommand = new RelayCommand(ApplyAutoScheduleTimes);
            ClearLogCommand = new RelayCommand(ClearLog);

            _appSettings = SettingsManager.LoadSettings();

            if (!string.IsNullOrEmpty(_appSettings.DefaultScheduleSunriseStr))
            {
                _scheduleSunriseStr = _appSettings.DefaultScheduleSunriseStr;
                if (WeekAquaProtocol.TryParseTimeString(_scheduleSunriseStr, out TimeSpan ts))
                {
                    _scheduleSunriseTime = ts;
                }
            }
            if (!string.IsNullOrEmpty(_appSettings.DefaultScheduleSunsetStr))
            {
                _scheduleSunsetStr = _appSettings.DefaultScheduleSunsetStr;
                if (WeekAquaProtocol.TryParseTimeString(_scheduleSunsetStr, out TimeSpan ts))
                {
                    _scheduleSunsetTime = ts;
                }
            }
            _keepMoonlight = _appSettings.DefaultKeepMoonlight;

            InitializeRampSlots();
            AddVirtualDemoDevices();
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

            // Mandatory: Switch MCU to Mode 1 (Simple Sunrise/Sunset Mode)
            byte[] modePacket = WeekAquaProtocol.BuildModePacket(1);
            _bleService.EnqueueWritePacket(modePacket);

            string[] rampLabels = { "0h", "0.5h", "1h", "1.5h", "2h", "2.5h" };
            string label = SunriseRampIndex >= 0 && SunriseRampIndex < rampLabels.Length ? rampLabels[SunriseRampIndex] : $"{SunriseRampIndex}";
            AddLog(LogDirection.Info, $"Enqueued Sunrise/Sunset packet ({SunriseStartStr} ~ {SunriseEndStr}, Ramp: {label}) & activated Mode 1 (FDF1).");
            SaveCurrentDeviceConfig();
        }

        private void ApplyPresetSpectrum(object? presetName)
        {
            if (presetName == null) return;
            string key = presetName.ToString() ?? "";
            (double R, double G, double B, double W, double UV, double V) preset = key switch
            {
                "Green" or "GreenGrass" => WeekAquaProtocol.Presets.GreenGrass,
                "RedPlant" or "RedGrass" => WeekAquaProtocol.Presets.RedGrass,
                "Mixed" or "FishMixed" => WeekAquaProtocol.Presets.FishMixed,
                "Shrimp" => WeekAquaProtocol.Presets.Shrimp,
                "Fish" => WeekAquaProtocol.Presets.Fish,
                "Custom" => WeekAquaProtocol.Presets.Custom,
                "CoralAB" or "CoralAb" or "CoralMarine" => WeekAquaProtocol.Presets.CoralAb,
                "LPSCoral" or "CoralLps" => WeekAquaProtocol.Presets.CoralLps,
                "SPSCoral" or "CoralSps" => WeekAquaProtocol.Presets.CoralSps,
                "MarineFish" or "MarineFot" => WeekAquaProtocol.Presets.MarineFot,
                "DeepBlue" => WeekAquaProtocol.Presets.DeepBlue,
                "Moonlight" => WeekAquaProtocol.Presets.Moonlight,
                "AlgaeMax" => WeekAquaProtocol.Presets.AlgaeMax,
                _ => WeekAquaProtocol.Presets.GreenGrass
            };

            // 1. Set Live Manual Spectrum Controls atomically with normalized max power (<= 100%)
            SetLiveSpectrum(preset.R, preset.G, preset.B, preset.W, preset.UV, preset.V);

            // 2. Naturally scale preset color ratio across Ramp Schedule Slots (Intelligently mapped for 5, 8, or 12 slots)
            int maxSlots = ScheduleSlotLimit;
            double[] dailyIntensityCurve;

            if (maxSlots == 5)
            {
                // 5-Slot Layout (Mode 1 / 2)
                dailyIntensityCurve = new double[]
                {
                    0.25, // Slot 1: Sunrise (25%)
                    0.70, // Slot 2: Morning (70%)
                    1.00, // Slot 3: Noon Peak (100%)
                    0.35, // Slot 4: Sunset Finish (35% -> 0%)
                    0.00, // Slot 5: Night Rest / Moonlight
                    0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00 // Slots 6~12: Disabled
                };
            }
            else if (maxSlots == 12)
            {
                // 12-Slot Layout (Mode 3 / 5 / 9) - 11 Daytime slots + 1 Night slot
                dailyIntensityCurve = new double[]
                {
                    0.15, // Slot 1: Dawn (15%)
                    0.30, // Slot 2: Early Sunrise (30%)
                    0.50, // Slot 3: Morning Ramp Up 1 (50%)
                    0.70, // Slot 4: Morning Ramp Up 2 (70%)
                    0.85, // Slot 5: Mid Morning (85%)
                    1.00, // Slot 6: Noon Peak (100%)
                    0.85, // Slot 7: Early Afternoon (85%)
                    0.70, // Slot 8: Late Afternoon (70%)
                    0.50, // Slot 9: Early Sunset (50%)
                    0.30, // Slot 10: Sunset (30%)
                    0.15, // Slot 11: Dusk Finish (15%)
                    0.00  // Slot 12: Night Rest / Moonlight (0%)
                };
            }
            else
            {
                // 8-Slot Layout (Standard / M800 Pro / Safe for all models) - 7 Daytime slots + 1 Night slot
                dailyIntensityCurve = new double[]
                {
                    0.20, // Slot 1: Sunrise Start (20%)
                    0.50, // Slot 2: Morning Ramp Up 1 (50%)
                    0.75, // Slot 3: Morning Ramp Up 2 (75%)
                    1.00, // Slot 4: Noon Peak (100%)
                    0.75, // Slot 5: Afternoon Ramp Down 1 (75%)
                    0.50, // Slot 6: Sunset Ramp Down 2 (50%)
                    0.20, // Slot 7: Sunset Finish (20%)
                    0.00, // Slot 8: Night Rest / Moonlight (0%)
                    0.00, 0.00, 0.00, 0.00 // Slots 9~12: Disabled
                };
            }

            int nightSlotIndex = maxSlots - 1; // 0-based index for the night/moonlight slot

            for (int i = 0; i < RampSlots.Count && i < dailyIntensityCurve.Length; i++)
            {
                double factor = dailyIntensityCurve[i];
                var slot = RampSlots[i];

                if (i == nightSlotIndex)
                {
                    if (KeepMoonlight)
                    {
                        slot.IsEnabled = true;
                        slot.RedPercent = 0;
                        slot.GreenPercent = 0;
                        slot.BluePercent = 4.0;
                        slot.WhitePercent = 0;
                        slot.UvPercent = 0;
                        slot.VioletPercent = 0;
                    }
                    else
                    {
                        slot.IsEnabled = false;
                        slot.RedPercent = 0;
                        slot.GreenPercent = 0;
                        slot.BluePercent = 0;
                        slot.WhitePercent = 0;
                        slot.UvPercent = 0;
                        slot.VioletPercent = 0;
                    }
                }
                else if (i >= maxSlots)
                {
                    slot.IsEnabled = false;
                    slot.RedPercent = 0;
                    slot.GreenPercent = 0;
                    slot.BluePercent = 0;
                    slot.WhitePercent = 0;
                    slot.UvPercent = 0;
                    slot.VioletPercent = 0;
                }
                else
                {
                    var slotNorm = WeekAquaProtocol.NormalizeSpectrumToMaxPower(
                        preset.R * factor,
                        preset.G * factor,
                        preset.B * factor,
                        preset.W * factor,
                        HasUvChannel ? preset.UV * factor : 0.0,
                        Has6Channel ? preset.V * factor : 0.0,
                        CurrentModelCode
                    );

                    slot.IsEnabled = (slotNorm.R > 0 || slotNorm.G > 0 || slotNorm.B > 0 || slotNorm.W > 0 || slotNorm.UV > 0 || slotNorm.Violet > 0);
                    slot.RedPercent = slotNorm.R;
                    slot.GreenPercent = slotNorm.G;
                    slot.BluePercent = slotNorm.B;
                    slot.WhitePercent = slotNorm.W;
                    slot.UvPercent = slotNorm.UV;
                    slot.VioletPercent = slotNorm.Violet;
                }
                slot.Validate();
            }

            if (IsConnected)
            {
                SendLiveSpectrum();
            }

            AddLog(LogDirection.Info, $"Applied '{key}' preset spectrum to Live control and schedule slots (Moonlight: {(KeepMoonlight ? "ON" : "OFF")}).");
            SaveCurrentDeviceConfig();
        }

        private void InitializeRampSlots()
        {
            RampSlots.Clear();

            // Natural 8-slot Sunrise/Sunset Cycle (08:00 ~ 20:00 Photoperiod + 20:00 ~ 08:00 Night Rest)
            var defaultSlots = new (TimeSpan Start, TimeSpan End, double R, double G, double B, double W, bool Active)[]
            {
                (new TimeSpan(8, 0, 0),   new TimeSpan(9, 45, 0),  14, 13, 14, 11, false), // 1: 🌅 Sunrise Start (20%)
                (new TimeSpan(9, 45, 0),  new TimeSpan(11, 30, 0), 35, 32, 35, 28, false), // 2: 🌄 Morning Ramp Up 1 (50%)
                (new TimeSpan(11, 30, 0), new TimeSpan(13, 15, 0), 52, 49, 52, 41, false), // 3: ☀️ Morning Ramp Up 2 (75%)
                (new TimeSpan(13, 15, 0), new TimeSpan(14, 45, 0), 70, 65, 70, 55, false), // 4: ☀️ Noon Peak (100%)
                (new TimeSpan(14, 45, 0), new TimeSpan(16, 30, 0), 52, 49, 52, 41, false), // 5: 🌤️ Afternoon Ramp Down 1 (75%)
                (new TimeSpan(16, 30, 0), new TimeSpan(18, 15, 0), 35, 32, 35, 28, false), // 6: 🌇 Sunset Ramp Down 2 (50%)
                (new TimeSpan(18, 15, 0), new TimeSpan(20, 0, 0),  14, 13, 14, 11, false), // 7: 🌙 Sunset Finish (20%) -> Completes at 20:00!
                (new TimeSpan(20, 0, 0),  new TimeSpan(8, 0, 0),   0,  0,  4,  0,  false), // 8: 🌑 Night Rest / Moonlight (Covers 20:00 ~ 08:00)
                (new TimeSpan(0, 0, 0),   new TimeSpan(0, 0, 0),   0,  0,  0,  0,  false), // 9: (Disabled)
                (new TimeSpan(0, 0, 0),   new TimeSpan(0, 0, 0),   0,  0,  0,  0,  false), // 10: (Disabled)
                (new TimeSpan(0, 0, 0),   new TimeSpan(0, 0, 0),   0,  0,  0,  0,  false), // 11: (Disabled)
                (new TimeSpan(0, 0, 0),   new TimeSpan(0, 0, 0),   0,  0,  0,  0,  false)  // 12: (Disabled)
            };

            for (int i = 0; i < defaultSlots.Length; i++)
            {
                var s = defaultSlots[i];
                var slot = new RampPointSlot
                {
                    PointId = i + 1,
                    IsEnabled = s.Active,
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
                _uvPercent = config.UvPercent;
                _violetPercent = config.VioletPercent;
                _fanSpeedPercent = config.FanSpeedPercent;
                _autoSendLiveSpectrum = config.AutoSendLiveSpectrum;
                SunriseStartStr = config.SunriseStartStr;
                SunriseEndStr = config.SunriseEndStr;
                SunriseRampIndex = config.SunriseRampIndex;

                if (!string.IsNullOrEmpty(config.ScheduleSunriseStr))
                {
                    _scheduleSunriseStr = config.ScheduleSunriseStr;
                    if (WeekAquaProtocol.TryParseTimeString(_scheduleSunriseStr, out TimeSpan ts))
                    {
                        _scheduleSunriseTime = ts;
                        OnPropertyChanged(nameof(ScheduleSunriseTime));
                    }
                    OnPropertyChanged(nameof(ScheduleSunriseStr));
                }

                if (!string.IsNullOrEmpty(config.ScheduleSunsetStr))
                {
                    _scheduleSunsetStr = config.ScheduleSunsetStr;
                    if (WeekAquaProtocol.TryParseTimeString(_scheduleSunsetStr, out TimeSpan ts))
                    {
                        _scheduleSunsetTime = ts;
                        OnPropertyChanged(nameof(ScheduleSunsetTime));
                    }
                    OnPropertyChanged(nameof(ScheduleSunsetStr));
                }

                _keepMoonlight = config.KeepMoonlight;
                OnPropertyChanged(nameof(KeepMoonlight));

                OnPropertyChanged(nameof(RedPercent));
                OnPropertyChanged(nameof(GreenPercent));
                OnPropertyChanged(nameof(BluePercent));
                OnPropertyChanged(nameof(WhitePercent));
                OnPropertyChanged(nameof(UvPercent));
                OnPropertyChanged(nameof(VioletPercent));
                OnPropertyChanged(nameof(FanSpeedPercent));
                OnPropertyChanged(nameof(AutoSendLiveSpectrum));
                OnPropertyChanged(nameof(RedByte));
                OnPropertyChanged(nameof(GreenByte));
                OnPropertyChanged(nameof(BlueByte));
                OnPropertyChanged(nameof(WhiteByte));
                OnPropertyChanged(nameof(UvByte));
                OnPropertyChanged(nameof(VioletByte));
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
                            target.UvPercent = slotConfig.UvPercent;
                            target.VioletPercent = slotConfig.VioletPercent;
                        }
                    }
                }

                AddLog(LogDirection.Info, $"Loaded saved JSON settings for device [{macAddress}].");
            }
        }

        public void SaveCurrentDeviceConfig()
        {
            if (_appSettings == null) return;

            // Always update global defaults so settings survive app restarts even without active connection
            _appSettings.DefaultScheduleSunriseStr = ScheduleSunriseStr;
            _appSettings.DefaultScheduleSunsetStr = ScheduleSunsetStr;
            _appSettings.DefaultKeepMoonlight = KeepMoonlight;

            if (SelectedDevice != null && !string.IsNullOrWhiteSpace(SelectedDevice.MacAddress))
            {
                var config = new DeviceConfig
                {
                    MacAddress = SelectedDevice.MacAddress,
                    DeviceName = SelectedDevice.Name,
                    RedPercent = RedPercent,
                    GreenPercent = GreenPercent,
                    BluePercent = BluePercent,
                    WhitePercent = WhitePercent,
                    UvPercent = UvPercent,
                    VioletPercent = VioletPercent,
                    FanSpeedPercent = FanSpeedPercent,
                    AutoSendLiveSpectrum = AutoSendLiveSpectrum,
                    SunriseStartStr = SunriseStartStr,
                    SunriseEndStr = SunriseEndStr,
                    SunriseRampIndex = SunriseRampIndex,
                    ScheduleSunriseStr = ScheduleSunriseStr,
                    ScheduleSunsetStr = ScheduleSunsetStr,
                    KeepMoonlight = KeepMoonlight
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
                        WhitePercent = slot.WhitePercent,
                        UvPercent = slot.UvPercent,
                        VioletPercent = slot.VioletPercent
                    });
                }

                _appSettings.Devices[SelectedDevice.MacAddress] = config;
                _appSettings.LastConnectedMac = SelectedDevice.MacAddress;
            }

            SettingsManager.SaveSettings(_appSettings);
            AddLog(LogDirection.Info, "Saved device & schedule settings to JSON.");
        }

        private void StartScan()
        {
            DiscoveredDevices.Clear();
            AddVirtualDemoDevices();
            IsScanning = true;
            _bleService.StartScan();
        }

        private void AddVirtualDemoDevices()
        {
            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua M800 Pro [Virtual 4CH RGB/UV]",
                BluetoothAddress = 0xAABBCC112255,
                MacAddress = "AA:BB:CC:11:22:55",
                ModelCode = "5746",
                HasUvChannel = false,
                Is4ChannelRgbUv = true,
                Rssi = -42
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua L-Series [Virtual 4CH RGBW]",
                BluetoothAddress = 0xAABBCC112233,
                MacAddress = "AA:BB:CC:11:22:33",
                ModelCode = "5746",
                HasUvChannel = false,
                Is4ChannelRgbUv = false,
                Rssi = -45
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua T90_UV [Virtual 4CH RGB/UV]",
                BluetoothAddress = 0xAABBCC112244,
                MacAddress = "AA:BB:CC:11:22:44",
                ModelCode = "5746",
                HasUvChannel = false,
                Is4ChannelRgbUv = true,
                Rssi = -48
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua T90 Pro [Virtual 5CH+UV]",
                BluetoothAddress = 0xAABBCC223344,
                MacAddress = "AA:BB:CC:22:33:44",
                ModelCode = "5748",
                HasUvChannel = true,
                Rssi = -50
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua A-Series [Virtual 6CH+UV]",
                BluetoothAddress = 0xAABBCC334455,
                MacAddress = "AA:BB:CC:33:44:55",
                ModelCode = "5749",
                HasUvChannel = true,
                Rssi = -55
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua Marine Pro [Virtual 10CH Coral]",
                BluetoothAddress = 0xAABBCC445566,
                MacAddress = "AA:BB:CC:44:55:66",
                ModelCode = "5752",
                HasUvChannel = true,
                Rssi = -60
            });

            DiscoveredDevices.Add(new BleDeviceInfo
            {
                Name = "WeekAqua Smart Plug [Virtual Power Meter]",
                BluetoothAddress = 0xAABBCC556677,
                MacAddress = "AA:BB:CC:55:66:77",
                ModelCode = "5755",
                HasUvChannel = false,
                Rssi = -65
            });
        }

        private int _currentHardwareMode = 0;

        private async void ConnectToSelectedDevice()
        {
            if (SelectedDevice == null) return;
            _currentHardwareMode = 0;
            IsScanning = false;
            ConnectionStatusText = "Connecting...";
            await _bleService.ConnectAsync(SelectedDevice.BluetoothAddress);
        }

        private void DisconnectDevice()
        {
            _currentHardwareMode = 0;
            SaveCurrentDeviceConfig();
            _bleService.Disconnect();
        }

        private void OnSpectrumChanged()
        {
            if (AutoSendLiveSpectrum && IsConnected)
            {
                SendLiveSpectrum();
            }
        }

        private void SendLiveSpectrum(bool forceModeSequence = false)
        {
            byte ch4 = UvByte > 0 ? UvByte : WhiteByte;
            byte[] packet = Is4ChannelRgbUv
                ? new byte[] { 0xFB, 0xEF, RedByte, GreenByte, BlueByte, ch4, 0x55, 0x55 }
                : WeekAquaProtocol.BuildLiveSpectrumPacket(RedByte, GreenByte, BlueByte, WhiteByte, UvByte, VioletByte);

            if (_currentHardwareMode != 1 || forceModeSequence)
            {
                _currentHardwareMode = 1;
                var modePackets = WeekAquaProtocol.BuildLiveModeSequence(packet);
                foreach (var pkt in modePackets)
                {
                    string desc = WeekAquaProtocol.DescribePacket(pkt);
                    _bleService.EnqueueWritePacket(pkt, desc);
                }
                AddLog(LogDirection.Info, "Enqueued Mode 1 transition sequence for Live Manual Spectrum.");
            }
            else
            {
                string specDesc = WeekAquaProtocol.DescribePacket(packet);
                _bleService.EnqueueWritePacket(packet, specDesc);
            }

            SaveCurrentDeviceConfig();
        }

        private void SendFanSpeed()
        {
            byte[] packet = WeekAquaProtocol.BuildFanSpeedPacket(FanByte);
            _bleService.EnqueueWritePacket(packet, $"Fan Speed ({FanSpeedPercent}%)");
            SaveCurrentDeviceConfig();
        }

        private void SyncRtcTime()
        {
            byte[] packet = WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now);
            _bleService.EnqueueWritePacket(packet, "RTC Time Sync (FF)");
            AddLog(LogDirection.Info, "Enqueued RTC Time Sync packet.");
        }

        private void SelectMode(object? modeParam)
        {
            if (modeParam != null && int.TryParse(modeParam.ToString(), out int modeId))
            {
                // Synchronize corresponding spectrum preset and schedule slots
                string presetName = modeId switch
                {
                    1 => "Green",
                    2 => "RedPlant",
                    3 => "Mixed",
                    4 => "Fish",
                    _ => "Green"
                };
                ApplyPresetSpectrum(presetName);

                byte[] packet = WeekAquaProtocol.BuildModePacket(modeId);
                _bleService.EnqueueWritePacket(packet, $"Mode {modeId} Select (FD)");
                AddLog(LogDirection.Info, $"Enqueued Mode {modeId} packet ({presetName}).");
            }
        }

        private void SyncAllRampSlots()
        {
            int maxSlots = ScheduleSlotLimit;
            if (maxSlots < 1) maxSlots = 8;

            // Warn if user has enabled slots beyond device hardware limits
            var extraActiveSlots = RampSlots.Where(s => s.PointId > maxSlots && s.IsEnabled).ToList();
            if (extraActiveSlots.Any())
            {
                AddLog(LogDirection.Warning, $"Current device supports up to {maxSlots} slots. Slot(s) #{string.Join(", #", extraActiveSlots.Select(s => s.PointId))} exceed hardware limits and will not be transmitted.");
            }

            // Option 3: Batch Validation Check only for slots within hardware capability (Slot 1 ~ maxSlots)
            var invalidSlots = RampSlots.Where(s => s.PointId <= maxSlots && s.HasErrors).ToList();
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

            // 0. Prepend Real-time Clock RTC Sync Packet (0xFF) so MCU internal time matches PC time
            byte[] rtcPacket = WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now);
            _bleService.EnqueueWritePacket(rtcPacket, $"RTC Clock Sync ({DateTime.Now:HH:mm:ss})");
            enqueuedCount += 1;

            var targetSlots = RampSlots.Where(s => s.PointId <= maxSlots).ToList();
            foreach (var slot in targetSlots)
            {
                byte startH = (byte)slot.StartTime.TotalHours;
                byte startM = (byte)slot.StartTime.Minutes;
                byte endH = (byte)slot.EndTime.TotalHours;
                byte endM = (byte)slot.EndTime.Minutes;

                // 1. Time Range Packet (FEF1 ~ FEFC with BCD time)
                byte[] timePacket = WeekAquaProtocol.BuildRampTimePacket(
                    slot.PointId,
                    startH,
                    startM,
                    endH,
                    endM,
                    slot.IsEnabled
                );
                string timeDesc = slot.IsEnabled 
                    ? $"Slot #{slot.PointId} Time ({slot.StartTimeStr} ~ {slot.EndTimeStr})" 
                    : $"Slot #{slot.PointId} Time (Disabled / Clear)";
                _bleService.EnqueueWritePacket(timePacket, timeDesc);

                // 2. Spectrum Packet (FBF1 ~ FBFC)
                if (slot.IsEnabled)
                {
                    byte[] spectrumPacket = WeekAquaProtocol.BuildRampSpectrumPacket(
                        slot.PointId,
                        slot.RedByte,
                        slot.GreenByte,
                        slot.BlueByte,
                        slot.WhiteByte,
                        slot.UvByte,
                        slot.VioletByte
                    );
                    string specDesc = $"Slot #{slot.PointId} Spectrum (R:{slot.RedPercent}% G:{slot.GreenPercent}% B:{slot.BluePercent}% W:{slot.WhitePercent}%)";
                    _bleService.EnqueueWritePacket(spectrumPacket, specDesc);
                }
                else
                {
                    // Send 0 power spectrum for disabled slot to clear MCU slot state
                    byte[] clearSpectrumPacket = WeekAquaProtocol.BuildRampSpectrumPacket(
                        slot.PointId,
                        0, 0, 0, 0, 0, 0
                    );
                    _bleService.EnqueueWritePacket(clearSpectrumPacket, $"Slot #{slot.PointId} Spectrum (Clear 0W)");
                }
                enqueuedCount += 2;
            }

            // 3. Mandatory: Switch MCU to Mode 2 (Advanced Custom Ramp Schedule Mode)
            _currentHardwareMode = 2;
            byte[] modePacket = WeekAquaProtocol.BuildModePacket(2);
            _bleService.EnqueueWritePacket(modePacket, "Activate Mode 2 (FDF2)");
            enqueuedCount += 1;

            AddLog(LogDirection.Info, $"Enqueued {enqueuedCount} schedule slot packets (Slots 1~{maxSlots}) & Mode 2 (FDF2) for {SelectedDevice?.ChannelTypeDescription ?? $"{maxSlots}-Slot Device"}.");
            SaveCurrentDeviceConfig();
        }

        public static (TimeSpan Start, TimeSpan End)[] CalculateAutoSlotTimes(TimeSpan sunrise, TimeSpan sunset, int maxSlots)
        {
            var slotTimes = new (TimeSpan Start, TimeSpan End)[12];
            for (int i = 0; i < 12; i++)
            {
                slotTimes[i] = (TimeSpan.Zero, TimeSpan.Zero);
            }

            if (maxSlots < 1) maxSlots = 8;
            int daySlotCount = maxSlots - 1;
            int nightSlotIndex = maxSlots - 1;

            // Normalize sunrise midnight to 00:00
            TimeSpan t0 = sunrise.TotalHours == 24.0 ? TimeSpan.Zero : new TimeSpan(sunrise.Hours, sunrise.Minutes, 0);

            // Normalize sunset
            TimeSpan tSunset = sunset;
            if (tSunset == TimeSpan.Zero && t0 > TimeSpan.Zero)
            {
                tSunset = TimeSpan.FromHours(24);
            }
            else if (tSunset.TotalHours != 24.0)
            {
                tSunset = new TimeSpan(sunset.Hours, sunset.Minutes, 0);
            }

            bool isFullCycle = (t0 == tSunset || (t0 == TimeSpan.Zero && tSunset.TotalHours == 24.0));
            bool crossesMidnight = (!isFullCycle && tSunset.TotalHours < t0.TotalHours);

            if (isFullCycle)
            {
                // 24-Hour full day cycle
                double totalMinutes = 24.0 * 60.0;
                double step = totalMinutes / daySlotCount;
                for (int i = 0; i < daySlotCount; i++)
                {
                    double mStart = (t0.TotalMinutes + (i * step)) % (24.0 * 60.0);
                    double mEnd = (t0.TotalMinutes + ((i + 1) * step)) % (24.0 * 60.0);
                    TimeSpan s = MinutesToTimeSpan(mStart);
                    TimeSpan e = (i == daySlotCount - 1 && t0 == TimeSpan.Zero) ? TimeSpan.FromHours(24) : MinutesToTimeSpan(mEnd);
                    slotTimes[i] = (s, e);
                }
                slotTimes[nightSlotIndex] = (slotTimes[daySlotCount - 1].End == TimeSpan.FromHours(24) ? TimeSpan.Zero : slotTimes[daySlotCount - 1].End, t0);
            }
            else if (crossesMidnight)
            {
                // Photoperiod crosses midnight (e.g. 18:00 ~ 02:00)
                double minutesBefore = (24.0 * 60.0) - t0.TotalMinutes; // e.g. 18:00 -> 24:00 (360 mins)
                double minutesAfter = tSunset.TotalMinutes;             // e.g. 00:00 -> 02:00 (120 mins)
                double totalPhotoperiodMinutes = minutesBefore + minutesAfter;

                int slotsBefore = (int)Math.Round(daySlotCount * (minutesBefore / totalPhotoperiodMinutes));
                slotsBefore = Math.Clamp(slotsBefore, 1, daySlotCount - 1);
                int slotsAfter = daySlotCount - slotsBefore;

                // Day 1 Slots (Sunrise -> 24:00)
                double stepBefore = minutesBefore / slotsBefore;
                for (int i = 0; i < slotsBefore; i++)
                {
                    double mStart = t0.TotalMinutes + (i * stepBefore);
                    double mEnd = t0.TotalMinutes + ((i + 1) * stepBefore);
                    TimeSpan s = MinutesToTimeSpan(mStart);
                    TimeSpan e = (i == slotsBefore - 1) ? TimeSpan.FromHours(24) : MinutesToTimeSpan(mEnd);
                    slotTimes[i] = (s, e);
                }

                // Day 2 Slots (00:00 -> Sunset)
                double stepAfter = minutesAfter / slotsAfter;
                for (int j = 0; j < slotsAfter; j++)
                {
                    int slotIdx = slotsBefore + j;
                    double mStart = j * stepAfter;
                    double mEnd = (j + 1) * stepAfter;
                    TimeSpan s = (j == 0) ? TimeSpan.Zero : MinutesToTimeSpan(mStart);
                    TimeSpan e = (j == slotsAfter - 1) ? tSunset : MinutesToTimeSpan(mEnd);
                    slotTimes[slotIdx] = (s, e);
                }

                // Night Slot: Sunset -> Sunrise
                slotTimes[nightSlotIndex] = (tSunset, t0);
            }
            else
            {
                // Same-day photoperiod (e.g. 08:00 ~ 20:00 or 16:00 ~ 24:00)
                double photoperiodMinutes = (tSunset - t0).TotalMinutes;
                double step = photoperiodMinutes / daySlotCount;

                for (int i = 0; i < daySlotCount; i++)
                {
                    double mStart = t0.TotalMinutes + (i * step);
                    double mEnd = t0.TotalMinutes + ((i + 1) * step);
                    TimeSpan s = MinutesToTimeSpan(mStart);
                    TimeSpan e = (i == daySlotCount - 1) ? tSunset : MinutesToTimeSpan(mEnd);
                    slotTimes[i] = (s, e);
                }

                // Night Slot: Sunset -> Sunrise (if sunset is 24:00, night starts at 00:00)
                TimeSpan nightStart = (tSunset.TotalHours == 24.0) ? TimeSpan.Zero : tSunset;
                slotTimes[nightSlotIndex] = (nightStart, t0);
            }

            return slotTimes;
        }

        private static TimeSpan MinutesToTimeSpan(double totalMinutes)
        {
            double modMinutes = totalMinutes % (24.0 * 60.0);
            if (modMinutes < 0) modMinutes += 24.0 * 60.0;
            int h = (int)(modMinutes / 60.0) % 24;
            int m = (int)Math.Round(modMinutes % 60.0);
            if (m >= 60) { h = (h + 1) % 24; m = 0; }
            return new TimeSpan(h, m, 0);
        }

        private void ApplyAutoScheduleTimes()
        {
            if (WeekAquaProtocol.TryParseTimeString(ScheduleSunriseStr, out TimeSpan sunriseTime))
            {
                if (sunriseTime.TotalHours == 24.0) sunriseTime = TimeSpan.Zero;
                ScheduleSunriseTime = sunriseTime;
            }
            if (WeekAquaProtocol.TryParseTimeString(ScheduleSunsetStr, out TimeSpan sunsetTime))
            {
                if (sunsetTime == TimeSpan.Zero && ScheduleSunriseTime > TimeSpan.Zero)
                {
                    sunsetTime = TimeSpan.FromHours(24);
                }
                ScheduleSunsetTime = sunsetTime;
            }

            int maxSlots = ScheduleSlotLimit;
            var slotTimes = CalculateAutoSlotTimes(ScheduleSunriseTime, ScheduleSunsetTime, maxSlots);

            double totalHours;
            if (ScheduleSunriseTime == ScheduleSunsetTime || (ScheduleSunriseTime == TimeSpan.Zero && ScheduleSunsetTime.TotalHours == 24.0))
            {
                totalHours = 24.0;
            }
            else if (ScheduleSunsetTime > ScheduleSunriseTime)
            {
                totalHours = (ScheduleSunsetTime - ScheduleSunriseTime).TotalHours;
            }
            else
            {
                totalHours = (24.0 - ScheduleSunriseTime.TotalHours) + ScheduleSunsetTime.TotalHours;
            }

            int nightSlotIndex = maxSlots - 1;

            for (int i = 0; i < RampSlots.Count && i < slotTimes.Length; i++)
            {
                var times = slotTimes[i];
                var slot = RampSlots[i];
                slot.StartTime = times.Start;
                slot.EndTime = times.End;

                if (i == nightSlotIndex)
                {
                    if (KeepMoonlight)
                    {
                        slot.IsEnabled = true;
                        slot.RedPercent = 0;
                        slot.GreenPercent = 0;
                        slot.BluePercent = 4.0;
                        slot.WhitePercent = 0;
                        slot.UvPercent = 0;
                        slot.VioletPercent = 0;
                    }
                    else
                    {
                        slot.IsEnabled = false;
                        slot.RedPercent = 0;
                        slot.GreenPercent = 0;
                        slot.BluePercent = 0;
                        slot.WhitePercent = 0;
                        slot.UvPercent = 0;
                        slot.VioletPercent = 0;
                    }
                }
                else if (i >= maxSlots)
                {
                    slot.IsEnabled = false;
                    slot.RedPercent = 0;
                    slot.GreenPercent = 0;
                    slot.BluePercent = 0;
                    slot.WhitePercent = 0;
                    slot.UvPercent = 0;
                    slot.VioletPercent = 0;
                }
                else
                {
                    slot.IsEnabled = true;
                }

                slot.Validate();
            }

            string modeDesc = ScheduleSunriseTime == ScheduleSunsetTime ? "24-Hour Cycle" : $"{totalHours:F1}h Photoperiod";
            AddLog(LogDirection.Info, $"Auto-calculated {maxSlots}-slot schedule based on Sunrise ({ScheduleSunriseStr}) & Sunset ({ScheduleSunsetStr}) [{modeDesc}, Moonlight: {(KeepMoonlight ? "ON" : "OFF")}].");
            SaveCurrentDeviceConfig();
        }

        private void ClearLog()
        {
            LogEntries.Clear();
        }

        private void OnDeviceDiscovered(BleDeviceInfo device)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = DiscoveredDevices.FirstOrDefault(d => d.BluetoothAddress == device.BluetoothAddress);
                if (existing != null)
                {
                    existing.Rssi = device.Rssi;

                    // Update name if new valid name is discovered or if previous name was Unknown
                    if (!string.IsNullOrWhiteSpace(device.Name) && device.Name != "Unknown BLE Device")
                    {
                        if (string.IsNullOrWhiteSpace(existing.Name) || existing.Name == "Unknown BLE Device" || existing.Name != device.Name)
                        {
                            existing.Name = device.Name;
                        }
                    }

                    if (string.IsNullOrEmpty(existing.ModelCode) && !string.IsNullOrEmpty(device.ModelCode))
                    {
                        existing.ModelCode = device.ModelCode;
                    }

                    if (!existing.HasUvChannel && device.HasUvChannel)
                    {
                        existing.HasUvChannel = device.HasUvChannel;
                    }

                    if (!existing.Is4ChannelRgbUv && device.Is4ChannelRgbUv)
                    {
                        existing.Is4ChannelRgbUv = device.Is4ChannelRgbUv;
                    }
                }
                else
                {
                    DiscoveredDevices.Add(device);
                }
            });
        }

        private void OnConnectionStateChanged(bool connected, string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsConnected = connected;
                ConnectionStatusText = message;
                CommandManager.InvalidateRequerySuggested();

                if (connected && SelectedDevice != null)
                {
                    LoadDeviceConfig(SelectedDevice.MacAddress);
                    _appSettings.LastConnectedMac = SelectedDevice.MacAddress;
                    SettingsManager.SaveSettings(_appSettings);
                }
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
            SaveCurrentDeviceConfig();
            _bleService.Dispose();
        }
    }
}
