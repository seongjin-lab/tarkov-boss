# Tarkov Boss Monitor

**한국어** · [English](README.en.md) · [简体中文](README.zh-CN.md)

타르코프 공식 PvE를 로컬로 실행한 레이드에서 보스 출현을 확인하는 Windows x64 앱입니다.

## 주요 기능

- 레이드 로그에서 현재 맵 자동 감지
- 맵별 감시 보스 선택
- 게임 실행 중 설치 지원
- Windows 예약 작업으로 오래된 게임 로그 안전하게 정리

## 다운로드

[최신 버전 설치 파일 받기](https://github.com/seongjin-lab/tarkov-boss/releases/latest)

설치 파일은 코드 서명되지 않아 Windows SmartScreen 경고가 표시될 수 있습니다.

## 화면

<img src="screenshot.png" alt="Tarkov Boss Monitor 보스 스폰 확인 화면" width="600">

## 런처 복구 경고

상세 AI 스폰 로그를 받기 위해 `Logging.config`의 `aiData` 로그 수준을 변경하므로, BSG Launcher가 이 파일을 변조된 파일로 판단해 **게임 파일 복구 필요** 경고를 표시할 수 있습니다.

경고가 나타나면 런처에서 복구를 완료한 뒤 게임을 완전히 종료하고 Tarkov Boss Monitor를 다시 실행해 로그 설정을 재적용한 다음 게임을 다시 시작하세요. 게임 업데이트나 파일 무결성 검사로 `Logging.config`가 원본으로 복구된 경우에도 같은 절차가 필요합니다.

## 제거

타르코프를 완전히 종료한 뒤 시작 메뉴의 **Tarkov Boss Monitor 제거**를 실행하거나, Windows **설정 → 앱 → 설치된 앱**에서 Tarkov Boss Monitor를 제거합니다.

제거 프로그램은 로그 정리 예약 작업을 삭제하고, 이 프로그램이 변경한 `Logging.config`의 로그 수준을 설치 전 값으로 자동 복원합니다. 게임이 실행 중이거나 설치 후 로그 설정이 변경된 경우에는 안전을 위해 자동 복원하지 않으며, 제거 결과에 백업 위치를 안내합니다.

## 지원 범위

- 지원: 로컬로 실행되며 상세 AI 로그를 기록하는 공식 PvE 레이드
- 미지원: PvP, 온라인 PvE, 로컬 실행을 지원하지 않는 맵
- Streets of Tarkov(스오타)처럼 로컬 플레이가 불가능한 맵은 지원하지 않습니다.
- 현재 위치와 생존 여부 판정은 보장하지 않습니다.

## 직접 빌드

Windows 10 이상, .NET 8 SDK, Inno Setup 6가 필요합니다.

```powershell
.\build.ps1
```

설치 파일은 `outputs/installer`에 생성됩니다.

## 라이선스

이 프로젝트는 [MIT 라이선스](LICENSE)를 따릅니다.

Battlestate Games 및 Escape from Tarkov와 관련 없는 비공식 도구입니다.
