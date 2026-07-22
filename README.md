# CoreWatch

CoreWatch는 Windows PC의 실시간 성능, 하드웨어 상태, 프로세스, 벤치마크와 보수적인 시스템 최적화 기능을 제공하는 .NET 9 WPF 데스크톱 애플리케이션입니다.

## CoreWatch 6.1.0

- GitHub Release 기반 업데이트 확인과 설치 파일 다운로드
  - 앱 시작 시 외부 통신 없음
  - 사용자가 확인·다운로드를 각각 승인
  - GitHub가 제공하는 SHA-256과 일치한 설치 파일만 보관·실행
- Windows 저장소 센스, 예약 저장소와 시스템 드라이브 여유 공간 진단
- 로컬 게이트웨이·사용자 입력 대상의 지연/패킷 손실 및 어댑터 오류·폐기 진단
- PC 구성, 사용 목적과 로컬 진단 결과를 반영한 개인화 적합도·개선 권장 사항
- 내부 표 위에서 휠을 돌려도 바깥 페이지가 우선 스크롤되는 중첩 스크롤 개선
- OPTIMIZE 기능 트리에 신규 네 기능 바로가기 추가

## 다운로드

- [GitHub v6.1.0 릴리스](https://github.com/Wakbu/CoreWatch/releases/tag/v6.1.0)
- [설치 없는 Windows x64 ZIP](https://github.com/Wakbu/CoreWatch/releases/download/v6.1.0/CoreWatch-v6.1.0-win-x64.zip)
  - 크기: `85,636,322 bytes`
  - SHA-256: `81C10DBC47A6174835DD45ABD180108DB0CA349DEE94CABFF40F1F014A4F3716`
- [Windows x64 설치 프로그램](https://github.com/Wakbu/CoreWatch/releases/download/v6.1.0/CoreWatch-v6.1.0-Setup.exe)
  - 크기: `320,105,462 bytes`
  - SHA-256: `C9606E43C91850FB46BDED9C05A6A0D3CE584A33AF7CD3C4B2F15A4E827FB1A3`
- 설치 없이 실행: `artifacts/publish/CoreWatch-v6.1.0-win-x64/CoreWatch.exe`

## 주요 기능

- CPU·메모리·디스크·네트워크 실시간 텔레메트리와 하드웨어·센서 상태
- 프로세스 분류·아이콘·증분 갱신과 보호 정책 기반 사용자 확인형 종료
- CPU·메모리·저장장치·GPU 벤치마크 및 외부 서버 없는 이 PC 로컬 기록 비교
- 전원 계획, 장시간 백그라운드 부하, 시작 프로그램, 메모리, 저장장치와 임시 파일 분석
- 진단·텔레메트리·하드웨어·SMART·배터리·벤치마크 통합 JSON/HTML/PDF 보고서
- 탭별 접이식 기능 트리와 섹션 바로가기

## 안전·개인정보 원칙

- 기본 상태와 앱 시작 시 외부 서버로 데이터를 전송하지 않습니다.
- 업데이트 확인과 외부 지연 진단은 사용자가 버튼을 누른 경우에만 통신합니다.
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

자동 무결성 검증은 기존 핵심 `37/37`, 확장 `8/8`과 v6.1.0 정책 `5/5`를 통과합니다. 자세한 상태는 [CURRENT_STATE_COREWATCH_V5_1.md](CURRENT_STATE_COREWATCH_V5_1.md)를 참고하세요.
