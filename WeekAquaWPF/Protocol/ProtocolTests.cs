using System;
using System.Diagnostics;

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

                // 2. Test RTC Sync Packet
                var testTime = new DateTime(2026, 8, 12, 14, 30, 15);
                byte[] rtcPacket = WeekAquaProtocol.BuildRtcSyncPacket(testTime);
                string rtcHex = WeekAquaProtocol.BytesToHexString(rtcPacket);
                if (rtcHex != "FF0E1E0F55555555") throw new Exception($"RTC packet mismatch: {rtcHex}");

                // 3. Test Live Spectrum Packet
                byte[] spectrumPacket = WeekAquaProtocol.BuildLiveSpectrumPacket(235, 200, 150, 0);
                string spectrumHex = WeekAquaProtocol.BytesToHexString(spectrumPacket);
                if (spectrumHex != "FBF9EBC896005555") throw new Exception($"Live Spectrum packet mismatch: {spectrumHex}");

                // 4. Test Cooling Fan Packet
                byte[] fanPacket = WeekAquaProtocol.BuildFanSpeedPacket(0);
                string fanHex = WeekAquaProtocol.BytesToHexString(fanPacket);
                if (fanHex != "FC00555555555555") throw new Exception($"Fan packet mismatch: {fanHex}");

                // 5. Test Ramp Time Packet (Slot 1, 08:00 - 20:00)
                byte[] rampTimePacket = WeekAquaProtocol.BuildRampTimePacket(1, 8, 0, 20, 0);
                string rampTimeHex = WeekAquaProtocol.BytesToHexString(rampTimePacket);
                if (rampTimeHex != "FEF1080014005555") throw new Exception($"Ramp time packet mismatch: {rampTimeHex}");

                // 6. Test Ramp Spectrum Packet (Slot 1)
                byte[] rampSpectrumPacket = WeekAquaProtocol.BuildRampSpectrumPacket(1, 235, 200, 150, 0);
                string rampSpectrumHex = WeekAquaProtocol.BytesToHexString(rampSpectrumPacket);
                if (rampSpectrumHex != "FBF1EBC896005555") throw new Exception($"Ramp spectrum packet mismatch: {rampSpectrumHex}");

                // 7. Test Power Parsing
                byte[] samplePowerBytes = WeekAquaProtocol.HexStringToBytes("0000000002FAF080"); // 50000000 rawVal
                double kwh = WeekAquaProtocol.ParsePowerData(samplePowerBytes);
                // 50000000 * 4.6566128730773926E-8 = ~2.328 => 2.3
                if (Math.Abs(kwh - 2.3) > 0.1) throw new Exception($"Power calculation mismatch: {kwh}");

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
