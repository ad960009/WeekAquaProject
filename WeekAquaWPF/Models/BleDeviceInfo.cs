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
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Has6Channel));
                    OnPropertyChanged(nameof(ChannelTypeDescription));
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
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Has6Channel));
                    OnPropertyChanged(nameof(ChannelTypeDescription));
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

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"Unknown ({MacAddress}) - {ChannelTypeDescription}" : $"{Name} [{MacAddress}] ({ChannelTypeDescription})";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
