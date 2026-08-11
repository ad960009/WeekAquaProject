# WeekAqua BLE Protocol Specification & C# WPF Controller

WeekAqua 스마트 수초 조명 및 스마트 플러그 디바이스의 BLE(Bluetooth Low Energy) 통신 프로토콜 역공학 명세서 및 C# WPF 제어 프로그램입니다.

> [!WARNING]
> **⚠️ 주의사항 (Disclaimer)**  
> 본 프로젝트는 안드로이드 앱 분석을 기반으로 작성된 **시험적(Experimental) 소프트웨어 및 연구용 명세서**입니다. 실제 실물 디바이스에서의 완벽한 동작 및 안전성을 보장하지 않으며, 본 프로그램 또는 명세서의 이용으로 인해 발생하는 어떠한 문제나 기기 이상에 대해서도 책임지지 않습니다.

---

## 📂 프로젝트 구성

- 📘 **[WeekAqua_BLE_Protocol_Specification.md](file:///e:/android/APK/WeekAqua/WeekAquaProject/WeekAqua_BLE_Protocol_Specification.md)**: 안드로이드 앱 분석 기반 완벽 프로토콜 명세서 (UUID, 패킷 구조, 스펙트럼 인코딩 공식, 스케줄링 큐 딜레이, 전력 파싱 연산)
- 💻 **[WeekAquaWPF/](file:///e:/android/APK/WeekAqua/WeekAquaProject/WeekAquaWPF/)**: .NET WPF C# 기반 Windows 전용 조명/플러그 제어 애플리케이션
  - 자세한 애플리케이션 가이드는 [WeekAquaWPF/README.md](file:///e:/android/APK/WeekAqua/WeekAquaProject/WeekAquaWPF/README.md) 참조

---

## 🚀 빠른 시작

```powershell
# WPF 애플리케이션 빌드 및 실행
cd WeekAquaWPF
dotnet run
```
