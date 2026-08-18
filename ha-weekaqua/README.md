# 🐠 Home Assistant WeekAqua Integration & Lovelace Card

Home Assistant(HA) 환경에서 **WeekAqua 수조 조명**을 제어할 수 있는 전용 **Custom Integration (HACS 지원)** 및 **WPF 스타일 Lovelace 커스텀 대시보드 카드**입니다.

조명 내장 MCU의 하드웨어 슬롯(5개/8개/12개) 한계에서 완전히 벗어나, **Home Assistant의 시간 기반 트리거 및 실시간 선형 보간(Linear Ramp Interpolation) 엔진**을 통해 **무제한 스케줄 단계(Unlimited Steps)**를 자유롭게 구성할 수 있습니다.

---

## ✨ 주요 기능 (Key Features)

1. **무제한 단계 동적 스케줄러 (Unlimited Steps Dynamic Scheduler)**:
   - 5개, 8개, 12개 슬롯 제한 없이 사용자가 원하는 만큼(10개, 20개, 50개 등) 시간-스펙트럼 포인트를 추가/삭제 가능
   - Home Assistant가 매 분마다 현재 시각의 스펙트럼을 **부드러운 선형 보간(Linear Interpolation)**으로 계산하여 실시간 패킷(`FBF9...`) 송신
   - MCU 펌웨어의 `FEF9` 커맨드 충돌 위험이 전혀 없는 100% 안전한 제어 방식
2. **ESPHome Bluetooth Proxy 완벽 지원**:
   - HA 서버와 어항의 거리가 멀어도 방마다 설치된 ESP32 Bluetooth Proxy를 통해 원격으로 블루투스 패킷 송수신
3. **100% 전력 안전 상한선 (Max Power Limit Guard)**:
   - 공식 안드로이드 APK 전력 공식 기반 채널 비율 보존 자동 정규화(Normalize) 내장
4. **WPF 룩앤필의 Lovelace 커스텀 카드 (`weekaqua-card.js`)**:
   - 다크 테마, 채널별 컬러 슬라이더(R, G, B, W, UV, Violet, Fan)
   - 실시간 총 전력 부하(%) 게이지 프로그레스 바
   - **24시간 인터랙티브 SVG 타임라인 그래프** 시각화
   - 원클릭 프리셋 버튼 (수초, 어항, 산호, 심야 달빛 등)
5. **스마트 플러그 전력 모니터링**:
   - GATT Notify 특성을 통한 누적 소비전력량(kWh) 실시간 디코딩 및 HA 에너지 대시보드 연동

---

## 📁 프로젝트 구조

```
ha-weekaqua/
├── custom_components/
│   └── weekaqua/
│       ├── __init__.py                # HA 통합 진입점 및 서비스 등록
│       ├── manifest.json              # HACS / HA 메타데이터
│       ├── const.py                   # UUID, 모델 코드, 프리셋 상수
│       ├── protocol.py                # Python WeekAqua BLE 프로토콜 엔진
│       ├── coordinator.py             # BLE 통신 & 무제한 스케줄 보간 엔진
│       ├── config_flow.py             # BLE 기기 자동 검색(Discovery) 플로우
│       ├── light.py                   # Master Light Entity
│       ├── number.py                  # 채널별 백분율 슬라이더 (R/G/B/W/UV/V/Fan)
│       ├── sensor.py                  # 전력량(kWh), 소비전력 부하(%) 센서
│       ├── switch.py                  # 무제한 스케줄러 On/Off 토글
│       ├── services.yaml              # 커스텀 서비스 명세
│       └── translations/              # 다국어 지원 (ko, en)
├── dist/
│   └── weekaqua-card.js               # WPF 스타일 Lovelace 커스텀 카드
├── hacs.json                          # HACS 지원 메타데이터
├── test_protocol.py                   # 단위 검증 테스트 스크립트
└── README.md
```

---

## 🚀 설치 방법 (Installation)

### 방법 1. 수동 설치 (Manual Installation)

1. `custom_components/weekaqua` 폴더를 Home Assistant 설정 디렉토리(`config/custom_components/weekaqua/`)에 복사합니다.
2. `dist/weekaqua-card.js` 파일을 `config/www/weekaqua-card.js`로 복사합니다.
3. Home Assistant를 재시작합니다.
4. **설정 > 대시보드 > 리소스**에서 `/local/weekaqua-card.js`를 JavaScript 모듈로 추가합니다.
5. **설정 > 기기 및 서비스 > 통합구성요소 추가**에서 **"WeekAqua"**를 검색하여 추가합니다.

### 방법 2. HACS 사용자 지정 저장소 등록 (Custom Repository)

1. HACS > Integrations > 우측 상단 메뉴 > **Custom repositories** 클릭
2. Repository URL 입력 및 Category를 **Integration**으로 선택 후 추가
3. WeekAqua 다운로드 후 HA 재시작

---

## 🎨 Lovelace 대시보드 카드 사용법

대시보드 수정 모드에서 수동 카드를 추가하고 아래 YAML을 입력합니다:

```yaml
type: custom:weekaqua-card
title: 거실 수초항 조명
entity: light.aquarium_light
```

---

## 🤖 HA 자동화 (Automation) 서비스 예시

### 1. 스펙트럼 프리셋 적용
```yaml
service: weekaqua.apply_preset
data:
  preset: RedGrass  # GreenGrass, RedGrass, FishMixed, CoralAb, Moonlight 등
```

### 2. 수동 스펙트럼 전송
```yaml
service: weekaqua.set_spectrum
data:
  red: 80
  green: 60
  blue: 40
  white: 70
  uv: 30
  violet: 20
```

### 3. 무제한 스케줄 업로드
```yaml
service: weekaqua.set_schedule
data:
  points:
    - time: "08:00"
      r: 0
      g: 0
      b: 0
      w: 0
      uv: 0
      v: 0
    - time: "10:00"
      r: 30
      g: 40
      b: 30
      w: 40
      uv: 10
      v: 10
    - time: "13:00"
      r: 70
      g: 100
      b: 70
      w: 90
      uv: 50
      v: 40
    - time: "18:00"
      r: 40
      g: 60
      b: 40
      w: 50
      uv: 20
      v: 10
    - time: "20:30"
      r: 0
      g: 0
      b: 4
      w: 0
      uv: 0
      v: 0
```
