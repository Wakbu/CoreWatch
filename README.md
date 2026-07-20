# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 벤치마크, 프로세스 안전 분류와 보고서를 한 화면에서 확인하는 WPF 데스크톱 애플리케이션입니다.

![CoreWatch icon](src/SystemChecker.App/Assets/corewatch-icon.png)

## 주요 기능

- CPU, 메모리, 디스크 I/O, 네트워크 수치와 안정된 최근 60초 그래프
- CPU/GPU 온도, 클럭, 전력, 팬 센서 및 PawnIO 안내
- 작업 관리자형 하드웨어 분류/상세 인벤토리
- 1초마다 CPU·메모리를 갱신하는 프로세스 목록, 검색·정렬·안전 종료
- Windows 핵심/Windows 구성요소/확인 필요/일반 프로그램 보호 분류
- CPU, 메모리, Microsoft DiskSpd, Direct3D GPU 벤치마크와 S~D 등급
- SMART, 배터리, 드라이버 진단과 SQLite 이력
- JSON, HTML, PDF 보고서 및 시스템 트레이 알림

탐색 순서는 `개요 → 프로세스 → 하드웨어 → 벤치마크 → 리포트`이며 리포트는 항상 마지막입니다. 프로세스와 하드웨어 화면은 Windows 11 작업 관리자의 정보 밀도와 여백을 참고했고 선택 색상은 강한 파란색 대신 중립 회색을 사용합니다.

## 빌드 및 검증

요구 사항: Windows 10/11, .NET 9 SDK

```powershell
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Release
dotnet run --project tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj -c Release
```

자체 포함 배포:

```powershell
dotnet publish src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/CoreWatch-v5.1-win-x64
```

## 센서 및 외부 구성요소

LibreHardwareMonitor 0.9.6으로 CPU 패키지, 메인보드 CPU 센서, GPU 센서를 탐색하고 Windows ACPI Thermal Zone을 보조 경로로 사용합니다. 저수준 CPU 센서가 차단되면 화면에서 [공식 PawnIO 설치 페이지](https://github.com/namazso/PawnIO.Setup/releases/latest)를 열 수 있습니다.

- LibreHardwareMonitor — MPL-2.0 및 구성요소별 라이선스
- Microsoft DiskSpd — 번들 EULA 참조
- Microsoft.Data.Sqlite
- QuestPDF Community License

상세 인수인계 내용은 [CURRENT_STATE_COREWATCH_V5_1.md](CURRENT_STATE_COREWATCH_V5_1.md)를 참고하세요.
