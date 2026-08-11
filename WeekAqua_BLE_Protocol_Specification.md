# WeekAqua BLE 통신 프로토콜 명세서

본 문서는 WeekAqua(Week Aqua) 스마트 수초 조명 및 관련 디바이스(스마트 플러그 등)의 안드로이드 앱 디컴파일 소스 코드(`com.weekled.weekaquas`)를 분석하여 역산한 BLE(Bluetooth Low Energy) 통신 프로토콜 명세서입니다.

---

## 1. 블루투스 식별자 (UUID) 및 통신 흐름

### (1) Service 및 Characteristic UUID 명세
WeekAqua 앱은 기기의 BLE 칩셋 및 펌웨어 종류에 따라 크게 **0000FFE0 시리즈**와 **0000FFF0 시리즈** 두 가지 UUID 세트를 자동 탐지하여 연결을 수행합니다.

| 구분 | FFE0 계열 (주력 기기) | FFF0 계열 (호환 기기) | 속성 (Properties) 및 용도 |
| :--- | :--- | :--- | :--- |
| **Service UUID** | `0000ffe0-0000-1000-8000-00805f9b34fb` | `0000fff0-0000-1000-8000-00805f9b34fb` | Primary Service |
| **Write Characteristic** | `0000ffe1-0000-1000-8000-00805f9b34fb` | `0000fff2-0000-1000-8000-00805f9b34fb` | **Write Without Response / Write** (명령 전송) |
| **Notify Characteristic** | `0000ffe3-0000-1000-8000-00805f9b34fb` | `0000fff1-0000-1000-8000-00805f9b34fb` | **Notify / Read** (전력 모니터링 데이터 수신 등) |

### (2) 기기 연결 직후의 통신 흐름 (Handshake / Initial Sync)
`FastBle` 라이브러리(`com.clj.fastble.BleManager`)를 활용한 초기 연결 절차는 다음과 같습니다.

1. **GATT Connect**: BLE 연결 시도 및 성공 확인.
2. **MTU 변경 요청**: 연결 성공 직후 MTU를 `128 Byte`로 확장 요청 (`gatt.requestMtu(128)`).
3. **UUID 수색 및 탐지**: GATT Service/Characteristic 수색 중 `0000ffe1`/`0000ffe3` 또는 `0000fff1`/`0000fff2` UUID 패턴을 탐지하여 전역 매핑 테이블(`MyApp`)에 저장.
4. **RTC 시계 보정 패킷 송신**: 연결 직후 스마트폰의 현재 시/분/초 시간을 `FF` 패킷으로 조명에 전송하여 조명 내부 RTC 시계를 동기화.
5. **모드 초기화 패킷 송신**: `F0` 또는 `F9` 커맨드를 송신하여 조명의 모드 및 출력 상태 동기화.
6. **Notify 구독 (스마트 플러그 기기인 경우)**: `0000FFE3` 또는 `0000FFF1` Characteristic의 Notify를 활성화하여 전력 모니터링 데이터를 수신.

---

## 2. 명령어(Command) 및 패킷(Payload) 프레임 구조

WeekAqua 조명 제어 커맨드는 Hex String을 `byte[]` 배열로 변환하여 GATT Write로 전송하며, **고정 길이 프레임 구조**를 갖습니다.

### (1) 전체 프레임 구조

| Byte 0~1 (Header / Command Code) | Byte 2 ~ N-2 (Payload Data) | Byte N-1 ~ N (Footer / Padding) |
| :--- | :--- | :--- |
| 커맨드 및 대상 슬롯 구분 | 데이터 (시간, 색상 채널값, 쿨링팬 속도 등) | `0x55` 패딩 (`"5555..."`) |

* **시리즈별 기기 구분 코드** (ASCII 문자의 Hex 표현):
  * `5745` ("WE") : HJ 시리즈
  * `5746` ("WF") : WEK 시리즈
  * `5747` ("WG") : Series 4
  * `5748` ("WH") : Series 5
  * `5749` ("WI") : Series 6
  * `5750` ("WP") : Series 7
  * `5751` ("WQ") : Series 8
  * `5752` ("WR") : Series 9

### (2) RGBW/UV 채널 색상 및 밝기 데이터 매핑

앱의 채널별 밝기 백분율($0\% \sim 100\%$)은 선형 변환식을 통해 `1 Byte` Unsigned Integer($0 \sim 235$ 또는 $0 \sim 246$) Hex 값으로 인코딩됩니다.

$$\text{Raw Byte Value} = \text{Math.round}\left( \frac{\text{Percent}}{100.0} \times 235.0 \right)$$

* $0\% \rightarrow \mathtt{0x00}$ (`"00"`)
* $100\% \rightarrow \mathtt{0xEB}$ (`"EB"`, 235)

#### 수동 스펙트럼(Live Spectrum) 설정 패킷 (`FBF9` 커맨드)
* **프레임 예시**: `FBF9` + `[R Byte]` + `[G Byte]` + `[B Byte]` + `[W/UV Byte]` + `5555`
* **인덱스 매핑**:
  * `Byte 0~1`: `FB F9` (수동 스펙트럼 라이브 적용 커맨드)
  * `Byte 2`: Red 채널 ($0 \sim 235$)
  * `Byte 3`: Green 채널 ($0 \sim 235$)
  * `Byte 4`: Blue 채널 ($0 \sim 235$)
  * `Byte 5`: White / UV / K 채널 ($0 \sim 235$)
  * `Byte 6~7`: `55 55` (Footer Padding)

#### 쿨링팬 속도 조절 패킷 (`FC` 커맨드)
* **프레임 예시**: `FC` + `[Fan Byte]` + `555555555555`
* **Fan Byte**: $\text{Math.round}\left( \frac{\text{FanPercent}}{100.0} \times 235.0 \right)$
* 백분율 $0\%$일 경우 `FC00555555555555` 전송.

---

## 3. 데이터 무결성(Checksum) 및 보안/암호화 로직

### (1) Checksum 연산 알고리즘 분석
* **CRC, XOR, 또는 Sum Checksum 연산이 존재하지 않음**: 패킷 무결성 검증은 **고정 프레임 길이**와 **트레일링 패딩 바이트 `0x55` (`01010101` 비트 패턴)** 로 대체됩니다.

### (2) JNI 네이티브 라이브러리 및 난독화 여부
* `.so` 네이티브 라이브러리를 통한 JNI 암호화/복호화 로직이 **사용되지 않음**.
* 모든 커맨드는 문자열(Hex String)을 생성한 뒤 `ByteUtils.hexStringToBytes()`를 거쳐 BLE 바이트 배열로 **평문(Plaintext) 전송**됩니다.

---

## 4. 스케줄링(타이머) 및 시간 동기화 로직

### (1) RTC 시간 동기화 패킷 (`FF` 커맨드)
스마트폰의 현재 시간을 조명에 동기화할 때 사용되는 패킷 구조입니다.

* **Hex 프레임**: `FF` + `HH` + `MM` + `SS` + `55555555` (총 8 Byte)
  * `Byte 0`: `0xFF` (Command Header)
  * `Byte 1`: Hour (`00` ~ `23`)
  * `Byte 2`: Minute (`00` ~ `59`)
  * `Byte 3`: Second (`00` ~ `59`)
  * `Byte 4~7`: `55 55 55 55` (Padding)

### (2) 다중 시간대(Ramp Up/Down) 고급 타이머 설정 패킷 구조
고급(Advanced) 스케줄 모드에서는 하루를 복수의 타임포인트(Point 1 ~ Point 8/12)로 나눕니다. 1개의 타임포인트를 설정할 때 **[시간 범위 패킷]**과 **[해당 시간의 스펙트럼 패킷]** 2개의 패킷을 쌍으로 전송합니다.

#### 1) 시간 범위 패킷 (`FEF1` ~ `FEF8`)
* **형식**: `FEF` + `[Point ID]` + `[Start Hour]` + `[Start Min]` + `[End Hour]` + `[End Min]` + `5555`
* **예시** (Point 1이 08:00 시작 ~ 20:00 종료인 경우):
  * `FEF1` + `08` + `00` + `20` + `00` + `5555`
  * Point ID: 1~9 $\rightarrow$ `FEF1` ~ `FEF9`, 10 $\rightarrow$ `FEFA`, 11 $\rightarrow$ `FEFB`, 12 $\rightarrow$ `FEFC`
  * 슬롯 삭제/초기화 시: `FEF1000000000000` (8 바이트 `0` 처리)

#### 2) 해당 타임포인트의 출력/스펙트럼 패킷 (`FBF1` ~ `FBF8`)
* **형식**: `FBF` + `[Point ID]` + `[R Byte]` + `[G Byte]` + `[B Byte]` + `[W Byte]` + `5555`
* **예시** (Point 1의 RGBW 전력 설정):
  * `FBF1` + `EB` + `C8` + `96` + `00` + `5555`

### (3) BLE 패킷 분할 전송 및 딜레이 스케줄링 (Write Queue)
다수의 타이머/스펙트럼 패킷 전송 시 패킷 손실 방지를 위해 앱 내부에서는 `writePowerDelayed()` 메소드를 통해 **500ms(0.5초) 간격의 큐(Queue) 딜레이 전송**을 수행합니다.

---

## 5. 응답 데이터(RX) 파싱 로직 분석

### (1) 조명 기기 (Light Fixture)
* WeekAqua 조명 기기는 단방향 제어(App $\rightarrow$ Light Write) 위주로 동작합니다. 온/오프 상태 및 온도, 스펙트럼 정보 등은 앱 로컬 데이터베이스(Room DB) 상의 설정값을 기준으로 UI에 유지됩니다.

### (2) 스마트 플러그 / 에너지 모니터링 기기 (`SpileModel`)
스마트 플러그 제품군의 경우 `0000FFE3` 또는 `0000FFF1` Characteristic을 통해 실시간 누적 전력 사용량(kWh)을 Notify 수신합니다.

```java
// SpileModel.java 수신 파싱 로직
@Override
public void onCharacteristicChanged(byte[] data) {
    if (data == null) return;
    try {
        String hex = StringTools.bytesToHex(data);
        double rawVal = Long.parseLong(hex, 16);
        
        // 전력량(kWh) 스케일링 공식 (상수: 4.6566128730773926E-8)
        double kWh = BigDecimal.valueOf(rawVal * 4.6566128730773926E-8d)
                               .setScale(1, RoundingMode.HALF_UP)
                               .doubleValue();
                               
        this.liveTotallKWData.postValue(kWh);
    } catch (Exception e) {
        e.printStackTrace();
    }
}
```

---

## 요약 명세 정리 표

| 기능 구분 | Command Prefix | 데이터 바이트 매핑 예시 | 비고 |
| :--- | :--- | :--- | :--- |
| **RTC 시간 동기화** | `FF` | `FF` + `HH` + `MM` + `SS` + `55555555` | 8 바이트 |
| **라이브 스펙트럼 제어** | `FBF9` | `FBF9` + `RR` + `GG` + `BB` + `WW` + `5555` | $0\% \rightarrow \mathtt{00}, 100\% \rightarrow \mathtt{EB}$ |
| **쿨링팬 속도 설정** | `FC` | `FC` + `Speed` + `555555555555` | $0\% \rightarrow \mathtt{FC005555...}$ |
| **Ramp 시간대 슬롯 $N$** | `FEF1` ~ `FEFC` | `FEF1` + `StartH` + `StartM` + `EndH` + `EndM` + `5555` | 슬롯 초기화 시 `0`으로 채움 |
| **Ramp 스펙트럼 슬롯 $N$** | `FBF1` ~ `FBFC` | `FBF1` + `RR` + `GG` + `BB` + `WW` + `5555` | 각 슬롯별 500ms 딜레이 전송 |
| **기기 모드 전환** | `FDF1` ~ `FDF5` | `FDF1555555555555` | Preset/Custom Mode 전환 |
