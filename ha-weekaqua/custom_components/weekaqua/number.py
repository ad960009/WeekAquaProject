"""Number platform for individual WeekAqua channel control."""

from __future__ import annotations
from typing import Any

from homeassistant.components.number import NumberEntity, NumberMode
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity import DeviceInfo
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import DOMAIN
from .coordinator import WeekAquaCoordinator

CHANNELS = [
    ("red", "Red Channel", "mdi:palette", 0, 100),
    ("green", "Green Channel", "mdi:palette", 0, 100),
    ("blue", "Blue Channel", "mdi:palette", 0, 100),
    ("white", "White Channel", "mdi:palette", 0, 100),
    ("uv", "UV / UVA Channel", "mdi:weather-sunny-alert", 0, 100),
    ("violet", "Violet / UV2 Channel", "mdi:creation", 0, 100),
    ("fan", "Cooling Fan Speed", "mdi:fan", 0, 100),
]


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up WeekAqua number entities."""
    coordinator: WeekAquaCoordinator = hass.data[DOMAIN][entry.entry_id]
    entities = [
        WeekAquaChannelNumber(coordinator, ch_id, ch_name, icon, min_val, max_val)
        for ch_id, ch_name, icon, min_val, max_val in CHANNELS
    ]
    async_add_entities(entities)


class WeekAquaChannelNumber(CoordinatorEntity[WeekAquaCoordinator], NumberEntity):
    """Representation of an individual channel slider."""

    _attr_has_entity_name = True
    _attr_mode = NumberMode.SLIDER
    _attr_native_step = 1.0
    _attr_native_unit_of_measurement = "%"

    def __init__(
        self,
        coordinator: WeekAquaCoordinator,
        ch_id: str,
        ch_name: str,
        icon: str,
        min_val: float,
        max_val: float,
    ) -> None:
        """Initialize the channel number entity."""
        super().__init__(coordinator)
        self.ch_id = ch_id
        self._attr_unique_id = f"{coordinator.mac}_{ch_id}"
        self._attr_name = ch_name
        self._attr_icon = icon
        self._attr_native_min_value = min_val
        self._attr_native_max_value = max_val

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
    def native_value(self) -> float:
        """Return current channel percentage."""
        if self.ch_id == "red":
            return self.coordinator.current_r
        elif self.ch_id == "green":
            return self.coordinator.current_g
        elif self.ch_id == "blue":
            return self.coordinator.current_b
        elif self.ch_id == "white":
            return self.coordinator.current_w
        elif self.ch_id == "uv":
            return self.coordinator.current_uv
        elif self.ch_id == "violet":
            return self.coordinator.current_v
        elif self.ch_id == "fan":
            return self.coordinator.current_fan
        return 0.0

    async def async_set_native_value(self, value: float) -> None:
        """Update channel percentage."""
        if self.ch_id == "fan":
            await self.coordinator.async_set_fan_speed(value)
            return

        r = value if self.ch_id == "red" else self.coordinator.current_r
        g = value if self.ch_id == "green" else self.coordinator.current_g
        b = value if self.ch_id == "blue" else self.coordinator.current_b
        w = value if self.ch_id == "white" else self.coordinator.current_w
        uv = value if self.ch_id == "uv" else self.coordinator.current_uv
        v = value if self.ch_id == "violet" else self.coordinator.current_v

        await self.coordinator.async_set_spectrum(r, g, b, w, uv, v, disable_schedule=True)
