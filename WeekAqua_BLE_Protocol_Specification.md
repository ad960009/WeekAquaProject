# WeekAqua BLE 통신 프로토콜 명세서 (통합 개정판)

본 문서는 WeekAqua(Week Aqua) 스마트 수초/해수 조명 및 관련 디바이스(스마트 플러그, 수류 모터 등)의 공식 안드로이드 앱(`com.weekled.weekaquas` v3.0.25) 디컴파일 소스 코드를 역분석하여 작성된 통합 BLE(Bluetooth Low Energy) 통신 프로토콜 명세서입니다.

---

## 1. 블루투스 식별자 (UUID) 및 통신 인프라

### (1) Service 및 Characteristic UUID 명세
WeekAqua 기기는 장착된 BLE 칩셋 및 펌웨어에 따라 **0000FFE0 시리즈**와 **0000FFF0 시리즈** 두 가지 UUID 세트를 자동 감지하여 통신합니다.

| 구분 | FFE0 계열 (주력 조명 기기) | FFF0 계열 (호환 기기 / 스마트 플러그) | 속성 (Properties) 및 용도 |
| :--- | :--- | :--- | :--- |
| **Service UUID** | `0000ffe0-0000-1000-8000-00805f9b34fb` | `0000fff0-0000-1000-8000-00805f9b34fb` | Primary Service |
| **Write Characteristic** | `0000ffe1-0000-1000-8000-00805f9b34fb` | `0000fff2-0000-1000-8000-00805f9b34fb` | **Write Without Response / Write** (명령 송신) |
| **Notify Characteristic** | `0000ffe3-0000-1000-8000-00805f9b34fb` | `0000fff1-0000-1000-8000-00805f9b34fb` | **Notify / Read** (전력 모니터링 수신 등) |

### (2) 기기 연결 핸드셰이크 (Handshake / Initial Sync)
1. **GATT Connect**: BLE 기기 연결 성공 확인.
2. **MTU 협상 (128 Byte)**: 연결 성공 즉시 `gatt.requestMtu(128)`를 호출하여 패킷 MTU를 128 Byte로 확장.
3. **UUID 수색 및 매핑**: GATT Service 수색 후 `0000ffe1`/`0000ffe3` 또는 `0000fff1`/`0000fff2` UUID를 찾아 전역 테이블에 매핑.
4. **RTC 시계 보정 (`FF` 패킷)**: 연결 직후 호스트 기기의 현재 시/분/초 시간을 전송하여 조명 RTC 동기화.
5. **Notify 활성화**: 스마트 플러그 기기인 경우 Notify Characteristic을 구독하여 실시간 전력량 수신 대기.

### (3) BLE ScanRecord 기반 기기 모델 자동 식별
앱은 BLE Advertising 패킷(`ScanRecord`)의 Hex 바이트열에서 **18~22번째 인덱스(`substring(18, 22)`)**를 추출하여 하드웨어 모델을 판별합니다.

| Model Code (Hex) | ASCII 문자 | 대응 시리즈 / 모드 | 지원 채널 및 특징 |
| :--- | :--- | :--- | :--- |
| `5745` | `"WE"` | **HJ 시리즈 (Legacy)** | 기본 4CH RGBW / RGB-UV (`StringTools`) |
| `5746` | `"WF"` | **Mode 1 (WEK 시리즈)** | 담수 수초 4CH RGB-UV (24V / 36V) (`StringOneTools`) |
| `5747` | `"WG"` | **Mode 2 (Series 4)** | 해수/산호 5CH (UV, Violet, DeepBlue, LightBlue, White) (`StringTwoTools`) |
| `5748` | `"WH"` | **Mode 3 / 5 (Series 5)** | 7CH WRGB+WW+W+CW / 10CH 풀스펙트럼 (`StringThreeTools`, `StringFiveTools`) |
| `5749` | `"WI"` | **Mode 6 (Series 6)** | 2CH 색온도 조명 (Warm White, Cool White) (`StringSixTools`) |
| `5750` | `"WP"` | **Series 7** | 차세대 확장 모델 |
| `5751` | `"WQ"` | **Model 8 (Series 8)** | 스마트 수류 모터 / 웨이브메이커 (`StringEightTools`) |
| `5752` | `"WR"` | **Model 9 (Series 9)** | 5CH RGB-UV-W 조명 (`StringNineTools`) |
| `5755` / etc. | - | **스마트 플러그 (Spile)** | 6구 독립 타이머 소켓 & 전력량계 (`SpileModel`) |

---

## 2. 공통 프레임 구조 및 데이터 인코딩 규칙

### (1) 전체 프레임 구조
* 모든 커맨드는 Hex String으로 생성된 후 `byte[]` 배열로 변환되어 GATT Write로 전송됩니다.
* 체크섬(CRC/XOR)은 사용하지 않으며, **고정 프레임 길이**와 **트레일링 `0x55` 패딩 바이트**로 프레임 무결성을 검증합니다.

```
+-------------------+-----------------------------------+--------------------+
| Byte 0~1 (Header) | Byte 2 ~ N-1 (Payload Data)       | Byte N (Padding)   |
| 커맨드 코드       | 색상값, 시간, 동작 파라미터 등    | 0x55 ("5555...")   |
+-------------------+-----------------------------------+--------------------+
```

### (2) 시간 데이터의 BCD(Binary-Coded Decimal) 인코딩 규칙
> [!IMPORTANT]
> 안드로이드 앱은 시간을 2자리 10진수 문자열(`"08"`, `"18"`, `"22"`, `"59"`)로 만든 뒤 `hexStringToBytes()`로 변환하여 BLE로 전송합니다.
> MCU는 시간 바이트를 **BCD(Binary-Coded Decimal)**로 해석하므로, 십진수 18시는 `0x18`(십진수 24)로 전송해야 합니다.

### (3) 디바이스 이름 변경 커맨드 (`ADV_NAME`)
* **형식**: `!%!%:ADV_NAME:[NewDeviceName]` 문자열을 UTF-8 바이트 배열로 변환하여 Write Characteristic으로 송신.
* **예시**: `WeekAqua L800`으로 변경 시 `!%!%:ADV_NAME:WeekAqua L800` 전송.

---

## 3. 모델별 프로토콜 세부 명세

### (1) Legacy / Standard (HJ 시리즈 / 5745) - `StringTools.java`
* **채널 (4CH)**: Red, Green, Blue, UV (또는 White) + Cooling Fan
* **밝기 변환식**: $\text{Byte} = \text{Math.round}\left( \frac{\text{Percent}}{100.0} \times 235.0 \right)$ ($0\% \rightarrow \mathtt{0x00}, 100\% \rightarrow \mathtt{0xEB}$)
* **라이브 스펙트럼 패킷 (`FBF9`)**:
  * `FBF9` + `[R(1B)]` + `[G(1B)]` + `[B(1B)]` + `[UV/W(1B)]` + `5555` (총 8 Byte)
* **쿨링팬 속도 패킷 (`FC`)**:
  * `FC` + `[Fan(1B: 0~235)]` + `555555555555` (총 8 Byte)
* **슬롯별 스펙트럼 (`FBF1` ~ `FBF8`)**:
  * `FBF[1~8]` + `[R]` + `[G]` + `[B]` + `[UV/W]` + `5555`
* **슬롯별 시간 설정 (`FEF1` ~ `FEF8`)**:
  * `FEF[1~8]` + `BCD[StartHH]` + `BCD[Startmm]` + `BCD[EndHH]` + `BCD[Endmm]` + `5555`
* **간편 일출/일몰 모드 시간 (`FEF9`)**:
  * `FEF9` + `BCD[StartHH]` + `BCD[Startmm]` + `BCD[EndHH]` + `BCD[Endmm]` + `[Type: 00/01]` + `[Ramp: 00~05]`
* **RTC 시간 동기화 (`FF`)**:
  * `FF` + `BCD[HH]` + `BCD[mm]` + `BCD[SS]` + `55555555` (총 8 Byte)

### (2) Mode 1 (담수 수초 / 5746) - `StringOneTools.java`
* **채널 (4CH)**: Red, Green, Blue, UV + Cooling Fan
* **라이브 스펙트럼 패킷 (`FBEF`)**:
  * `FBEF` + `[R]` + `[G]` + `[B]` + `[UV]` + `5555`
* **쿨링팬 속도 패킷 (`F9`)**:
  * `F9` + `[Fan(1B: 0~235)]` + `555555555555`
* **하드웨어 전원/타이머 ON/OFF 스위치 패킷 (`F6`)**:
  * 전원 ON / 점등 활성화: `F6F1555555555555` (`StringOneTools.java: getModeSettingSwitchTime(1)`)
  * 전원 OFF / 강제 소등: `F6F2555555555555` (`StringOneTools.java: getModeSettingSwitchTime(0)`)
* **모드 설정 시간 패킷 (`FEEF`)**:
  * `FEEF` + `BCD[StartHH]` + `BCD[Startmm]` + `BCD[EndHH]` + `BCD[Endmm]` + `5555`

### (3) Mode 2 (해수 산호 Reef/Marine / 5747) - `StringTwoTools.java`
* **채널 (5CH)**: UV (Ultraviolet), V (Violet), DB (Deep Blue), LB (Light Blue), W (White) + Cooling Fan
* **라이브 스펙트럼 패킷 (`FAEF`)**:
  * `FAEF` + `[UV]` + `[V]` + `[DB]` + `[LB]` + `[W]` + `5555` (총 8 Byte)
* **슬롯별 프리셋 패킷 (`FAF1` ~ `FAF8`)**:
  * `FAF[1~8]` + `[UV]` + `[V]` + `[DB]` + `[LB]` + `[W]` + `5555`
* **슬롯별 시간 설정 (`FCF1` ~ `FCF8`)**:
  * `FCF[1~8]` + `BCD[StartHH]` + `BCD[Startmm]` + `BCD[EndHH]` + `BCD[Endmm]` + `5555`
* **쿨링팬 속도 패킷 (`F9`)**:
  * `F9` + `[Fan(1B: 0~235)]` + `555555555555`

### (4) Mode 3 (7CH 풀스펙트럼 / 5748) - `StringThreeTools.java`
* **채널 (7CH)**: R, G, B, UV, WW (Warm White), W (White), CW (Cool White) + Cooling Fan
* **라이브 스펙트럼 패킷 (`FBFD`)**:
  * `FBFD` + `[R]` + `[G]` + `[B]` + `[UV]` + `[WW]` + `[W]` + `[CW]` + `5555`
* **슬롯별 프리셋 (12개 슬롯 지원)**:
  * 슬롯 1~9: `FBF1` ~ `FBF9` + `[7CH Data]` + `5555`
  * 슬롯 10~12: `FBFA`, `FBFB`, `FBFC` + `[7CH Data]` + `5555`
* **슬롯별 시간 설정 (12개 슬롯 지원)**:
  * 슬롯 1~9: `FEF1` ~ `FEF9` + `BCD[StartHH][Startmm][EndHH][Endmm]` + `555555`
  * 슬롯 10~12: `FEFA`, `FEFB`, `FEFC` + `BCD[StartHH][Startmm][EndHH][Endmm]` + `555555`
* **램프 타임 모드 시간 패킷 (`FEFD`)**:
  * `FEFD` + `BCD[StartHH][Startmm][EndHH][Endmm]` + `[Type:00/01]` + `[Ramp:0X]` + `55`

### (5) Mode 5 (10CH 대형 수초/산호 조명 / 5748) - `StringFiveTools.java`
* **채널 (10CH)**: 10개 파장 독립 채널 + Cooling Fan
* **라이브 스펙트럼 패킷 (`FBFD`)**:
  * `FBFD` + `[10CH Data (10 Byte)]` + `5555`
* **슬롯별 프리셋 (`FBF1` ~ `FBFC`)**:
  * `FBF[1~C]` + `[10CH Data]` + `5555`
* **슬롯별 시간 설정 (`FEF1` ~ `FEFC`)**:
  * `FEF[1~C]` + `BCD[StartHH][Startmm][EndHH][Endmm]` + `555555555555`
* **쿨링팬 속도 패킷 (`FC`)**:
  * `FC` + `[Fan]` + `55555555555555555555` (24 Byte 고정 길이)

### (6) Mode 6 (2CH 색온도 CCT 조명 / 5749) - `StringSixTools.java`
* **채널 (2CH)**: WW (Warm White), CW (Cool White) + Cooling Fan
* **라이브 스펙트럼 패킷 (`FBF95555`)**:
  * `FBF95555` + `[WW(1B)]` + `[CW(1B)]` + `5555`
* **슬롯별 프리셋 (`FBF15555` ~ `FBF85555`)**:
  * `FBF[1~8]5555` + `[WW]` + `[CW]` + `5555`

### (7) Model 8 (스마트 수류 모터 / 웨이브메이커 / 5751) - `StringEightTools.java`
* **동작 모드 코드**: 정류(0), 조석(1), 주기(2), 파도(3), 클래식(4), 피딩(5)
* **단계별 출력 바이트 매핑**:
  * 10%: `0x43`, 20%: `0x56`, 30%: `0x6A`, 40%: `0x7D`, 50%: `0x90`
  * 60%: `0xA3`, 70%: `0xB6`, 80%: `0xCA`, 90%: `0xDD`, 100%: `0xFD` (또는 `0xF0`)
* **제어 커맨드 패킷 (`FEF` / `FBF`)**:
  * `FEF[Pos]` + `[ModelCode]` + `[PowerHex]` + `55555555`

### (8) Model 9 (5CH RGB-UV-W 조명 / 5752) - `StringNineTools.java`
* **채널 (5CH)**: R, G, B, UV, W + Cooling Fan
* **라이브 스펙트럼 패킷 (`FBFD`)**:
  * `FBFD` + `[R]` + `[G]` + `[B]` + `[UV]` + `[W]` + `5555`
* **슬롯별 프리셋 / 시간 설정**:
  * 프리셋: `FBF[1~C]` + `[5CH Data]` + `5555`
  * 시간: `FEF[1~C]` + `BCD[StartHH][Startmm][EndHH][Endmm]` + `555555`

---

## 4. 스마트 플러그 (Spile) 통신 프로토콜

스마트 플러그(멀티탭)는 6구 독립 소켓별로 각각 6개의 스케줄 태스크를 관리하며, 누적 전력량(kWh)을 실시간 Notify 수신합니다.

### (1) 스케줄 태스크 송신 패킷 구조
* **프레임 형식**:
  $$\mathtt{[SocketCode(1B)]} + \mathtt{[SlotCode(1B)]} + \mathtt{[WeekBitmask(1B)]} + \mathtt{[StartHH(1B)]} + \mathtt{[Startmm(1B)]} + \mathtt{[EndHH(1B)]} + \mathtt{[Endmm(1B)]} + \mathtt{[IsOpen(1B)]}$$

| 필드 | 값 / 인코딩 | 설명 |
| :--- | :--- | :--- |
| **소켓 번호 (SocketCode)** | `FE`(1구), `FD`(2구), `FC`(3구), `FB`(4구), `FA`(5구), `F9`(6구) | 대상 플러그 소켓 |
| **태스크 슬롯 (SlotCode)** | `F1`(1번), `F2`(2번), `F3`(3번), `F4`(4번), `F5`(5번), `F6`(6번) | 소켓 내 스케줄 번호 |
| **요일 비트마스크 (WeekHex)** | 7비트 이진수를 Hex로 표현 (2자리) | $(\text{Sun}\ll 6) \mid (\text{Sat}\ll 5) \mid \dots \mid (\text{Mon}\ll 0)$ |
| **시작 시/분** | `BCD(HH)`, `BCD(mm)` | 켜짐 시작 시각 (예: `08:00` $\rightarrow$ `08 00`) |
| **종료 시/분** | `BCD(HH)`, `BCD(mm)` | 꺼짐 종료 시각 (예: `18:00` $\rightarrow$ `18 00`) |
| **스케줄 활성화 (IsOpen)** | `01` (ON/활성화), `00` (OFF/비활성화) | 태스크 활성화 여부 |

* **송신 예시**: 1번 소켓의 1번 태스크, 월~일 매일(`0x7F`), 08:30 시작 ~ 17:45 종료, 활성화
  $$\rightarrow \mathtt{FE\ F1\ 7F\ 08\ 30\ 17\ 45\ 01}$$

### (2) 실시간 누적 전력량(kWh) 수신 및 파싱
* Notify Characteristic(`0000FFE3` 또는 `0000FFF1`) 수신:
```csharp
// C# / .NET 변환 구현
public static double ParseEnergyKwh(byte[] data)
{
    if (data == null || data.Length == 0) return 0.0;
    string hex = Convert.ToHexString(data);
    long rawVal = Convert.ToInt64(hex, 16);
    
    // APK 원본 스케일링 상수: 4.6566128730773926E-8 (100.0 / 2^31)
    double kwh = Math.Round(rawVal * 4.6566128730773926E-8, 2);
    return kwh;
}
```

---

## 5. 채널별 전력 가중치 및 상한선 (Max Power Limit) 공식

WeekAqua 앱은 SMPS 전원 공급 장치와 LED 기판의 과부하 및 발열을 방지하기 위해 채널별 가중 합산 전력이 $100.0\%$를 넘지 않도록 제한합니다.

### (1) 표준 4채널 RGBW (235 스케일)
$$\text{Total Power (\%)} = (R\% \times 0.39) + (G\% \times 0.41) + (B\% \times 0.53) + (W\% \times 0.11)$$

### (2) Mode 1 전압별 담수 모드 (246 역스케일)
* **24V 전압 모드**:
  $$\text{Total Power (\%)} = (R\% \times 0.32) + (G\% \times 0.34) + (B\% \times 0.51) + (UV\% \times 0.08)$$
* **36V 전압 모드**:
  $$\text{Total Power (\%)} = (R\% \times 0.43) + (G\% \times 0.44) + (B\% \times 0.58) + (UV\% \times 0.11)$$

### (3) 6채널 멀티 스펙트럼 (해수/산호)
$$\text{Total Power (\%)} = (CH_1\% \times 0.41) + (CH_2\% \times 0.42) + (CH_3\% \times 0.49) + (CH_4\% \times 0.08) + (CH_5\% \times 0.08) + (CH_6\% \times 0.08)$$

---

## 6. 스케줄 슬롯 개수 제한 및 자정(24:00) 무암전 규격

### (1) 기기 시리즈별 MCU 최대 슬롯 한도
* **4채널 일반/구형 모델 (Mode 1, 2, 6, 8)**:
  * **최대 8개 슬롯 (`FEF1` ~ `FEF8`, `FBF1` ~ `FBF8`)** 지원
  * 9번 이상의 헤더(`FEF9`)는 일출일몰 간편 타이머로 예약되어 있어 슬롯으로 인식되지 않음. 따라서 4CH 기기에서는 8개 슬롯 내에 24시간 사이클을 모두 구성해야 함.
* **최신 5채널/6채널 모델 (Mode 3, 5, 9 / T90 Pro, M-Series Pro 등)**:
  * **최대 12개 슬롯 (`FEF1` ~ `FEFC`, `FBF1` ~ `FBFC`)** 지원 (10번=`FEFA`, 11번=`FEFB`, 12번=`FEFC`)

### (2) 자정 24:00 (`0x24 0x00`) 무암전(Gapless) 연속 점등 규격
* 안드로이드 공식 APK(`StringTools.java`)는 자정 끝 시각(1439분)을 전송할 때 `23:59` 대신 **`24:00` (BCD `0x24 0x00`)**으로 인코딩하여 전송합니다.
* 조명 MCU는 `24:00`을 `23:59:59.999`까지 꽉 채운 자정 끝점으로 인식하므로, 익일 `00:00:00` 시작 슬롯과 연결될 때 1분의 조명 꺼짐(암전) 없이 0.1초의 끊김 없는 연속 점등이 보장됩니다.

---

## 7. 전송 딜레이 및 패킷 큐 스케줄링
다수의 타이머 및 스펙트럼 슬롯 패킷을 연속 전송할 때 BLE 칩셋의 FIFO 버퍼 오버플로우를 방지하기 위해, 모든 Write 명령은 **500ms(0.5초) 간격의 큐(Queue) 딜레이**를 거쳐 순차적으로 송신해야 합니다.
