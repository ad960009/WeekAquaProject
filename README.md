# WeekAqua BLE Protocol Specification & C# WPF Controller

WeekAqua(위크아쿠아) 스마트 수초 조명 및 스마트 플러그 디바이스의 BLE(Bluetooth Low Energy) 통신 프로토콜 역공학 명세서 및 C# WPF 기반 Windows 전용 제어 프로그램입니다.

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
- 📅 **8-Slot / 12-Slot Ramp-up/down 고급 다중 스케줄 에디터 (Advanced Schedule Mode)**
  - 4CH(최대 8슬롯) 및 5/6CH(최대 12슬롯) 전 모델 MCU 펌웨어 완벽 호환 8슬롯 안전 모드 기본 탑재
  - 자정 24:00 (`0x24 0x00`) 무암전(Gapless) 연속 점등 및 스케줄 전송 시 RTC 시계(`0xFF`) 자동 선행 동기화
  - 비활성화 슬롯 자동 초기화 및 스케줄 모드 활성화(`FDF2`) 자동 연동
  - 슬롯 시간 역전/중복 방지 실시간 유효성 검사 (Batch Validation)
- ⚡ **실시간 안전 전력 상한선 (Max Power Limit) 가드**
  - 안드로이드 공식 전력 공식(4CH, 5CH, 6CH, 7CH+) 적용
  - $100.0\%$ 초과 시 색상비율을 보존하며 비례 축소하는 자동 정규화(Normalize) 기능
- 🎨 **4채널 RGBW / 4채널 RGB/UV / 5채널 / 6채널 다중 라인업 자동 감지**
  - 기기 모델 코드(`5745`~`5752`) 및 디바이스 네이밍(`_UV`, `MARINE`, `CORAL` 등) 자동 식별
- 💾 **기기별 JSON 설정 영구 보관**
  - MAC 주소별 색상, 팬 속도, 일출일몰, 스케줄 설정을 `%AppData%\WeekAquaWPF\device_config.json`에 자동 저장/로드
- 🔌 **스마트 플러그 전력 모니터링**
  - GATT Notify를 통한 누적 전력량(kWh) 실시간 디코딩 및 UI 표출

---

## 📂 프로젝트 구성

- 📄 **[WeekAqua_BLE_Protocol_Specification.md](WeekAqua_BLE_Protocol_Specification.md)**: 안드로이드 APK 분석 기반의 상세 BLE 통신 프로토콜 명세서
- 💻 **[WeekAquaWPF/](WeekAquaWPF/)**: .NET 10.0 WPF 기반 Windows 제어 애플리케이션
  - 상세 가이드: [WeekAquaWPF/README.md](WeekAquaWPF/README.md)

---

## 🚀 빠른 시작 (Quick Start)

### 요구 사항
- **OS**: Windows 10 (버전 2004 / 빌드 19041 이상) 또는 Windows 11
- **SDK**: .NET 10.0 SDK (또는 .NET 8.0/9.0)
- **하드웨어**: Bluetooth 4.0 이상 지원 블루투스 어댑터

### 실행 및 빌드

```powershell
# 프로젝트 디렉터리로 이동
cd WeekAquaWPF

# 1. 개발 모드 빌드 및 실행
dotnet run

# 2. 단일 실행 파일(.exe) 원클릭 배포 (Self-Contained & Framework-Dependent 2종 자동 생성)
publish.bat
```

