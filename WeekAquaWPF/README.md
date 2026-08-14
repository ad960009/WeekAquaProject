# ⚡ WeekAqua BLE Controller (C# WPF)

WeekAqua 스마트 수초 조명 및 스마트 플러그 디바이스를 Windows 환경에서 제어하기 위한 C# .NET 10.0 WPF 기반 Bluetooth Low Energy (BLE) 애플리케이션입니다.

> [!WARNING]
> **⚠️ 주의사항 (Disclaimer)**  
> 본 프로그램은 안드로이드 공식 앱 디컴파일 분석을 바탕으로 제작된 시험용(Experimental) 소프트웨어입니다. 실물 디바이스 제어 시 발생하는 문제에 대해서는 책임을 지지 않으므로 테스트 및 연구 목적으로 활용해 주시기 바랍니다.

---

## 🌟 주요 기능 (Features)

1. **Bluetooth Low Energy (BLE) 자동 검색 및 스마트 연결**
   - 주변 WeekAqua 기기 실시간 탐지 (RSSI 신호 세기 표시)
   - 주력 기기(`0000FFE0` 시리즈) 및 호환 기기(`0000FFF0` 시리즈) GATT Service/Characteristic 자동 탐지
   - 연결 직후 **BCD 포맷 RTC 시간 동기화(`0xFF`) 및 기기 상태 초기화(`0xF0`)** 자동 수행
   - 실제 기기 없이도 UI와 기능을 검증할 수 있는 **가상 데모 디바이스(Virtual Demo Device)** 내장

2. **수동 라이브 스펙트럼 (Live Spectrum) 제어**
   - Red, Green, Blue, White/UV, UV2/Violet 채널별 백분율($0\% \sim 100\%$) 슬라이더 제어
   - 안드로이드 공식 선형 인코딩 적용 ($0\% \rightarrow \mathtt{0x00}$, $100\% \rightarrow \mathtt{0xEB}$, 235단계)
   - 실시간 전력량 계산 및 $100\%$ 초과 방지 안전 가드
   - 단일 클릭 프리셋 스펙트럼(녹색수초, 적색수초, 혼양, 산호 LPS/SPS, Coral AB+ 등) 적용

3. **간편 일출 & 일몰 모드 (Simple Sunrise & Sunset Mode)**
   - 시작 시간(예: 08:00) 및 종료 시간(예: 18:00), 램프 시간(0h ~ 2.5h) 설정
   - **BCD 시간 인코딩** 적용 타이머 패킷(`FEF9`) 및 **일출일몰 모드 활성화 패킷(`FDF1`)** 동시 전송

4. **8-Slot / 12-Slot Ramp-up/down 고급 다중 스케줄 에디터 (Advanced Schedule Mode)**
   - **전 모델 호환 8슬롯 안전 모드 (8-Slot Safe Layout)**: 4CH(최대 8슬롯) 및 5/6CH(최대 12슬롯) 전 모델 MCU 펌웨어와 100% 호환되도록 일출 $\rightarrow$ 정오 피크 $\rightarrow$ 일몰 $\rightarrow$ 심야 달빛을 8개 슬롯 내에 완벽 분배
   - **자정 24:00 (`0x24 0x00`) 무암전(Gapless) 연속 점등**: 23:59의 1분 암전 문제를 공식 규격(`24:00`)으로 해결하여 0.1초의 끊김 없는 자정 바통 터치 지원
   - **🌙 심야 달빛 유지 (Keep Night Moonlight Glow)**: 심야/소등 구간(Slot 8)에 완전 암흑 대신 4%의 은은한 달빛(Blue 4%)을 익일 일출 전까지 밤새 유지하는 원클릭 토글 옵션
   - **스케줄 전송 시 실시간 RTC 시계 자동 동기화 (`0xFF`)**: 스케줄 전송 시 PC 시각을 0번 패킷으로 조명에 무조건 먼저 동기화하여 시계 오차 방지
   - **기기별 White/UV 컬럼 헤더 동적 자동 전환**: 4CH RGB-UV, 4CH RGBW, 5/6CH 멀티 스펙트럼 등 연결된 하드웨어 구성에 맞춰 DataGrid 컬럼 헤더 실시간 전환
   - **500ms Delayed Write Queue**: 다중 패킷 연쇄 송신 시 패킷 유실 방지를 위한 500ms 지연 큐 전송
   - 비활성화 슬롯 0W 초기화 및 **스케줄 모드 활성화 패킷(`FDF2`)** 자동 연동

5. **실시간 스마트 플러그 전력 모니터링**
   - GATT Notify 특성을 통해 전달되는 누적 전력량(kWh) 실시간 스케일링 디코딩 (`rawVal * 4.6566E-8`)

6. **기기별 및 전역 JSON 설정 영구 보관 (Persistence)**
   - 기기 MAC 주소별 색상, 쿨링팬 속도, 간편/상세 일출일몰 시각, 문라이트 옵션, 12개 슬롯 스케줄을 `%AppData%\WeekAquaWPF\device_config.json`에 영구 보관 (앱 재실행 시 완벽 복원)

---

## 📋 BLE 프로토콜 요약 명세

> [!IMPORTANT]
> 조명 내부 MCU는 시간 데이터를 **BCD(Binary-Coded Decimal)** 바이트로 수신합니다. (예: 22시 $\rightarrow$ `0x22`, 18시 $\rightarrow$ `0x18`, 59분 $\rightarrow$ `0x59`)

| 기능 구분 | Command Header | Payload 데이터 매핑 예시 | 프레임 길이 |
| :--- | :--- | :--- | :--- |
| **RTC 시간 동기화** | `0xFF` | `FF` + `BCD(HH)` + `BCD(MM)` + `BCD(SS)` + `55555555` | 8 Bytes |
| **상태 초기화 (Init)** | `0xF0` | `F0` + `55555555555555` | 8 Bytes |
| **라이브 스펙트럼 제어** | `0xFBF9` | `FBF9` + `RR` + `GG` + `BB` + `WW` + `5555` | 8~10 Bytes |
| **쿨링팬 속도 설정** | `0xFC` | `FC` + `SpeedByte` + `555555555555` | 8 Bytes |
| **일출일몰 타이머** | `0xFEF9` | `FEF9` + `BCD(StartH)` + `BCD(StartM)` + `BCD(EndH)` + `BCD(EndM)` + `01` + `RampIdx` | 8 Bytes |
| **Ramp 시간 슬롯 $N$** | `FEF1` ~ `FEFC` | `FEF1` + `BCD(StartH)` + `BCD(StartM)` + `BCD(EndH)` + `BCD(EndM)` + `5555` | 8 Bytes |
| **Ramp 스펙트럼 슬롯 $N$** | `FBF1` ~ `FBFC` | `FBF1` + `RR` + `GG` + `BB` + `WW` + `5555` | 8~10 Bytes |
| **모드 활성화** | `0xFD` | `FDF1` (일출일몰 모드), `FDF2` (다중 스케줄 모드) + `555555555555` | 8 Bytes |

---

## 📁 프로젝트 구조 (Project Structure)

```text
WeekAquaWPF/
├── Protocol/
│   ├── WeekAquaProtocol.cs    # UUID, BCD 시간 변환, 패킷 빌더, 전력 공식
│   └── ProtocolTests.cs       # BCD 인코딩 및 패킷 무결성 단위 검증
├── Services/
│   ├── BleService.cs          # Windows Native BLE 스캔/연결/500ms 딜레이 큐
│   └── SettingsManager.cs     # AppData 로컬 JSON 설정 입출력 관리자
├── Models/
│   ├── RampPointSlot.cs       # 12슬롯 스케줄 데이터 모델 및 유효성 검사기
│   ├── BleDeviceInfo.cs       # BLE 스캔 기기 정보 모델
│   ├── DeviceConfig.cs        # JSON 직렬화용 설정 모델
│   └── LogEntry.cs            # 실시간 TX/RX 터미널 로그 모델
├── ViewModels/
│   ├── MainViewModel.cs       # WPF MVVM 데이터 바인딩 및 커맨드 로직
│   └── RelayCommand.cs        # WPF ICommand 구현체
├── MainWindow.xaml            # 현대적인 다크 테마 Glassmorphism UI
├── MainWindow.xaml.cs        # 윈도우 라이프사이클 이벤트 핸들러
├── App.xaml / App.xaml.cs     # 애플리케이션 진입점 및 프로토콜 검증 테스트
└── WeekAquaWPF.csproj         # .NET 10.0 Windows SDK 프로젝트 파일
```

---

## 🛠️ 빌드 및 실행 방법

### 요구 사항
- **OS**: Windows 10 (버전 2004 / 빌드 19041 이상) 또는 Windows 11
- **SDK**: .NET 10.0 SDK
- **Hardware**: BLE 지원 Bluetooth 4.0+ 어댑터

### 실행 방법

```powershell
# 1. 빌드
dotnet build WeekAquaWPF.csproj

# 2. 실행
dotnet run --project WeekAquaWPF.csproj
```

---

## 📦 단일 실행 파일(`.exe`) 원클릭 배포 (Single-File Publish)

[publish.bat](file:///e:/android/APK/WeekAqua/WeekAquaProject/WeekAquaWPF/publish.bat)을 실행하면 단 한 번의 실행으로 **2가지 버전의 단일 실행 파일**이 자동 생성됩니다:

```cmd
# 탐색기에서 더블클릭하거나 터미널에서 실행
publish.bat
```

### 생성되는 단일 실행 파일:
1. **자체 포함(Self-Contained) 버전** (`publish/SelfContained/WeekAquaWPF.exe`):
   - .NET 런타임이 설치되지 않은 다른 PC에서도 **추가 설치 없이 더블클릭만으로 실행** 가능 (~70MB)
2. **프레임워크 종속(Framework-Dependent) 버전** (`publish/FrameworkDependent/WeekAquaWPF.exe`):
   - .NET 런타임이 설치된 PC에서 사용하는 **초경량 단일 파일** (~25MB)



