# 개발 안내

[소개](../README.md) · [실행·조작](getting-started.md) · [개발 안내](development.md)

## 개발할 때

화면 배치는 `Assets/Game/Scripts/Editor/UILayout`의 빌더에서 수정합니다. 씬에서 직접 바꾼 내용은 재생성할 때 덮어써집니다.

- **Game Tools > Scenes > Build All Scenes**: 전체 씬 재생성. 화면 하나만 수정했다면 해당 화면의 Build 명령을 사용합니다.
- **Game Tools > Content > Prepare Default and Verification Content**: 기본 리소스 준비와 검증용 콘텐츠 재생성. 일반 UI 수정에는 필요하지 않습니다.
- 채보 에디터는 `Assets/Game/Scenes/ChartEditor.unity`에서 실행합니다.

| 경로 | 내용 |
| --- | --- |
| `Assets/Game/Scripts/Domain` | 판정·채보·타이밍 규칙 |
| `Assets/Game/Scripts/Application` | 게임 진행과 편집 로직, 서비스 계약 |
| `Assets/Game/Scripts/Runtime` | Unity 연결, 화면, 오디오·입력, 채보 에디터 |
| `Assets/Game/Scripts/Editor` | 씬·콘텐츠 생성과 검증 도구 |
| `Assets/Game/Tests` | Domain / EditMode / PlayMode 테스트 |
| `Docs` | 협업 규칙과 기능별 문서 |
| `Logs`, `Builds` | 검증 결과와 빌드 출력 |

Domain과 Application에는 Unity API를 사용하지 않습니다. 에셋을 옮길 때는 `.meta`도 함께 옮깁니다.

## 검증

Unity가 열린 상태에서는 저장소 루트에서 Unity CLI로 관련 테스트를 실행합니다. 코드 수정 후에는 먼저 **Assets > Refresh**로 컴파일을 마칩니다.

```powershell
unity status --json
unity command run_tests --mode playmode --filter SelectionBrowserTests --async_tests true --json
unity command test_status --json
```

`test_status`를 반복 호출해 완료 결과와 실패 항목을 확인합니다. EditMode는 `--mode editor`를 사용합니다. 테스트는 한 번에 하나씩 실행하고, 다음 실행 전 Play Mode 종료와 `unity status`의 준비 상태를 확인합니다.

- 일반 수정은 관련 테스트만 실행합니다. 문서만 수정했다면 diff를 검토합니다.
- 씬 구성·공통 화면 전환을 바꾸면 관련 테스트와 **Verify Smoke Actions**를 실행합니다.
- 여러 기능의 공통 계약을 바꾸면 **Verify All Tests (No Build)**를 실행합니다.
- 빌드 관련 변경이나 전체 납품 검증에는 **Verify Foundation (Play Mode and Player Build)**를 사용합니다. 출력은 `Builds/Windows/RhythmDojo.exe`입니다.

검증 메뉴는 **Game Tools > Verification**에 있습니다. 세부 실행 방법과 결과 경로는 [검증 안내](verification.md)를 참고하세요.

## 관련 문서

- [팀 담당 영역과 UI 작업 방법](team-ownership.md)
- [채보 에디터와 저장 형식](chart-format-v1.md) · [v3 JSON Schema](rdchart-v3.schema.json)
- [파형·GRID 사용법](editor-audio-timeline.md)
- [설정 팝업](settings-popup.md)
- [타이틀 화면](pixel-dojo-title.md)
