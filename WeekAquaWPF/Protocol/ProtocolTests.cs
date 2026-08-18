using System;
using System.Diagnostics;
using WeekAquaWPF.Models;

namespace WeekAquaWPF.Protocol
{
    public static class ProtocolTests
    {
        public static bool RunVerification()
        {
            try
            {
                // 1. Test PercentToByte & ByteToPercent
                byte byte100 = WeekAquaProtocol.PercentToByte(100.0);
                if (byte100 != 235) throw new Exception($"PercentToByte(100) expected 235, got {byte100}");
                byte byte0 = WeekAquaProtocol.PercentToByte(0.0);
                if (byte0 != 0) throw new Exception($"PercentToByte(0) expected 0, got {byte0}");

                // 2. Test DecimalToBcd & BcdToDecimal
                byte bcd22 = WeekAquaProtocol.DecimalToBcd(22);
                if (bcd22 != 0x22) throw new Exception($"DecimalToBcd(22) expected 0x22, got 0x{bcd22:X2}");
                int dec22 = WeekAquaProtocol.BcdToDecimal(0x22);
                if (dec22 != 22) throw new Exception($"BcdToDecimal(0x22) expected 22, got {dec22}");

                // 3. Test State Init Packet
                byte[] initPacket = WeekAquaProtocol.BuildStateInitPacket();
                string initHex = WeekAquaProtocol.BytesToHexString(initPacket);
                if (initHex != "F055555555555555") throw new Exception($"State Init packet mismatch: {initHex}");

                // 4. Test RTC Sync Packet with BCD (14:30:15 -> 0x14, 0x30, 0x15)
                var testTime = new DateTime(2026, 8, 12, 14, 30, 15);
                byte[] rtcPacket = WeekAquaProtocol.BuildRtcSyncPacket(testTime);
                string rtcHex = WeekAquaProtocol.BytesToHexString(rtcPacket);
                if (rtcHex != "FF14301555555555") throw new Exception($"RTC packet mismatch: {rtcHex}");

                // 5. Test Live Spectrum Packet
                byte[] spectrumPacket = WeekAquaProtocol.BuildLiveSpectrumPacket(235, 200, 150, 0);
                string spectrumHex = WeekAquaProtocol.BytesToHexString(spectrumPacket);
                if (spectrumHex != "FBF9EBC896005555") throw new Exception($"Live Spectrum packet mismatch: {spectrumHex}");

                // 6. Test Cooling Fan Packet
                byte[] fanPacket = WeekAquaProtocol.BuildFanSpeedPacket(0);
                string fanHex = WeekAquaProtocol.BytesToHexString(fanPacket);
                if (fanHex != "FC00555555555555") throw new Exception($"Fan packet mismatch: {fanHex}");

                // 7. Test Sunrise / Sunset Packet with BCD (08:00 ~ 18:00, Ramp 2 = 1h)
                byte[] sunrisePacket = WeekAquaProtocol.BuildSunriseSunsetPacket(8, 0, 18, 0, 2);
                string sunriseHex = WeekAquaProtocol.BytesToHexString(sunrisePacket);
                if (sunriseHex != "FEF9080018000102") throw new Exception($"Sunrise/Sunset packet mismatch: {sunriseHex}");

                // 8. Test Ramp Time Packet with BCD (Slot 1, 08:00 - 20:00)
                byte[] rampTimePacket = WeekAquaProtocol.BuildRampTimePacket(1, 8, 0, 20, 0);
                string rampTimeHex = WeekAquaProtocol.BytesToHexString(rampTimePacket);
                if (rampTimeHex != "FEF1080020005555") throw new Exception($"Ramp time packet mismatch: {rampTimeHex}");

                // 9. Test Ramp Spectrum Packet (Slot 1)
                byte[] rampSpectrumPacket = WeekAquaProtocol.BuildRampSpectrumPacket(1, 235, 200, 150, 0);
                string rampSpectrumHex = WeekAquaProtocol.BytesToHexString(rampSpectrumPacket);
                if (rampSpectrumHex != "FBF1EBC896005555") throw new Exception($"Ramp spectrum packet mismatch: {rampSpectrumHex}");

                // 11. Test RampPointSlot Validation (Allow midnight crossing slot like 22:00 ~ 06:00)
                var midnightSlot = new WeekAquaWPF.Models.RampPointSlot
                {
                    PointId = 1,
                    IsEnabled = true,
                    StartTime = new TimeSpan(22, 0, 0),
                    EndTime = new TimeSpan(6, 0, 0),
                    RedPercent = 50,
                    GreenPercent = 50,
                    BluePercent = 50,
                    WhitePercent = 50
                };
                midnightSlot.Validate();
                if (midnightSlot.HasErrors)
                {
                    throw new Exception($"Midnight-crossing slot unexpectedly has errors: {midnightSlot.FirstErrorMessage}");
                }

                // 12. Test Device Model Slot Limit Detection (5, 8, 12 Slots)
                var dev5 = new BleDeviceInfo { ModelCode = "5745" };
                if (dev5.MaxScheduleSlots != 5) throw new Exception($"Model 5745 expected 5 slots, got {dev5.MaxScheduleSlots}");

                var dev8_old = new BleDeviceInfo { ModelCode = "5746" };
                if (dev8_old.MaxScheduleSlots != 8) throw new Exception($"Model 5746 expected 8 slots, got {dev8_old.MaxScheduleSlots}");

                var dev8_six = new BleDeviceInfo { ModelCode = "5749" };
                if (dev8_six.MaxScheduleSlots != 8) throw new Exception($"Model 5749 expected 8 slots, got {dev8_six.MaxScheduleSlots}");

                var dev12_four = new BleDeviceInfo { ModelCode = "5747" };
                if (dev12_four.MaxScheduleSlots != 12) throw new Exception($"Model 5747 expected 12 slots, got {dev12_four.MaxScheduleSlots}");

                var dev12_five = new BleDeviceInfo { ModelCode = "5748" };
                if (dev12_five.MaxScheduleSlots != 12) throw new Exception($"Model 5748 expected 12 slots, got {dev12_five.MaxScheduleSlots}");

                var dev12_nine = new BleDeviceInfo { ModelCode = "5752" };
                if (dev12_nine.MaxScheduleSlots != 12) throw new Exception($"Model 5752 expected 12 slots, got {dev12_nine.MaxScheduleSlots}");

                // 13. Test 24:00 Midnight Ramp Time Packet (e.g. Slot 5: 22:48 ~ 24:00)
                byte[] ramp24Packet = WeekAquaProtocol.BuildRampTimePacket(5, 22, 48, 24, 0);
                string ramp24Hex = WeekAquaProtocol.BytesToHexString(ramp24Packet);
                if (ramp24Hex != "FEF5224824005555") throw new Exception($"24:00 Ramp packet mismatch: {ramp24Hex}");

                // 14. Test Cross-Midnight Auto Schedule Slot Calculation (18:00 ~ 02:00 for 8 slots)
                var autoTimes = ViewModels.MainViewModel.CalculateAutoSlotTimes(new TimeSpan(18, 0, 0), new TimeSpan(2, 0, 0), 8);
                if (autoTimes[4].End.TotalHours != 24.0)
                {
                    throw new Exception($"Slot 5 EndTime expected 24:00, got {WeekAquaProtocol.FormatTimeString(autoTimes[4].End)}");
                }
                if (autoTimes[5].Start != TimeSpan.Zero)
                {
                    throw new Exception($"Slot 6 StartTime expected 00:00, got {WeekAquaProtocol.FormatTimeString(autoTimes[5].Start)}");
                }
                if (autoTimes[6].End != new TimeSpan(2, 0, 0))
                {
                    throw new Exception($"Slot 7 EndTime expected 02:00, got {WeekAquaProtocol.FormatTimeString(autoTimes[6].End)}");
                }
                if (autoTimes[7].Start != new TimeSpan(2, 0, 0) || autoTimes[7].End != new TimeSpan(18, 0, 0))
                {
                    throw new Exception($"Slot 8 (Night) expected 02:00 ~ 18:00, got {WeekAquaProtocol.FormatTimeString(autoTimes[7].Start)} ~ {WeekAquaProtocol.FormatTimeString(autoTimes[7].End)}");
                }

                // 15. Test RampPointSlot automatic 00:00 -> 24:00 normalization for evening slots
                var eveningSlot = new RampPointSlot
                {
                    PointId = 5,
                    StartTimeStr = "22:48",
                    EndTimeStr = "00:00"
                };
                if (eveningSlot.EndTimeStr != "24:00" || eveningSlot.EndTime.TotalHours != 24.0)
                {
                    throw new Exception($"Evening slot 00:00 EndTimeStr expected to normalize to 24:00, got {eveningSlot.EndTimeStr}");
                }

                Debug.WriteLine("All WeekAqua Protocol Verification Tests Passed!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Protocol Verification Failed: {ex.Message}");
                return false;
            }
        }
    }
}
