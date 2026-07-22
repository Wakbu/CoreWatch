# CoreWatch publish 보존 규칙

## 필수 규칙

- 기능 구현이 완료되면 새 버전 번호로 Release publish를 생성합니다.
- 설치하지 않고 바로 실행할 수 있도록 `artifacts/publish/CoreWatch-v<버전>-win-x64` 폴더를 삭제하지 않고 유지합니다.
- 직접 실행 파일은 위 폴더의 `CoreWatch.exe`입니다.
- 기존 버전별 publish 폴더도 사용자 요청 없이 삭제하거나 덮어쓰지 않습니다.
- publish 후에는 해당 폴더의 `CoreWatch.exe`를 직접 실행해 시작 직후 종료·미응답 여부를 확인합니다.
- 완료 보고에 publish 폴더, 실행 파일, 전체 파일 수·크기와 대표 실행 파일의 SHA-256을 기록합니다.
- GitHub Release에는 설치 없는 ZIP과 같은 버전의 `CoreWatch-v<버전>-Setup.exe`를 모두 첨부합니다.
- 설치 프로그램은 내장 앱 버전·필수 리소스·파일 크기·SHA-256을 확인하고 실행 취소 스모크 테스트를 거칩니다.
- 두 배포 파일 중 하나라도 누락된 상태로 릴리스를 완료 처리하지 않습니다.

## 현재 구현 버전

- 통합 리포트 강화: `6.0.0`
- 실행 경로: `artifacts/publish/CoreWatch-v6.0.0-win-x64/CoreWatch.exe`
- 설치 프로그램: `artifacts/CoreWatch-v6.0.0-Setup.exe`
