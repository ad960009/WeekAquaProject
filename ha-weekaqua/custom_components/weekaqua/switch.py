"""Switch platform for WeekAqua."""

from __future__ import annotations
from typing import Any

from homeassistant.components.switch import SwitchEntity
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
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
    """Set up WeekAqua switch entities."""
    coordinator: WeekAquaCoordinator = hass.data[DOMAIN][entry.entry_id]
    async_add_entities([
        WeekAquaScheduleSwitch(coordinator),
        WeekAquaMoonlightSwitch(coordinator),
    ])


class WeekAquaScheduleSwitch(CoordinatorEntity[WeekAquaCoordinator], SwitchEntity):
    """Switch to enable/disable unlimited dynamic schedule."""

    _attr_has_entity_name = True
    _attr_icon = "mdi:calendar-clock"

    def __init__(self, coordinator: WeekAquaCoordinator) -> None:
        """Initialize switch."""
        super().__init__(coordinator)
        self._attr_unique_id = f"{coordinator.mac}_schedule_enable"
        self._attr_name = "Dynamic Schedule"

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
        """Return True if schedule is currently running."""
        return self.coordinator.schedule_enabled

    async def async_turn_on(self, **kwargs: Any) -> None:
        """Enable dynamic schedule."""
        await self.coordinator.async_set_schedule_enabled(True)

    async def async_turn_off(self, **kwargs: Any) -> None:
        """Disable dynamic schedule (freeze at current or manual)."""
        await self.coordinator.async_set_schedule_enabled(False)


class WeekAquaMoonlightSwitch(CoordinatorEntity[WeekAquaCoordinator], SwitchEntity):
    """Switch to toggle night moonlight retention."""

    _attr_has_entity_name = True
    _attr_icon = "mdi:moon-waning-crescent"

    def __init__(self, coordinator: WeekAquaCoordinator) -> None:
        """Initialize switch."""
        super().__init__(coordinator)
        self._attr_unique_id = f"{coordinator.mac}_moonlight"
        self._attr_name = "Keep Night Moonlight"

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
        """Return True if moonlight mode is enabled."""
        return self.coordinator.keep_moonlight

    async def async_turn_on(self, **kwargs: Any) -> None:
        """Enable moonlight."""
        self.coordinator.keep_moonlight = True
        self.coordinator.async_set_updated_data(self.coordinator._build_data())

    async def async_turn_off(self, **kwargs: Any) -> None:
        """Disable moonlight."""
        self.coordinator.keep_moonlight = False
        self.coordinator.async_set_updated_data(self.coordinator._build_data())
