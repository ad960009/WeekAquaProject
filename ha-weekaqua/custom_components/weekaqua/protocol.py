"""WeekAqua BLE Protocol Implementation in Python.

Reverse-engineered from WeekAqua Android APK and verified with WeekAquaWPF.
"""

from __future__ import annotations
from dataclasses import dataclass
from datetime import datetime, time
import math


@dataclass
class NormalizedSpectrum:
    r: float
    g: float
    b: float
    w: float
    uv: float
    violet: float


class WeekAquaProtocol:
    """Protocol packet builder and parser for WeekAqua BLE devices."""

    @staticmethod
    def percent_to_byte(percent: float) -> int:
        """Convert 0.0~100.0% to WeekAqua linear byte 0x00~0xEB (235 max)."""
        clamped = max(0.0, min(100.0, float(percent)))
        val = int(round(clamped * 2.35))
        return max(0, min(235, val))

    @staticmethod
    def byte_to_percent(b: int) -> float:
        """Convert WeekAqua byte 0x00~0xEB to percentage 0.0~100.0%."""
        val = max(0, min(235, int(b)))
        return round(val / 2.35, 1)

    @staticmethod
    def decimal_to_bcd(val: int) -> int:
        """Convert integer (0-99) to BCD encoded byte."""
        clamped = max(0, min(99, int(val)))
        high = clamped // 10
        low = clamped % 10
        return (high << 4) | low

    @staticmethod
    def bcd_to_decimal(bcd: int) -> int:
        """Convert BCD byte to integer decimal."""
        high = (bcd >> 4) & 0x0F
        low = bcd & 0x0F
        return high * 10 + low

    @staticmethod
    def calculate_total_power_percent(
        red: float,
        green: float,
        blue: float,
        white: float,
        uv: float = 0.0,
        violet: float = 0.0,
        model_code: str = ""
    ) -> float:
        """Calculate total wattage load percentage according to official APK formulas."""
        r = max(0.0, min(100.0, red))
        g = max(0.0, min(100.0, green))
        b = max(0.0, min(100.0, blue))
        w = max(0.0, min(100.0, white))
        u = max(0.0, min(100.0, uv))
        v = max(0.0, min(100.0, violet))

        if model_code == "5748":  # 5-Channel Mode 5
            total = (r * 0.41) + (g * 0.42) + (b * 0.49) + (w * 0.08) + (u * 0.08)
        elif model_code == "5749":  # 6-Channel Mode 6
            total = (r * 0.41) + (g * 0.42) + (b * 0.49) + (w * 0.08) + (u * 0.08) + (v * 0.08)
        elif model_code in ("5750", "5751", "5752"):  # 7+ Channel Advanced
            total = ((r * 0.29) + (g * 0.69) + (b * 0.73) + (w * 0.10) + (u * 0.40) + (v * 0.40)) / 1.06
        else:  # Standard 4-Channel (RGBW / RGB-UV)
            total = (r * 0.41) + (g * 0.42) + (b * 0.49) + (w * 0.08)

        return round(total, 1)

    @classmethod
    def normalize_spectrum_to_max_power(
        cls,
        red: float,
        green: float,
        blue: float,
        white: float,
        uv: float = 0.0,
        violet: float = 0.0,
        model_code: str = ""
    ) -> NormalizedSpectrum:
        """Proportionally scale down all channels if total power exceeds 100%."""
        total = cls.calculate_total_power_percent(red, green, blue, white, uv, violet, model_code)
        if total <= 100.0 or total <= 0.0:
            return NormalizedSpectrum(
                r=round(max(0.0, red), 1),
                g=round(max(0.0, green), 1),
                b=round(max(0.0, blue), 1),
                w=round(max(0.0, white), 1),
                uv=round(max(0.0, uv), 1),
                violet=round(max(0.0, violet), 1),
            )

        factor = 100.0 / total
        return NormalizedSpectrum(
            r=round(max(0.0, red * factor), 1),
            g=round(max(0.0, green * factor), 1),
            b=round(max(0.0, blue * factor), 1),
            w=round(max(0.0, white * factor), 1),
            uv=round(max(0.0, uv * factor), 1),
            violet=round(max(0.0, violet * factor), 1),
        )

    @classmethod
    def build_live_spectrum_packet(
        cls,
        red_pct: float,
        green_pct: float,
        blue_pct: float,
        white_pct: float,
        uv_pct: float = 0.0,
        violet_pct: float = 0.0,
        model_code: str = ""
    ) -> bytes:
        """Build 8-byte live manual spectrum packet (0xFB 0xF9 R G B W UV V)."""
        norm = cls.normalize_spectrum_to_max_power(red_pct, green_pct, blue_pct, white_pct, uv_pct, violet_pct, model_code)
        r = cls.percent_to_byte(norm.r)
        g = cls.percent_to_byte(norm.g)
        b = cls.percent_to_byte(norm.b)
        w = cls.percent_to_byte(norm.w)
        uv = cls.percent_to_byte(norm.uv)
        v = cls.percent_to_byte(norm.violet)
        # Pad with 0x55 if 4-ch
        b6 = uv if uv > 0 or model_code in ("5748", "5749", "5751", "5752") else 0x55
        b7 = v if v > 0 or model_code in ("5749", "5751", "5752") else 0x55
        return bytes([0xFB, 0xF9, r, g, b, w, b6, b7])

    @classmethod
    def build_fan_speed_packet(cls, fan_pct: float) -> bytes:
        """Build 8-byte cooling fan speed packet (0xFC fan 0x55 0x55 0x55 0x55 0x55 0x55)."""
        f = cls.percent_to_byte(fan_pct)
        return bytes([0xFC, f, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55])

    @classmethod
    def build_rtc_sync_packet(cls, dt: datetime | None = None) -> bytes:
        """Build 8-byte real-time clock synchronization packet with BCD encoding."""
        if dt is None:
            dt = datetime.now()
        h_bcd = cls.decimal_to_bcd(dt.hour)
        m_bcd = cls.decimal_to_bcd(dt.minute)
        s_bcd = cls.decimal_to_bcd(dt.second)
        return bytes([0xFF, h_bcd, m_bcd, s_bcd, 0x55, 0x55, 0x55, 0x55])

    @classmethod
    def build_state_init_packet(cls) -> bytes:
        """Build 8-byte state initialization query packet (0xF0 0x55 ...)."""
        return bytes([0xF0, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55])

    @classmethod
    def build_mode_packet(cls, mode_id: int) -> bytes:
        """Build mode activation packet (0xFD 0xF1 ~ 0xF3). Mode 1: Sunrise/Sunset, Mode 2: Ramp, Mode 3: Manual."""
        sub = 0xF1 if mode_id == 1 else (0xF2 if mode_id == 2 else 0xF3)
        return bytes([0xFD, sub, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55])

    @staticmethod
    def parse_power_kwh(data: bytes) -> float | None:
        """Parse accumulated energy (kWh) from Smart Plug GATT Notify characteristic."""
        if len(data) >= 8 and data[0] == 0xFC and data[1] == 0xFD:
            raw_val = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5]
            kwh = raw_val * 4.656612873077393e-8
            return round(kwh, 4)
        return None
