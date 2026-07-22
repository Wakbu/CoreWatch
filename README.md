# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 프로세스, 벤치마크와 보수적인 시스템 최적화 기능을 제공하는 .NET 9 WPF 데스크톱 애플리케이션입니다.

## CoreWatch 6.0.0

- 보고서 생성 전 종합 판정, 데이터 포함 범위와 기준 시각을 확인하는 리포트 요약
- 진단·최근 텔레메트리·하드웨어·SMART·배터리·벤치마크 이력을 동일 스냅샷으로 JSON·HTML·PDF 생성
- 자동 업로드 없이 이 PC에서만 보고서를 생성하는 개인정보 안내
- 사이드바 탭별 접이식 기능 트리와 섹션 바로가기
- TRIM, 볼륨 여유 공간, 파일 시스템 오류 상태와 보수적인 권장 사항
- 외부 서버 없이 이 PC의 동일 버전 벤치마크 기록만 비교
- 설치 없는 실행본: `artifacts/publish/CoreWatch-v6.0.0-win-x64/CoreWatch.exe`
- [GitHub v6.0.0 릴리스](https://github.com/Wakbu/CoreWatch/releases/tag/v6.0.0)
- [설치 없는 Windows x64 ZIP 다운로드](https://github.com/Wakbu/CoreWatch/releases/download/v6.0.0/CoreWatch-v6.0.0-win-x64.zip)
  - 크기: `85,609,445 bytes`
  - SHA-256: `05421DA064FA5567C73F117E99B282F6136B3376F8CEC57BF5E14F03954189AE`
- [Windows x64 설치 프로그램 다운로드](https://github.com/Wakbu/CoreWatch/releases/download/v6.0.0/CoreWatch-v6.0.0-Setup.exe)
  - 크기: `320,048,118 bytes`
  - SHA-256: `81A818DE48A5BF9B5AA2944A7728590970D7B12ADBD8B913584E16354A911A60`

- Windows GetPerformanceInfo 기반 물리 메모리·커밋·캐시·커널 풀 분석
- 페이지 파일 자동 관리, 할당·현재·최고 사용량 진단
- 메모리 압축 상태와 일반 권한 조회 제한 구분
- 프로세스별 작업 집합·전용 커밋·최대 작업 집합·스레드 표시
- 커밋 한계와 물리 메모리 압박 기준의 정상·주의·위험 판정
- 인터넷 연결 없이 동일 벤치마크 버전의 이 PC 로컬 기록만 비교

- 전원·성능 설정 카드 제목에 16px 상단 여백 적용
- Windows StartupApproved 기반 시작 프로그램 활성·비활성 상태 표시
- 사용자 선택형 상태 전환과 변경 전 JSON 자동 백업
- 마지막 변경의 원래 레지스트리 상태 원클릭 복원
- Windows·보안 항목 보호, 전체 사용자 항목의 관리자 권한 안내
- 보호 항목 체크 비활성화와 제한·영향 사유 열 제공

- 15·30·60초 비동기 장시간 백그라운드 부하 측정
- 프로세스별 CPU 평균·최대, 메모리 평균·최대, 디스크 누적·평균 처리량 기록
- IPv4·IPv6 활성 TCP 연결의 평균·최대 개수 표시
- 지속 부하 높음·검토 권장·정상 범위·Windows 구성요소 판정
- PID 기반 행 재사용과 순위 이동으로 측정 중 표 깜빡임 방지
- 측정 중지와 앱 종료 연동 취소 지원

- 활성 Windows 전원 계획, 전원 연결·배터리 상태 분석
- CPU AC/DC 최소·최대 상태와 프로세서 부스트 모드 진단
- 균형 조정·고성능·절전 프로필의 설치 여부와 용도별 추천
- 변경 전 자동 JSON 백업과 마지막 전원 계획 원클릭 복원
- GPU 전력 정책은 제조사 드라이버 영역으로 명확히 구분해 임의 변경 방지
- 설치 없이 실행 가능한 버전별 `artifacts/publish` 폴더 유지

- 메뉴 순서와 번호 자동 동기화: `01 OVERVIEW → 02 PROCESSES → 03 HARDWARE → 04 BENCHMARK → 05 OPTIMIZE → 06 REPORT`
- 프로세스 상단 CPU·메모리 요약을 고정 폭으로 배치해 실시간 수치 변경 시 흔들림 방지
- 표 셀에 명시적인 18px 좌측 여백과 세로 구분선을 적용해 속성 간 경계 강화
- 정리 미리보기 선택 열을 22px 둥근 체크 컨트롤과 은은한 선택 행 강조로 교체
- 프로세스를 앱, 백그라운드 프로세스, Windows 프로세스로 분류
- 실행 파일의 실제 프로그램 아이콘 표시 및 경로별 캐시
- PID 기반 증분 갱신으로 선택, 정렬, 스크롤 위치를 유지하고 주기적 깜빡임 제거
- CPU·메모리 사용량 1초 갱신과 보호 정책 기반 프로세스 종료
- 프로세스 및 하드웨어 표 텍스트의 실제 수직 중앙 정렬
- 시작 프로그램 게시자·등록 위치·유지 권장 여부 분석
- 24시간 이상 된 임시 파일의 정리 가능 용량 미리보기와 사용자 승인형 정리
- CPU·메모리·프로세스 수·디스크 여유 공간을 이용한 최적화 전후 비교
- Pull Request, 강제 푸시 및 삭제 제한을 위한 GitHub CI·브랜치 보호 구성

## 최적화 안전 원칙

- 진단과 미리보기는 시스템 설정을 변경하지 않습니다.
- 임시 파일은 사용자가 항목을 선택하고 확인한 경우에만 삭제합니다.
- 승인된 임시 폴더 내부, 24시간 이상 된 파일만 대상으로 하며 사용 중이거나 접근 불가능한 파일은 건너뜁니다.
- 시작 프로그램은 근거와 권장 사항을 표시하며 자동으로 비활성화하지 않습니다.
- Windows 핵심 서비스, 보안 기능, 업데이트, 레지스트리 트윅 및 RAM 클리너는 사용하지 않습니다.

## 프로세스 보호 정책

`보호된 시스템`, `Windows 구성요소`, `확인 필요` 프로세스는 CoreWatch에서 종료할 수 없습니다. `일반 프로그램`만 사용자 확인 후 종료할 수 있으며, 이는 안전장치이지 악성 코드 판정 기능은 아닙니다.

## 센서와 권한

CoreWatch 본체는 일반 사용자 권한으로 실행합니다. 설치 프로그램만 관리자 권한을 요청하여 PawnIO 2.2.0을 구성합니다. 기존 PawnIO 2.2.0 이상이 있으면 재설치하지 않으며, 설치 실패가 CoreWatch 본체 설치와 실행을 차단하지 않습니다.

일부 메인보드는 LibreHardwareMonitor가 지원하는 센서 이름이나 컨트롤러를 제공하지 않아 PawnIO 설치 후에도 CPU 온도가 없을 수 있습니다.

## 빌드 및 검증

```powershell
dotnet restore src/SystemChecker.App/CoreWatch.V5.1.Release.csproj
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Debug -warnaserror
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Release -warnaserror
dotnet list src/SystemChecker.App/CoreWatch.V5.1.Release.csproj package --vulnerable --include-transitive
dotnet run --project tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj -c Release
```

현재 자동 무결성 검증은 핵심 `37/37`과 확장 `8/8`이며 알려진 취약 패키지는 없습니다. 자세한 상태는 [CURRENT_STATE_COREWATCH_V5_1.md](CURRENT_STATE_COREWATCH_V5_1.md)를 참고하세요.




