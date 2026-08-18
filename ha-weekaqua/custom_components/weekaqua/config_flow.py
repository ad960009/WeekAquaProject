"""Config flow for WeekAqua Bluetooth integration."""

from __future__ import annotations
import logging
from typing import Any

import voluptuous as vol

from homeassistant import config_entries
from homeassistant.components.bluetooth import BluetoothServiceInfoBleak, async_discovered_service_info
from homeassistant.data_entry_flow import FlowResult

from .const import DOMAIN, SERVICE_UUID, CONF_MAC, CONF_NAME, CONF_MODEL_CODE, DEFAULT_NAME

_LOGGER = logging.getLogger(__name__)


class WeekAquaConfigFlow(config_entries.ConfigFlow, domain=DOMAIN):
    """Handle a config flow for WeekAqua."""

    VERSION = 1

    def __init__(self) -> None:
        """Initialize flow."""
        self._discovery_info: BluetoothServiceInfoBleak | None = None
        self._discovered_devices: dict[str, str] = {}

    async def async_step_bluetooth(self, discovery_info: BluetoothServiceInfoBleak) -> FlowResult:
        """Handle bluetooth discovery by Home Assistant."""
        await self.async_set_unique_id(discovery_info.address)
        self._abort_if_unique_id_configured()

        self._discovery_info = discovery_info
        return await self.async_step_bluetooth_confirm()

    async def async_step_bluetooth_confirm(self, user_input: dict[str, Any] | None = None) -> FlowResult:
        """Confirm discovery of a WeekAqua device."""
        assert self._discovery_info is not None

        if user_input is not None:
            model_code = self._extract_model_code(self._discovery_info)
            return self.async_create_entry(
                title=user_input.get(CONF_NAME, self._discovery_info.name or DEFAULT_NAME),
                data={
                    CONF_MAC: self._discovery_info.address,
                    CONF_NAME: user_input.get(CONF_NAME, self._discovery_info.name or DEFAULT_NAME),
                    CONF_MODEL_CODE: model_code,
                },
            )

        name = self._discovery_info.name or DEFAULT_NAME
        return self.async_show_form(
            step_id="bluetooth_confirm",
            description_placeholders={"name": name, "address": self._discovery_info.address},
            data_schema=vol.Schema({
                vol.Optional(CONF_NAME, default=name): str,
            }),
        )

    async def async_step_user(self, user_input: dict[str, Any] | None = None) -> FlowResult:
        """Handle user-initiated setup (scan or manual MAC input)."""
        errors: dict[str, str] = {}

        if user_input is not None:
            mac = user_input[CONF_MAC].strip().upper()
            await self.async_set_unique_id(mac)
            self._abort_if_unique_id_configured()

            name = user_input.get(CONF_NAME, f"WeekAqua ({mac})")
            return self.async_create_entry(
                title=name,
                data={
                    CONF_MAC: mac,
                    CONF_NAME: name,
                    CONF_MODEL_CODE: user_input.get(CONF_MODEL_CODE, ""),
                },
            )

        # Populate discovered WeekAqua devices
        discovered = {}
        for service_info in async_discovered_service_info(self.hass, connectable=True):
            if SERVICE_UUID.lower() in [s.lower() for s in service_info.service_uuids]:
                discovered[service_info.address] = f"{service_info.name or 'WeekAqua'} ({service_info.address})"

        if discovered:
            return self.async_show_form(
                step_id="user",
                data_schema=vol.Schema({
                    vol.Required(CONF_MAC): vol.In(discovered),
                    vol.Optional(CONF_NAME, default=DEFAULT_NAME): str,
                }),
                errors=errors,
            )

        # Fallback to manual MAC address entry
        return self.async_show_form(
            step_id="user",
            data_schema=vol.Schema({
                vol.Required(CONF_MAC): str,
                vol.Optional(CONF_NAME, default=DEFAULT_NAME): str,
            }),
            errors=errors,
        )

    def _extract_model_code(self, discovery_info: BluetoothServiceInfoBleak) -> str:
        """Extract 2-byte WeekAqua model code from Manufacturer Data or Service Data."""
        # Manufacturer Data heuristic from ScanRecord
        try:
            for m_id, m_data in discovery_info.manufacturer_data.items():
                if len(m_data) >= 2:
                    return f"{m_data[0]:02X}{m_data[1]:02X}"
        except Exception:
            pass
        return ""
