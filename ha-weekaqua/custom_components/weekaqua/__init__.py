"""WeekAqua Home Assistant Custom Integration."""

from __future__ import annotations
import logging
from typing import Any

import voluptuous as vol

from homeassistant.config_entries import ConfigEntry
from homeassistant.const import Platform
from homeassistant.core import HomeAssistant, ServiceCall
from homeassistant.helpers import config_validation as cv

from .const import DOMAIN, PRESETS
from .coordinator import WeekAquaCoordinator
from .protocol import WeekAquaProtocol

_LOGGER = logging.getLogger(__name__)

PLATFORMS: list[Platform] = [
    Platform.LIGHT,
    Platform.NUMBER,
    Platform.SENSOR,
    Platform.SWITCH,
]

# Service Schemas
SERVICE_APPLY_PRESET = "apply_preset"
SERVICE_SET_SPECTRUM = "set_spectrum"
SERVICE_SET_SCHEDULE = "set_schedule"
SERVICE_SYNC_RTC = "sync_rtc"

SCHEMA_APPLY_PRESET = vol.Schema({
    vol.Required("device_id"): cv.string,
    vol.Required("preset"): vol.In(list(PRESETS.keys())),
})

SCHEMA_SET_SPECTRUM = vol.Schema({
    vol.Required("device_id"): cv.string,
    vol.Required("red"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Required("green"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Required("blue"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Required("white"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Optional("uv", default=0.0): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Optional("violet", default=0.0): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
    vol.Optional("disable_schedule", default=True): cv.boolean,
})

SCHEMA_SET_SCHEDULE = vol.Schema({
    vol.Required("device_id"): cv.string,
    vol.Required("points"): vol.All(
        cv.ensure_list,
        [
            vol.Schema({
                vol.Required("time"): cv.string,
                vol.Required("r"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
                vol.Required("g"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
                vol.Required("b"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
                vol.Required("w"): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
                vol.Optional("uv", default=0.0): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
                vol.Optional("v", default=0.0): vol.All(vol.Coerce(float), vol.Range(min=0, max=100)),
            })
        ],
    ),
})


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up WeekAqua from a config entry."""
    hass.data.setdefault(DOMAIN, {})

    coordinator = WeekAquaCoordinator(hass, entry.data)
    await coordinator.async_setup()
    hass.data[DOMAIN][entry.entry_id] = coordinator

    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)

    # Register Custom Services
    async def handle_apply_preset(call: ServiceCall) -> None:
        preset_key = call.data["preset"]
        if preset_key in PRESETS:
            preset = PRESETS[preset_key]
            for coord in hass.data[DOMAIN].values():
                await coord.async_set_spectrum(
                    preset["r"], preset["g"], preset["b"], preset["w"],
                    preset.get("uv", 0), preset.get("v", 0)
                )

    async def handle_set_spectrum(call: ServiceCall) -> None:
        r = call.data["red"]
        g = call.data["green"]
        b = call.data["blue"]
        w = call.data["white"]
        uv = call.data.get("uv", 0.0)
        violet = call.data.get("violet", 0.0)
        disable_sched = call.data.get("disable_schedule", True)
        for coord in hass.data[DOMAIN].values():
            await coord.async_set_spectrum(r, g, b, w, uv, violet, disable_schedule=disable_sched)

    async def handle_set_schedule(call: ServiceCall) -> None:
        points = call.data["points"]
        for coord in hass.data[DOMAIN].values():
            await coord.async_set_schedule(points)

    async def handle_sync_rtc(call: ServiceCall) -> None:
        for coord in hass.data[DOMAIN].values():
            await coord.enqueue_packet(WeekAquaProtocol.build_rtc_sync_packet())

    if not hass.services.has(DOMAIN, SERVICE_APPLY_PRESET):
        hass.services.async_register(DOMAIN, SERVICE_APPLY_PRESET, handle_apply_preset, schema=SCHEMA_APPLY_PRESET)
        hass.services.async_register(DOMAIN, SERVICE_SET_SPECTRUM, handle_set_spectrum, schema=SCHEMA_SET_SPECTRUM)
        hass.services.async_register(DOMAIN, SERVICE_SET_SCHEDULE, handle_set_schedule, schema=SCHEMA_SET_SCHEDULE)
        hass.services.async_register(DOMAIN, SERVICE_SYNC_RTC, handle_sync_rtc)

    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload a config entry."""
    coordinator: WeekAquaCoordinator = hass.data[DOMAIN].pop(entry.entry_id)
    await coordinator.async_unload()

    return await hass.config_entries.async_unload_platforms(entry, PLATFORMS)
