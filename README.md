# Waffle-Refresh 🧇

- 전원 상태에 따라 노트북 디스플레이 주사율을 자동 전환하는 초간단 트레이 앱
- AC(전원) 연결 시 165Hz, 배터리 시 60Hz로 자동 전환 (기본값)
- Windows 전용 / 단일 실행 파일 배포

## 주요 기능

- 전원 상태 감지(AC/배터리) → 주사율 자동 전환
- 작업 표시줄 트레이 아이콘과 메뉴 제공
- 로그 기록: `%LOCALAPPDATA%/Waffle-Refresh/prs.log`

## 다운로드/실행

- 단일 EXE 위치
  - `bin/Release/net8.0-windows/win-x64/publish/Waffle-Refresh.exe`
- 다른 PC에 복사해도 실행 가능(.NET 런타임 포함)

## 환경/호환성

- Windows 11 환경에서 정상 동작 확인
- **Lenovo ThinkPad P1 Gen 5** 165Hz 디스플레이에서 정상 작동 확인

## 빌드 방법

사전 준비: .NET SDK 8 이상

```powershell
# 프로젝트 루트에서 실행
cd .

dotnet restore
# 단일 EXE(런타임 포함)로 퍼블리시
 dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 사용 방법

- 실행 후 트레이 아이콘이 생성됩니다.
- 트레이 메뉴
  - 자동 전환 사용: 켜기/끄기 토글
  - 지금 적용: 현재 전원 상태에 맞춰 즉시 주사율 적용
  - 종료
- 실제 주사율은 Windows 설정 → 시스템 → 디스플레이 → 고급 디스플레이에서 확인

## 기본 동작 범위

- 기본(Primary) 디스플레이에만 적용됩니다.
  - 일반적으로 노트북 내장 패널이 기본이면 내장만 변경
  - 외부 모니터를 기본으로 설정한 경우 외부에 적용
  - 복제 모드에서는 Windows가 공통 주사율을 강제할 수 있음

## 커스터마이즈

- 주사율 값 변경(예: 전원 144Hz / 배터리 60Hz)
- 특정 디스플레이만 대상으로 지정(예: 내장 패널 고정)
- 폴링 주기 변경(기본 10초)
- 알림/아이콘 커스터마이즈

## 자동 시작(선택)

- Windows 시작 시 자동 실행하려면, 다음 중 택1:
  - 시작폴더에 바로가기 추가: Win + R → `shell:startup` → 폴더에 `Waffle-Refresh.exe` 바로가기 붙여넣기
  - 레지스트리 Run 키 사용(HKCU\Software\Microsoft\Windows\CurrentVersion\Run)

## 문제 해결

- 아이콘/파일 잠김으로 빌드 실패: 실행 중인 `Waffle-Refresh.exe` 종료 후 다시 빌드
- 주사율이 적용되지 않음: 해당 해상도에서 원하는 주사율이 지원되는지 확인(고급 디스플레이 설정)
- Windows 11 동적 주사율(DRR)이 개입할 수 있음 → 필요시 끄기 권장

## 라이선스

- MIT License. 자세한 내용은 `LICENSE` 파일을 참고하세요.
