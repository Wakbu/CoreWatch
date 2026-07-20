# CoreWatch V5 현재 상태

마지막 갱신: 2026-07-20

## 활성 프로젝트

- 앱: `src/SystemChecker.App/CoreWatch.V5.Release2.csproj`
- 설치 프로그램: `tools/SystemChecker.Installer/CoreWatch.V5.Installer.csproj`
- 자동 검증: `tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj`

## 이번 버전 변경

- Catmull-Rom 곡선 보간을 제거해 그래프의 과도한 출렁임과 오버슈트를 제거했다.
- 500ms 동안 한 칸씩 수평 이동하는 선형 시계열 렌더링으로 변경했다.
- Y축을 1·2·5 단위 구간과 30초 축소 히스테리시스로 고정했다. 현재 범위를 벗어날 때만 즉시 확장한다.
- 온도 표시 영역을 245px로 늘리고 센서 상세 영역과 버튼 배치를 조정했다.
- CPU 패키지 외에 메인보드의 CPU/Tctl/Tdie/Socket/PECI 센서와 Windows ACPI Thermal Zone을 탐색한다.
- PawnIO 설치 상태를 확인하고 공식 드라이버 설치 페이지 버튼을 제공한다.
- 하드웨어 인벤토리 셀과 헤더의 좌우 여백을 늘렸다.
- 벤치마크에 S~D 성능 구간, 용도 설명, 기준 처리량과 전체 테스트 하드웨어 목록을 추가했다.
- 리포트 탭을 스크롤 구조로 수정하고 데이터셋 요약, SMART, 배터리, 이력, 드라이버 진단을 배치했다.
- 프로세스 탭에 검색·정렬·CPU·메모리·보호 등급·경로·종료 기능을 추가했다.

## 프로세스 종료 안전성

- PID 0~4와 알려진 Windows 핵심 프로세스는 `보호된 시스템`이다.
- Windows 폴더의 실행 파일은 `Windows 구성요소`이다.
- 실행 경로를 읽을 수 없으면 `확인 필요`이다.
- 위 세 분류는 종료할 수 없다.
- 일반 프로그램만 확인 대화상자 후 정상 종료를 먼저 요청하고, 2초 뒤에도 남아 있을 때 프로세스 트리를 종료한다.

## 센서 한계

현재 테스트 PC에서는 ACPI Thermal Zone과 PawnIO가 제공되지 않았다. 관리자 권한만으로 해결되지 않는 이유는 최신 Windows의 메모리 무결성 환경에서 저수준 하드웨어 접근 드라이버가 별도로 필요할 수 있기 때문이다. CoreWatch는 임의의 커널 드라이버를 자동 설치하지 않고, 사용자가 공식 PawnIO 설치를 선택하도록 한다.

## 검증 결과

- Debug 빌드: 경고 0 / 오류 0
- Release 빌드: 경고 0 / 오류 0
- 자동 무결성 검사: 13/13
- GUI 초기화 및 응답 정상
- 프로세스 탭에서 CPU·메모리 열과 일반/Windows 분류 확인
- 벤치마크 비교 기준과 테스트 하드웨어 UI 확인
- 리포트 데이터셋, SMART, SQLite 이력, 드라이버 진단 UI 확인
- 전체 CPU·메모리·DiskSpd·GPU 벤치마크 실제 완료

## 배포 경로

- 포터블: `artifacts/publish/CoreWatch-v5-win-x64/CoreWatch.exe`
- 설치 프로그램: `artifacts/CoreWatch-v5-Setup.exe`
- ZIP: `artifacts/CoreWatch-v5-win-x64.zip`

## GitHub 게시

대상 저장소: `https://github.com/Wakbu/CoreWatch`

GitHub CLI의 Wakbu 인증을 갱신한 뒤 첫 커밋과 푸시가 필요하다.
