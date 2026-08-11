using System;

namespace WeekAquaWPF.Models
{
    public class BleDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong BluetoothAddress { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public int Rssi { get; set; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"Unknown ({MacAddress})" : $"{Name} [{MacAddress}]";
        }
    }
}
