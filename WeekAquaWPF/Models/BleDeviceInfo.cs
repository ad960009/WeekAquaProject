using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WeekAquaWPF.Models
{
    public class BleDeviceInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private ulong _bluetoothAddress;
        private string _macAddress = string.Empty;
        private int _rssi;
        private string _modelCode = string.Empty;
        private bool _hasUvChannel = false;
        private bool _is4ChannelRgbUv = false;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    AutoDetectChannelTypes();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Has6Channel));
                    OnPropertyChanged(nameof(ChannelTypeDescription));
                    OnPropertyChanged(nameof(Channel4Label));
                    OnPropertyChanged(nameof(Channel5Label));
                    OnPropertyChanged(nameof(Channel6Label));
                    OnPropertyChanged(nameof(Channel4Color));
                    OnPropertyChanged(nameof(Channel5Color));
                    OnPropertyChanged(nameof(Channel6Color));
                    OnPropertyChanged(nameof(MaxScheduleSlots));
                    OnPropertyChanged(nameof(ScheduleSlotLimitDescription));
                }
            }
        }

        public ulong BluetoothAddress
        {
            get => _bluetoothAddress;
            set { _bluetoothAddress = value; OnPropertyChanged(); }
        }

        public string MacAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(); }
        }

        public int Rssi
        {
            get => _rssi;
            set { _rssi = value; OnPropertyChanged(); }
        }

        public string ModelCode
        {
            get => _modelCode;
            set
            {
                if (_modelCode != value)
                {
                    _modelCode = value;
                    AutoDetectChannelTypes();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Has6Channel));
                    OnPropertyChanged(nameof(ChannelTypeDescription));
                    OnPropertyChanged(nameof(Channel4Label));
                    OnPropertyChanged(nameof(Channel5Label));
                    OnPropertyChanged(nameof(Channel6Label));
                    OnPropertyChanged(nameof(Channel4Color));
                    OnPropertyChanged(nameof(Channel5Color));
                    OnPropertyChanged(nameof(Channel6Color));
                    OnPropertyChanged(nameof(MaxScheduleSlots));
                    OnPropertyChanged(nameof(ScheduleSlotLimitDescription));
                }
            }
        }

        public bool HasUvChannel
        {
            get => _hasUvChannel;
            set
            {
                if (_hasUvChannel != value)
                {
                    _hasUvChannel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChannelTypeDescription));
                    OnPropertyChanged(nameof(Channel5Color));
                }
            }
        }

        public bool Is4ChannelRgbUv
        {
            get => _is4ChannelRgbUv;
            set
            {
                if (_is4ChannelRgbUv != value)
                {
                    _is4ChannelRgbUv = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ChannelTypeDescription));
                    OnPropertyChanged(nameof(Channel4Label));
                    OnPropertyChanged(nameof(Channel5Label));
                    OnPropertyChanged(nameof(Channel4Color));
                    OnPropertyChanged(nameof(Channel5Color));
                }
            }
        }

        public void AutoDetectChannelTypes()
        {
            if (ModelCode == "5748" || ModelCode == "5749" || ModelCode == "5750" || ModelCode == "5751" || ModelCode == "5752")
            {
                _hasUvChannel = true;
                _is4ChannelRgbUv = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(Name))
            {
                string upper = Name.ToUpperInvariant();

                // 6CH / Multi-Channel / Marine models
                if (upper.Contains("6CH") || upper.Contains("10CH") || upper.Contains("MARINE") || upper.Contains("CORAL") || upper.Contains("A-SERIES") || upper.Contains("A430"))
                {
                    _hasUvChannel = true;
                    _is4ChannelRgbUv = false;
                    return;
                }

                // 4-Channel RGB/UV Lineups: M-Series (M800, M600, M450, M400, M900, M1200 Pro), S-Series, T-Series (T90, T60), Z-Series, P-Series
                bool isRgbUv = upper.Contains("UV") || upper.Contains("UVA") || upper.Contains("RGB/UV") || upper.Contains("RGB-UV") || upper.Contains("RGB_UV") ||
                               upper.Contains("M800") || upper.Contains("M600") || upper.Contains("M450") || upper.Contains("M400") || upper.Contains("M900") || upper.Contains("M1200") || upper.Contains("M-PRO") || upper.Contains("M PRO") || upper.Contains("M_PRO") || upper.StartsWith("M") ||
                               upper.Contains("S400") || upper.Contains("S450") || upper.Contains("S600") || upper.Contains("S800") || upper.Contains("S900") || upper.Contains("S1200") || upper.Contains("S-PRO") || upper.Contains("S PRO") || upper.Contains("S_PRO") ||
                               upper.Contains("T90") || upper.Contains("T45") || upper.Contains("T60") || upper.Contains("T80") || upper.Contains("T120") ||
                               upper.Contains("Z400") || upper.Contains("Z600") || upper.Contains("Z800") ||
                               upper.Contains("P600") || upper.Contains("P800") || upper.Contains("P900") || upper.Contains("P1200") ||
                               System.Text.RegularExpressions.Regex.IsMatch(upper, @"\b[MSTZP][0-9]{2,4}");

                if (isRgbUv)
                {
                    _is4ChannelRgbUv = true;
                    _hasUvChannel = false;
                    if (string.IsNullOrEmpty(_modelCode))
                    {
                        _modelCode = "5746";
                    }
                }
            }
        }

        public bool Has6Channel => ModelCode switch
        {
            "5749" or "5750" or "5751" or "5752" => true,
            _ => Name.Contains("6CH") || Name.Contains("10CH")
        };

        public string ChannelTypeDescription => ModelCode switch
        {
            "5746" or "5747" => Is4ChannelRgbUv ? "4-Ch (RGB/UV)" : "4-Ch (RGBW)",
            "5748" => "5-Ch (RGBW+UV)",
            "5749" => "6-Ch (Multi-Spectrum)",
            "5750" or "5751" or "5752" => "7+ Ch (Advanced)",
            _ => Is4ChannelRgbUv ? "4-Ch (RGB/UV)" : (HasUvChannel ? "5/6-Ch (UV/UVA)" : "4-Ch (RGBW)")
        };

        public string Channel4Label => Is4ChannelRgbUv ? "UV (Ultraviolet)" : "White (W)";
        public string Channel5Label => Is4ChannelRgbUv ? "White (W)" : "UV/UVA";
        public string Channel6Label => "Violet";

        public string Channel4Color => Is4ChannelRgbUv ? "#C084FC" : "#F4F4F5";
        public string Channel5Color => HasUvChannel ? "#8B5CF6" : "#71717A";
        public string Channel6Color => Has6Channel ? "#EC4899" : "#71717A";

        public int MaxScheduleSlots => ModelCode switch
        {
            "5745" => 5,                       // Mode 1 / 2 (Classic 4-CH, e.g. T90 early)
            "5747" or "5748" or "5752" => 12,  // Mode 3, Mode 5, Mode 9 (Multi-Channel Pro 12-Slot)
            "5746" or "5749" or "5751" => 8,   // Mode 6, Mode 8, Old 8-Slot
            "5750" => 8,                       // Smart Plug fallback
            _ => (Name.Contains("6CH") || Name.Contains("10CH") || Name.Contains("MARINE") || Name.Contains("CORAL") || Name.Contains("A430")) ? 12 : 8
        };

        public string ScheduleSlotLimitDescription => $"{MaxScheduleSlots} Slots (1~{MaxScheduleSlots})";

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"Unknown ({MacAddress}) - {ChannelTypeDescription} [{ScheduleSlotLimitDescription}]" : $"{Name} [{MacAddress}] ({ChannelTypeDescription}, {ScheduleSlotLimitDescription})";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
