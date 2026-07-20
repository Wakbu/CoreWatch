# CoreWatch 5.2 구현 상태

## 활성 프로젝트

- 앱: `src/SystemChecker.App/CoreWatch.V5.1.Release.csproj`
- 설치 프로그램: `tools/SystemChecker.Installer/CoreWatch.V5.1.Installer.csproj`
- 자동 검증: `tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj`
- 표시 버전: `5.2.0`

파일명은 기존 자동화 호환성을 위해 V5.1을 유지하지만 프로젝트 및 바이너리 버전은 5.2입니다.

## 프로세스 화면

- `ProcessMonitorService`가 최상위 창과 보호 분류를 함께 사용해 앱/백그라운드/Windows 프로세스를 구분합니다.
- 모두, 앱, 백그라운드 프로세스, Windows 프로세스 탭이 실시간 개수를 표시합니다.
- 이름, PID, 유형, 보호 상태와 경로를 즉시 검색합니다.
- CPU와 메모리를 1초마다 다시 측정하고 현재 갱신 시각을 표시합니다.
- 이름 셀은 아이콘, 이름, 유형의 2단 정보 구조입니다.
- Windows 핵심·Windows 폴더·경로 미확인 프로세스는 종료하지 않습니다.

## 디자인

- Microsoft Windows 11 Fluent 원칙의 calm/coherent 방향과 작업 관리자 정보 구조를 참고했습니다.
- 콘텐츠 시작 여백, 제목 간격, 표 셀 패딩을 확대했습니다.
- 검색창, 버튼, 표면은 8~12px 모서리 반경을 사용합니다.
- 기본 파란 선택 배경을 제거하고 중립 회색 hover/selection을 사용합니다.
- 기본 스크롤바를 10px 폭의 둥근 thumb 스타일로 교체했습니다.
- 하드웨어는 왼쪽 유형 탐색과 오른쪽 상세 표의 2단 구조입니다.

## PawnIO 설치

- CoreWatch 앱은 `requireAdministrator`를 사용하지 않습니다.
- CoreWatch 설치 프로그램만 `requireAdministrator` 매니페스트를 사용합니다.
- 설치 시 공식 GitHub에서 PawnIO 2.2.0을 다운로드합니다.
- 기대 SHA-256: `1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032`
- 검증 후 `PawnIO_setup.exe -install -silent`를 실행하며 종료 코드 0과 3010을 처리합니다.
- PawnIO는 다른 프로그램도 사용할 수 있으므로 CoreWatch 제거 시 자동 제거하지 않습니다.
- 기존 센서 권한 재시작과 PawnIO 설치 페이지 버튼은 제거했습니다.

## 검증 결과

- Debug/Release 빌드: 경고 0, 오류 0
- 자동 무결성 검증: 15/15
- 프로세스 3분류 및 앱/백그라운드 분리 확인
- 실제 실행 UI에서 분류 탭 개수와 1초 갱신 확인
- 콘텐츠 헤더의 창 기준 왼쪽 위치 276px: 226px 사이드바 이후 약 50px 여백
- 설치 프로그램 관리자 매니페스트 포함 확인
- 게시 실행본 버전 5.2.0.0 및 정상 시작 확인

## 배포 파일

- `artifacts/CoreWatch-v5.2-Setup.exe`
- `artifacts/CoreWatch-v5.2-win-x64.zip`

## 참고한 공식 자료

- Microsoft Support: Task Manager는 프로세스를 apps, background processes, Windows processes로 구분
- Microsoft Learn: Windows 11 디자인 원칙과 Task Manager의 실시간 데이터 표
- PawnIO.Setup GitHub: 공식 2.2.0 릴리스와 silent CLI
- LibreHardwareMonitor GitHub: 일부 센서는 관리자 권한 또는 저수준 드라이버 필요
