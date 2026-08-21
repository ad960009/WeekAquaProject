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

        public static readonly Guid SERVICE_FF60 = Guid.Parse("0000ff60-0000-1000-8000-00805f9b34fb");
        public static readonly Guid WRITE_FF61   = Guid.Parse("0000ff61-0000-1000-8000-00805f9b34fb");
        public static readonly Guid NOTIFY_FF62  = Guid.Parse("0000ff62-0000-1000-8000-00805f9b34fb");

        // Scaling constant for Smart Plug kWh calculation
        public const double POWER_KWH_SCALE = 4.6566128730773926E-8d;

        // Predefined App Preset Spectrum Ratios (RGBW + UV + Violet Percentages)
        public static class Presets
        {
            public static (double R, double G, double B, double W, double UV, double V) GreenGrass  => (75, 95, 38, 75, 10, 5); // 녹색 수초 모드
            public static (double R, double G, double B, double W, double UV, double V) RedGrass    => (95, 30, 65, 75, 15, 10); // 적색 수초 발색 모드
            public static (double R, double G, double B, double W, double UV, double V) FishMixed   => (70, 70, 70, 95, 5, 5);  // 어류/혼양 관상 모드
            public static (double R, double G, double B, double W, double UV, double V) Shrimp      => (40, 90, 60, 80, 10, 5);  // 비쉬림프/생새우 관상 모드
            public static (double R, double G, double B, double W, double UV, double V) Fish        => (80, 50, 85, 95, 10, 10); // 열대어 선명 발색 모드
            public static (double R, double G, double B, double W, double UV, double V) Custom      => (100, 100, 100, 100, 100, 100); // 100% 피크 출력 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralMarine => (10, 20, 95, 95, 60, 40); // 산호/해수 기본 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralLps    => (15, 25, 90, 70, 50, 60); // LPS 연산호 특화 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralSps    => (5, 15, 100, 60, 75, 85); // SPS 경산호 특화 모드
            public static (double R, double G, double B, double W, double UV, double V) CoralAb     => (10, 20, 100, 40, 80, 90); // Coral AB+ 형광 스펙트럼 모드
            public static (double R, double G, double B, double W, double UV, double V) MarineFot   => (50, 50, 85, 90, 25, 30); // FOT 해수어 관상 모드
            public static (double R, double G, double B, double W, double UV, double V) DeepBlue   => (0, 10, 100, 20, 80, 95);  // 심해 딥블루 모드
            public static (double R, double G, double B, double W, double UV, double V) Moonlight  => (0, 0, 25, 0, 15, 30);    // 은은한 달빛 야간 모드
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
        /// Converts integer decimal (0 - 99) to BCD (Binary-Coded Decimal) byte (e.g. 22 -> 0x22).
        /// Matches official Android APK byte conversion via hex string parsing.
        /// </summary>
        public static byte DecimalToBcd(int val)
        {
            int clamped = Math.Clamp(val, 0, 99);
            return (byte)(((clamped / 10) << 4) | (clamped % 10));
        }

        /// <summary>
        /// Converts BCD byte to decimal integer (e.g. 0x22 -> 22).
        /// </summary>
        public static int BcdToDecimal(byte bcd)
        {
            return ((bcd >> 4) * 10) + (bcd & 0x0F);
        }

        /// <summary>
        /// Builds the MCU state initialization / reset packet (0xF0 + 55555555555555).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildStateInitPacket()
        {
            return new byte[]
            {
                0xF0,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55
            };
        }

        /// <summary>
        /// Builds the RTC time sync packet with BCD-encoded hours, minutes, and seconds (0xFF + BCD(HH) + BCD(MM) + BCD(SS) + 55555555).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildRtcSyncPacket(DateTime time)
        {
            return new byte[]
            {
                0xFF,
                DecimalToBcd(time.Hour),
                DecimalToBcd(time.Minute),
                DecimalToBcd(time.Second),
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
        /// Builds the Simple Sunrise/Sunset spectrum packet (0xFBEF + R + G + B + W + 5555).
        /// </summary>
        public static byte[] BuildSimpleSpectrumPacket(byte r, byte g, byte b, byte w)
        {
            return new byte[]
            {
                0xFB, 0xEF,
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
        /// Builds the dedicated Sunrise & Sunset timer packet with BCD times (0xFEF9 + BCD(StartH) + BCD(StartM) + BCD(EndH) + BCD(EndM) + Type + RampIndex).
        /// RampIndex: 0 (0h), 1 (0.5h), 2 (1h), 3 (1.5h), 4 (2h), 5 (2.5h).
        /// Total length: 8 Bytes.
        /// </summary>
        public static byte[] BuildSunriseSunsetPacket(byte startH, byte startM, byte endH, byte endM, byte rampIndex, bool enabled = true)
        {
            return new byte[]
            {
                0xFE, 0xF9,
                DecimalToBcd(startH),
                DecimalToBcd(startM),
                DecimalToBcd(endH),
                DecimalToBcd(endM),
                (byte)(enabled ? 0x01 : 0x00),
                (byte)Math.Clamp(rampIndex, (byte)0, (byte)5)
            };
        }

        /// <summary>
        /// Builds the Ramp-up/down schedule slot time packet with BCD times (0xFEF[Point] + BCD(StartH) + BCD(StartM) + BCD(EndH) + BCD(EndM) + 5555).
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
                DecimalToBcd(startH),
                DecimalToBcd(startM),
                DecimalToBcd(endH),
                DecimalToBcd(endM),
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
        /// Returns the sequence of packets to unlock MCU hardware mode from Mode 2 (Schedule) to Mode 1 (Live Spectrum).
        /// Enforces exact order: FDF1 (Mode1) -> Spectrum -> FEEF (Timer) -> FEF9 (Timer)
        /// </summary>
        public static List<byte[]> BuildLiveModeSequence(byte[] spectrumPacket)
        {
            var seq = new List<byte[]>
            {
                // 1. Switch MCU to Mode 1 (Live Spectrum Output)
                new byte[] { 0xFD, 0xF1, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55 },
                // 2. Target Spectrum
                spectrumPacket
            };
            
            // 3. Open 24h timer window to prevent MCU from automatically dimming/turning off the light
            if (spectrumPacket.Length >= 2 && spectrumPacket[1] == 0xEF)
            {
                // M-Series / 5746 specific timer packet
                seq.Add(new byte[] { 0xFE, 0xEF, 0x00, 0x00, 0x24, 0x00, 0x55, 0x55 });
            }
            else
            {
                // Standard timer packet
                seq.Add(new byte[] { 0xFE, 0xF9, 0x00, 0x00, 0x24, 0x00, 0x01, 0x00 });
            }
            
            return seq;
        }

        /// <summary>
        /// Human-readable disassembler for WeekAqua BLE packets.
        /// </summary>
        public static string DescribePacket(byte[] packet)
        {
            if (packet == null || packet.Length < 2) return "Unknown Packet";
            byte p0 = packet[0];
            byte p1 = packet[1];
            if (packet.Length >= 8 && p0 == 0xFB && (p1 == 0xF9 || p1 == 0xEF))
            {
                int r = (int)Math.Round(ByteToPercent(packet[2]));
                int g = (int)Math.Round(ByteToPercent(packet[3]));
                int b = (int)Math.Round(ByteToPercent(packet[4]));
                int ch4 = (int)Math.Round(ByteToPercent(packet[5]));
                string hdr = p1 == 0xEF ? "FBEF" : "FBF9";
                return $"SetSpectrum [{hdr}] (R:{r}% G:{g}% B:{b}% CH4:{ch4}%)";
            }
            if (p0 == 0xFF) return $"Sync RTC Time [0xFF] ({packet[1]:X2}:{packet[2]:X2}:{packet[3]:X2})";
            if (p0 == 0xFD)
            {
                string desc = p1 switch
                {
                    0xF1 => "Mode 1 (Live/Spectrum)",
                    0xF2 => "Mode 2 (Ramp Schedule)",
                    0xF3 => "Mode 3 (Ramp Init)",
                    0xF4 => "Mode 1 Sub (Activate Spectrum)",
                    0xF5 => "Mode 5",
                    _ => $"0x{p1:X2}"
                };
                return $"SetMode [FD{p1:X2}] ({desc})";
            }
            if (p0 == 0xFE)
            {
                if (p1 == 0xEF) return $"SetTimer [FEEF] ({packet[2]:X2}:{packet[3]:X2} - {packet[4]:X2}:{packet[5]:X2})";
                if (p1 == 0xF9) return $"SetTimer [FEF9] ({packet[2]:X2}:{packet[3]:X2} - {packet[4]:X2}:{packet[5]:X2})";
                int slot = p1 & 0x0F;
                return $"SetRampTime [FE{p1:X2}] Slot #{slot}";
            }
            if (p0 == 0xFB)
            {
                int slot = p1 & 0x0F;
                return $"SetRampSpectrum [FB{p1:X2}] Slot #{slot}";
            }
            if (p0 == 0xFC)
            {
                int pct = (int)Math.Round(ByteToPercent(p1));
                return $"SetFanSpeed [FC] ({pct}%)";
            }
            if (p0 == 0xF6)
            {
                string state = p1 == 0xF1 ? "ON (Power Enabled)" : (p1 == 0xF2 ? "OFF (Power Disabled)" : $"0x{p1:X2}");
                return $"PowerSwitch [F6{p1:X2}] ({state})";
            }
            if (p0 == 0xF0) return "InitHandshake [F0]";
            return $"Cmd 0x{p0:X2} 0x{p1:X2}";
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
        /// Parses human-entered time strings (e.g. "08:00", "8:00", "8", "20:00", "20", "8:30") into a valid 24-hour TimeSpan (00:00:00 to 23:59:59).
        /// Avoids .NET TimeSpan.TryParse pitfall where single digits parse as Days (e.g. "8" -> 8 days).
        /// </summary>
        public static bool TryParseTimeString(string? input, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input)) return false;

            // 0. Explicit check for 24:00 midnight end
            if (input == "24:00" || input == "24:0" || input == "24")
            {
                time = TimeSpan.FromHours(24);
                return true;
            }

            // 1. Try parsing exact standard time formats
            string[] formats = new[] { "h\\:mm", "hh\\:mm", "h\\:m", "hh\\:m", "H\\:mm", "HH\\:mm", "H\\:m", "HH\\:m" };
            if (TimeSpan.TryParseExact(input, formats, CultureInfo.InvariantCulture, out TimeSpan tsExact))
            {
                if (tsExact.TotalHours == 24.0)
                {
                    time = TimeSpan.FromHours(24);
                    return true;
                }
                int h = tsExact.Hours % 24;
                if (h < 0) h += 24;
                time = new TimeSpan(h, tsExact.Minutes, 0);
                return true;
            }

            // 2. Try DateTime parsing for formats like "8:00 AM", "20:00", "8:00"
            if (DateTime.TryParseExact(input, new[] { "H:mm", "HH:mm", "h:mm", "hh:mm", "h:mm tt", "hh:mm tt", "H", "HH" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                time = new TimeSpan(dt.Hour, dt.Minute, 0);
                return true;
            }

            // 3. If input is integer hour like "8", "08", "20"
            if (int.TryParse(input, out int hour) && hour >= 0 && hour <= 24)
            {
                if (hour == 24)
                {
                    time = TimeSpan.FromHours(24);
                    return true;
                }
                time = new TimeSpan(hour % 24, 0, 0);
                return true;
            }

            // 4. Fallback general TimeSpan parse, clamping/normalizing Days into Hours
            if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out TimeSpan tsGeneral))
            {
                if (tsGeneral.TotalHours == 24.0)
                {
                    time = TimeSpan.FromHours(24);
                    return true;
                }
                if (tsGeneral.Days > 0 && tsGeneral.Hours == 0 && tsGeneral.Minutes == 0)
                {
                    time = new TimeSpan(tsGeneral.Days % 24, 0, 0);
                }
                else
                {
                    int h = (int)(tsGeneral.TotalHours % 24);
                    if (h < 0) h += 24;
                    time = new TimeSpan(h, tsGeneral.Minutes, 0);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Formats TimeSpan to HH:mm string format with 24:00 support.
        /// </summary>
        public static string FormatTimeString(TimeSpan ts)
        {
            if (ts.TotalHours == 24.0)
            {
                return "24:00";
            }
            int totalH = (int)(ts.TotalHours % 24);
            if (totalH < 0) totalH += 24;
            return $"{totalH:D2}:{ts.Minutes:D2}";
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
