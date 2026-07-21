# CoreWatch 5.3.0 구현 상태

## 활성 프로젝트

- 앱: `src/SystemChecker.App/CoreWatch.V5.1.Release.csproj`
- 설치 프로그램: `tools/SystemChecker.Installer/CoreWatch.V5.1.Installer.csproj`
- 자동 검증: `tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj`
- 앱 및 설치 프로그램 버전: `5.3.0`

파일명은 기존 자동화 호환성을 위해 V5.1을 유지합니다.

## 이번 구현

- 메뉴 번호를 탐색 순서에서 다시 계산하며 OPTIMIZE를 리포트 앞에 추가했습니다.
- 프로세스 실제 실행 파일 아이콘을 추출하고 경로별로 캐시합니다.
- 프로세스 목록을 PID 기반으로 증분 갱신해 전체 컬렉션 교체와 주기적 깜빡임을 제거했습니다.
- 갱신 중 동일 PID 행 컨테이너, 선택 상태, 검색과 정렬을 유지합니다.
- 프로세스 상태·CPU·메모리·PID와 하드웨어 장치·세부 정보를 템플릿 셀로 바꿔 수직 중앙에 배치했습니다.
- OPTIMIZE 탭에 시작 프로그램 분석, 임시 파일 정리 미리보기/승인형 정리, 최적화 전후 기준 비교를 구현했습니다.
- Microsoft.Data.Sqlite 10.0.10과 SQLitePCLRaw 3.0.4로 업데이트해 알려진 High 취약점을 제거했습니다.
- GitHub Actions에 Debug/Release 빌드, 취약점 검사와 자동 검증 워크플로를 추가했습니다.

## 최적화 안전 범위

- 시작 프로그램은 현재 활성 등록 항목, 게시자, 등록 위치와 보수적인 권장 사항을 읽기 전용으로 표시합니다.
- 정리 후보는 승인된 임시 폴더 내부의 24시간 이상 된 파일만 포함합니다.
- 실제 삭제는 사용자가 체크한 항목과 확인 대화상자를 거친 경우에만 실행합니다.
- 사용 중, 접근 제한, 승인 범위 밖 파일은 삭제하지 않습니다.
- 최적화 기준은 CPU, 메모리, 프로세스 수, 디스크 최소 여유 공간을 기록합니다.

## 검증 결과

- NuGet 복원 성공
- 알려진 취약 패키지 없음
- Debug/Release 빌드: 경고 0, 오류 0
- 자동 무결성 검증: 21/21
- Release 앱 시작 및 응답 정상
- 메뉴 순서: `01 OVERVIEW, 02 PROCESSES, 03 HARDWARE, 04 BENCHMARK, 05 OPTIMIZE, 06 REPORT`
- 프로세스 화면: 실제 아이콘 10개 표본 확인
- 프로세스 상태·CPU·메모리·PID 중심 오차: 최대 0.5px
- 하드웨어 장치·세부 정보 중심 오차: 최대 0.5px
- 4초 실시간 갱신 후 동일 행 컨테이너 및 선택 상태 유지
- OPTIMIZE 시작 프로그램/정리 미리보기 분석 완료 및 기준 기록 동작 확인

## 배포 산출물

- `artifacts/CoreWatch-v5.3.0-Setup.exe`
- `artifacts/CoreWatch-v5.3.0-win-x64.zip`

SHA-256과 실제 게시 실행본 검증 결과는 v5.3.0 릴리스에 기록합니다.

## 다음 단계

`SYSTEM_OPTIMIZATION_ROADMAP.md`의 전원 설정 분석, 장시간 백그라운드 부하 탐지, 시작 프로그램 활성/비활성화와 되돌리기는 이후 확장 범위입니다.
