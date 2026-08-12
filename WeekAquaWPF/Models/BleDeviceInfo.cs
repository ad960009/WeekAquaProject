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
        public string ChannelTypeDescription => HasUvChannel ? "5/6-Ch (UV/UVA)" : "4-Ch (RGBW)";

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"Unknown ({MacAddress}) - {ChannelTypeDescription}" : $"{Name} [{MacAddress}] ({ChannelTypeDescription})";
        }
    }
}
