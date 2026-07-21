# CoreWatch 5.6.0 구현 상태

## 현재 버전

- 앱 및 설치 프로그램: `5.6.0`
- 대상: Windows x64, .NET 9 WPF
- 이번 변경: 전원 카드 상단 여백과 시작 프로그램 관리

## 5.6.0 구현 내용

- 전원·성능 설정 제목에 16px 상단 여백을 적용했습니다.
- HKCU/HKLM Run과 사용자·공용 시작 폴더를 분석합니다.
- Windows StartupApproved 상태로 활성·비활성을 표시하고 전환합니다.
- 상태 변경 직전 레지스트리 값을 `%LOCALAPPDATA%\\CoreWatch\\StartupBackups`에 JSON으로 백업합니다.
- 마지막 변경을 원래 값 또는 값 없음 상태로 복원합니다.
- Windows System32, 보안·Defender 항목은 선택할 수 없습니다.
- 일반 권한에서 변경할 수 없는 전체 사용자 항목은 관리자 권한 필요로 표시합니다.
- 보호 항목의 체크박스를 비활성화하고 제한·영향 사유를 표에 표시합니다.

## 검증 결과

- NuGet 취약 패키지: 없음
- 앱·설치 프로그램 Debug/Release: 경고 0, 오류 0
- 자동 무결성 검증: `33/33` 통과
- 게시 앱: OPTIMIZE 이동, 상단 여백, 시작 프로그램 관리 UI, 응답 상태 확인
- ZIP 7개 필수 항목과 설치 프로그램 내장 리소스 4종 확인

## 배포 파일

- `artifacts/CoreWatch-v5.6.0-Setup.exe`
  - 크기: `319,970,730 bytes`
  - SHA-256: `36143AFCA715A1D1FCB42D539883E5C3D0D736A826FF25031F76A1C7CA152992`
- `artifacts/CoreWatch-v5.6.0-win-x64.zip`
  - 크기: `85,579,388 bytes`
  - SHA-256: `8188374C226EE91F6814871F74E67BD0FBC1151CEAC34F4023444B13722E9075`
- 설치 없이 실행: `artifacts/publish/CoreWatch-v5.6.0-win-x64/CoreWatch.exe`

`artifacts/publish/<버전>`은 삭제하지 않고 유지합니다.

## 다음 구현 순서

1. 메모리 상세 분석
2. 저장장치 진단 강화
3. 벤치마크 비교 기능
4. 리포트 강화
5. 자동 업데이트

사용자가 `ㄱㄱ`라고 입력하면 메모리 상세 분석을 구현·검증·배포합니다.
