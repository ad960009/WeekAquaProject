using System;
using System.Globalization;
using System.Text;

namespace WeekAquaWPF.Protocol
{
    public static class WeekAquaProtocol
    {
        // UUID Definitions
        public static readonly Guid SERVICE_FFE0 = Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
        public static readonly Guid WRITE_FFE1   = Guid.Parse("0000ffe1-0000-1000-8000-00805f9b34fb");
        public static readonly Guid NOTIFY_FFE3  = Guid.Parse("0000ffe3-0000-1000-8000-00805f9b34fb");

        public static readonly Guid SERVICE_FFF0 = Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb");
        public static readonly Guid WRITE_FFF2   = Guid.Parse("0000fff2-0000-1000-8000-00805f9b34fb");
        public static readonly Guid NOTIFY_FFF1  = Guid.Parse("0000fff1-0000-1000-8000-00805f9b34fb");

        // Scaling constant for Smart Plug kWh calculation
        public const double POWER_KWH_SCALE = 4.6566128730773926E-8d;

        // Predefined App Preset Spectrum Ratios (RGBW Percentages)
        public static class Presets
        {
            public static (double R, double G, double B, double W) GreenGrass => (80, 100, 40, 80);    // 녹색 수초 모드
            public static (double R, double G, double B, double W) RedGrass   => (100, 30, 70, 80);    // 적색 수초 발색 모드
            public static (double R, double G, double B, double W) FishMixed  => (70, 70, 70, 100);    // 어류/혼양 관상 모드
            public static (double R, double G, double B, double W) CoralMarine => (10, 20, 100, 100);  // 산호/해수관상 모드
            public static (double R, double G, double B, double W) AlgaeMax   => (100, 100, 100, 100); // 물잡이/이끼/최대출력 모드
        }

        /// <summary>
        /// Converts percentage (0% - 100%) to raw byte value (0 - 235).
        /// </summary>
        public static byte PercentToByte(double percent)
        {
            double clamped = Math.Clamp(percent, 0.0, 100.0);
            return (byte)Math.Round(clamped / 100.0 * 235.0);
        }

        /// <summary>
        /// Converts raw byte value (0 - 235) to percentage (0% - 100%).
        /// </summary>
        public static double ByteToPercent(byte val)
        {
            double pct = ((double)val / 235.0) * 100.0;
            return Math.Round(Math.Clamp(pct, 0.0, 100.0), 1);
        }

        /// <summary>
        /// Builds the RTC time sync packet (0xFF + HH + MM + SS + 55555555).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildRtcSyncPacket(DateTime time)
        {
            return new byte[]
            {
                0xFF,
                (byte)time.Hour,
                (byte)time.Minute,
                (byte)time.Second,
                0x55, 0x55, 0x55, 0x55
            };
        }

        /// <summary>
        /// Builds the live manual spectrum packet (0xFBF9 + R + G + B + W + 5555).
        /// Values expected in range 0 - 235.
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildLiveSpectrumPacket(byte r, byte g, byte b, byte w)
        {
            return new byte[]
            {
                0xFB, 0xF9,
                r, g, b, w,
                0x55, 0x55
            };
        }

        /// <summary>
        /// Overload accepting percentage values (0 - 100%).
        /// </summary>
        public static byte[] BuildLiveSpectrumPacket(double rPct, double gPct, double bPct, double wPct)
        {
            return BuildLiveSpectrumPacket(
                PercentToByte(rPct),
                PercentToByte(gPct),
                PercentToByte(bPct),
                PercentToByte(wPct)
            );
        }

        /// <summary>
        /// Builds the cooling fan speed packet (0xFC + FanByte + 555555555555).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildFanSpeedPacket(byte fanByte)
        {
            return new byte[]
            {
                0xFC,
                fanByte,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55
            };
        }

        /// <summary>
        /// Overload accepting fan speed percentage (0 - 100%).
        /// </summary>
        public static byte[] BuildFanSpeedPacket(double fanPct)
        {
            return BuildFanSpeedPacket(PercentToByte(fanPct));
        }

        /// <summary>
        /// Builds the Ramp-up/down schedule slot time packet (0xFEF[Point] + StartH + StartM + EndH + EndM + 5555).
        /// Point ID: 1 to 12.
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildRampTimePacket(int pointId, byte startH, byte startM, byte endH, byte endM, bool enabled = true)
        {
            if (pointId < 1 || pointId > 12)
                throw new ArgumentOutOfRangeException(nameof(pointId), "Point ID must be between 1 and 12.");

            byte secondHeaderByte = (byte)(0xF0 | (pointId & 0x0F));

            if (!enabled)
            {
                return new byte[] { 0xFE, secondHeaderByte, 0x00, 0x00, 0x00, 0x00, 0x55, 0x55 };
            }

            return new byte[]
            {
                0xFE,
                secondHeaderByte,
                startH,
                startM,
                endH,
                endM,
                0x55, 0x55
            };
        }

        /// <summary>
        /// Builds the Ramp-up/down schedule slot spectrum packet (0xFBF[Point] + R + G + B + W + 5555).
        /// Point ID: 1 to 12.
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildRampSpectrumPacket(int pointId, byte r, byte g, byte b, byte w)
        {
            if (pointId < 1 || pointId > 12)
                throw new ArgumentOutOfRangeException(nameof(pointId), "Point ID must be between 1 and 12.");

            byte secondHeaderByte = (byte)(0xF0 | (pointId & 0x0F));

            return new byte[]
            {
                0xFB,
                secondHeaderByte,
                r, g, b, w,
                0x55, 0x55
            };
        }

        /// <summary>
        /// Builds the mode/preset selection packet (0xFDF1 ~ 0xFDF5 + 555555555555).
        /// Mode ID: 1 to 5.
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildModePacket(int modeId)
        {
            if (modeId < 1 || modeId > 5)
                throw new ArgumentOutOfRangeException(nameof(modeId), "Mode ID must be between 1 and 5.");

            byte secondHeaderByte = (byte)(0xF0 | (modeId & 0x0F));

            return new byte[]
            {
                0xFD,
                secondHeaderByte,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55
            };
        }

        /// <summary>
        /// Parses Smart Plug energy monitoring RX bytes (kWh).
        /// Formula: rawVal * 4.6566128730773926E-8
        /// </summary>
        public static double ParsePowerData(byte[] data)
        {
            if (data == null || data.Length == 0) return 0.0;

            try
            {
                string hex = BytesToHexString(data);
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong rawVal))
                {
                    double kWh = rawVal * POWER_KWH_SCALE;
                    return Math.Round(kWh, 1);
                }
            }
            catch
            {
                // Ignore parse errors
            }

            return 0.0;
        }

        /// <summary>
        /// Utility method to convert byte array to formatted Hex string (e.g. "FB F9 EB C8 96 00 55 55").
        /// </summary>
        public static string BytesToHexString(byte[] bytes, string separator = "")
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0 && !string.IsNullOrEmpty(separator))
                    sb.Append(separator);
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts hex string to byte array.
        /// </summary>
        public static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            hex = hex.Replace(" ", "").Replace("-", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
