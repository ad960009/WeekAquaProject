"""Sensor platform for WeekAqua."""

from __future__ import annotations
from typing import Any

from homeassistant.components.sensor import (
    SensorDeviceClass,
    SensorEntity,
    SensorStateClass,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import UnitOfEnergy, PERCENTAGE
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
    """Set up WeekAqua sensor entities."""
    coordinator: WeekAquaCoordinator = hass.data[DOMAIN][entry.entry_id]
    async_add_entities([
        WeekAquaPowerLoadSensor(coordinator),
        WeekAquaEnergySensor(coordinator),
    ])


class WeekAquaPowerLoadSensor(CoordinatorEntity[WeekAquaCoordinator], SensorEntity):
    """Total lighting power load percentage sensor (0.0 ~ 100.0%)."""

    _attr_has_entity_name = True
    _attr_native_unit_of_measurement = PERCENTAGE
    _attr_state_class = SensorStateClass.MEASUREMENT
    _attr_icon = "mdi:gauge"

    def __init__(self, coordinator: WeekAquaCoordinator) -> None:
        """Initialize power load sensor."""
        super().__init__(coordinator)
        self._attr_unique_id = f"{coordinator.mac}_power_load"
        self._attr_name = "Total Power Load"

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
        """Return current total power percentage."""
        return self.coordinator.total_power_pct


class WeekAquaEnergySensor(CoordinatorEntity[WeekAquaCoordinator], SensorEntity):
    """Accumulated energy meter (kWh) from Smart Plug GATT Notify."""

    _attr_has_entity_name = True
    _attr_device_class = SensorDeviceClass.ENERGY
    _attr_state_class = SensorStateClass.TOTAL_INCREASING
    _attr_native_unit_of_measurement = UnitOfEnergy.KILO_WATT_HOUR
    _attr_icon = "mdi:lightning-bolt-circle"

    def __init__(self, coordinator: WeekAquaCoordinator) -> None:
        """Initialize energy sensor."""
        super().__init__(coordinator)
        self._attr_unique_id = f"{coordinator.mac}_energy"
        self._attr_name = "Energy Consumed"

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
        """Return accumulated energy in kWh."""
        return self.coordinator.power_kwh
