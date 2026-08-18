"""Constants for WeekAqua Home Assistant Integration."""

DOMAIN = "weekaqua"

# BLE GATT UUIDs
SERVICE_UUID = "00010203-0405-0607-0809-0a0b0c0d1911"
WRITE_CHAR_UUID = "00010203-0405-0607-0809-0a0b0c0d1912"
NOTIFY_CHAR_UUID = "00010203-0405-0607-0809-0a0b0c0d1914"

# Configuration Keys
CONF_MAC = "mac"
CONF_NAME = "name"
CONF_MODEL_CODE = "model_code"
CONF_MAX_SLOTS = "max_slots"
CONF_CHANNELS = "channels"
CONF_KEEP_MOONLIGHT = "keep_moonlight"
CONF_SCHEDULE = "schedule"
CONF_SCHEDULE_INTERVAL = "schedule_interval"

# Defaults
DEFAULT_NAME = "WeekAqua Light"
DEFAULT_SCHEDULE_INTERVAL = 60  # seconds between ramp updates

# Model Definitions
MODEL_5745 = "5745"  # Mode 1 / 2 (Classic 4-CH 5-Slot)
MODEL_5746 = "5746"  # Old 4-CH (8-Slot)
MODEL_5747 = "5747"  # Mode 3 (4-CH Pro 12-Slot)
MODEL_5748 = "5748"  # Mode 5 (5-CH RGBW+UV 12-Slot)
MODEL_5749 = "5749"  # Mode 6 (6-CH Multi-Spectrum 8-Slot)
MODEL_5750 = "5750"  # Smart Plug Power Meter
MODEL_5751 = "5751"  # Mode 8 (7+ CH Advanced 8-Slot)
MODEL_5752 = "5752"  # Mode 9 (7+ CH Advanced 12-Slot)

# Spectrum Presets: (R, G, B, W, UV, Violet) in percentages (0.0 ~ 100.0)
PRESETS = {
    "GreenGrass": {
        "name": "Green Plant / 수초 (녹색)",
        "r": 50.0, "g": 90.0, "b": 60.0, "w": 80.0, "uv": 40.0, "v": 30.0
    },
    "RedGrass": {
        "name": "Red Plant / 수초 (붉은색)",
        "r": 100.0, "g": 30.0, "b": 40.0, "w": 80.0, "uv": 70.0, "v": 60.0
    },
    "FishMixed": {
        "name": "Mixed Community / 혼양 (수초+열대어)",
        "r": 70.0, "g": 80.0, "b": 90.0, "w": 90.0, "uv": 50.0, "v": 40.0
    },
    "Shrimp": {
        "name": "Shrimp / 새우 전용",
        "r": 40.0, "g": 60.0, "b": 100.0, "w": 70.0, "uv": 30.0, "v": 20.0
    },
    "Fish": {
        "name": "Tropical Fish / 열대어 관상",
        "r": 60.0, "g": 50.0, "b": 100.0, "w": 60.0, "uv": 40.0, "v": 30.0
    },
    "CoralAb": {
        "name": "Coral AB+ / 산호 성장 (해수)",
        "r": 20.0, "g": 40.0, "b": 100.0, "w": 20.0, "uv": 100.0, "v": 90.0
    },
    "CoralLps": {
        "name": "LPS Coral / 연산호",
        "r": 30.0, "g": 50.0, "b": 100.0, "w": 40.0, "uv": 90.0, "v": 80.0
    },
    "CoralSps": {
        "name": "SPS Coral / 경산호",
        "r": 40.0, "g": 60.0, "b": 100.0, "w": 50.0, "uv": 100.0, "v": 95.0
    },
    "MarineFot": {
        "name": "Marine Fish / 해수어 관상",
        "r": 30.0, "g": 40.0, "b": 100.0, "w": 80.0, "uv": 60.0, "v": 50.0
    },
    "Moonlight": {
        "name": "Moonlight / 심야 달빛",
        "r": 0.0, "g": 0.0, "b": 4.0, "w": 0.0, "uv": 0.0, "v": 0.0
    }
}
