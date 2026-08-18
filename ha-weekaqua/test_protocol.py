"""Unit verification tests for Python WeekAquaProtocol implementation."""

import unittest
from datetime import datetime
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "custom_components", "weekaqua"))
from protocol import WeekAquaProtocol


class TestWeekAquaProtocol(unittest.TestCase):
    def test_percent_to_byte_and_back(self):
        self.assertEqual(WeekAquaProtocol.percent_to_byte(100.0), 235)
        self.assertEqual(WeekAquaProtocol.percent_to_byte(0.0), 0)
        self.assertEqual(WeekAquaProtocol.percent_to_byte(50.0), 118)
        self.assertAlmostEqual(WeekAquaProtocol.byte_to_percent(235), 100.0, delta=0.5)
        self.assertAlmostEqual(WeekAquaProtocol.byte_to_percent(0), 0.0, delta=0.5)

    def test_bcd_encoding(self):
        bcd = WeekAquaProtocol.decimal_to_bcd(22)
        self.assertEqual(bcd, 0x22)
        dec = WeekAquaProtocol.bcd_to_decimal(0x22)
        self.assertEqual(dec, 22)

    def test_rtc_sync_packet(self):
        dt = datetime(2026, 8, 18, 14, 30, 15)
        packet = WeekAquaProtocol.build_rtc_sync_packet(dt)
        self.assertEqual(packet.hex().upper(), "FF14301555555555")

    def test_fan_speed_packet(self):
        packet = WeekAquaProtocol.build_fan_speed_packet(0)
        self.assertEqual(packet.hex().upper(), "FC00555555555555")

    def test_live_spectrum_packet_normalization(self):
        # 100% on all channels exceeds 100% total power -> must be clamped/normalized
        packet = WeekAquaProtocol.build_live_spectrum_packet(100, 100, 100, 100, 0, 0, model_code="5746")
        self.assertEqual(packet[0], 0xFB)
        self.assertEqual(packet[1], 0xF9)
        # Verify normalized values are <= 235 and power is <= 100%
        r = WeekAquaProtocol.byte_to_percent(packet[2])
        g = WeekAquaProtocol.byte_to_percent(packet[3])
        b = WeekAquaProtocol.byte_to_percent(packet[4])
        w = WeekAquaProtocol.byte_to_percent(packet[5])
        total_pwr = WeekAquaProtocol.calculate_total_power_percent(r, g, b, w, model_code="5746")
        self.assertLessEqual(total_pwr, 100.5)

    def test_state_init_packet(self):
        packet = WeekAquaProtocol.build_state_init_packet()
        self.assertEqual(packet.hex().upper(), "F055555555555555")


if __name__ == "__main__":
    unittest.main()
