# Rhythm Dojo

Unity로 개발 중인 4레인 리듬 게임. 도장 테마의 화면과 게임 내 채보 에디터를 함께 만들고 있습니다.

## 실행

- Unity **6000.3.23f1** 사용. 주요 패키지는 URP 17.3.0, Input System 1.20.0, Test Framework 1.6.0입니다.
- 처음 저장소를 받았다면 [Git·Unity 협업 설정](Docs/git-unity-setup.md)을 참고하세요.
- Project 창에서 `Assets/Game/Scenes/Title.unity`를 열고 Play Mode를 시작합니다. 중앙 현판을 누르면 플레이·설정·종료 메뉴가 열립니다.
- 곡 선택부터 확인하려면 `Bootstrap.unity`를 엽니다. 다른 씬에서 바로 실행할 때는 **Game Tools > Scenes > Start Play Mode From Title**을 끕니다.

| 화면 | 조작 |
| --- | --- |
| 곡 선택 | 마우스 또는 방향키로 곡·채보 선택, Enter 또는 플레이 버튼으로 진입 |
| 게임 | Space로 시작·재시작, D / F / J / K로 노트 입력 |
| 게임에서 나가기 | Esc로 곡 선택 복귀 |

## 현재 구현

- **게임플레이**: 탭·롱노트, 콤보·점수·결과 표시, BPM 변화, 재시작과 포커스 상실 처리.
- **플레이 규칙**: Warmup / Sparring 판정, Gauge / Challenge 모드, 롱노트 끝 자동·릴리스 판정. 채보 난이도와 판정 설정은 별개입니다.
- **곡 선택**: Easy / Standard / Hard 채보 선택, 정렬, 즐겨찾기, 저장한 곡 탐색, 기록 저장. 없는 채보 슬롯은 선택할 수 없습니다.
- **설정**: 타이틀·곡 선택 위에서 열리는 팝업. 음량, 해상도·화면 비율, FPS 제한, 노트 속도, 입력 타이밍 보정, DSP 버퍼를 조절합니다. DSP 버퍼 선택은 실행 중에만 유지됩니다.
- **채보 에디터**: 탭·롱노트 편집, 난이도별 채보, 실행 취소·다시 실행, 저장·불러오기, 테스트 플레이와 편집 화면 복귀.
- **음원·그리드 편집**: WAV / OGG / MP3 가져오기, 전체·상세 RGB 파형, 미리듣기, 노트 스냅, BPM·박자표·첫 박 위치 편집.

채보는 음원과 함께 로컬에 저장하며 현재 JSON 형식은 **v3**입니다. v2는 일반 불러오기, v1은 별도 가져오기를 지원합니다.

기본 콘텐츠는 Foundation Pulse, Tempo Shift, Demo Melody 등 테스트·데모 곡 중심입니다. 채보 에디터의 음원 가져오기는 파일 전체 경로를 입력하는 방식이며, 파일 선택 대화상자는 아직 없습니다. 긴 곡의 성능 최적화와 실제 입력·오디오 지연 측정도 남아 있습니다.

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

검증 메뉴는 **Game Tools > Verification**에 있습니다. 세부 실행 방법과 결과 경로는 [검증 안내](Docs/verification.md)를 참고하세요.

## 관련 문서

- [팀 담당 영역과 UI 작업 방법](Docs/team-ownership.md)
- [채보 에디터와 저장 형식](Docs/chart-format-v1.md) · [v3 JSON Schema](Docs/rdchart-v3.schema.json)
- [파형·GRID 사용법](Docs/editor-audio-timeline.md)
- [설정 팝업](Docs/settings-popup.md)
- [타이틀 화면](Docs/pixel-dojo-title.md)
