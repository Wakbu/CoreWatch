# CoreWatch publish 보존 규칙

## 필수 규칙

- 기능 구현이 완료되면 새 버전 번호로 Release publish를 생성합니다.
- 설치하지 않고 바로 실행할 수 있도록 `artifacts/publish/CoreWatch-v<버전>-win-x64` 폴더를 삭제하지 않고 유지합니다.
- 직접 실행 파일은 위 폴더의 `CoreWatch.exe`입니다.
- 기존 버전별 publish 폴더도 사용자 요청 없이 삭제하거나 덮어쓰지 않습니다.
- publish 후에는 해당 폴더의 `CoreWatch.exe`를 직접 실행해 시작 직후 종료·미응답 여부를 확인합니다.
- 완료 보고에 publish 폴더, 실행 파일, 전체 파일 수·크기와 대표 실행 파일의 SHA-256을 기록합니다.

## 현재 구현 버전

- 커뮤니티 벤치마크 비교 기반: `5.8.0`
- 실행 경로: `artifacts/publish/CoreWatch-v5.8.0-win-x64/CoreWatch.exe`
