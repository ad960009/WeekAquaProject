# ⚡ WeekAqua BLE Controller (C# WPF)

WeekAqua 스마트 수초 조명 및 스마트 플러그 디바이스를 제어하기 위한 C# WPF 기반 Windows 전용 Bluetooth Low Energy (BLE) 애플리케이션입니다.

> [!WARNING]
> **⚠️ 주의사항 (Disclaimer)**  
> 본 프로그램은 시험적(Experimental) 구현체이며 실제 기기에서의 완전한 동작 및 안전성을 보장하지 않습니다. 실물 디바이스 제어 시 발생하는 문제에 대해서는 책임을 지지 않으므로 테스트 및 연구 목적으로만 참고해 주시기 바랍니다.

---

## 🌟 주요 기능 (Features)

1. **Bluetooth Low Energy (BLE) 자동 검색 및 연결**
   - 주변 WeekAqua 기기 자동 탐지 (RSSI signal strength 표출)
   - 주력 기기(`0000FFE0` 시리즈) 및 호환 기기(`0000FFF0` 시리즈) UUID 자동 매핑
   - 연결 직후 스마트폰 RTC 시간 동기화 및 128 Byte MTU 확장 요청

2. **수동 라이브 스펙트럼 (Live Spectrum) 제어**
   - Red, Green, Blue, White/UV 채널별 백분율($0\% \sim 100\%$) 슬라이더 제어
   - 선형 인코딩 알고리즘 적용 ($0\% \rightarrow \mathtt{0x00}$, $100\% \rightarrow \mathtt{0xEB}$)
   - 슬라이더 이동 시 실시간 바이트(`0x00`~`0xEB`) 표출 및 자동 전송 옵션

3. **쿨링팬 (Cooling Fan) 속도 제어**
   - 쿨링팬 속도 백분율 슬라이더 제어 및 명령 전송

4. **RTC 시계 동기화 & 프리셋 모드 선택**
   - 현재 시각(시/분/초) 기반 RTC 시계 보정 패킷(`0xFF`) 원클릭 송신
   - P1 ~ P4 및 커스텀 모드 선택 기능 (`FDF1` ~ `FDF5`)

5. **고급 다중 타임포인트 (Ramp Up/Down) 스케줄 에디터**
   - 1~12번 타임포인트별 시작/종료 시간 및 RGBW 스펙트럼 설정
   - **500ms Delayed Write Queue**: 스케줄 패킷 연쇄 송신 시 패킷 손실 방지를 위한 500ms 간격 큐(Queue) 자동 전송

6. **스마트 플러그 실시간 전력 모니터링**
   - Notify 특성을 통해 전달되는 누적 전력 사용량(kWh) 파싱 및 라이브 표출

7. **Raw BLE 통신 터미널 로그**
- 🌅 **일출 & 일몰 모드 제어 (Sunrise & Sunset Timer Mode)**: 시작/종료 시각 및 0h, 0.5h, 1h, 1.5h, 2h, 2.5h Ramp 시간 선택 후 `FEF9` 패킷 송신
- 💾 **기기별 JSON 설정 자동 저장/로드**: 기기 MAC 주소별 설정값(RGBW, 팬 속도, 일출일몰, 스케줄)을 `%AppData%\WeekAquaWPF\device_config.json`에 자동 보관
- 📅 **12-Point Ramp-up/down Scheduler**: 하루 12개 시간 슬롯별 RGBW 스펙트럼 시분할 자동 제어 (`FEF1`~`FEFC`, `FBF1`~`FBFC`)
- 🕒 **스마트폰/PC RTC 시계 동기화**: 현지 시각(`0xFF`) 자동 동기화 기능
- 🎨 **고대비 다크 테마 UI**: DataGrid 헤더 및 ComboBox 가독성을 대폭 향상시킨 커스텀 스타일링 적용

---

## 📋 BLE 프로토콜 요약 명세

| 기능 | Command Header | Payload 데이터 매핑 예시 | 프레임 길이 |
| :--- | :--- | :--- | :--- |
| **RTC 시간 동기화** | `0xFF` | `FF` + `HH` + `MM` + `SS` + `55555555` | 8 Bytes |
| **라이브 스펙트럼 제어** | `0xFBF9` | `FBF9` + `RR` + `GG` + `BB` + `WW` + `5555` | 8 Bytes |
| **쿨링팬 속도 설정** | `0xFC` | `FC` + `SpeedByte` + `555555555555` | 8 Bytes |
| **Ramp 시간대 슬롯 $N$** | `FEF1` ~ `FEFC` | `FEF1` + `StartH` + `StartM` + `EndH` + `EndM` + `5555` | 8 Bytes |
| **Ramp 스펙트럼 슬롯 $N$** | `FBF1` ~ `FBFC` | `FBF1` + `RR` + `GG` + `BB` + `WW` + `5555` | 8 Bytes |
| **모드 전환** | `FDF1` ~ `FDF5` | `FDF1555555555555` | 8 Bytes |

---

## 📁 프로젝트 구조 (Project Structure)

```text
WeekAquaWPF/
├── Protocol/
│   ├── WeekAquaProtocol.cs    # UUID, 패킷 인코딩/디코딩, 파서 logic
│   └── ProtocolTests.cs       # 프로토콜 검증 단위 테스트
├── Services/
│   └── BleService.cs          # Windows Native BLE 스캔, 연결 및 500ms 전송 큐
├── Models/
│   ├── RampPointSlot.cs       # 스케줄 슬롯 데이터 모델
│   ├── BleDeviceInfo.cs       # BLE 스캔 기기 정보 모델
│   └── LogEntry.cs            # BLE 통신 로그 모델
├── ViewModels/
│   ├── MainViewModel.cs       # WPF UI 바인딩 및 커맨드 처리
│   └── RelayCommand.cs        # WPF ICommand 구현체
├── MainWindow.xaml            # 현대적인 다크 테마 사용자 인터페이스
├── MainWindow.xaml.cs        # 윈도우 이벤트 처리
├── App.xaml / App.xaml.cs     # 앱 시작점 및 검증 실행
└── WeekAquaWPF.csproj         # Windows SDK 10.0.19041+ 프로젝트 파일
```

---

## 🛠️ 요구 사항 및 실행 방법 (Requirements & Running)

### 요구 사항
- **OS**: Windows 10 (Build 19041 이상) 또는 Windows 11
- **Hardware**: Bluetooth 4.0 이상 지원 블루투스 어댑터/동글
- **SDK**: .NET 8.0 / .NET 9.0 / .NET 10.0 SDK

### 실행 방법

1. **프로젝트 빌드**
   ```powershell
   dotnet build WeekAquaWPF.csproj
   ```

2. **애플리케이션 실행**
   ```powershell
   dotnet run --project WeekAquaWPF.csproj
   ```
