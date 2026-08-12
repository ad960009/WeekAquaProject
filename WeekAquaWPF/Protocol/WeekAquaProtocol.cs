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

        // Predefined App Preset Spectrum Ratios (RGBW + UV + Violet Percentages)
        public static class Presets
        {
            public static (double R, double G, double B, double W, double UV, double V) GreenGrass  => (75, 95, 38, 75, 10, 5); // 녹색 수초 모드
            public static (double R, double G, double B, double W, double UV, double V) RedGrass    => (95, 30, 65, 75, 15, 10); // 적색 수초 발색 모드
            public static (double R, double G, double B, double W, double UV, double V) FishMixed   => (70, 70, 70, 95, 5, 5);  // 어류/혼양 관상 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralMarine => (10, 20, 95, 95, 60, 40); // 산호/해수 기본 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralLps    => (15, 25, 90, 70, 50, 60); // LPS 연산호 특화 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralSps    => (5, 15, 100, 60, 75, 85); // SPS 경산호 특화 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralAb     => (10, 20, 100, 40, 80, 90); // Coral AB+ 형광 스펙트럼 모드
            public static (double R, double G, double B, double W, double UV, double V) MarineFot   => (50, 50, 85, 90, 25, 30); // FOT 해수어 관상 모드
            public static (double R, double G, double B, double W, double UV, double V) AlgaeMax    => (70, 65, 70, 55, 20, 15); // 최적 밸런스 피크 출력 모드
        }

        /// <summary>
        /// Calculates weighted total power percentage based on Android APK formula per device lineup series:
        /// - 4-Channel (5746/5747): (R*0.39) + (G*0.41) + (B*0.53) + (W*0.11)
        /// - 5-Channel (5748):       (R*0.41) + (G*0.42) + (B*0.49) + (W*0.08) + (UV*0.08)
        /// - 6-Channel (5749):       (CH1*0.41) + (CH2*0.42) + (CH3*0.49) + (CH4*0.08) + (CH5*0.08) + (CH6*0.08)
        /// - 7+ Channel (5750+):     ((CH1*0.29) + (CH2*0.69) + (CH3*0.73) + (CH4*0.10) + (CH5*0.82)) / 1.06
        /// </summary>
        public static double CalculateTotalPowerPercent(double redPercent, double greenPercent, double bluePercent, double whitePercent, double uvPercent = 0.0, double violetPercent = 0.0, string modelCode = "")
        {
            double total;
            switch (modelCode)
            {
                case "5748": // 5-Channel Series (StringOneTools.java)
                    total = (redPercent * 0.41) + (greenPercent * 0.42) + (bluePercent * 0.49) + (whitePercent * 0.08) + (uvPercent * 0.08);
                    break;

                case "5749": // 6-Channel Series (StringTwoTools.java)
                    total = (redPercent * 0.41) + (greenPercent * 0.42) + (bluePercent * 0.49) + (whitePercent * 0.08) + (uvPercent * 0.08) + (violetPercent * 0.08);
                    break;

                case "5750":
                case "5751":
                case "5752": // 7+ Channel Series (StringThreeTools.java / StringFiveTools.java)
                    total = ((redPercent * 0.29) + (greenPercent * 0.69) + (bluePercent * 0.73) + (whitePercent * 0.10) + (uvPercent * 0.40) + (violetPercent * 0.40)) / 1.06;
                    break;

                default: // 4-Channel Series (5746/5747)
                    total = (redPercent * 0.39) + (greenPercent * 0.41) + (bluePercent * 0.53) + (whitePercent * 0.11);
                    break;
            }

            double rounded = Math.Round(total, 1);
            return (rounded > 100.0 && rounded <= 100.15) ? 100.0 : rounded;
        }

        /// <summary>
        /// Scales down channel percentages proportionally if total calculated power exceeds 100.0% max limit,
        /// preserving exact spectrum color ratios and color temperature balance.
        /// </summary>
        public static (double R, double G, double B, double W, double UV, double Violet) NormalizeSpectrumToMaxPower(
            double r, double g, double b, double w, double uv = 0.0, double violet = 0.0, string modelCode = "")
        {
            double totalPower = CalculateTotalPowerPercent(r, g, b, w, uv, violet, modelCode);
            if (totalPower > 100.0 && totalPower > 0)
            {
                double scaleFactor = 99.8 / totalPower;
                r = Math.Round(r * scaleFactor, 1);
                g = Math.Round(g * scaleFactor, 1);
                b = Math.Round(b * scaleFactor, 1);
                w = Math.Round(w * scaleFactor, 1);
                uv = Math.Round(uv * scaleFactor, 1);
                violet = Math.Round(violet * scaleFactor, 1);

                int safetyCount = 0;
                while (CalculateTotalPowerPercent(r, g, b, w, uv, violet, modelCode) > 100.0 && safetyCount++ < 10)
                {
                    if (b >= r && b >= g && b >= w && b >= uv && b >= violet && b > 0) b = Math.Round(b - 0.1, 1);
                    else if (g >= r && g >= w && g >= uv && g >= violet && g > 0) g = Math.Round(g - 0.1, 1);
                    else if (r >= w && r >= uv && r >= violet && r > 0) r = Math.Round(r - 0.1, 1);
                    else if (w >= uv && w >= violet && w > 0) w = Math.Round(w - 0.1, 1);
                    else if (uv >= violet && uv > 0) uv = Math.Round(uv - 0.1, 1);
                    else if (violet > 0) violet = Math.Round(violet - 0.1, 1);
                }
            }
            return (r, g, b, w, uv, violet);
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
        /// Builds the live manual spectrum packet (0xFBF9 + R + G + B + W + [UV] + [Violet] + 5555).
        /// Values expected in range 0 - 235.
        /// Total length: 8, 9, or 10 Bytes.
        /// </summary>
        public static byte[] BuildLiveSpectrumPacket(byte r, byte g, byte b, byte w, byte uv = 0, byte violet = 0)
        {
            if (violet > 0)
            {
                return new byte[]
                {
                    0xFB, 0xF9,
                    r, g, b, w, uv, violet,
                    0x55, 0x55
                };
            }
            if (uv > 0)
            {
                return new byte[]
                {
                    0xFB, 0xF9,
                    r, g, b, w, uv,
                    0x55, 0x55
                };
            }
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
        public static byte[] BuildLiveSpectrumPacket(double rPct, double gPct, double bPct, double wPct, double uvPct = 0.0, double violetPct = 0.0)
        {
            return BuildLiveSpectrumPacket(
                PercentToByte(rPct),
                PercentToByte(gPct),
                PercentToByte(bPct),
                PercentToByte(wPct),
                PercentToByte(uvPct),
                PercentToByte(violetPct)
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
        /// Builds the dedicated Sunrise & Sunset timer packet (0xFEF9 + StartH + StartM + EndH + EndM + Type + RampIndex).
        /// RampIndex: 0 (0h), 1 (0.5h), 2 (1h), 3 (1.5h), 4 (2h), 5 (2.5h).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildSunriseSunsetPacket(byte startH, byte startM, byte endH, byte endM, byte rampIndex, bool enabled = true)
        {
            return new byte[]
            {
                0xFE, 0xF9,
                startH, startM,
                endH, endM,
                (byte)(enabled ? 0x01 : 0x00),
                (byte)Math.Clamp(rampIndex, (byte)0, (byte)5)
            };
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
        /// Builds the Ramp-up/down schedule slot spectrum packet (0xFBF[Point] + R + G + B + W + [UV] + [Violet] + 5555).
        /// Point ID: 1 to 12.
        /// Total length: 8, 9, or 10 Bytes.
        /// </summary>
        public static byte[] BuildRampSpectrumPacket(int pointId, byte r, byte g, byte b, byte w, byte uv = 0, byte violet = 0)
        {
            if (pointId < 1 || pointId > 12)
                throw new ArgumentOutOfRangeException(nameof(pointId), "Point ID must be between 1 and 12.");

            byte secondHeaderByte = (byte)(0xF0 | (pointId & 0x0F));

            if (violet > 0)
            {
                return new byte[]
                {
                    0xFB,
                    secondHeaderByte,
                    r, g, b, w, uv, violet,
                    0x55, 0x55
                };
            }
            if (uv > 0)
            {
                return new byte[]
                {
                    0xFB,
                    secondHeaderByte,
                    r, g, b, w, uv,
                    0x55, 0x55
                };
            }

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
