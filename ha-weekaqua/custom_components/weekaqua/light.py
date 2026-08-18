"""Light platform for WeekAqua."""

from __future__ import annotations
from typing import Any

from homeassistant.components.light import (
    ATTR_BRIGHTNESS,
    ATTR_RGBW_COLOR,
    ColorMode,
    LightEntity,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, callback
from homeassistant.helpers.entity import DeviceInfo
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import DOMAIN
from .coordinator import WeekAquaCoordinator


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up WeekAqua light entity."""
    coordinator: WeekAquaCoordinator = hass.data[DOMAIN][entry.entry_id]
    async_add_entities([WeekAquaLight(coordinator, entry)])


class WeekAquaLight(CoordinatorEntity[WeekAquaCoordinator], LightEntity):
    """Representation of the Master WeekAqua Aquarium Light."""

    _attr_has_entity_name = True
    _attr_color_mode = ColorMode.RGBW
    _attr_supported_color_modes = {ColorMode.RGBW, ColorMode.BRIGHTNESS, ColorMode.ONOFF}

    def __init__(self, coordinator: WeekAquaCoordinator, entry: ConfigEntry) -> None:
        """Initialize the light."""
        super().__init__(coordinator)
        self._entry = entry
        self._attr_unique_id = f"{coordinator.mac}_light"
        self._attr_name = "Aquarium Light"

    @property
    def device_info(self) -> DeviceInfo:
        """Return device registry info."""
        return DeviceInfo(
            identifiers={(DOMAIN, self.coordinator.mac)},
            name=self.coordinator.device_name,
            manufacturer="WeekAqua",
            model=f"WeekAqua ({self.coordinator.model_code or 'BLE'})",
        )

    @property
    def is_on(self) -> bool:
        """Return True if any channel has output > 0."""
        return (
            self.coordinator.current_r > 0
            or self.coordinator.current_g > 0
            or self.coordinator.current_b > 0
            or self.coordinator.current_w > 0
            or self.coordinator.current_uv > 0
            or self.coordinator.current_v > 0
        )

    @property
    def brightness(self) -> int:
        """Return 0~255 master brightness based on maximum channel output."""
        max_ch = max(
            self.coordinator.current_r,
            self.coordinator.current_g,
            self.coordinator.current_b,
            self.coordinator.current_w,
            self.coordinator.current_uv,
            self.coordinator.current_v,
        )
        return int(round(max_ch * 2.55))

    @property
    def rgbw_color(self) -> tuple[int, int, int, int]:
        """Return current RGBW channel intensities scaled to 0-255."""
        return (
            int(round(self.coordinator.current_r * 2.55)),
            int(round(self.coordinator.current_g * 2.55)),
            int(round(self.coordinator.current_b * 2.55)),
            int(round(self.coordinator.current_w * 2.55)),
        )

    async def async_turn_on(self, **kwargs: Any) -> None:
        """Turn on the light or apply requested color/brightness."""
        if ATTR_RGBW_COLOR in kwargs:
            rgbw = kwargs[ATTR_RGBW_COLOR]
            r = rgbw[0] / 2.55
            g = rgbw[1] / 2.55
            b = rgbw[2] / 2.55
            w = rgbw[3] / 2.55
            await self.coordinator.async_set_spectrum(r, g, b, w, self.coordinator.current_uv, self.coordinator.current_v)
        elif ATTR_BRIGHTNESS in kwargs:
            target_scale = kwargs[ATTR_BRIGHTNESS] / 255.0
            cur_max = max(1.0, self.brightness / 2.55)
            ratio = (target_scale * 100.0) / cur_max
            await self.coordinator.async_set_spectrum(
                self.coordinator.current_r * ratio,
                self.coordinator.current_g * ratio,
                self.coordinator.current_b * ratio,
                self.coordinator.current_w * ratio,
                self.coordinator.current_uv * ratio,
                self.coordinator.current_v * ratio,
            )
        else:
            # Re-enable dynamic schedule or turn to last set spectrum
            await self.coordinator.async_set_schedule_enabled(True)

    async def async_turn_off(self, **kwargs: Any) -> None:
        """Turn off all light channels."""
        await self.coordinator.async_set_spectrum(0, 0, 0, 0, 0, 0, disable_schedule=True)
