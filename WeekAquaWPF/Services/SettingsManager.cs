using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WeekAquaWPF.Models;

namespace WeekAquaWPF.Services
{
    public class SlotConfig
    {
        public int PointId { get; set; }
        public bool IsEnabled { get; set; }
        public string StartTimeStr { get; set; } = "08:00";
        public string EndTimeStr { get; set; } = "18:00";
        public double RedPercent { get; set; } = 80;
        public double GreenPercent { get; set; } = 80;
        public double BluePercent { get; set; } = 80;
        public double WhitePercent { get; set; } = 80;
        public double UvPercent { get; set; } = 0;
        public double VioletPercent { get; set; } = 0;
    }

    public class DeviceConfig
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public double RedPercent { get; set; } = 50;
        public double GreenPercent { get; set; } = 50;
        public double BluePercent { get; set; } = 50;
        public double WhitePercent { get; set; } = 50;
        public double UvPercent { get; set; } = 0;
        public double VioletPercent { get; set; } = 0;
        public double FanSpeedPercent { get; set; } = 50;
        public bool AutoSendLiveSpectrum { get; set; } = false;
        public string SunriseStartStr { get; set; } = "08:00";
        public string SunriseEndStr { get; set; } = "18:00";
        public int SunriseRampIndex { get; set; } = 2; // Default 1h
        public List<SlotConfig> RampSlots { get; set; } = new List<SlotConfig>();
    }

    public class AppSettingsData
    {
        public string LastConnectedMac { get; set; } = string.Empty;
        public Dictionary<string, DeviceConfig> Devices { get; set; } = new Dictionary<string, DeviceConfig>();
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WeekAquaWPF",
            "device_config.json"
        );

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public static AppSettingsData LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var data = JsonSerializer.Deserialize<AppSettingsData>(json, JsonOptions);
                    if (data != null) return data;
                }
            }
            catch
            {
                // Fallback to default
            }

            return new AppSettingsData();
        }

        public static void SaveSettings(AppSettingsData data)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(data, JsonOptions);
                File.ReadAllText(SettingsFilePath); // test file access
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                try
                {
                    // Fallback to local directory if AppData fails
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "device_config.json");
                    string json = JsonSerializer.Serialize(data, JsonOptions);
                    File.WriteAllText(localPath, json);
                }
                catch { }
            }
        }
    }
}
