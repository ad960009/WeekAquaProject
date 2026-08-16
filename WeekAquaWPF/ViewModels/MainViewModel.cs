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
                string modelCode = _selectedDevice?.ModelCode ?? string.Empty;
                Is4ChannelRgbUv = _selectedDevice?.Is4ChannelRgbUv ?? false;
                HasUvChannel = _selectedDevice?.HasUvChannel ?? false;
                Has6Channel = _selectedDevice?.Has6Channel ?? false;
                OnPropertyChanged(nameof(Channel4Name));
                OnPropertyChanged(nameof(Channel4Color));
                OnPropertyChanged(nameof(Channel4Header));
                OnPropertyChanged(nameof(ScheduleChannel4Header));
                OnPropertyChanged(nameof(ScheduleChannel5Header));
                OnPropertyChanged(nameof(ScheduleChannel6Header));
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
                OnPropertyChanged(nameof(ScheduleChannel4Header));
                OnPropertyChanged(nameof(ScheduleChannel5Header));
            }
        }

        public string Channel4Name => Is4ChannelRgbUv ? "UV (4th Ch)" : "White";
        public string Channel4Color => Is4ChannelRgbUv ? "#C084FC" : "#F59E0B";
        public string Channel4Header => Is4ChannelRgbUv ? "UV Channel (395nm)" : "White Channel";

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
                _scheduleSunriseTime = new TimeSpan(value.Hours % 24, value.Minutes, 0);
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
                    _scheduleSunriseTime = ts;
                    OnPropertyChanged(nameof(ScheduleSunriseTime));
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
                    _scheduleSunsetTime = ts;
                    OnPropertyChanged(nameof(ScheduleSunsetTime));
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

            // 1. Normalize preset for Live Manual Spectrum Controls to ensure total power <= 100%
            var liveNorm = WeekAquaProtocol.NormalizeSpectrumToMaxPower(
                preset.R,
                preset.G,
                preset.B,
                preset.W,
                HasUvChannel ? preset.UV : 0.0,
                Has6Channel ? preset.V : 0.0,
                CurrentModelCode
            );

            RedPercent = liveNorm.R;
            GreenPercent = liveNorm.G;
            BluePercent = liveNorm.B;
            WhitePercent = liveNorm.W;
            UvPercent = liveNorm.UV;
            VioletPercent = liveNorm.Violet;

            // 2. Naturally scale preset color ratio across 8 Ramp Schedule Slots (Universal compatibility with all 4CH/5CH/6CH WeekAqua models)
            double[] dailyIntensityCurve = new double[]
            {
                0.20, // Slot 1: 🌅 Sunrise Start
                0.65, // Slot 2: 🌄 Morning Ramp Up
                1.00, // Slot 3: ☀️ Noon Peak 1
                1.00, // Slot 4: ☀️ Afternoon Peak 2 / Midnight
                0.75, // Slot 5: 🌤️ Afternoon Ramp Down
                0.45, // Slot 6: 🌇 Sunset Start
                0.15, // Slot 7: 🌙 Sunset Finish (Ends at SunsetTime)
                0.00, // Slot 8: 🌑 Night Rest / Moonlight (Covers rest of 24h)
                0.00, // Slot 9: Clear
                0.00, // Slot 10: Clear
                0.00, // Slot 11: Clear
                0.00  // Slot 12: Clear
            };

            for (int i = 0; i < RampSlots.Count && i < dailyIntensityCurve.Length; i++)
            {
                double factor = dailyIntensityCurve[i];
                var slot = RampSlots[i];

                if (i == 7) // Slot 8 (Night rest / Moonlight slot)
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
                else if (i >= 8) // Slot 9~12 (Disabled / Clear for 8-slot MCU compatibility)
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
                (new TimeSpan(8, 0, 0),   new TimeSpan(9, 30, 0),  20, 10, 15, 10, false), // 1: 🌅 Sunrise Start (20%)
                (new TimeSpan(9, 30, 0),  new TimeSpan(11, 20, 0), 50, 40, 50, 35, false), // 2: 🌄 Morning Ramp Up (65%)
                (new TimeSpan(11, 20, 0), new TimeSpan(14, 35, 0), 70, 65, 70, 55, false), // 3: ☀️ Noon Peak 1 (100%)
                (new TimeSpan(14, 35, 0), new TimeSpan(17, 0, 0),  70, 65, 70, 55, false), // 4: ☀️ Afternoon Peak 2 (100%)
                (new TimeSpan(17, 0, 0),  new TimeSpan(18, 35, 0), 60, 50, 60, 45, false), // 5: 🌤️ Afternoon Ramp Down (75%)
                (new TimeSpan(18, 35, 0), new TimeSpan(19, 30, 0), 40, 20, 30, 15, false), // 6: 🌇 Sunset Start (45%)
                (new TimeSpan(19, 30, 0), new TimeSpan(20, 0, 0),  10, 5,  15, 5,  false), // 7: 🌙 Sunset Finish (15%) -> Completes at 20:00!
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

        private async void ConnectToSelectedDevice()
        {
            if (SelectedDevice == null) return;
            IsScanning = false;
            ConnectionStatusText = "Connecting...";
            await _bleService.ConnectAsync(SelectedDevice.BluetoothAddress);
        }

        private void DisconnectDevice()
        {
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

        private void SendLiveSpectrum()
        {
            byte[] packet = WeekAquaProtocol.BuildLiveSpectrumPacket(RedByte, GreenByte, BlueByte, WhiteByte, UvByte, VioletByte);
            _bleService.EnqueueWritePacket(packet, "Live Spectrum (FBF9)");
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

            // 0. Prepend Real-time Clock RTC Sync Packet (0xFF) so MCU internal time matches PC time
            byte[] rtcPacket = WeekAquaProtocol.BuildRtcSyncPacket(DateTime.Now);
            _bleService.EnqueueWritePacket(rtcPacket, $"RTC Clock Sync ({DateTime.Now:HH:mm:ss})");
            enqueuedCount += 1;

            foreach (var slot in RampSlots)
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
            byte[] modePacket = WeekAquaProtocol.BuildModePacket(2);
            _bleService.EnqueueWritePacket(modePacket, "Activate Mode 2 (FDF2)");
            enqueuedCount += 1;

            AddLog(LogDirection.Info, $"Enqueued {enqueuedCount} schedule slot & Mode 2 (FDF2) packets (Processing with 500ms queue delay).");
            SaveCurrentDeviceConfig();
        }

        private static TimeSpan AddHoursMod24(TimeSpan baseTime, double hoursToAdd)
        {
            double baseMinutes = (baseTime.Hours * 60.0) + baseTime.Minutes;
            double totalMinutes = baseMinutes + (hoursToAdd * 60.0);
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
                ScheduleSunriseTime = sunriseTime;
            }
            if (WeekAquaProtocol.TryParseTimeString(ScheduleSunsetStr, out TimeSpan sunsetTime))
            {
                ScheduleSunsetTime = sunsetTime;
            }

            double totalHours;
            if (ScheduleSunriseTime == ScheduleSunsetTime)
            {
                // Identical times -> 24-hour full photoperiod cycle
                totalHours = 24.0;
            }
            else if (ScheduleSunsetTime > ScheduleSunriseTime)
            {
                // Same-day photoperiod (e.g., 08:00 to 20:00)
                totalHours = (ScheduleSunsetTime - ScheduleSunriseTime).TotalHours;
            }
            else
            {
                // Midnight-crossing photoperiod (e.g., 18:00 to 02:00)
                totalHours = 24.0 - (ScheduleSunriseTime - ScheduleSunsetTime).TotalHours;
            }

            // Continuous 24-hour cycle layout without artificial 00:00 / 24:00 cuts
            TimeSpan t0 = new TimeSpan(ScheduleSunriseTime.Hours, ScheduleSunriseTime.Minutes, 0);
            TimeSpan tSunset = new TimeSpan(ScheduleSunsetTime.Hours, ScheduleSunsetTime.Minutes, 0);

            double r1 = totalHours * 0.12;
            double r2 = totalHours * 0.28;
            double r3 = totalHours * 0.55;
            double r4 = totalHours * 0.75;
            double r5 = totalHours * 0.88;
            double r6 = totalHours * 0.96;

            var slotTimes = new (TimeSpan Start, TimeSpan End)[]
            {
                (t0, AddHoursMod24(t0, r1)),                                        // Slot 1: 🌅 Sunrise (20%)
                (AddHoursMod24(t0, r1), AddHoursMod24(t0, r2)),                     // Slot 2: 🌄 Morning Ramp (65%)
                (AddHoursMod24(t0, r2), AddHoursMod24(t0, r3)),                     // Slot 3: ☀️ Noon Peak 1 (100%)
                (AddHoursMod24(t0, r3), AddHoursMod24(t0, r4)),                     // Slot 4: ☀️ Afternoon Peak 2 (100%)
                (AddHoursMod24(t0, r4), AddHoursMod24(t0, r5)),                     // Slot 5: 🌤️ Afternoon Ramp Down (75%)
                (AddHoursMod24(t0, r5), AddHoursMod24(t0, r6)),                     // Slot 6: 🌇 Sunset Start (45%)
                (AddHoursMod24(t0, r6), tSunset),                                   // Slot 7: 🌙 Sunset Finish (15%) -> Completes at SunsetTime!
                (tSunset, t0),                                                      // Slot 8: 🌑 Night Rest / Moonlight (Covers rest of 24h)
                (TimeSpan.Zero, TimeSpan.Zero),                                     // Slot 9: Clear
                (TimeSpan.Zero, TimeSpan.Zero),                                     // Slot 10: Clear
                (TimeSpan.Zero, TimeSpan.Zero),                                     // Slot 11: Clear
                (TimeSpan.Zero, TimeSpan.Zero)                                      // Slot 12: Clear
            };

            for (int i = 0; i < RampSlots.Count && i < slotTimes.Length; i++)
            {
                var times = slotTimes[i];
                var slot = RampSlots[i];
                slot.StartTime = times.Start;
                slot.EndTime = times.End;

                if (i == 7) // Slot 8 (Night / Moonlight slot)
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
                else if (i >= 8) // Slots 9~12 (Disabled / Clear)
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
            AddLog(LogDirection.Info, $"Auto-calculated 8-slot safe schedule based on Sunrise ({ScheduleSunriseStr}) & Sunset ({ScheduleSunsetStr}) [{modeDesc}, Moonlight: {(KeepMoonlight ? "ON" : "OFF")}].");
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
