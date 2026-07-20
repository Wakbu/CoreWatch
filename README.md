# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 프로세스와 벤치마크를 확인하는 .NET 9 WPF 데스크톱 애플리케이션입니다.

## CoreWatch 5.2

- 프로세스를 `앱`, `백그라운드 프로세스`, `Windows 프로세스`로 자동 분류
- 유형별 탭, 실시간 개수, 이름/PID/유형/보호 상태/경로 검색
- CPU·메모리 1초 갱신, 열 정렬, 보호 정책 기반 작업 종료
- Windows 11 작업 관리자에 가까운 정보 구조와 Fluent 스타일
- 넓어진 상단·좌측 여백, 12px 표면 모서리, 둥근 검색창과 버튼
- 얇은 오버레이형 스크롤바와 중립 회색 선택 상태
- 하드웨어 유형/세부 정보 2단 화면과 넉넉한 셀 여백
- 설치 프로그램에서 공식 PawnIO 2.2.0 자동 다운로드·SHA-256 검증·자동 설치

탐색 순서는 `개요 → 프로세스 → 하드웨어 → 벤치마크 → 리포트`입니다.

## 프로세스 분류

- `앱`: 최상위 창이 있는 일반 사용자 프로그램
- `백그라운드 프로세스`: 창 없이 실행되는 일반 프로그램과 보조 작업
- `Windows 프로세스`: Windows 핵심 프로세스 또는 Windows 디렉터리 구성요소

보호 분류는 별도로 유지합니다. `보호된 시스템`, `Windows 구성요소`, `확인 필요`는 종료할 수 없고 `일반 프로그램`만 사용자 확인 후 종료할 수 있습니다. 이는 안전장치이며 악성 코드 판정 기능은 아닙니다.

## 센서와 권한

CoreWatch 본체는 기본적으로 일반 사용자 권한으로 실행합니다. 설치 프로그램만 관리자 권한을 요청하여 [PawnIO 2.2.0 공식 릴리스](https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0)를 `-install -silent`로 구성합니다. 다운로드 파일은 설치 전에 고정 SHA-256으로 검증합니다. 설치 전에 기존 PawnIO 버전을 확인하므로 2.2.0 이상이 있으면 재설치하지 않습니다. PawnIO 설치가 실패해도 CoreWatch 설치와 실행은 계속됩니다. 별도의 `센서 권한 재시작` 버튼은 제거했습니다.

일부 메인보드는 LibreHardwareMonitor가 지원하는 센서 이름이나 컨트롤러를 제공하지 않아 PawnIO 설치 후에도 CPU 온도가 없을 수 있습니다.

## 빌드 및 검증

```powershell
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Release
dotnet run --project tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj -c Release
```

현재 자동 무결성 검증 결과는 `16/16`입니다. 자세한 구현 상태와 다음 작업 인수인계는 [CURRENT_STATE_COREWATCH_V5_1.md](CURRENT_STATE_COREWATCH_V5_1.md)를 참고하세요.

