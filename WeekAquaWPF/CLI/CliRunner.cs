using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WeekAquaWPF.Models;
using WeekAquaWPF.Protocol;
using WeekAquaWPF.Services;

namespace WeekAquaWPF.CLI
{
    public static class CliRunner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        public static async Task<int> RunAsync(string[] args)
        {
            // If output is not redirected (e.g. user running directly in interactive CMD/PowerShell), attach to parent console
            if (!Console.IsOutputRedirected)
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    var standardOutput = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                    Console.SetOut(standardOutput);
                    var standardError = new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
                    Console.SetError(standardError);
                }
            }

            try
            {
                if (args == null || args.Length == 0 || IsHelpArg(args[0]))
                {
                    PrintHelp();
                    return 0;
                }

                string command = args[0].ToLowerInvariant();
                var options = ParseArguments(args.Skip(1).ToArray());

                switch (command)
                {
                    case "scan":
                        return await ExecuteScanAsync(options);

                    case "sync-rtc":
                    case "rtc":
                        return await ExecuteSyncRtcAsync(options);

                    case "set-spectrum":
                    case "spectrum":
                        return await ExecuteSetSpectrumAsync(options);

                    case "set-timer":
                    case "timer":
                        return await ExecuteSetTimerAsync(options);

                    case "set-preset":
                    case "preset":
                        return await ExecuteSetPresetAsync(options);

                    case "set-fan":
                    case "fan":
                        return await ExecuteSetFanAsync(options);

                    case "test":
                    case "verify":
                        ProtocolTests.RunVerification();
                        Console.WriteLine("Protocol unit verification tests completed successfully.");
                        return 0;

                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[ERROR] Unknown command: '{command}'");
                        Console.ResetColor();
                        Console.WriteLine("Type 'WeekAquaWPF.exe --help' to view available commands and examples.\n");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CLI EXCEPTION] {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        private static bool IsHelpArg(string arg)
        {
            return arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("/?", StringComparison.OrdinalIgnoreCase) ||
                   arg.Equals("help", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key.StartsWith("-"))
                {
                    key = key.TrimStart('-');
                    string val = "true";
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        val = args[i + 1];
                        i++;
                    }
                    dict[key] = val;
                }
            }
            return dict;
        }

        private static string? GetOption(Dictionary<string, string> options, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (options.TryGetValue(k, out var val))
                    return val;
            }
            return null;
        }

        #region Command Executions

        private static async Task<int> ExecuteScanAsync(Dictionary<string, string> options)
        {
            int timeoutSec = 5;
            string? timeoutVal = GetOption(options, "t", "timeout", "sec");
            if (int.TryParse(timeoutVal, out int parsedTimeout) && parsedTimeout > 0)
            {
                timeoutSec = parsedTimeout;
            }

            Console.WriteLine($"[1/2] Scanning for WeekAqua BLE devices ({timeoutSec}s timeout)...");

            var discovered = new List<BleDeviceInfo>();
            using var ble = new BleService();

            ble.DeviceDiscovered += (device) =>
            {
                lock (discovered)
                {
                    var existing = discovered.FirstOrDefault(d => d.BluetoothAddress == device.BluetoothAddress);
                    if (existing != null)
                    {
                        existing.Rssi = device.Rssi;
                        if (!string.IsNullOrWhiteSpace(device.Name) && device.Name != "Unknown BLE Device")
                            existing.Name = device.Name;
                    }
                    else
                    {
                        discovered.Add(device);
                    }
                }
            };

            ble.StartScan();
            await Task.Delay(timeoutSec * 1000);
            ble.StopScan();

            Console.WriteLine($"[2/2] Discovery complete. Found {discovered.Count} device(s).\n");

            if (discovered.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No WeekAqua BLE devices found nearby. Ensure Bluetooth is enabled and light is powered on.");
                Console.ResetColor();
                return 0;
            }

            Console.WriteLine("-----------------------------------------------------------------------------------------");
            Console.WriteLine(string.Format("{0,-19} {1,-8} {2,-22} {3}", "MAC Address", "RSSI", "Channel Type", "Device Name"));
            Console.WriteLine("-----------------------------------------------------------------------------------------");

            foreach (var d in discovered.OrderByDescending(x => x.Rssi))
            {
                Console.WriteLine(string.Format("{0,-19} {1,-8} {2,-22} {3}",
                    d.MacAddress,
                    $"{d.Rssi} dBm",
                    d.ChannelTypeDescription,
                    d.Name));
            }
            Console.WriteLine("-----------------------------------------------------------------------------------------\n");
            return 0;
        }

        private static async Task<int> ExecuteSyncRtcAsync(Dictionary<string, string> options)
        {
            string? mac = GetOption(options, "m", "mac", "address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required option: --mac <MAC_ADDRESS> (e.g. -m DC:12:34:56:78:9A)");
                Console.ResetColor();
                return 1;
            }

            ulong btAddress;
            try
            {
                btAddress = BleService.ParseMacAddress(mac);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"Connecting to WeekAqua device [{mac}]...");
            using var ble = new BleService();

            bool connected = await ble.ConnectAsync(btAddress);
            if (!connected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to connect to device [{mac}]. Check distance and power.");
                Console.ResetColor();
                return 1;
            }

            var now = DateTime.Now;
            Console.WriteLine($"[TX] Enqueuing RTC Time Sync (BCD: {now:yyyy-MM-dd HH:mm:ss})...");
            ble.EnqueueWritePacket(WeekAquaProtocol.BuildRtcSyncPacket(now), "CLI RTC Sync (FF)");

            Console.WriteLine("[TX] Enqueuing State Reset Packet (0xF0)...");
            ble.EnqueueWritePacket(WeekAquaProtocol.BuildStateInitPacket(), "CLI State Reset (F0)");

            Console.WriteLine("Flushing transmission queue over BLE...");
            await ble.FlushQueueAsync(5000);

            ble.Disconnect();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] RTC Time successfully synchronized with PC time ({now:yyyy-MM-dd HH:mm:ss}) for [{mac}].\n");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> ExecuteSetSpectrumAsync(Dictionary<string, string> options)
        {
            string? mac = GetOption(options, "m", "mac", "address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required option: --mac <MAC_ADDRESS> (e.g. -m DC:12:34:56:78:9A)");
                Console.ResetColor();
                return 1;
            }

            ulong btAddress;
            try
            {
                btAddress = BleService.ParseMacAddress(mac);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            // Check if preset is specified instead of individual channels
            string? presetName = GetOption(options, "p", "preset");
            double r = 0, g = 0, b = 0, w = 0, uv = 0, violet = 0;

            if (!string.IsNullOrWhiteSpace(presetName))
            {
                var presetTuple = ResolvePreset(presetName);
                r = presetTuple.R;
                g = presetTuple.G;
                b = presetTuple.B;
                w = presetTuple.W;
                uv = presetTuple.UV;
                violet = presetTuple.V;
                Console.WriteLine($"Using Spectrum Preset: '{presetName}' (R:{r}% G:{g}% B:{b}% W:{w}% UV:{uv}% V:{violet}%)");
            }
            else
            {
                r = ParsePercentage(GetOption(options, "r", "red"), 0);
                g = ParsePercentage(GetOption(options, "g", "green"), 0);
                b = ParsePercentage(GetOption(options, "b", "blue"), 0);
                w = ParsePercentage(GetOption(options, "w", "white"), 0);
                uv = ParsePercentage(GetOption(options, "u", "uv"), 0);
                violet = ParsePercentage(GetOption(options, "v", "violet"), 0);
            }

            // Power safety validation & normalization
            double totalPower = WeekAquaProtocol.CalculateTotalPowerPercent(r, g, b, w, uv, violet);
            if (totalPower > 100.0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] Total calculated power ({totalPower:F1}%) exceeds 100% safety limit. Normalizing proportionally...");
                Console.ResetColor();
                var norm = WeekAquaProtocol.NormalizeSpectrumToMaxPower(r, g, b, w, uv, violet);
                r = norm.R; g = norm.G; b = norm.B; w = norm.W; uv = norm.UV; violet = norm.Violet;
                totalPower = WeekAquaProtocol.CalculateTotalPowerPercent(r, g, b, w, uv, violet);
            }

            byte rByte = WeekAquaProtocol.PercentToByte(r);
            byte gByte = WeekAquaProtocol.PercentToByte(g);
            byte bByte = WeekAquaProtocol.PercentToByte(b);
            byte wByte = WeekAquaProtocol.PercentToByte(w);
            byte uvByte = WeekAquaProtocol.PercentToByte(uv);
            byte vByte = WeekAquaProtocol.PercentToByte(violet);

            Console.WriteLine($"Connecting to WeekAqua device [{mac}]...");
            using var ble = new BleService();

            bool connected = await ble.ConnectAsync(btAddress);
            if (!connected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to connect to device [{mac}].");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"[TX] Enqueuing Live Spectrum Packet (FBF9: R:{r:F0}% G:{g:F0}% B:{b:F0}% W:{w:F0}% UV:{uv:F0}% V:{violet:F0}% | Total Power: {totalPower:F1}%)...");
            byte[] packet = WeekAquaProtocol.BuildLiveSpectrumPacket(rByte, gByte, bByte, wByte, uvByte, vByte);
            ble.EnqueueWritePacket(packet, "CLI Live Spectrum (FBF9)");

            Console.WriteLine("Flushing transmission queue over BLE...");
            await ble.FlushQueueAsync(5000);

            ble.Disconnect();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Spectrum brightness successfully applied to [{mac}].\n");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> ExecuteSetTimerAsync(Dictionary<string, string> options)
        {
            string? mac = GetOption(options, "m", "mac", "address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required option: --mac <MAC_ADDRESS> (e.g. -m DC:12:34:56:78:9A)");
                Console.ResetColor();
                return 1;
            }

            string? minStr = GetOption(options, "d", "duration", "m", "min", "minutes");
            if (string.IsNullOrWhiteSpace(minStr) || !int.TryParse(minStr, out int durationMinutes) || durationMinutes <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing or invalid duration: --minutes <MINUTES> or -d <MINUTES> (e.g. -d 30, -d 60)");
                Console.ResetColor();
                return 1;
            }

            ulong btAddress;
            try
            {
                btAddress = BleService.ParseMacAddress(mac);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            // Spectrum or Preset
            string? presetName = GetOption(options, "p", "preset");
            double r = 0, g = 0, b = 0, w = 0, uv = 0, violet = 0;

            if (!string.IsNullOrWhiteSpace(presetName))
            {
                var presetTuple = ResolvePreset(presetName);
                r = presetTuple.R;
                g = presetTuple.G;
                b = presetTuple.B;
                w = presetTuple.W;
                uv = presetTuple.UV;
                violet = presetTuple.V;
                Console.WriteLine($"Using Spectrum Preset: '{presetName}'");
            }
            else
            {
                r = ParsePercentage(GetOption(options, "r", "red"), 80);
                g = ParsePercentage(GetOption(options, "g", "green"), 60);
                b = ParsePercentage(GetOption(options, "b", "blue"), 50);
                w = ParsePercentage(GetOption(options, "w", "white"), 30);
                uv = ParsePercentage(GetOption(options, "u", "uv"), 0);
                violet = ParsePercentage(GetOption(options, "v", "violet"), 0);
            }

            double totalPower = WeekAquaProtocol.CalculateTotalPowerPercent(r, g, b, w, uv, violet);
            if (totalPower > 100.0)
            {
                var norm = WeekAquaProtocol.NormalizeSpectrumToMaxPower(r, g, b, w, uv, violet);
                r = norm.R; g = norm.G; b = norm.B; w = norm.W; uv = norm.UV; violet = norm.Violet;
                totalPower = WeekAquaProtocol.CalculateTotalPowerPercent(r, g, b, w, uv, violet);
            }

            byte rByte = WeekAquaProtocol.PercentToByte(r);
            byte gByte = WeekAquaProtocol.PercentToByte(g);
            byte bByte = WeekAquaProtocol.PercentToByte(b);
            byte wByte = WeekAquaProtocol.PercentToByte(w);
            byte uvByte = WeekAquaProtocol.PercentToByte(uv);
            byte vByte = WeekAquaProtocol.PercentToByte(violet);

            DateTime now = DateTime.Now;
            DateTime endTime = now.AddMinutes(durationMinutes);

            Console.WriteLine("==========================================================================");
            Console.WriteLine($" Configuring Timed Schedule for [{mac}]");
            Console.WriteLine($"   • Start Time (Now) : {now:HH:mm:ss} (PC Clock Sync)");
            Console.WriteLine($"   • End Time         : {endTime:HH:mm} ({durationMinutes} minutes duration)");
            Console.WriteLine($"   • Light Spectrum   : R:{r:F0}% G:{g:F0}% B:{b:F0}% W:{w:F0}% UV:{uv:F0}% V:{violet:F0}% (Power: {totalPower:F1}%)");
            Console.WriteLine($"   • Auto Shut-off    : Yes (Slots 2~12 cleared to 0W power at {endTime:HH:mm})");
            Console.WriteLine("==========================================================================");

            Console.WriteLine($"Connecting to WeekAqua device [{mac}]...");
            using var ble = new BleService();

            bool connected = await ble.ConnectAsync(btAddress);
            if (!connected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to connect to device [{mac}].");
                Console.ResetColor();
                return 1;
            }

            // 1. RTC Sync & State Reset
            ble.EnqueueWritePacket(WeekAquaProtocol.BuildRtcSyncPacket(now), "CLI RTC Sync (FF)");
            ble.EnqueueWritePacket(WeekAquaProtocol.BuildStateInitPacket(), "CLI State Reset (F0)");

            // 2. Slot 1 (Active Timer Window)
            byte[] slot1Time = WeekAquaProtocol.BuildRampTimePacket(1, (byte)now.Hour, (byte)now.Minute, (byte)endTime.Hour, (byte)endTime.Minute, true);
            byte[] slot1Spec = WeekAquaProtocol.BuildRampSpectrumPacket(1, rByte, gByte, bByte, wByte, uvByte, vByte);
            ble.EnqueueWritePacket(slot1Time, $"Slot #1 Time ({now:HH:mm} ~ {endTime:HH:mm})");
            ble.EnqueueWritePacket(slot1Spec, $"Slot #1 Spectrum (R:{r:F0}% G:{g:F0}% B:{b:F0}% W:{w:F0}%)");

            // 3. Slots 2 ~ 12 (Cleared / Disabled to 0W to guarantee complete auto shut-off)
            for (int slotId = 2; slotId <= 12; slotId++)
            {
                byte[] slotTime = WeekAquaProtocol.BuildRampTimePacket(slotId, 0, 0, 0, 0, false);
                byte[] slotSpec = WeekAquaProtocol.BuildRampSpectrumPacket(slotId, 0, 0, 0, 0, 0, 0);
                ble.EnqueueWritePacket(slotTime, $"Slot #{slotId} Time (Disabled / 00:00)");
                ble.EnqueueWritePacket(slotSpec, $"Slot #{slotId} Spectrum (Clear 0W)");
            }

            // 4. Mode 2 Activation (Advanced Custom Ramp Schedule Mode)
            byte[] modePacket = WeekAquaProtocol.BuildModePacket(2);
            ble.EnqueueWritePacket(modePacket, "Activate Mode 2 (FDF2)");

            Console.WriteLine("Enqueued 27 packets (RTC Sync + Slot 1 Active + Slots 2~12 Cleared + Mode 2).");
            Console.WriteLine("Flushing transmission queue over BLE (approx. 14 seconds)...");

            await ble.FlushQueueAsync(18000);

            ble.Disconnect();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Timed schedule successfully applied to [{mac}]!");
            Console.WriteLine($"Light will remain ON until {endTime:HH:mm}, then automatically turn OFF.\n");
            Console.ResetColor();
            return 0;
        }

        private static async Task<int> ExecuteSetPresetAsync(Dictionary<string, string> options)
        {
            string? mac = GetOption(options, "m", "mac", "address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required option: --mac <MAC_ADDRESS> (e.g. -m DC:12:34:56:78:9A)");
                Console.ResetColor();
                return 1;
            }

            string? preset = GetOption(options, "p", "preset", "name");
            if (string.IsNullOrWhiteSpace(preset))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing preset name: --preset <NAME> (e.g. -p Green, -p RedPlant, -p CoralAB)");
                Console.WriteLine("Available presets: Green, RedPlant, Mixed, Shrimp, Fish, CoralAB, CoralLPS, CoralSPS, MarineFish, DeepBlue, Moonlight, AlgaeMax, Custom, Off");
                Console.ResetColor();
                return 1;
            }

            return await ExecuteSetSpectrumAsync(options);
        }

        private static async Task<int> ExecuteSetFanAsync(Dictionary<string, string> options)
        {
            string? mac = GetOption(options, "m", "mac", "address");
            if (string.IsNullOrWhiteSpace(mac))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing required option: --mac <MAC_ADDRESS>");
                Console.ResetColor();
                return 1;
            }

            string? speedStr = GetOption(options, "s", "speed", "fan");
            if (string.IsNullOrWhiteSpace(speedStr) || !double.TryParse(speedStr, out double speedPercent))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Missing or invalid fan speed: --speed <0-100> (e.g. -s 50)");
                Console.ResetColor();
                return 1;
            }

            ulong btAddress = BleService.ParseMacAddress(mac);
            byte fanByte = WeekAquaProtocol.PercentToByte(speedPercent);

            Console.WriteLine($"Connecting to WeekAqua device [{mac}]...");
            using var ble = new BleService();
            bool connected = await ble.ConnectAsync(btAddress);
            if (!connected)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to connect to device [{mac}].");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine($"[TX] Enqueuing Fan Speed Packet ({speedPercent:F0}% -> 0x{fanByte:X2})...");
            ble.EnqueueWritePacket(WeekAquaProtocol.BuildFanSpeedPacket(fanByte), $"CLI Fan Speed ({speedPercent}%)");

            await ble.FlushQueueAsync(5000);
            ble.Disconnect();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Fan speed set to {speedPercent:F0}% on [{mac}].\n");
            Console.ResetColor();
            return 0;
        }

        #endregion

        #region Helper Methods

        private static (double R, double G, double B, double W, double UV, double V) ResolvePreset(string name)
        {
            string key = name.Trim().ToLowerInvariant();
            return key switch
            {
                "green" or "greengrass" => WeekAquaProtocol.Presets.GreenGrass,
                "red" or "redplant" or "redgrass" => WeekAquaProtocol.Presets.RedGrass,
                "mixed" or "fishmixed" => WeekAquaProtocol.Presets.FishMixed,
                "shrimp" => WeekAquaProtocol.Presets.Shrimp,
                "fish" => WeekAquaProtocol.Presets.Fish,
                "custom" or "max" or "100" => WeekAquaProtocol.Presets.Custom,
                "coralab" or "coral" or "marine" or "coralmarine" => WeekAquaProtocol.Presets.CoralAb,
                "corallps" or "lps" => WeekAquaProtocol.Presets.CoralLps,
                "coralsps" or "sps" => WeekAquaProtocol.Presets.CoralSps,
                "marinefish" or "fot" => WeekAquaProtocol.Presets.MarineFot,
                "deepblue" or "deep" => WeekAquaProtocol.Presets.DeepBlue,
                "moonlight" or "moon" or "night" => WeekAquaProtocol.Presets.Moonlight,
                "algaemax" or "algae" => WeekAquaProtocol.Presets.AlgaeMax,
                "off" or "zero" => (0, 0, 0, 0, 0, 0),
                _ => WeekAquaProtocol.Presets.GreenGrass
            };
        }

        private static double ParsePercentage(string? val, double fallback)
        {
            if (string.IsNullOrWhiteSpace(val)) return fallback;
            if (double.TryParse(val, out double parsed))
            {
                return Math.Clamp(parsed, 0.0, 100.0);
            }
            return fallback;
        }

        private static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================================================================");
            Console.WriteLine("  WeekAqua CLI Controller (Bluetooth Low Energy Aquarium Light Automation)               ");
            Console.WriteLine("==========================================================================================");
            Console.ResetColor();

            Console.WriteLine("\nUSAGE:");
            Console.WriteLine("  WeekAquaWPF.exe <command> [options]\n");

            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  scan                   Scan for nearby WeekAqua BLE lights and print device table");
            Console.WriteLine("  sync-rtc               Synchronize RTC clock with current PC time (BCD encoded)");
            Console.WriteLine("  set-spectrum           Apply live RGBW / UV / Violet spectrum brightness immediately");
            Console.WriteLine("  set-timer              Apply timed schedule for N minutes then automatically turn off");
            Console.WriteLine("  set-preset             Apply preset spectrum (Green, RedPlant, Mixed, CoralAB, etc.)");
            Console.WriteLine("  set-fan                Adjust cooling fan speed percentage (0-100%)");
            Console.WriteLine("  verify                 Run internal BLE protocol encoder/decoder verification tests");
            Console.WriteLine("  help, -h, --help       Show this comprehensive help and usage guide\n");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("COMMAND DETAILS & EXAMPLES:\n");
            Console.ResetColor();

            Console.WriteLine("1. SCAN FOR DEVICES (Scan 주변 기기 검색):");
            Console.WriteLine("   WeekAquaWPF.exe scan [--timeout <seconds>]");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     WeekAquaWPF.exe scan");
            Console.WriteLine("     WeekAquaWPF.exe scan --timeout 8\n");

            Console.WriteLine("2. SYNC RTC TIME (RTC 시계 동기화):");
            Console.WriteLine("   WeekAquaWPF.exe sync-rtc -m <MAC_ADDRESS>");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     WeekAquaWPF.exe sync-rtc -m DC:12:34:56:78:9A");
            Console.WriteLine("     WeekAquaWPF.exe sync-rtc --mac AA:BB:CC:11:22:33\n");

            Console.WriteLine("3. SET LIVE SPECTRUM / BRIGHTNESS (현재 밝기/스펙트럼 실시간 설정):");
            Console.WriteLine("   WeekAquaWPF.exe set-spectrum -m <MAC> -r <R> -g <G> -b <B> -w <W> [-u <UV>] [-v <Violet>]");
            Console.WriteLine("   Options:");
            Console.WriteLine("     -m, --mac        Target device Bluetooth MAC address (Required)");
            Console.WriteLine("     -r, --red        Red channel percentage (0 - 100) (Default: 0)");
            Console.WriteLine("     -g, --green      Green channel percentage (0 - 100) (Default: 0)");
            Console.WriteLine("     -b, --blue       Blue channel percentage (0 - 100) (Default: 0)");
            Console.WriteLine("     -w, --white      White channel percentage (0 - 100) (Default: 0)");
            Console.WriteLine("     -u, --uv         UV channel percentage (0 - 100) (Optional, Default: 0)");
            Console.WriteLine("     -v, --violet     Violet / UV2 channel percentage (0 - 100) (Optional, Default: 0)");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     WeekAquaWPF.exe set-spectrum -m DC:12:34:56:78:9A -r 80 -g 60 -b 50 -w 30");
            Console.WriteLine("     WeekAquaWPF.exe set-spectrum -m DC:12:34:56:78:9A -r 50 -g 30 -b 90 -w 0 -u 40 -v 20");
            Console.WriteLine("     WeekAquaWPF.exe set-spectrum -m DC:12:34:56:78:9A -r 0 -g 0 -b 0 -w 0  (Turn Off)\n");

            Console.WriteLine("4. SET TIMED SCHEDULE (지정 시간 점등 후 자동 소등 스케줄 전송):");
            Console.WriteLine("   WeekAquaWPF.exe set-timer -m <MAC> -d <MINUTES> [-r <R> -g <G> -b <B> -w <W> | -p <PRESET>]");
            Console.WriteLine("   Options:");
            Console.WriteLine("     -m, --mac        Target device Bluetooth MAC address (Required)");
            Console.WriteLine("     -d, --minutes    Duration in minutes for light to stay ON (Required)");
            Console.WriteLine("     -r, --red        Red channel percentage (0 - 100)");
            Console.WriteLine("     -g, --green      Green channel percentage (0 - 100)");
            Console.WriteLine("     -b, --blue       Blue channel percentage (0 - 100)");
            Console.WriteLine("     -w, --white      White channel percentage (0 - 100)");
            Console.WriteLine("     -u, --uv         UV channel percentage (0 - 100)");
            Console.WriteLine("     -v, --violet     Violet channel percentage (0 - 100)");
            Console.WriteLine("     -p, --preset     Use predefined preset (Green, RedPlant, Mixed, CoralAB, etc.)");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     # Turn on light with RGBW (80,60,50,30) for 30 minutes, then auto shut-off:");
            Console.WriteLine("     WeekAquaWPF.exe set-timer -m DC:12:34:56:78:9A -d 30 -r 80 -g 60 -b 50 -w 30\n");
            Console.WriteLine("     # Turn on light with 'Green' preset for 60 minutes, then auto shut-off:");
            Console.WriteLine("     WeekAquaWPF.exe set-timer -m DC:12:34:56:78:9A -d 60 -p Green\n");
            Console.WriteLine("     # Turn on marine coral light for 120 minutes, then auto shut-off:");
            Console.WriteLine("     WeekAquaWPF.exe set-timer -m DC:12:34:56:78:9A -d 120 -p CoralAB\n");

            Console.WriteLine("5. SET PRESET SPECTRUM (프리셋 스펙트럼 적용):");
            Console.WriteLine("   WeekAquaWPF.exe set-preset -m <MAC_ADDRESS> -p <PRESET_NAME>");
            Console.WriteLine("   Available Presets:");
            Console.WriteLine("     Green, RedPlant, Mixed, Shrimp, Fish, CoralAB, CoralLPS, CoralSPS, MarineFish, DeepBlue, Moonlight, AlgaeMax, Custom, Off");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     WeekAquaWPF.exe set-preset -m DC:12:34:56:78:9A -p Green");
            Console.WriteLine("     WeekAquaWPF.exe set-preset -m DC:12:34:56:78:9A -p RedPlant");
            Console.WriteLine("     WeekAquaWPF.exe set-preset -m DC:12:34:56:78:9A -p CoralAB\n");

            Console.WriteLine("6. SET FAN SPEED (냉각 팬 속도 조절):");
            Console.WriteLine("   WeekAquaWPF.exe set-fan -m <MAC_ADDRESS> -s <SPEED_PERCENT>");
            Console.WriteLine("   Examples:");
            Console.WriteLine("     WeekAquaWPF.exe set-fan -m DC:12:34:56:78:9A -s 50");
            Console.WriteLine("     WeekAquaWPF.exe set-fan -m DC:12:34:56:78:9A -s 100\n");
        }

        #endregion
    }
}
