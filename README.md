# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 벤치마크, 프로세스 안전 분류와 보고서를 한 화면에서 확인하는 WPF 데스크톱 애플리케이션입니다.

![CoreWatch icon](src/SystemChecker.App/Assets/corewatch-icon.png)

## 주요 기능

- CPU, 메모리, 디스크 I/O, 네트워크의 정확한 실시간 수치와 최근 60초 그래프
- 500ms 비동기 수집, 부드러운 수평 스크롤, 구간화·히스테리시스 기반의 안정된 Y축
- CPU/GPU 온도, 클럭, 전력, 팬 센서와 PawnIO 저수준 센서 드라이버 안내
- CPU, GPU, 메인보드, BIOS, RAM, 저장장치 하드웨어 인벤토리
- CPU SHA-256, 메모리 대역폭, Microsoft DiskSpd, Direct3D GPU 벤치마크
- S~D 성능 등급, 용도 기준, 이전 결과 비교와 테스트 하드웨어 컨텍스트
- 실행 프로세스 CPU·메모리 정렬/검색, 보안 등급 분류 및 일반 프로그램 종료
- SMART, 배터리 건강도, 드라이버·알 수 없는 장치 진단
- SQLite 30일 텔레메트리와 벤치마크 이력
- JSON, HTML, PDF 보고서와 시스템 트레이 임계값 알림
- 포터블 배포 및 현재 사용자용 설치/제거 프로그램

## 프로세스 보호 정책

CoreWatch는 프로세스를 다음처럼 보수적으로 분류합니다.

- `보호된 시스템`: Windows 핵심 프로세스와 PID 0~4. 종료 불가.
- `Windows 구성요소`: Windows 디렉터리에서 실행된 구성요소. 종료 불가.
- `확인 필요`: 실행 경로를 확인할 수 없는 프로세스. 종료 불가.
- `일반 프로그램`: 위 조건에 해당하지 않는 프로그램. 사용자 확인 후 종료 가능.

분류는 안전장치이지 악성 코드 판정 기능은 아닙니다.

## 센서

CoreWatch는 LibreHardwareMonitor 0.9.6으로 CPU 패키지, 메인보드 CPU 센서, GPU 센서를 탐색하고 Windows ACPI Thermal Zone을 보조 경로로 사용합니다. Windows 메모리 무결성 환경에서 저수준 CPU 센서가 차단되면 화면의 `PawnIO 센서 드라이버` 버튼에서 [공식 PawnIO 설치 페이지](https://github.com/namazso/PawnIO.Setup/releases/latest)를 열 수 있습니다.

## 빌드

요구 사항: Windows 10/11, .NET 9 SDK

```powershell
dotnet build src/SystemChecker.App/CoreWatch.V5.Release2.csproj -c Release
dotnet run --project tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj -c Release
```

자체 포함 배포:

```powershell
dotnet publish src/SystemChecker.App/CoreWatch.V5.Release2.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/CoreWatch-v5-win-x64
dotnet publish tools/SystemChecker.Installer/CoreWatch.V5.Installer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/installer-corewatch-v5
```

## 외부 구성요소

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — MPL-2.0 및 구성요소별 라이선스
- [Microsoft DiskSpd](https://github.com/microsoft/diskspd) — 번들 EULA 참조
- Microsoft.Data.Sqlite
- QuestPDF Community License
- PawnIO는 CoreWatch에 번들하지 않으며 사용자가 공식 설치 여부를 선택합니다.

## 검증

현재 자동 검증은 그래프 축 안정성, 센서 값 유효성, 프로세스 CPU·메모리 범위와 보호 정책, 벤치마크 등급, SQLite, SMART·배터리를 검사합니다. 최신 결과는 `13/13` 통과입니다.

상세 구현 상태는 [CURRENT_STATE_COREWATCH_V5.md](CURRENT_STATE_COREWATCH_V5.md)를 참고하세요.

