# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 프로세스, 벤치마크와 보수적인 시스템 최적화 기능을 제공하는 .NET 9 WPF 데스크톱 애플리케이션입니다.

## CoreWatch 6.1.1

- 업데이트 기능을 OPTIMIZE에서 제거하고 상단 `모니터링 중` 상태 옆으로 이동
  - 새 버전이 있을 때만 `업데이트 vX.Y.Z` 배지 표시
  - 사용자가 누른 뒤에만 설치 파일 다운로드
  - GitHub Release SHA-256과 일치한 설치 파일만 실행 가능
- 네트워크 진단 표의 좌우 경계·높이를 고정하고 열·행 크기 변경 차단
- 바깥 페이지 안의 표는 내부 스크롤바 대신 `모두 보기 · N개`로 펼치며 바깥 스크롤만 사용
- 저장 공간 정리 실행 버튼을 미리보기 목록 바로 위로 이동
- 벤치마크 실행 단계·진행률 표시를 추가하고 조회 표의 파란 선택 배경 제거
- 개인화 추천의 기본 ComboBox를 현재 UI와 어울리는 4개 용도 세그먼트 버튼으로 교체
- 하드웨어 기능 트리의 CPU·GPU·메모리·저장장치 선택이 실제 상세 목록으로 이동하도록 수정
- 내부 표 위 마우스 휠은 바깥 페이지 스크롤을 우선 사용

## 다운로드

- [GitHub v6.1.1 릴리스](https://github.com/Wakbu/CoreWatch/releases/tag/v6.1.1)
- [설치 없는 Windows x64 ZIP](https://github.com/Wakbu/CoreWatch/releases/download/v6.1.1/CoreWatch-v6.1.1-win-x64.zip)
  - 크기: `85,580,620 bytes`
  - SHA-256: `7A48040575FA4E69BE2740D03A64F91A8D03A78FB062AB05E2D09072AD93576C`
- [Windows x64 설치 프로그램](https://github.com/Wakbu/CoreWatch/releases/download/v6.1.1/CoreWatch-v6.1.1-Setup.exe)
  - 크기: `320,109,994 bytes`
  - SHA-256: `4E1F76055BB44854B06C36176D1BA5090D5EA6988C2A4215C87CB458CC15540C`
- 설치 없이 실행: `artifacts/publish/CoreWatch-v6.1.1-win-x64/CoreWatch.exe`

## 주요 기능

- CPU·메모리·디스크·네트워크 실시간 텔레메트리와 하드웨어·센서 상태
- 프로세스 분류·아이콘·증분 갱신과 보호 정책 기반 사용자 확인형 종료
- CPU·메모리·저장장치·GPU 벤치마크 및 외부 서버 없는 이 PC 로컬 기록 비교
- 전원 계획, 장시간 백그라운드 부하, 시작 프로그램, 메모리, 저장장치와 임시 파일 분석
- 네트워크 지연·패킷 손실·어댑터 오류 진단과 PC 용도별 개인화 추천
- 진단·텔레메트리·하드웨어·SMART·배터리·벤치마크 통합 JSON/HTML/PDF 보고서
- 탭별 접이식 기능 트리와 섹션 바로가기

## 안전·개인정보 원칙

- 앱 시작 후 업데이트 알림 확인을 위해 공개 GitHub Release 메타데이터만 조회하며 PC 진단 데이터는 전송하지 않습니다.
- 설치 파일 다운로드와 실행은 사용자가 업데이트 배지를 누르고 확인한 경우에만 수행합니다.
- 외부 지연 진단은 사용자가 네트워크 진단 버튼을 누른 경우에만 실행합니다.
- 시스템 설정 변경·파일 정리는 미리보기와 사용자 확인 후 실행합니다.
- Windows 보안 기능, 업데이트와 핵심 서비스를 임의로 끄지 않습니다.
- 보고서와 벤치마크 비교 데이터는 로컬에만 저장합니다.

## 빌드 및 검증

```powershell
dotnet restore src/SystemChecker.App/CoreWatch.V5.1.Release.csproj
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Debug -warnaserror
dotnet build src/SystemChecker.App/CoreWatch.V5.1.Release.csproj -c Release -warnaserror
dotnet list src/SystemChecker.App/CoreWatch.V5.1.Release.csproj package --vulnerable --include-transitive
dotnet run --project tools/CoreWatch.V5.Verification/CoreWatch.V5.Verification.Final.csproj -c Release
```

자동 무결성 검증은 기존 핵심 `37/37`, 확장 `8/8`과 UI·정책 `7/7`을 통과합니다. 자세한 상태는 [CURRENT_STATE_COREWATCH_V5_1.md](CURRENT_STATE_COREWATCH_V5_1.md)를 참고하세요.
