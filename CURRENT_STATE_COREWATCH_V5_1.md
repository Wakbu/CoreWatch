# CoreWatch 5.3.1 구현 상태

## 활성 프로젝트

- 앱: `src/SystemChecker.App/CoreWatch.V5.1.Release.csproj`
- 설치 프로그램: `tools/SystemChecker.Installer/CoreWatch.V5.1.Installer.csproj`
- 자동 검증: `tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj`
- 앱 및 설치 프로그램 버전: `5.3.1`

파일명은 기존 자동화 호환성을 위해 V5.1을 유지합니다.

## 5.3.1 UI 수정

- 프로세스 상단 CPU 요약 영역을 96px, 메모리 요약 영역을 220px 고정 폭으로 변경했습니다.
- 값 문자열에는 Consolas와 우측 정렬을 사용해 실시간 수치가 바뀌어도 주변 UI가 이동하지 않습니다.
- 프로세스 이름·아이콘 셀에 명시적인 좌측 18px 외부 여백을 추가했습니다.
- 상태, PID, 하드웨어와 최적화 표의 일반 텍스트 셀에도 좌측 18px·우측 14px 여백을 적용했습니다.
- 모든 표 셀과 열 머리글에 얇은 세로·가로 경계선을 적용해 속성 구분을 강화했습니다.
- 저장 공간 정리 선택 열을 22px 둥근 체크 컨트롤로 교체하고 선택된 행 전체를 은은하게 강조합니다.

## 기존 주요 기능

- 메뉴 번호를 탐색 순서에서 자동 계산합니다.
- 프로세스 실제 실행 파일 아이콘을 경로별로 캐시합니다.
- PID 기반 증분 갱신으로 주기적 깜빡임을 제거하고 선택·검색·정렬을 유지합니다.
- OPTIMIZE 탭에서 시작 프로그램 분석, 임시 파일 정리 미리보기/승인형 정리와 전후 비교를 제공합니다.
- Microsoft.Data.Sqlite 10.0.10과 SQLitePCLRaw 3.0.4를 사용하며 알려진 취약 패키지가 없습니다.

## 검증 결과

- NuGet 복원 및 취약점 검사 통과
- Debug/Release 빌드: 경고 0, 오류 0
- 자동 무결성 검증: 21/21
- Release 앱 시작 및 응답 정상
- 메모리 값이 `41.0% · 변화 -1.2 MB`에서 `41.0% · 변화 +0.5 MB`로 바뀌어도 X=1376, Width=220 유지
- CPU 요약 영역 X=1256, Width=96 유지
- 프로세스 아이콘 좌측 여백 22px
- 프로세스 상태·PID 좌측 여백 18px
- 하드웨어 장치·세부 정보 좌측 여백 18px
- 저장 공간 정리 체크 컨트롤 4개 표본, 22×22px 및 Off→On 토글 확인
- 프로세스 및 하드웨어 텍스트 수직 중심 오차 최대 0.5px

## 배포 산출물

- `artifacts/CoreWatch-v5.3.1-Setup.exe`
- `artifacts/CoreWatch-v5.3.1-win-x64.zip`
- Setup SHA-256: `76549A1848B5A47D2D5FC966A4A148874CD2428FD832A1BED84011AAE73E5AC2`
- ZIP SHA-256: `048D69DFDDA704CD69B4A484A986EF502EE6A78553E0CAC8E2C9C48ACC3C3795`

## 다음 단계

`SYSTEM_OPTIMIZATION_ROADMAP.md`의 전원 설정 분석, 장시간 백그라운드 부하 탐지, 시작 프로그램 활성/비활성화와 되돌리기는 이후 확장 범위입니다.
