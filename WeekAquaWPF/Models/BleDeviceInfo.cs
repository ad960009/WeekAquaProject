using System;

namespace WeekAquaWPF.Models
{
    public class BleDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong BluetoothAddress { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public int Rssi { get; set; }

        public string ModelCode { get; set; } = string.Empty;
        public bool HasUvChannel { get; set; } = false;
        public bool Has6Channel => ModelCode switch
        {
            "5749" or "5750" or "5751" or "5752" => true,
            _ => Name.Contains("6CH") || Name.Contains("10CH")
        };

        public string ChannelTypeDescription => ModelCode switch
        {
            "5746" or "5747" => "4-Ch (RGBW)",
            "5748" => "5-Ch (RGBW+UV)",
            "5749" => "6-Ch (Multi-Spectrum)",
            "5750" or "5751" or "5752" => "7+ Ch (Advanced)",
            _ => HasUvChannel ? "5/6-Ch (UV/UVA)" : "4-Ch (RGBW)"
        };

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"Unknown ({MacAddress}) - {ChannelTypeDescription}" : $"{Name} [{MacAddress}] ({ChannelTypeDescription})";
        }
    }
}
