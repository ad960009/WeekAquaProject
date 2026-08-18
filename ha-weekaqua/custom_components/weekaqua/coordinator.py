"""DataUpdateCoordinator and Schedule Engine for WeekAqua."""

from __future__ import annotations
import asyncio
from datetime import datetime, time, timedelta
import logging
from typing import Any

from bleak import BleakClient
from bleak.exc import BleakError

from homeassistant.components import bluetooth
from homeassistant.core import HomeAssistant, callback
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator

from .const import (
    DOMAIN,
    SERVICE_UUID,
    WRITE_CHAR_UUID,
    NOTIFY_CHAR_UUID,
    CONF_MAC,
    CONF_NAME,
    CONF_MODEL_CODE,
    CONF_KEEP_MOONLIGHT,
    CONF_SCHEDULE,
    CONF_SCHEDULE_INTERVAL,
    DEFAULT_SCHEDULE_INTERVAL,
)
from .protocol import WeekAquaProtocol, NormalizedSpectrum

_LOGGER = logging.getLogger(__name__)


def parse_time_str(time_str: str) -> time:
    """Parse HH:MM or HH:MM:SS string to datetime.time."""
    parts = [int(p) for p in time_str.strip().split(":")]
    if len(parts) == 2:
        return time(parts[0], parts[1], 0)
    elif len(parts) >= 3:
        return time(parts[0], parts[1], parts[2])
    return time(0, 0, 0)


class WeekAquaCoordinator(DataUpdateCoordinator[dict[str, Any]]):
    """Coordinator managing WeekAqua BLE communication and the Unlimited Dynamic Schedule Engine."""

    def __init__(self, hass: HomeAssistant, entry_data: dict[str, Any]) -> None:
        """Initialize the WeekAqua coordinator."""
        self.mac: str = entry_data[CONF_MAC]
        self.device_name: str = entry_data.get(CONF_NAME, "WeekAqua")
        self.model_code: str = entry_data.get(CONF_MODEL_CODE, "")
        self.keep_moonlight: bool = entry_data.get(CONF_KEEP_MOONLIGHT, True)
        self.schedule_interval: int = entry_data.get(CONF_SCHEDULE_INTERVAL, DEFAULT_SCHEDULE_INTERVAL)

        # Dynamic Unlimited Schedule Waypoints: list of dicts {"time": "08:00", "r": 0, "g": 0, "b": 0, "w": 0, "uv": 0, "v": 0}
        self.schedule_points: list[dict[str, Any]] = entry_data.get(CONF_SCHEDULE, self._get_default_schedule())
        self.schedule_enabled: bool = True

        # Current live channel state (0.0 ~ 100.0)
        self.current_r: float = 0.0
        self.current_g: float = 0.0
        self.current_b: float = 0.0
        self.current_w: float = 0.0
        self.current_uv: float = 0.0
        self.current_v: float = 0.0
        self.current_fan: float = 50.0

        # Sensor state
        self.is_connected: bool = False
        self.power_kwh: float = 0.0
        self.total_power_pct: float = 0.0

        # Bleak Client & Lock
        self._client: BleakClient | None = None
        self._lock = asyncio.Lock()
        self._write_queue: asyncio.Queue[bytes] = asyncio.Queue()
        self._queue_task: asyncio.Task | None = None
        self._schedule_unsub: Any = None
        self._last_sent_spectrum: bytes | None = None

        super().__init__(
            hass,
            _LOGGER,
            name=f"{DOMAIN}_{self.mac}",
            update_interval=timedelta(seconds=self.schedule_interval),
        )

    def _get_default_schedule(self) -> list[dict[str, Any]]:
        """Default natural 8-point smooth aquarium photoperiod."""
        return [
            {"time": "08:00", "r": 0.0, "g": 0.0, "b": 0.0, "w": 0.0, "uv": 0.0, "v": 0.0},
            {"time": "09:30", "r": 20.0, "g": 30.0, "b": 20.0, "w": 30.0, "uv": 10.0, "v": 10.0},
            {"time": "11:30", "r": 60.0, "g": 85.0, "b": 60.0, "w": 75.0, "uv": 40.0, "v": 30.0},
            {"time": "14:00", "r": 70.0, "g": 100.0, "b": 70.0, "w": 90.0, "uv": 50.0, "v": 40.0},
            {"time": "17:00", "r": 50.0, "g": 75.0, "b": 50.0, "w": 70.0, "uv": 30.0, "v": 20.0},
            {"time": "19:00", "r": 25.0, "g": 30.0, "b": 20.0, "w": 25.0, "uv": 10.0, "v": 5.0},
            {"time": "20:30", "r": 0.0, "g": 0.0, "b": 4.0, "w": 0.0, "uv": 0.0, "v": 0.0},
            {"time": "23:00", "r": 0.0, "g": 0.0, "b": 0.0, "w": 0.0, "uv": 0.0, "v": 0.0},
        ]

    async def async_setup(self) -> None:
        """Start background queue worker and initial sync."""
        self._queue_task = asyncio.create_task(self._process_write_queue())
        await self.async_refresh()

    async def async_unload(self) -> None:
        """Stop tasks and disconnect BLE client."""
        if self._queue_task:
            self._queue_task.cancel()
        await self.async_disconnect()

    # --- BLE Connection & Write Queue ---

    async def _ensure_connected(self) -> bool:
        """Ensure Bleak connection using Home Assistant Bluetooth backend / BLE Proxy."""
        if self._client and self._client.is_connected:
            return True

        ble_device = bluetooth.async_ble_device_from_address(self.hass, self.mac, connectable=True)
        if not ble_device:
            _LOGGER.debug("BLE device %s not found in HA Bluetooth scanner cache", self.mac)
            return False

        try:
            self._client = BleakClient(ble_device, disconnected_callback=self._on_disconnected)
            await self._client.connect()
            self.is_connected = True
            _LOGGER.info("Connected to WeekAqua BLE (%s) successfully", self.mac)

            # Subscribe to GATT Notify for Smart Plug Power Meter
            try:
                await self._client.start_notify(NOTIFY_CHAR_UUID, self._on_notify)
            except Exception as notify_err:
                _LOGGER.debug("Notify subscription on %s skipped: %s", self.mac, notify_err)

            # Sync RTC time and query initial state
            await self.enqueue_packet(WeekAquaProtocol.build_rtc_sync_packet())
            await self.enqueue_packet(WeekAquaProtocol.build_state_init_packet())
            return True
        except (BleakError, asyncio.TimeoutError) as err:
            _LOGGER.warning("Failed to connect to WeekAqua (%s): %s", self.mac, err)
            self.is_connected = False
            self._client = None
            return False

    def _on_disconnected(self, client: BleakClient) -> None:
        """Callback when BLE device disconnects."""
        self.is_connected = False
        self._client = None
        _LOGGER.info("WeekAqua (%s) disconnected", self.mac)
        self.async_set_updated_data(self._build_data())

    def _on_notify(self, sender: Any, data: bytearray) -> None:
        """Handle incoming notify packets from WeekAqua Smart Plug."""
        kwh = WeekAquaProtocol.parse_power_kwh(bytes(data))
        if kwh is not None:
            self.power_kwh = kwh
            self.async_set_updated_data(self._build_data())

    async def async_disconnect(self) -> None:
        """Explicitly disconnect BLE client."""
        async with self._lock:
            if self._client and self._client.is_connected:
                try:
                    await self._client.disconnect()
                except Exception:
                    pass
            self.is_connected = False
            self._client = None

    async def enqueue_packet(self, packet: bytes) -> None:
        """Enqueue an 8-byte command packet to be transmitted."""
        await self._write_queue.put(packet)

    async def _process_write_queue(self) -> None:
        """Sequential writer with 300ms pacing to prevent packet collision."""
        while True:
            packet = await self._write_queue.get()
            try:
                async with self._lock:
                    connected = await self._ensure_connected()
                    if connected and self._client and self._client.is_connected:
                        await self._client.write_gatt_char(WRITE_CHAR_UUID, packet, response=False)
                        _LOGGER.debug("TX -> %s: %s", self.mac, packet.hex().upper())
                        await asyncio.sleep(0.3)
                    else:
                        _LOGGER.debug("Skipped TX (device offline): %s", packet.hex().upper())
            except Exception as ex:
                _LOGGER.error("BLE TX error on %s: %s", self.mac, ex)
            finally:
                self._write_queue.task_done()

    # --- Unlimited Dynamic Schedule & Linear Ramp Engine ---

    def calculate_interpolated_spectrum(self, target_time: time | None = None) -> NormalizedSpectrum:
        """Calculate exact interpolated spectrum at given time using N waypoints."""
        if not self.schedule_points:
            return NormalizedSpectrum(0, 0, 0, 0, 0, 0)

        if target_time is None:
            target_time = datetime.now().time()

        now_sec = target_time.hour * 3600 + target_time.minute * 60 + target_time.second

        # Convert waypoints to (seconds, spectrum)
        points: list[tuple[int, dict[str, float]]] = []
        for pt in self.schedule_points:
            t = parse_time_str(pt["time"])
            sec = t.hour * 3600 + t.minute * 60 + t.second
            points.append((sec, {
                "r": float(pt.get("r", 0)),
                "g": float(pt.get("g", 0)),
                "b": float(pt.get("b", 0)),
                "w": float(pt.get("w", 0)),
                "uv": float(pt.get("uv", 0)),
                "v": float(pt.get("v", 0)),
            }))

        points.sort(key=lambda x: x[0])

        if len(points) == 1:
            p = points[0][1]
            return WeekAquaProtocol.normalize_spectrum_to_max_power(p["r"], p["g"], p["b"], p["w"], p["uv"], p["v"], self.model_code)

        # Find surrounding waypoints (p1 -> p2)
        p1 = points[-1]
        p2 = points[0]

        for i in range(len(points) - 1):
            if points[i][0] <= now_sec < points[i + 1][0]:
                p1 = points[i]
                p2 = points[i + 1]
                break

        # Calculate progress ratio t in [0.0, 1.0]
        t1, spec1 = p1
        t2, spec2 = p2

        if t2 > t1:
            progress = (now_sec - t1) / (t2 - t1)
        else:
            # Wrap around midnight (e.g. 23:00 to 08:00)
            span = (86400 - t1) + t2
            elapsed = (now_sec - t1) if now_sec >= t1 else (86400 - t1 + now_sec)
            progress = elapsed / span if span > 0 else 0.0

        progress = max(0.0, min(1.0, progress))

        # Linear Interpolation (Lerp)
        lerp_r = spec1["r"] + (spec2["r"] - spec1["r"]) * progress
        lerp_g = spec1["g"] + (spec2["g"] - spec1["g"]) * progress
        lerp_b = spec1["b"] + (spec2["b"] - spec1["b"]) * progress
        lerp_w = spec1["w"] + (spec2["w"] - spec1["w"]) * progress
        lerp_uv = spec1["uv"] + (spec2["uv"] - spec1["uv"]) * progress
        lerp_v = spec1["v"] + (spec2["v"] - spec1["v"]) * progress

        return WeekAquaProtocol.normalize_spectrum_to_max_power(lerp_r, lerp_g, lerp_b, lerp_w, lerp_uv, lerp_v, self.model_code)

    async def _async_update_data(self) -> dict[str, Any]:
        """Periodic schedule evaluator and BLE synchronization tick."""
        if self.schedule_enabled and self.schedule_points:
            target = self.calculate_interpolated_spectrum()
            self.current_r = target.r
            self.current_g = target.g
            self.current_b = target.b
            self.current_w = target.w
            self.current_uv = target.uv
            self.current_v = target.violet

            packet = WeekAquaProtocol.build_live_spectrum_packet(
                self.current_r, self.current_g, self.current_b, self.current_w,
                self.current_uv, self.current_v, self.model_code
            )

            # Send only if spectrum changed significantly or on reconnection
            if packet != self._last_sent_spectrum or not self.is_connected:
                self._last_sent_spectrum = packet
                await self.enqueue_packet(packet)

        self.total_power_pct = WeekAquaProtocol.calculate_total_power_percent(
            self.current_r, self.current_g, self.current_b, self.current_w,
            self.current_uv, self.current_v, self.model_code
        )

        return self._build_data()

    def _build_data(self) -> dict[str, Any]:
        """Construct dictionary of current state for HA entities."""
        return {
            "connected": self.is_connected,
            "r": self.current_r,
            "g": self.current_g,
            "b": self.current_b,
            "w": self.current_w,
            "uv": self.current_uv,
            "v": self.current_v,
            "fan": self.current_fan,
            "power_pct": self.total_power_pct,
            "power_kwh": self.power_kwh,
            "schedule_enabled": self.schedule_enabled,
            "schedule_points": self.schedule_points,
        }

    # --- Public Control Methods (Called by Entities & Services) ---

    async def async_set_spectrum(
        self,
        r: float,
        g: float,
        b: float,
        w: float,
        uv: float = 0.0,
        violet: float = 0.0,
        disable_schedule: bool = True
    ) -> None:
        """Set manual spectrum immediately."""
        if disable_schedule:
            self.schedule_enabled = False

        norm = WeekAquaProtocol.normalize_spectrum_to_max_power(r, g, b, w, uv, violet, self.model_code)
        self.current_r = norm.r
        self.current_g = norm.g
        self.current_b = norm.b
        self.current_w = norm.w
        self.current_uv = norm.uv
        self.current_v = norm.violet

        packet = WeekAquaProtocol.build_live_spectrum_packet(
            self.current_r, self.current_g, self.current_b, self.current_w,
            self.current_uv, self.current_v, self.model_code
        )
        self._last_sent_spectrum = packet
        await self.enqueue_packet(packet)
        self.async_set_updated_data(self._build_data())

    async def async_set_fan_speed(self, fan_pct: float) -> None:
        """Set fan speed (0.0 ~ 100.0%)."""
        self.current_fan = max(0.0, min(100.0, float(fan_pct)))
        packet = WeekAquaProtocol.build_fan_speed_packet(self.current_fan)
        await self.enqueue_packet(packet)
        self.async_set_updated_data(self._build_data())

    async def async_set_schedule(self, points: list[dict[str, Any]]) -> None:
        """Update unlimited schedule waypoints and re-evaluate immediately."""
        self.schedule_points = points
        self.schedule_enabled = True
        await self.async_refresh()

    async def async_set_schedule_enabled(self, enabled: bool) -> None:
        """Toggle dynamic schedule on or off."""
        self.schedule_enabled = enabled
        if enabled:
            await self.async_refresh()
        else:
            self.async_set_updated_data(self._build_data())
