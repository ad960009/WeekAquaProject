# ⚡ WeekAqua BLE Controller (C# WPF)

WeekAqua 스마트 수초 조명 및 스마트 플러그 디바이스를 Windows 환경에서 제어하기 위한 C# .NET 10.0 WPF 기반 Bluetooth Low Energy (BLE) 애플리케이션입니다.

> [!WARNING]
> **⚠️ 주의사항 (Disclaimer)**  
> 본 프로그램은 안드로이드 공식 앱 디컴파일 분석을 바탕으로 제작된 시험용(Experimental) 소프트웨어입니다. 실물 디바이스 제어 시 발생하는 문제에 대해서는 책임을 지지 않으므로 테스트 및 연구 목적으로 활용해 주시기 바랍니다.

---

## 🔌 호환 및 테스트된 기기 (Tested Devices)

본 프로젝트는 다음과 같은 하드웨어 펌웨어 환경에서 직접 테스트 및 검증되었습니다.
- **B3.0-M800Pro-18** (4-Channel RGB/UV, Legacy 5745 Protocol Mode)

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

4. **5-Slot / 8-Slot / 12-Slot Ramp-up/down 고급 다중 스케줄 에디터 (Advanced Schedule Mode)**
   - **기기 모델별 하드웨어 슬롯 한도 자동 감지 (5/8/12 Slots Intelligent Dispatch)**: 연결된 기기 펌웨어 스펙(`5745` 5슬롯, `5746`/`5749`/`5751` 8슬롯, `5747`/`5748`/`5752` 12슬롯)에 맞춰 정확한 슬롯 범위(`FEF1~FEF5`, `FEF1~FEF8`, `FEF1~FEFC`)만 분기 전송하여 헤더 오동작 방지
   - **자연스러운 대칭형 종형 일주기 곡선 (Natural Bell Curves)**:
     - **5-Slot 곡선 (낮 4 + 밤 1)**: `[25% -> 70% -> 100% -> 35% -> 0%]` (3번 단일 피크)
     - **8-Slot 곡선 (낮 7 + 밤 1)**: `[20% -> 50% -> 75% -> 100% -> 75% -> 50% -> 20% -> 0%]` (4번 단일 피크 & 중복 없는 7단계 자연 그라데이션)
     - **12-Slot 곡선 (낮 11 + 밤 1)**: `[15% -> 30% -> 50% -> 70% -> 85% -> 100% -> 85% -> 70% -> 50% -> 30% -> 15% -> 0%]` (6번 단일 피크 & 11단계 미세 그라데이션)
   - **자정 24:00 (`0x24 0x00`) 무암전(Gapless) 연속 점등 및 자정 넘김 자동 분할**:
     - 18:00 ~ 02:00과 같이 점등 시간이 자정을 넘길 때 자정 이전과 이후를 비례 분할하여 자정 직전 슬롯을 `24:00`으로 종료하고 다음 슬롯을 `00:00`으로 시작
     - 23:59의 1분 암전 문제를 공식 규격(`24:00`)으로 해결하여 0.1초의 끊김 없는 자정 바통 터치 지원
   - **🌙 심야 달빛 유지 (Keep Night Moonlight Glow)**: 심야/소등 구간에 완전 암흑 대신 4%의 은은한 달빛(Blue 4%)을 익일 일출 전까지 밤새 유지하는 원클릭 토글 옵션
   - **스케줄 전송 시 실시간 RTC 시계 자동 동기화 (`0xFF`)**: 스케줄 전송 시 PC 시각을 0번 패킷으로 조명에 무조건 먼저 동기화하여 시계 오차 방지
   - **기기별 White/UV 컬럼 헤더 동적 자동 전환**: 4CH RGB-UV, 4CH RGBW, 5/6CH 멀티 스펙트럼 등 연결된 하드웨어 구성에 맞춰 DataGrid 컬럼 헤더 실시간 전환
   - **500ms Delayed Write Queue**: 다중 패킷 연쇄 송신 시 패킷 유실 방지를 위한 500ms 지연 큐 전송
   - 비활성화 슬롯 0W 초기화 및 **스케줄 모드 활성화 패킷(`FDF2`)** 자동 연동

5. **실시간 스마트 플러그 전력 모니터링**
   - GATT Notify 특성을 통해 전달되는 누적 전력량(kWh) 실시간 스케일링 디코딩 (`rawVal * 4.6566E-8`)

6. **기기별 및 전역 JSON 설정 영구 보관 (Persistence)**
   - 기기 MAC 주소별 색상, 쿨링팬 속도, 간편/상세 일출일몰 시각, 문라이트 옵션, 12개 슬롯 스케줄을 `%AppData%\WeekAquaWPF\device_config.json`에 영구 보관 (앱 재실행 시 완벽 복원)

---

## 📈 5 / 8 / 12 슬롯 스케줄 곡선 사양

| 슬롯 구성 | 대응 기기 및 모델 코드 | 슬롯 분할 | 일주기 강도 비율 곡선 (Daily Intensity Curve) | 피크 슬롯 |
| :--- | :--- | :--- | :--- | :--- |
| **5-Slot** | `5745` (Mode 1/2, Classic 4-CH) | 주간 4 + 야간 1 | `[0.25, 0.70, 1.00, 0.35, 0.00]` | **Slot 3** (100%) |
| **8-Slot** | `5746`, `5749`, `5751` (M800 Pro, M-Series 등) | 주간 7 + 야간 1 | `[0.20, 0.50, 0.75, 1.00, 0.75, 0.50, 0.20, 0.00]` | **Slot 4** (100%) |
| **12-Slot** | `5747`, `5748`, `5752` (Multi-CH, Marine, Coral, A430) | 주간 11 + 야간 1 | `[0.15, 0.30, 0.50, 0.70, 0.85, 1.00, 0.85, 0.70, 0.50, 0.30, 0.15, 0.00]` | **Slot 6** (100%) |

### 💡 8슬롯 자정 넘김 스케줄 계산 예시 (18:00 ~ 02:00 점등)

```text
[Slot 1] 18:00 ~ 19:12 (20%  - 🌅 일출 점등 시작)
[Slot 2] 19:12 ~ 20:24 (50%  - 🌄 오전 램프업 1)
[Slot 3] 20:24 ~ 21:36 (75%  - ☀️ 오전 램프업 2)
[Slot 4] 21:36 ~ 22:48 (100% - ☀️ 정오 최대 피크 단일 정점)
[Slot 5] 22:48 ~ 24:00 (75%  - 🌤️ 자정 24:00 도달, MCU 패킷: 0x24 0x00)
[Slot 6] 00:00 ~ 01:00 (50%  - 🌇 00:00 시작 ➡️ 일몰 램프다운, MCU 패킷: 0x00 0x00)
[Slot 7] 01:00 ~ 02:00 (20%  - 🌙 황혼 소등 마무리)
[Slot 8] 02:00 ~ 18:00 (0%   - 🌑 야간 휴식 / Moonlight 4%)
```

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
├── CLI/
│   └── CliRunner.cs           # 명령줄 인수 파싱, Win32 콘솔 부착, 자동화 CLI 실행 엔진
├── Models/
│   ├── BleDeviceInfo.cs       # BLE 스캔 기기 정보 모델 (MAC, RSSI, 채널 사양 등)
│   ├── LogEntry.cs            # 실시간 TX/RX 터미널 로그 모델
│   └── RampPointSlot.cs       # 12슬롯 스케줄 데이터 모델 및 유효성 검사기
├── Protocol/
│   ├── WeekAquaProtocol.cs    # UUID, BCD 시간 변환, 패킷 빌더, 전력 공식, 체크섬
│   └── ProtocolTests.cs       # BCD 인코딩 및 패킷 무결성 단위 검증
├── Services/
│   ├── BleService.cs          # Windows Native BLE 스캔/연결/GATT 통신/500ms 딜레이 큐
│   └── SettingsManager.cs     # AppData 로컬 JSON 설정 입출력 및 DeviceConfig 관리자
├── ViewModels/
│   ├── MainViewModel.cs       # WPF MVVM 데이터 바인딩 및 커맨드/스펙트럼 로직
│   └── RelayCommand.cs        # WPF ICommand 구현체
├── MainWindow.xaml            # 현대적인 다크 테마 Glassmorphism UI
├── MainWindow.xaml.cs         # 윈도우 라이프사이클 이벤트 핸들러
├── App.xaml / App.xaml.cs     # 애플리케이션 진입점 (인수 유무에 따른 GUI / CLI 모드 자동 분기)
├── AssemblyInfo.cs            # 어셈블리 메타데이터
├── WeekAquaWPF.csproj         # .NET 10.0 Windows SDK 프로젝트 파일
└── publish.bat                # 자체 포함 / 프레임워크 종속 단일 실행 파일(.exe) 배포 스크립트
```

---

## 🛠️ 빌드 및 실행 방법

### 요구 사항
- **OS**: Windows 10 (버전 2004 / 빌드 19041 이상) 또는 Windows 11
- **SDK**: .NET 10.0 SDK
- **Hardware**: BLE 지원 Bluetooth 4.0+ 어댑터

### 실행 모드 안내
- **🖥️ GUI 모드 (기본값)**: **파라미터(인수) 없이 실행하거나 파일 탐색기에서 더블클릭**하면 자동으로 다크 테마의 **WPF GUI 창**이 실행됩니다.
- **⚡ CLI 모드**: 터미널 또는 스크립트에서 **명령줄 파라미터를 전달하여 실행**하면 콘솔 창이 자동으로 부착(Attach)되어 헤드리스 **CLI 자동화 모드**로 동작합니다.

### 실행 방법

```powershell
# 1. 빌드
dotnet build WeekAquaWPF.csproj

# 2. GUI 모드 실행 (파라미터 없이 실행)
dotnet run --project WeekAquaWPF.csproj

# 또는 빌드된 exe 직접 실행
WeekAquaWPF.exe
```

---

## 💻 명령줄 인터페이스 (CLI Automation)

`WeekAquaWPF.exe`에 파라미터(명령어)를 지정하여 실행하면 GUI 창을 띄우지 않고 콘솔을 통해 스크립트, 배치 파일, Windows 작업 스케줄러에서 직접 조명을 제어할 수 있는 **CLI 모드**로 동작합니다. (파라미터 없이 실행 시 GUI 모드로 실행)

```powershell
WeekAquaWPF.exe <command> [options]
```

### 📋 CLI 지원 명령어 목록

| 명령어 | 옵션 | 설명 |
| :--- | :--- | :--- |
| `scan` | `[-t <초>]` | 주변 WeekAqua BLE 기기를 검색하여 MAC, RSSI, 채널 타입을 표로 출력 |
| `sync-rtc` | `-m <MAC>` | 지정 기기의 RTC 시계를 현재 PC 시각(BCD 포맷)으로 동기화 및 상태 초기화 |
| `set-spectrum` | `-m <MAC> -r <R> -g <G> -b <B> -w <W> [-u <UV>] [-v <V>]` | 지정 기기의 밝기/스펙트럼을 즉시 전송 (전력 상한 자동 검증) |
| `set-timer` | `-m <MAC> -d <분> [-r <R> -g <G> -b <B> -w <W> \| -p <프리셋>]` | **현재 시각 + N분 동안 점등 후 자동으로 완전 소등**되는 타이머 스케줄 전송 |
| `set-preset` | `-m <MAC> -p <프리셋이름>` | Green, RedPlant, Mixed, CoralAB, Moonlight 등 프리셋 즉시 적용 |
| `set-fan` | `-m <MAC> -s <속도%>` | 쿨링팬 속도(0~100%) 설정 |
| `--help` | | 전체 CLI 명령어 및 상세 도움말 출력 |

### 💡 CLI 실전 사용 예제 (Examples)

```powershell
# 1. 주변 기기 5초간 스캔
WeekAquaWPF.exe scan --timeout 5

# 2. 지정 조명 RTC 시계 동기화
WeekAquaWPF.exe sync-rtc -m DC:12:34:56:78:9A

# 3. 실시간 RGBW 밝기 설정 (Red 80%, Green 60%, Blue 50%, White 30%)
WeekAquaWPF.exe set-spectrum -m DC:12:34:56:78:9A -r 80 -g 60 -b 50 -w 30

# 4. 30분간 조명을 켠 후 자동으로 완전히 끄기 (타이머 스케줄)
WeekAquaWPF.exe set-timer -m DC:12:34:56:78:9A -d 30 -r 80 -g 60 -b 50 -w 30

# 5. 60분간 'Green' 수초 프리셋으로 켠 후 자동 소등
WeekAquaWPF.exe set-timer -m DC:12:34:56:78:9A -d 60 -p Green

# 6. 프리셋 즉시 적용
WeekAquaWPF.exe set-preset -m DC:12:34:56:78:9A -p RedPlant

# 7. 쿨링팬 속도 50% 설정
WeekAquaWPF.exe set-fan -m DC:12:34:56:78:9A -s 50
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
   - .NET 런타임이 설치되지 않은 다른 PC에서도 **추가 설치 없이 더블클릭 및 CLI 실행 가능** (~70MB)
2. **프레임워크 종속(Framework-Dependent) 버전** (`publish/FrameworkDependent/WeekAquaWPF.exe`):
   - .NET 런타임이 설치된 PC에서 사용하는 **초경량 단일 파일** (~25MB)



