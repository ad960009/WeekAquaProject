# WeekAqua BLE Protocol Suite: Windows WPF Controller & Home Assistant Integration

WeekAqua(위크아쿠아) 스마트 수초 조명 및 스마트 플러그 디바이스의 BLE(Bluetooth Low Energy) 통신 프로토콜 역공학 명세서, C# WPF 기반 Windows 전용 제어 프로그램, 그리고 **Home Assistant 전용 Custom Integration & Lovelace UI 카드** 모음입니다.

> [!WARNING]
> **⚠️ 주의사항 (Disclaimer)**  
> 본 프로젝트는 안드로이드 공식 앱(`com.weekled.weekaquas`) 역공학 분석을 기반으로 작성된 **연구 및 시험용 소프트웨어**입니다. 실제 실물 디바이스에서의 완전한 동작 및 안전성을 보장하지 않으며, 본 프로그램 또는 명세서의 이용으로 인해 발생하는 기기 이상이나 데이터 손실에 대해 일체의 책임을 지지 않습니다.

---

## 🌟 핵심 특징 (Key Features)

- 🕒 **BCD(Binary-Coded Decimal) 시간 동기화 & 타이머 제어**
  - MCU 펌웨어 규격에 맞춘 24시간제 BCD 시간 인코딩(22시 $\rightarrow$ `0x22`, 18시 $\rightarrow$ `0x18`) 적용
  - 원클릭 RTC 시계 동기화(`0xFF`) 및 연결 시 자동 동기화 & 상태 초기화(`0xF0`)
- 🌅 **일출 & 일몰 모드 (Simple Sunrise & Sunset Mode)**
  - 시작/종료 시각 및 0h ~ 2.5h Ramp 구간 선택 후 타이머 패킷(`FEF9`) 및 모드 활성화(`FDF1`) 자동 전송
- 📅 **5-Slot / 8-Slot / 12-Slot Ramp-up/down 고급 다중 스케줄 에디터 (Advanced Schedule Mode)**
  - 기기 모델별 하드웨어 슬롯 한도 자동 감지 및 분기 전송 (`5745` 5슬롯, `5746`/`5749`/`5751` 8슬롯, `5747`/`5748`/`5752` 12슬롯)
  - 전 모델 호환 8슬롯 안전 모드 기본 탑재
  - 자정 24:00 (`0x24 0x00`) 무암전(Gapless) 연속 점등 및 스케줄 전송 시 RTC 시계(`0xFF`) 자동 선행 동기화
  - 비활성화 슬롯 자동 초기화 및 스케줄 모드 활성화(`FDF2`) 자동 연동
  - 슬롯 시간 역전/중복 방지 실시간 유효성 검사 (Batch Validation)
- 🏠 **Home Assistant 무제한 단계 동적 스케줄러 (Unlimited Steps)**
  - MCU 슬롯 제한(5/8/12) 없이 원하는 만큼 스케줄 단계를 무제한 구성
  - HA 실시간 선형 보간(Linear Ramp) 엔진 및 ESPHome Bluetooth Proxy 완벽 지원
- ⚡ **실시간 안전 전력 상한선 (Max Power Limit) 가드**
  - 안드로이드 공식 전력 공식(4CH, 5CH, 6CH, 7CH+) 적용
  - $100.0\%$ 초과 시 색상비율을 보존하며 비례 축소하는 자동 정규화(Normalize) 기능
- 🎨 **4채널 RGBW / 4채널 RGB/UV / 5채널 / 6채널 다중 라인업 자동 감지**
  - 기기 모델 코드(`5745`~`5752`) 및 디바이스 네이밍(`_UV`, `MARINE`, `CORAL` 등) 자동 식별
- 💾 **기기별 JSON 설정 영구 보관**
  - MAC 주소별 색상, 팬 속도, 일출일몰, 스케줄 설정을 `%AppData%\WeekAquaWPF\device_config.json`에 자동 저장/로드
- 🔌 **스마트 플러그 전력 모니터링**
  - GATT Notify를 통한 누적 전력량(kWh) 실시간 디코딩 및 UI 표출
- 💻 **CLI(명령줄 인터페이스) 무인 자동화 지원**
  - GUI 창 없이 기기 검색(`scan`), RTC 시계 동기화(`sync-rtc`), 실시간 밝기 조절(`set-spectrum`), 지정 시간 점등 후 자동 소등(`set-timer`), 프리셋 적용(`set-preset`), 팬 속도 조절(`set-fan`) 지원

---

## 📂 프로젝트 구성 (Project Structure)

- 📄 **[WeekAqua_BLE_Protocol_Specification.md](WeekAqua_BLE_Protocol_Specification.md)**: 안드로이드 APK 분석 기반의 상세 BLE 통신 프로토콜 명세서
- 💻 **[WeekAquaWPF/](WeekAquaWPF/)**: .NET 10.0 WPF 기반 Windows 제어 애플리케이션 (GUI 및 CLI 동시 지원)
  - 상세 가이드: [WeekAquaWPF/README.md](WeekAquaWPF/README.md)
- 🏠 **[ha-weekaqua/](ha-weekaqua/)**: Home Assistant Custom Integration (HACS) 및 Lovelace 커스텀 UI 카드
  - 상세 가이드: [ha-weekaqua/README.md](ha-weekaqua/README.md)

---

## 🚀 빠른 시작 (Quick Start)

### 1. Windows Desktop (WeekAquaWPF)
```powershell
cd WeekAquaWPF

# GUI 모드로 실행
dotnet run

# CLI 모드로 실행 (예: 도움말)
dotnet run -- --help
```

### 2. Home Assistant (ha-weekaqua)
- `ha-weekaqua/custom_components/weekaqua` 폴더를 HA `custom_components/`로 복사
- `ha-weekaqua/dist/weekaqua-card.js` 파일을 HA `www/`로 복사 후 대시보드 카드 등록
- 상세 매뉴얼: [ha-weekaqua/README.md](ha-weekaqua/README.md)


