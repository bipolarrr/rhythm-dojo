# 인게임 시각 리디자인 조사·구현 설계

> **최종 플레이 화면 — 2026-11-18**. 네 배경 위에 옻칠 목재 레인, 상아·옥 타격면, 붓획 충격, 한글 판정과 상단 연격 숫자를 적용했다. [고밀도 플레이 영상](verification-images/strike-patterns.mp4) · [적용 범위와 검증 결과](../Logs/strike-interface-verification.md)

> **구현 현황 — 2026-11-18:** `emergency`에서 도장 수련실 → 산 오르기 → 질주 → 목인 수련의 네 배경 페이즈를 구현했다. 아래 조사·제안은 작업 전 기록이다. 실제 차이는 문서 끝의 구현 반영과 [Phase 1 검증](../Logs/dojo-room-verification.md), [Phase 2~4 검증](../Logs/training-journey-verification.md)을 따른다. 레인·노트·HUD 및 다음 UI/이펙트 리디자인은 이번 작업에 포함하지 않았다.

작성일: 2026-11-18. 조사 기준: `emergency`, HEAD `24748c1` 및 현재 작업 트리. **설계만 작성했으며 게임 코드·에셋·씬은 변경하지 않았다.** 요청한 `docs/ingame-visual-plan.md`는 이 Windows 작업 공간의 기존 디렉터리 표기인 `Docs/ingame-visual-plan.md`에 저장한다.

조사 시작 시 Gameplay·Title 등 씬, 판정 조정자, 훈련·대화 관련 파일에 기존 수정/미추적 파일이 있었다. 본 문서는 이를 포함한 디스크의 실제 구현을 기준으로 한다. HEAD만의 상태나 현재 Unity 메모리 안의 미저장 상태와 동일하다고 가정하지 않는다. 소스, 씬 YAML의 스크립트/에셋 GUID, 이미지 원본, 기존 캡처를 대조했다. Editor 실행·씬 재생성·실시간 성능 측정은 하지 않았다. `Logs/ingame-playing.png`는 구도 참고용 기존 캡처이며 오늘 실행한 결과가 아니다.

## 1. 핵심 결정과 범위

- 완성형은 **도장 수련실 → 산 오르기 → 질주 → 목인 수련**, 정확히 네 페이즈다. 모든 배경은 수련자의 눈높이인 1인칭이다.
- 배경은 독립 오브젝트를 합성하는 **2.5D 도트 공간**으로 제작한다. 깊이별 투영·가림·개별 애니메이션을 사용하고, 기존 레인 카메라/투영과 분리한다. 단일 그림 확대나 전체 화면 흔들기로 공간 이동을 대체하지 않는다.
- 음악 시계가 페이즈를 선택하고, 판정 사실이 페이즈 안의 성취를 결정한다. 실패해도 시간표는 멈추지 않는다. 단, 기존 규칙이 GameOver로 게임 자체를 종료하면 배경도 종료한다.
- Perfect와 Good은 모두 정상 판정이다. 진행량은 동일하고 반응의 힘만 다르다. Miss·오입력·롱노트 중도 이탈은 진행을 보상하지 않는다. 누적 성취를 실수 때문에 되돌리지 않는다.
- 한 노트의 총 배경 기여량은 1로 정규화한다. 롱노트는 이 1을 시작·유지·끝에 나누고, 페이즈별 실제 기회량을 분모로 삼는다. 점수/체력/현재 콤보를 진행도 대신 사용하지 않는다.
- **새 진행 게이지·퍼센트·페이즈 숫자 UI는 없다.** 문틈, 고도, 바람과 목재 손상으로 읽힌다. 기존 숨김 상태의 진행 UI도 되살리지 않는다.
- 첫 제작 단계는 Phase 1의 아트·공간·반응을 독립 검증하는 데 한정한다. Phase 2~4 제작, HUD 재배치, 노트 스킨 교체는 그 검증 뒤의 별도 작업이다. 이 문서 작성 후 자동 구현하지 않는다.

## 2. 실제 구현 조사

`Assets/`, `Docs/`, `Logs/`, `LocalAudio/`로 시작하는 경로는 저장소 루트 기준이다. `Scripts/`, `Art/`, `Fonts/`, `Settings/`, `Tests/`는 `Assets/Game/` 아래 경로의 축약이며, `Domain/`, `Application/`, `Runtime/`, `Editor/`는 `Assets/Game/Scripts/` 아래 경로의 축약이다. 같은 문단에서 파일명만 쓸 때는 앞서 명시한 디렉터리를 따른다. 기존 타입과 아래에서 제안하는 신규 타입을 구분한다.

### 2.1 실제 플레이 경로와 씬 구성

| 영역 | 확인한 파일과 실제 동작 | 설계에 미치는 영향 |
| --- | --- | --- |
| 진입 | `Scripts/Runtime/Core/MainMenuCompositionRoot.cs`, `MainMenuActions.cs`, `AppFlowController.cs`; `Assets/Game/Scenes/Title.unity`, `ModeSelection.unity`, `SongSelection.unity` | Title의 문 진입과 ModeSelection의 방 선택을 거쳐 곡 선택으로 이동한다. 자유 수련/도전은 같은 Gameplay를 사용한다. 메뉴의 네 방과 곡 안의 네 페이즈는 별개 개념이다. |
| 직접 진입 | `Scripts/Runtime/Core/Bootstrap.cs`의 `Start()`는 `ShowSelection()` 호출. `GameplayCompositionRoot.Start()`는 초기화되지 않았을 때 기본 카탈로그 곡으로 초기화 | Bootstrap을 구형 인게임으로 오인하지 않는다. 직접 Gameplay 실행도 조립 루트의 기본 곡 경로로 검증한다. |
| 실제 조립 | `Scripts/Editor/SceneGeneration/ScenePresentationBuilder.cs`의 `Gameplay()`, `SceneDependencyAssembler.cs`, `Scripts/Runtime/Core/GameplayCompositionRoot.cs` | `RhythmGameController`, `SongClock`, `LaneKeyboardInput`, `NotePresenter`, `PlayfieldPresenter`, HUD/UI/타격 피드백을 조립한다. `Gameplay.unity`의 GUID 참조에서도 이 구성과 `TrainingPlaybackView`를 확인했다. |
| 씬 로딩 | `AppFlowController.SceneLoaded()`, `Load()` | Gameplay는 Single 로드다. 이전 메뉴 배경을 계속 쓰는 구조가 아니다. `PreparedSelectionScene`은 곡 선택 사전 준비용이다. 네 페이즈마다 씬 로딩을 추가하지 않는다. |
| 에디터 테스트 | `Scripts/Runtime/Authoring/WorkspacePlaytest.cs`, `AppFlowController.cs` | 전체 게임 테스트는 같은 Gameplay로 들어간다. 현재 위치 테스트는 앞 노트를 제외한 임시 채보를 사용할 수 있어 원곡 전체 플레이와 분리해야 한다. |
| 훈련 | 현재 작업 트리의 `Application/TrainingPlayback.cs`, `TrainingSession.cs`, `Runtime/UI/TrainingPlaybackView.cs`, `GameplayCompositionRoot.cs` | 시범 자동 입력, 연습/평가, 훈련 결과 복귀가 같은 Gameplay를 사용한다. 최초 도입은 일반 곡 플레이 대상으로 하고 훈련의 기존 교육 연출을 덮지 않는다. |

현재 월드 카메라는 `ScenePresentationBuilder.Gameplay()`에서 위치 `(0, 10, -10)`, 주시점 `(0, 0, 9)`, FOV 52, far 180으로 만든다. Directional Light 강도는 1.3이며 별도 `Gameplay Display Camera`는 cullingMask 0으로 Overlay Canvas 표시를 유지한다. 배경을 이동시키려고 이 월드 카메라를 움직이면 노트와 판정 위치도 바뀐다.

`Scripts/Editor/UILayout/PixelGameplayStyle.cs`는 sortingOrder -100의 `Pixel Dojo Gameplay Backdrop` Canvas, `Dojo Scenery`, `Training Floor Frame`, `Gameplay Viewport`를 만든다. `Scripts/Runtime/UI/Views/GameplayViewport.cs`는 전체 화면 크기의 ARGB32 RenderTexture를 생성하고 `RawImage.uvRect`로 플레이 창 부분을 표시한다. 렌더 텍스처는 화면 크기가 달라질 때 재생성하며 Bilinear 필터를 사용한다. **현재 배경은 2D, 레인·노트는 별도 3D 투영을 합성한 구조**다.

### 2.2 배경·레인·노트·이펙트·HUD

| 대상 | 실제 근거 | 현재 상태 |
| --- | --- | --- |
| 배경 | `Editor/UILayout/IngamePresentationAssets.cs`, `Art/DojoGameplay/TrainingHall.png` | 단일 정면 도장 이미지, Point 필터·mipmap 없음·비압축. 교체 대상이다. |
| 공간 보조 | `Editor/UILayout/GameplayAtmosphereBuilder.cs`, `Runtime/UI/Views/GameplayAtmosphere.cs`, `GameplayWindowLight.cs` | 중앙/좌우 먹칠, 창빛·먼지, 전경/원경 바닥 먹 레이어. `React()`는 Miss 무시, 프레임당 성공 충격 한 번, Perfect/Good 강도 차등. `Update()`가 unscaledDeltaTime으로 움직여 일시정지와 자동으로 함께 정지하지 않는다. |
| 레인 | `Runtime/Presentation/PlayfieldPresenter.cs`, `Runtime/Gameplay/GameModeDefinition.cs`, `GameplayPresentationSettings.cs` | 모드 레인 수에 맞춰 큐브로 바닥·레일·판정선·마디선을 구성한다. 투명도용 개별 재질이 있으며 마디선 풀 상한은 128이다. 모드·스크롤 기반의 투영을 배경 진행과 연결하면 안 된다. |
| 노트 | `Runtime/Presentation/NotePresenter.cs`, `NoteView.cs`; `Assets/Game/Prefabs/TapNote.prefab`, `HoldNote.prefab` | 채보 수에 따라 템플릿 인스턴스를 준비하고 `IScrollTimeline.DistanceBetween`으로 매 프레임 표시한다. 머리는 `Assets/Game/Resources/UI/SongpanNote.png` Sprite, 롱노트는 몸통/끝 마커도 표시한다. `Editor/SceneGeneration/PresentationAssetBuilder.cs`가 고정 구조를 만든다. |
| 타격 | `Runtime/Presentation/GameplayHitFeedback.cs`, `Editor/SceneGeneration/GameplayFeedbackBuilder.cs`, `Runtime/UI/Views/GameplayImpactGraphic.cs` | 직렬화된 24개 burst 재사용, 모드별 hold glow, 레인별 최근 burst 교체. 판정 위치를 카메라에서 HUD로 투영한다. raw `Pressed`는 헛스윙 소리에도 사용하며 이는 성공 판정이 아니다. |
| 판정 표시 | `Runtime/UI/RhythmHud.cs`, `RhythmHudFormatter.cs`, `Runtime/UI/Views/GameplayJudgmentMotion.cs` | 이벤트 기반 붓 판정/타이밍 표시. Domain의 표시 문자열은 Perfect / Hmm... / Nope.이며 각각 Perfect / Good / Miss다. |
| HUD | `Editor/UILayout/GameplayHudLayoutBuilder.cs`의 `ApplyIngameLayout()`, `GameplayAtmosphereBuilder.RefineHud()`, `Runtime/UI/Views/GameplayHudView.cs` | 좌측 자켓/곡명, 레인 옆 세로 체력, 하단 판정, 시작 키 안내, 일시정지. 콤보·BEST·세부 판정 기록·처리 노트 진행바·집중 HUD 버튼은 참조가 남지만 비활성화한다. 과거 Build() 중간 배치만 보고 현재 HUD라고 설명하면 틀린다. |

현재 캡처에서도 원근 레인 위 노트와 좌측 큰 목재 자켓 패널, 우측 세로 체력이 확인된다. 최종 리디자인에서 이 목재 패널의 크기·레인 표면·이펙트 형태까지 개선하되, Phase 1의 검증 기준 화면에는 현재 HUD와 실제 레인을 남겨 가림 문제를 먼저 평가한다.

### 2.3 판정·콤보·실수의 실제 흐름

```text
LaneKeyboardInput의 Pressed / Released (Input System 시각)
  → RhythmSessionCoordinator.Press / Release
  → GameplayTiming / SongClock.InputTimeToSongTime (입력 보정)
  → RhythmSession → rules가 있으면 RuleSession
  → Judged / HoldStarted / Failed
  → Coordinator → IGameplayReadModel → HUD / HitFeedback / TrackCombo
```

근거: `Runtime/Input/LaneKeyboardInput.cs`, `Application/RhythmSessionCoordinator.cs`, `GameplayTiming.cs`, `GameplayContracts.cs`, `Domain/RhythmSession.cs`, `RuleSession.cs`, `PlayRules.cs`, `SessionSnapshot.cs`, `NoteJudgmentRecord.cs`.

- 실제 등급은 `Judgment.Perfect`, `Good`, `Miss` 세 개다. `PlayRules` 기본 Warmup 판정창은 ±50/100ms, Sparring은 ±16/50ms다. 설정으로 달라질 수 있으므로 새 배경 코드에 수치를 하드코딩하지 않는다.
- 일반 플레이는 `PlayRequest.Rules`를 복사해 전달하며 `RhythmSession`이 `RuleSession`을 사용한다. 성공은 콤보 +1, Miss는 0. 현재의 TotalNotes는 이 경로에서는 원본 `ChartData.Count`가 아니라 **판정 체크 수**다.
- 탭은 머리 한 번. 롱노트는 머리, `HoldCheckBeats` 간격의 유지 체크, 끝으로 구성된다. 기본 유지 간격은 0.25박이며 `TempoMap.BeatAtTime/TimeAtBeat`를 사용한다. Automatic 끝은 유지 시 Perfect, Release 끝은 타이밍 판정이다. 머리는 `Judged` 뒤 `HoldStarted`도 발행하므로 둘을 합산하면 중복된다.
- `RuleSession.ReleaseLane()`에서 Automatic 롱노트를 일찍 놓으면 체크 사이에서도 Combo만 0이 될 수 있다. 다음 체크 전에 다시 누르면 Miss 수만으로 이 중도 이탈을 발견하지 못할 수 있다.
- 빈 레인/판정창 밖 `PressLane`은 적중 노트를 찾지 못하면 판정을 만들지 않는다. **현재 독립적인 WrongInput 이벤트나 카운터는 없다.** raw 입력에 스윙 소리가 난다는 것과 판정 성공/실패는 다르다.
- `JudgmentEvent`에는 NoteIndex, Lane, Kind, Grade, ErrorSeconds만 있다. **머리/중간/끝 구분·체크 ID·체크의 예정 시각은 없다.** `NoteJudgmentRecord`도 Start/End만 보관한다. 중간 체크를 사후 결과에서 복원하거나 이벤트 수만으로 구별하면 안 된다.
- rules가 없는 호환 경로는 같은 `RhythmSession.cs` 안에 남아 있다. 롱노트 시작은 `HoldStarted`만, 최종 Resolve는 시작/끝 중 나쁜 판정을 반영하는 방식이며 중간 체크가 없다. 구형이라고 삭제할 대상은 아니며 테스트/기존 생성자 사용자를 보존해야 한다.
- 체력 0 또는 Challenge 허용 횟수 초과 시 `Failed`가 발생한다. Gauge Continue는 실패 기록을 남기고 계속 진행, Stop/Challenge는 GameOver. Good도 특정 Challenge에서는 게임 종료를 일으킬 수 있지만 **배경 판정 분류에서는 정상**이다. 종료가 발생하면 새 성공 연출보다 게임 종료 처리가 우선한다.

### 2.4 재생 시간·종료·일시정지·결과

`Runtime/Content/PlayableSong.cs`는 Chart, Tempo, AudioOffset, AudioClip, Mode 및 음원 lease/복구 상태를 가진다. `Domain/ChartData.cs`의 노트는 시작/끝 시각·레인·종류를 제공한다. 저장 형식은 `Docs/chart-format-v1.md`라는 파일명과 달리 현재 v3를 포함한다. 배경 설정 때문에 채보 JSON 형식을 바꾸지 않는다.

`Runtime/Audio/SongClock.cs`는 DSP 예약 시작과 실시간 보간을 사용한다. 재생 중 `SongTime = InterpolatedDspTime - ScheduledStartDspTime + AudioOffset`이다. 입력 보정은 입력/판정용 논리 시각에 적용하며 배경의 음악 시각에 다시 적용하면 안 된다.

`GameplayCompositionRoot.Initialize()`가 실제 종료 시각을 조정한다. `lastEnd`는 모든 노트 EndTime의 최대, `F = lastEnd + GoodWindow + 2 × TimingTolerance`이고 tolerance는 1e-9다.

```text
C = max(F, min(max(원래 Chart.CompletionTime, AudioClip.length + AudioOffset), F + 1초))
실제로 controller에 전달하는 chart = chart.WithCompletionTime(C)
```

`RuleSession`은 논리 시각이 C 이상이며 모든 체크가 해결되었을 때 Completed가 된다. Coordinator는 완료 시 시계를 Stop하고 GameOver에서는 Pause/Stop한다. 따라서 원래 음원 길이나 JSON의 종료 시각만으로 네 페이즈를 나누면 실제 완료와 어긋난다.

진입은 음원 준비와 씬 전환 완료 후 키 안내 2초를 표시하고 세션을 시작한다. Esc/포커스 상실은 `Runtime/UI/GameplayPauseMenu.cs`를 통해 일시정지하고, 복구 후 3초 카운트다운 및 예약 Resume를 사용한다. 기존 Coordinator의 포커스 리셋 경로도 호환용으로 남아 있다.

`GameplayCompositionRoot.LateUpdate()`는 Completed/GameOver를 확인하고 실제 시간 1초 뒤 결과로 이동한다. `SessionSnapshot.IsFullCombo`이면 2초다. 이 값은 Completed, 실패 없음, `Combo == TotalNotes > 0` 조건이며 **오입력을 검사하지 않는다.** 재시작은 예약 결과 이동을 취소한다. `AppFlowController.ShowResults()`는 일반 Results로, 훈련은 `ShowTrainingResults()`로, 에디터 테스트는 해당 복귀 계약으로 처리한다. 새 목인 피니시가 이 소유권을 가져가지 않는다.

### 2.5 최신 도장 아트와 생성·로딩·애니메이션

| 실제 파일 | 현재 사용과 유지할 정체성 |
| --- | --- |
| `Art/DojoRenewal/HanokEntrance.png`, `HanokDoors.png`, `PracticeHall.png` | 마모된 갈색 보, 밝은 한지 창호, 청회색 기와, 돌 기단, 소나무. 원본을 직접 확인했다. 문을 좌우로 분리한 자산과 문틀 마스크를 사용한다. |
| `Art/TitleSky/LandscapeSource.png`, `SkySource.png`, `Cloud.png`, `SwallowSheet.png` | 최신 Title은 단순 대문 정지 화면에 그치지 않는다. `Editor/UILayout/TitleSkyLayoutBuilder.cs`가 하늘과 대문 연결을 베이크하고 `Runtime/UI/Views/TitleSkyView.cs`가 하강·구름·새를 담당한다. Title 씬에 SkyEntrance/SkyUpper 참조도 확인했다. |
| `Art/DojoRenewal/LessonRoom.png`, `PracticeHall.png`, `ScribingRoom.png`, `Art/AlphaDojo/Summit.png` | `MainMenuLayoutBuilder → AlphaDojoLayoutBuilder.Apply()`가 현재 네 모드 방을 구성한다. 기존 타이틀/문 이미지보다 이 최종 덮어쓰기와 방 구성을 기준으로 삼는다. |
| `Art/PracticeSelection/WoodenDummyRotation.png`, `WoodenDummyAtlas.png`, `StrikePoses.png` | 자유 수련의 곡 선택은 `PracticeSelectionLayoutBuilder`가 PracticeHall 안에 목인과 동작을 구성한다. `WoodenDummyRig`는 고정 밑동 + 8방향 몸통/곡 자켓 표시를 사용한다. 메뉴 목인은 1인칭 근접 파괴용 자산이 아니다. |
| `Art/DojoRenewal/PreparationRoom.png`, `RecordAlcove.png`; `Editor/UILayout/DojoScreenStyle.cs` | 설정·결과의 목재/종이/먹과 붓글씨 컨트롤 언어. `Fonts/Dokdo-Regular.ttf`를 포함한 현재 서체 체계를 유지한다. |
| `Art/AlphaDojo/Training/*.anim`, `*.controller`; `Runtime/UI/Views/DojoActorAnimation.cs` | 제작된 Sprite 프레임/AnimationClip을 Animator의 정규화 시각으로 샘플링한다. Runtime에서 원본 이미지 분리/변환을 하지 않는 패턴을 따른다. |

`Editor/UILayout/UiArtworkAssets.cs`가 에디터에서 full/alpha crop, Point·Clamp·mipmap 없음·비압축 import, Sprite `.asset` 생성을 담당한다. `DojoRenewalLayoutBuilder.Slice()`의 문짝 분리나 `TitleSkyLayoutBuilder`의 고정 합성도 베이크 작업이다. 게임 실행에서는 저장된 참조를 이용한다. `Runtime/Audio/DojoSoundPlayer.cs`/`DojoSoundBank.cs`는 미리 준비된 효과음 풀을 사용한다. 배경을 위해 매 타격 음원을 로드하지 않는다.

### 2.6 구형 흔적과 활성 경로 구분

- `Art/PixelDojo/*`, `Art/DojoEntrance/TrainingInterior.png`, `DojoGameplay/GameplayScroll.png` 등의 존재만으로 현재 배경이라고 판단하지 않는다. 일부는 호환 참조·다른 화면·초기 빌더 구성에 남아 있다. 활성 Gameplay 배경 참조는 TrainingHall이다.
- `Runtime/Gameplay/RhythmGameConfig.cs`와 `Settings/RhythmGameConfig.asset`는 `DefaultSettingsBuilder`/`TestContentBuilder`의 마이그레이션 입력이다. 현재 게임 조립은 GameSettings의 세분화된 설정과 PlayRequest를 사용한다.
- `Runtime/Authoring/ChartGameplayPreview.cs`는 레이어 31, 별도 카메라/RenderTexture를 쓰는 편집기 내 미리보기다. 실제 곡 플레이의 별도 구형 Gameplay 씬이 아니다. 공유 노트 프리팹 변경 시 영향을 받는다.
- 옛 통계 HUD는 필드/오브젝트가 남아도 비활성이다. 현재 장면과 구형 화면을 하나로 혼합해 새 설계를 잡지 않는다.
- 조사한 현재 트리에서 별도의 활성 ‘구형 인게임 루트’를 확인하지 못했다. 위 호환 코드, 비활성 UI, 편집기 미리보기를 구분해 보존한다. 이번 조사로 저장소 전체 미사용 에셋 삭제를 승인하거나 완전한 미사용 판정을 내리지는 않는다.

## 3. 전체 아트 디렉션

목표 장면은 **도장의 안쪽에서 문을 열고 산을 거쳐 돌아오는 수련자의 시선**이다. 사용자가 제시한 유비트/응원단 모티브에서는 ‘성과가 공간을 열고 환경이 상황에 응답한다’는 철학만 취한다. 특정 작품의 화면·캐릭터·패널·연출 시퀀스를 복제하지 않는다.

### 재질·색·해상도

- 큰 구조는 한옥 도장의 수평 보, 가는 창살, 낮은 돌 기단과 거친 마루. 종이는 따뜻하되 회백색, 목재는 짙은 적갈색, 원경은 저채도 청회/쑥빛이다. 붉은색은 실패의 짧은 국소 반응에만 쓴다.
- 출발 팔레트 제안: 먹 `#18201E`, 목재 `#594032`, 밝은 나뭇결 `#967050`, 한지 `#C9BA95`, 산안개 `#758985`, 볕 `#E5C993`. 기존 에셋에서 그대로 추출한 값이 아니라 새 아트의 비교용 출발점이다. 최종값은 PracticeHall과 나란히 검수한다.
- 16~32비트 느낌은 큰 픽셀 격자를 덮는 필터가 아니라 형태에 맞춘 픽셀 군집, 선택적인 1~2픽셀 하이라이트, 계단형 그림자와 적은 명암 단계로 만든다. 한 재질에 무작위 노이즈를 뿌리지 않는다.
- 배경 기준 제작 캔버스는 1280×720, 원근 이동 여백은 양옆/상하 각 10% 이상 별도 확보한다. 최종 출력 크기에서 기존 메뉴와 도트 밀도를 맞춘다. 레이어 간 texel 밀도를 통일하고 Point import를 사용한다. 배경 투영만 최종 픽셀에 맞추며 노트 위치·음악 시각은 스냅하지 않는다.
- 벚꽃·토리이·일본풍 문양, 애니메이션 캐릭터 배너, 네온 윤곽·전자 홀로그램·SF 선형 UI를 새 자산에 사용하지 않는다. 기존 `petals` 같은 필드명은 바꾸지 않되 신규 연출의 내용은 먼지/솔잎/안개다.

### 읽힘과 시선

현재 `.285~.715` 폭의 플레이 창을 기준 보호 영역으로 삼되 실제 레인 투영과 모드·화면비를 기준으로 마스크를 계산한다. 진입 노트 뒤 중앙은 저대비/저채도로 두고 밝은 문 가장자리·나뭇가지·목인 팔은 좌우 바깥에 배치한다. 판정선 주변에는 밝은 배경 섬광과 손이 들어오지 않는다. 문 개방은 중앙의 밝은 흰 사각형이 아니라 양쪽 창틀/바닥으로 번지는 빛으로 읽히게 한다.

배경 전경:중경:원경의 이동 크기 출발 비율은 1:0.35:0.08. 원경 산은 거의 고정하고 문틀/기둥이 빠르게 시야 밖으로 지나가게 한다. 배경의 전진감은 각 오브젝트의 깊이 투영으로 만든다. 시점 roll은 0, 반복적인 카메라 bob은 기본 끔. 배경 수직 변위도 기준 720p에서 상시 1px, 짧은 충격 최대 3px로 제한해 시작한다. 레인 카메라·판정선·HUD에는 적용하지 않는다.

콤보 유지는 고정 ‘100콤보 보상’ 대신 최근 정상 판정의 연속성으로 바람과 타격 잔향의 연결을 부드럽게 한다. 롱노트 체크가 많은 곡에서 밝기/속도가 폭증하지 않도록 단위 기여량으로 제한한다. 무노트 휴지부에는 콤보를 실패로 처리하지 않고 기본 호흡으로 가라앉힌다.

## 4. 네 페이즈의 장면 구성

아래 진행 값 p는 내부 설계값이며 화면에 표시하지 않는다. Phase 1~3의 절정 문턱은 초기안 `p ≥ 0.85`다. 시간만으로 절정을 해금하지 않는다. 조건을 못 채우면 미완의 장면을 유지하다가 정해진 시간에 다음 장면으로 넘어간다.

### Phase 1 — 닫힌 수련실에서 열린 문으로

실내에서 정면 출입문을 바라본다. 메뉴 대문의 외부 시점을 뒤집어 쓰지 않고 **실내 측 문짝·문틀을 새로 그린다**. 눈높이 수평선은 화면 상단 약 38%를 출발점으로 잡고, 실제 레인 보호 영역을 대조해 조정한다.

| 깊이/독립 요소 | 상시 움직임 | 성공·실수 반응 / 주요 전개 |
| --- | --- | --- |
| 먼 산·하늘·뜰 | 구름 12~20초 주기, 뜰의 작은 잎만 움직임 | 닫힌 문 뒤에도 독립 오브젝트로 존재. 개방 때 처음 드러나며 문과 함께 확대되지 않는다. |
| 문 안쪽 좌/우 문짝, 좌/우 틀, 문 위 보 | 닫힌 동안 문 자체는 고정 | p에 따라 좌우 문짝을 별도 이동, 한쪽이 40ms 늦게 따라가 무게를 표현. 성공 반동은 100~180ms로 작게, 개방량은 감소하지 않는다. |
| 측벽/창호/천 | 천 끝과 창 그림자만 느리게 변화 | 실수 때 천의 전진 바람이 0.25초 약해지지만 문을 다시 닫지 않는다. |
| 마루 원근 조각·문턱·좌우 기둥 | 거의 정지 | 0.4 이후 시점 전진을 시작. 가까운 기둥/문턱이 더 크게 움직이고 중경 문은 느리게 커진다. |
| 문틈 볕·바닥 빛 조각·먼지 | 노출된 빛 영역 안 먼지 6~12개 | 문 개방과 연동한 마스크/빛 폭. 성공 시 60ms 압축, 180ms 확산, 350ms 안정. 전면 밝기 상승은 하지 않는다. |

진행 흐름:

1. `p=0`: 닫힌 문, 한지의 약한 확산광, 조용한 실내 호흡. 입력이 없으면 문틈은 생기지 않는다.
2. `0<p<0.3`: 첫 성공부터 얇은 틈, 좌우 문짝의 무게감. 총 누적이 증가할 때만 개방 목표가 바뀐다.
3. `0.3≤p<0.65`: 문이 절반 가까이 열리고 바닥에 분리된 빛 조각이 길어진다. 바깥 나뭇가지/뜰/산의 서로 다른 깊이가 보인다.
4. `0.65≤p<0.85`: 문틀 접근, 빛은 측면과 바닥으로 확장. 시야의 중앙 밝기는 계속 억제한다.
5. `p≥0.85`: 이미 획득한 성과의 표현으로 완전 개방과 문턱 통과를 0.6~0.9초에 실행한다. 가까운 틀이 화면 밖으로 벌어지고 외부 원경은 남는다. 입력이 더 없어도 이 짧은 완결 애니메이션은 마칠 수 있지만 새로운 진행 점수를 주지는 않는다.

성과가 부족하면 안쪽에서 열린 만큼만 보며 끝난다. 다음 페이즈 시작 시에는 붓먹의 짧은 배경 가림으로 시간/장소의 생략을 보여주며, 문을 통과했다고 거짓 연출하지 않는다. 성공 시에는 외부 뜰→산길의 원근 방향을 이어준다.

### Phase 2 — 산을 오르는 수련

눈앞의 돌계단/흙길, 좌우 바위·소나무 뿌리, 중경 능선, 원경 산 3겹, 구름/낮은 안개로 구성한다. 플레이어 몸을 멀리서 보여주지 않는다.

- 상시: 가까운 풀 2~3초, 중경 가지 5~7초, 원경 구름 15초 이상으로 움직임의 주기를 분리한다.
- 성공: 한 발 디딤의 방향으로 가까운 바닥 조각이 아래쪽으로 지나간다. p가 증가할 때만 고도/도착 지형 목표가 올라간다. 짧은 발 디딤 모션은 180~280ms이고 화면 bob 대신 바닥·주변 사물로 무게를 전한다.
- 실수: 발 디딤 추진력/풀의 바람 반응만 잠시 약화. 이미 올라온 고도를 내리거나 산 아래로 리셋하지 않는다.
- 주요 전개: 낮은 수림→안개 낀 암릉→정상 직전 바위. `p≥0.85`면 전경 바위가 갈라져 시야를 내주고 산 아래 능선과 도장 지붕이 드러난다. 배경 시선만 최대 4도 내려다보는 구도를 쓰고 플레이 투영은 고정한다. 실패 시 정상 바로 아래/중턱에서 다음 장면으로 전환한다.

길 조각은 제작된 근/중/원경 구간을 투영하고 풀에서 재사용한다. 단일 산 그림의 y좌표 이동으로 상승을 표현하지 않는다.

### Phase 3 — 능선 길 질주

Phase 2의 산 재질과 연결되는 능선의 좁은 수련길. 원경 방향점, 구부러진 중경 길, 좌우 수목, 가까운 돌·풀·낮은 안개, 소매 가장자리를 별도로 둔다.

- 상시: 기본 주행 호흡과 잎 움직임. 무입력에도 음악 시간은 계속 흐르며 낮은 속도의 반복 지형은 움직일 수 있으나 성취 거리 p/주요 구간 해금은 증가하지 않는다.
- 성공: 누적 p로 더 열린 능선 구간을 해금하고, 별도 추진력 값 v에 제한된 충격을 준다. 중경 대비 전경 이동을 3배 이상 빠르게 해 속도를 표현한다. 콤보가 이어지면 바람과 소매 동작의 끊김이 줄어든다.
- 실수: v를 현재 값의 약 0.65로 낮춰 0.3~0.5초 동안 발을 다시 고르게 한다. p나 이미 지난 구간은 되돌리지 않는다. 복귀는 기본 속도까지이며 성공 없이 최고 속도를 자동 복원하지 않는다.
- 주요 전개: `p≥0.85`에서 양옆 숲이 열리며 긴 능선으로 질주하는 한 번의 넓은 시야. 억제된 황토 먼지와 길의 원근 수렴으로 절정을 만들고 만화식 방사 속도선·모션블러·전면 섬광은 쓰지 않는다.

기본 반복 이동의 위상, 성공으로 획득한 구간, 순간 추진력 v를 별도 상태로 둔다. 따라서 환경이 살아 있으면서도 시간만으로 성취가 증가하지 않는다.

### Phase 4 — 눈앞의 목인 수련

고정 밑동·수직 축·몸통·좌우 타격목·머리·그림자·균열 마스크·미리 제작한 파편, 전경 좌우 손/소매를 분리한다. 메뉴의 자켓/곡명 표시 목인을 복제하지 않는다. 목인은 상단과 양옆에서 형태가 읽히도록 배치하고 중앙 레인 뒤 몸통은 낮은 대비로 제한한다.

- 상시: 손의 작은 호흡, 목재 매듭의 정적 질감, 배경 천의 미세한 흔들림. 공격이 없을 때 목인이 흔들리지 않는다.
- 정상 탭/머리: 레인 위치에 맞춘 손/타격목 반응. 45~70ms 타격, 100~160ms 반동, 약 220ms 복귀. 동시치기는 한 번의 양손/복합 반응으로 묶되 실제 판정은 모두 집계한다.
- 정상 유지 체크: 손을 새로 휘두르지 않고 압박 자세·목재 떨림으로 연결한다. 정상 끝은 마무리 밀침. 고밀도 구간에서도 손과 파편 수를 계속 늘리지 않는다.
- 실수: 손을 200~300ms 털거나 그립을 다시 쥔다. 롱노트 Miss 연속 발생은 150ms 단위로 실패 제스처를 합쳐 재생하되 모든 실패 사실은 보관한다. Miss를 목인 타격/균열 증가로 표현하지 않는다.
- 누적 p는 마모/작은 균열·목재 결의 변화를 만든다. 이전 페이즈에서 실수했다면 Phase 4를 완벽히 쳐도 목인은 서 있는 상태로 끝난다.
- **전체 곡 무실수 완료일 때만 파괴**: 마지막 노트만 보고 시작하지 않고 Completed 확인 후, 0~0.12초 압축 → 0.12~0.45초 결 방향 파단 → 0.45~0.9초 밑동 주변 붕괴 → 정지. 미리 제작한 8~12개 파편을 곡선으로 움직이며 Rigidbody 물리나 랜덤 폭발은 사용하지 않는다. 파편/손은 레인 위로 나오지 않는다. 기존 결과 대기 2초 안에서 충분히 끝낸다.

## 5. 시간표와 성과를 분리하는 규칙

### 5.1 고정 시간표

초기화 때 **조정 완료된 `controller.Chart.CompletionTime` C**, 선택 음원 길이 L, AudioOffset o를 받아 한 번 계산한다. 일반 곡의 기본 음악 축은 `m = Snapshot.SongTime - o`다. 입력 보정값은 이 축에 더하거나 빼지 않는다.

```text
D = min(L, C - o)                 // 실제 플레이에서 들려줄 음악 구간의 명목 길이
B = [0, D/4, D/2, 3D/4, D]        // 곡 시작 전에 확정
Phase 1: m < B1                  // 진입 안내·preroll도 닫힌 Phase 1
Phase 2: B1 ≤ m < B2
Phase 3: B2 ≤ m < B3
Phase 4: B3 ≤ m                  // D 후 남은 판정 해결/완료 대기까지 유지
```

이는 점수·콤보와 무관한 네 구간이다. 기본안은 동일 시간 비율이며 노트 밀도에 따라 경계 자체를 실시간 변경하지 않는다. 노트 분포는 아래 기여량/연출 강도에 반영한다. D 이전 입력 보정으로 실제 완료 시점이 약간 달라도 마지막 상태는 Phase 4이고, Completed가 피니시의 유일한 완료 트리거다. 음악이 먼저 끝나고 판정이 남아도 Phase 4를 유지한다.

비정상적으로 `D≤0`인 오프셋/재생 범위는 새 계획 생성의 명시적 검증 실패다. 게임 규칙을 고쳐 조용히 늘리지 않는다. 일반 곡 리디자인을 활성화하지 않고 진단을 남기는 호환 정책이 필요하다. 에디터의 중간 시작·훈련은 별도 모드로 식별하며 첫 릴리스에서는 기존 표현을 유지한다. 아주 짧은 유효 곡도 논리 구간은 네 개이고 긴 전환 애니메이션을 줄인다. 프레임보다 짧은 네 구간을 모두 충분히 보여주겠다고 보장하지 않는다.

페이즈 전환은 경계 시각부터 새 페이즈를 활성 상태로 선택한다. 전환은 배경만 0.35~0.7초 가림/겹침 처리하며 `min(0.7초, 다음 구간 길이의 10%)`로 제한한다. 노트 입력·음악·HUD를 fade하거나 기다리게 하지 않는다. 종료 화면과 전환이 겹치면 기존 종료 흐름이 우선이다.

### 5.2 판정 기회량과 진행량

원본 노트마다 총 기여량 1을 부여한다. 제안하는 불변 판정 스케줄에서 각 단위를 `(NoteIndex, Part, TickOrdinal, ScheduledChartTime, Weight)`로 식별한다.

| 노트/단위 | 배경 기여량 | 처리 |
| --- | --- | --- |
| Tap | 머리 1 | 정상 판정 한 번만 적립 |
| Hold, 중간 체크 있음 | 머리 0.25, 중간 전체 0.5를 체크 수로 균등 분배, 끝 0.25 | 길거나 BPM이 높아도 노트 하나의 총량은 1 |
| Hold, 중간 체크 없음 | 머리 0.5, 끝 0.5 | 짧은 Hold에도 총량 1 |
| 동시치기 | 노트별 위 규칙 적용 | 계산은 모두 적립, 화면 충격만 묶음 |
| 호환 rules=null Hold | 머리 0.5, 최종 Resolve 0.5 | 중간 체크를 새로 발명하지 않음. 시작 Miss로 한 번에 해결되면 미발행 몫을 성공으로 채우지 않음 |

단위의 소속은 `ScheduledChartTime - o`를 위 경계에 대입해 **곡 시작 전에** 결정한다. 0 이전 단위는 Phase 1, D 이후 단위는 Phase 4. 경계와 같은 시각은 새 페이즈에 속한다. 입력이 빨랐거나 늦었다고 다른 페이즈로 이동하지 않는다. Phase를 가로지르는 Hold는 머리·각 체크·끝이 각자의 예정 시각 구간에 속한다.

```text
Wi = 해당 페이즈에 속한 모든 단위 Weight 합계 (곡 시작 전에 고정)
Si = 해당 페이즈에서 실제 Perfect/Good으로 확정된 고유 단위 Weight 합계
pi = Si / Wi   (Wi > 0)
pi = 0         (Wi = 0)
```

Miss는 해당 단위를 해결했지만 Si를 증가시키지 않는다. 원래 분모 Wi도 줄이지 않는다. 오입력은 단위가 없으므로 0. 같은 ID 재통지는 무시한다. HoldStarted는 자세 시작용으로만 사용하고 새 판정 통지와 동시에 적립하지 않는다. Perfect/Good 동일 기여는 ‘정상 입력’ 기준이며 기존 점수의 Good 0.5 가중치를 변경한다는 뜻이 아니다.

Wi=0은 평가 기회 없음이다. 자동 만점이나 무료 개방을 주지 않는다. 바람·먼지 등만 움직이며 고정 시간에 다음 페이즈로 넘어간다. Wi가 1이면 정상 단위가 들어왔을 때 큰 목표 변화가 생길 수 있다. 이를 0.4~0.9초 제한된 움직임으로 보여주되 다음 성공을 기다리는 무한 지연 큐를 만들지 않는다. 밀집 구간도 전체 목표를 한 번 갱신하고 오래된 충격은 합친다. 고정 연출 스무딩은 이미 획득한 성취 표현일 뿐 Si를 생성하지 않는다.

경계 부근의 조기 판정은 다음 페이즈의 성취로 미리 보관한다. 지연 판정은 이전 페이즈 성취로 보관하며 현재 페이즈에 이월하지 않는다. 이전 페이즈의 임계 달성은 남은 전환 겹침 동안 짧게 완결할 수 있지만 새 페이즈 활성 시각을 늦추지 않는다. 겹침이 끝난 뒤에는 과거 장면을 재등장시키지 않는다. 극단적 짧은 곡/프레임 지연에서는 성취 기록과 모든 절정 애니메이션의 전면 재생을 동시에 보장할 수 없으므로 시간표·판정 읽힘을 우선한다.

### 5.3 실제 채보에 대입한 예

`Assets/Game/Resources/Authoring/Pupa.json`은 929노트(탭 900, Hold 29), BPM 202, offset -0.02초, JSON 종료 131.5410625초다. 로컬 `LocalAudio/PUPA.wav`를 파일 헤더로 읽은 길이도 131.5410625초였다. 마지막 EndTime은 122.8666666667초다. 이것은 테스트 플레이 결과가 아닌 정적 채보/음원 분석이다.

Warmup GoodWindow 0.1초, Automatic/0.25박 유지 체크를 적용하면 C≈123.966667, D≈123.986667초다. 페이즈 경계는 음악 기준 약 30.996667 / 61.993333 / 92.990000초다. 소수 tolerance를 생략해 표시했다.

| 페이즈 | 머리 노트 수 | 판정 단위 수 | 제안 기여량 Wi |
| --- | ---: | ---: | ---: |
| 1 | 219 | 279 | 219 |
| 2 | 240 | 268 | 239.25 |
| 3 | 236 | 264 | 236.75 |
| 4 | 234 | 298 | 234 |

Phase 2→3을 가로지르는 Hold 때문에 머리 수와 기여량이 다르다. 단순 ‘콤보 N이면 문 개방’이나 `Judged` 횟수만 합산하면 이 차이를 놓친다. 실제 로드된 곡/규칙마다 다시 계획을 만들며 이 수치는 하드코딩하지 않는다. `Settings/TestChart.asset`의 12초짜리 짧은 채보도 따로 사용해 짧은 전환을 검증한다.

### 5.4 실수와 무실수 피니시의 정확한 의미

| 사실 | 진행 적립 | 짧은 반응 | 전체 곡 무실수 자격 |
| --- | --- | --- | --- |
| Perfect / Good | 해당 단위 Weight | 강/약 성공 반응 | 유지 |
| Miss: 미입력, 유지 체크 실패, 끝 실패 | 0 | 실패/추진력 감소 | 상실 |
| 처리 가능한 플레이 중 빈 레인/판정창 밖 눌림 | 0 | 헛손질/손 털기 | 상실 |
| 정상 끝 판정으로 받아들여지지 않은 유효 Hold 중도 release | 0 | 손 털기 | 상실. Automatic에서 Miss가 아직 없더라도 해당. Release 모드의 허용 창 안에서 성공한 이른 끝 입력은 제외 |
| 정상 Automatic 끝 뒤 release / 성공 Release 끝 | release 자체 추가 적립 없음 | 정상 복귀 | 유지 |
| 키 반복으로 무시된 중복 down, 메뉴/일시정지/재개 차단 입력, 비레인 키 | 0 | 없음 | 영향 없음 |
| 무노트 휴식 / 낮은 콤보 자체 | 0 | 상시 호흡 | 영향 없음 |

원시 `Pressed` 구독에서 ‘그 프레임에 Judged가 없으면 오입력’으로 판단하지 않는다. Press 안의 Sweep이 이전 Miss를 발행할 수 있고 Hold 시작 통지도 겹친다. **실제 노트 매칭을 결정하는 지점에서 입력 처리 결과를 관측**해야 한다. Automatic Hold 중 재누름을 정상 유지 회복으로 취급하는지는 현재 규칙 의미를 보존한다. 이미 있었던 중도 release가 무실수 자격을 상실시켰으므로 회복을 추가 성공 단위로 만들지 않는다.

신규 피니시 조건: 일반 곡의 처음부터 플레이, Completed, 최소 한 노트/판정 기회, 모든 계획 단위 해결, Miss=0, 오입력=0, 중도 Hold 이탈=0, Failure 없음. Good만으로도 이 조건을 만족하면 파괴 가능하다. 자동 시범·편집기 부분 테스트는 자격 없음. 기존 점수·콤보·레코드·IsFullCombo의 의미는 바꾸지 않고 **배경의 무실수 자격만 별도로 보관**한다.

ResetOccurred 또는 새로운 세션 식별자가 생기면 네 Si와 실수 기록·피니시 latch를 초기화한다. 일시정지, 전환, 실수에서는 초기화하지 않는다. 중도 이탈 후 재진입은 새 세션이다.

## 6. 이벤트와 컴포넌트 구현 설계

다음 이름은 **신규 제안이며 아직 저장소에 없는 타입/파일**이다. 기존 인터페이스/변수 이름을 변경하지 않고 별도 계약과 참조를 추가한다. 실제 구현 전 해당 로컬 `main` 구현을 읽고 패턴을 맞춘다.

### 6.1 최소한의 판정 관측 보강

`Domain/RuleSession.cs`의 체크 생성과 판정 발행이 기준이다. 추후 `Domain/JudgmentSchedule.cs`로 순수 불변 스케줄을 추출해 RuleSession과 배경 계획 양쪽이 **같은 자료**를 사용하게 한다. timeout 정렬(머리/Release 끝의 GoodWindow), inclusive 경계, 동일 시각 순서, TempoMap 계산은 그대로 보존한다. 시각 효과 때문에 판정 알고리즘을 새로 만들지 않는다. 예정 노트 시각과 timeout 처리 시각은 별도 보관한다.

추가 관측 계약 제안:

- `JudgmentResolvedEvent`: 세션 ID, 단위 ID(NoteIndex/Part/Ordinal), 예정 채보 시각, 실제 처리 논리 시각, Grade, Lane. 기존 `JudgmentEvent`를 교체하지 않는다. head/tick/tail 각각 한 번, 상태 반영 뒤 알린다.
- `InputResolutionEvent`: 세션 ID, 입력 식별자/레인, 실제 논리 시각, AcceptedHead / HoldRecovery / EmptyPress / HoldInterrupted / Ignored 등 처리 결과. 실제 매칭/중도 release 분기에서 생성하고 점수나 체력에 새 벌점을 넣지 않는다.
- Coordinator가 별도의 관측 인터페이스를 통해 전달한다. 기존 Judged/HoldStarted/Failed 구독 순서와 시점을 보존하며, 추가 이벤트의 수신자는 버퍼에 값만 넣는다. 성공 콜백에서 배경 애니메이션·음원 로딩·오브젝트 생성을 실행하지 않는다.

rules=null 호환 경로의 대응도 별도 어댑터로 명시한다. 스케줄 추출/이벤트 계약이 불가피한 공통 변경이므로 Phase 1 첫 미술 샘플은 먼저 기록된 가짜 입력 시나리오로 검증하고, 실제 플레이 연결 단계에서 관련 회귀를 통과시킨다. 기존 이벤트만으로 불완전하게 추측 연결한 버전을 완성본으로 승인하지 않는다.

### 6.2 책임 분리

| 제안 컴포넌트/파일 | 계층 | 입력·출력과 소유권 |
| --- | --- | --- |
| `BackgroundPhasePlan` | Application | 조정된 Chart, 공유 판정 스케줄, Tempo/규칙, L/o로 고정 B·Wi·단위 소속 생성. Unity API 없음. |
| `BackgroundPerformanceState` | Application | 고유 판정 단위·입력 결과를 소비, Si·실수 자격·완료 latch 계산. Unity API 없음. 중복 방지 저장소는 세션 수명이며 노트 수에 비례. |
| `GameplayBackgroundDirector` | Runtime/Presentation | 조립 루트에서 read model/관측 모델/뷰 주입. Tick 이후 LateUpdate에서 음악 시각·모아둔 이벤트를 반영해 Phase 선택과 뷰 목표를 갱신. 기존 Controller에 되먹임 없음. |
| `GameplayBackgroundView` | Runtime/UI/Views | 네 phase root, 전환 마스크, 중앙 보호 마스크, 고정 소품/풀을 직렬화. 계층/참조 검증. 카메라나 UI 생성하지 않음. |
| `DojoRoomPhaseView` | Runtime/UI/Views | Phase 1 문·문틀·마루·뜰·빛·먼지 제어. 동일 p에서도 문/빛/원근은 각기 다른 곡선. |
| `MountainPhaseView`, `RunPhaseView`, `DummyPhaseView` | Runtime/UI/Views | 각각 고도/지형, 반복 이동+추진력, 타격/실패/파괴의 표현만 소유. 다음 단계 이후 제작. |
| `BackgroundDepthLayer` | Runtime/UI/Views | 제작된 깊이/원근 좌표로 개별 RectTransform의 투영·가림 계산. 노트 카메라를 참조해 변경하지 않음. |
| `GameplayBackgroundBuilder` | Editor/UILayout | 고정 구성·Sprite/Clip·풀·마스크를 작성하고 `ScenePresentationBuilder`가 연결. 재생성 가능한 단일 구성 원천. |
| `GameplayBackgroundPreview` | Editor | 독립 Phase 1 프리팹과 가상 시각/판정 테이프를 샘플링. 기록/음원/입력 설정 저장 안 함. Standalone Open Scene 메뉴 추가 안 함. |

별도 `GameplayBackgroundProfile` ScriptableObject에 **표현 곡선·색·레이어/클립 참조·최대 변위**만 둔다. 판정창/체력/게임 종료/채보 스케줄을 이 자산에 복제하지 않는다. 프로필은 공유 불변으로 쓰고 런타임 상태는 Director와 Application 상태가 소유한다.

### 6.3 렌더링과 수명

```text
음악/판정 시스템 ──읽기 전용 사실──▶ BackgroundPhasePlan + PerformanceState
                                               │
                                           Director
                                               │
배경 Canvas: 원경 → 중경/문 → 전경 → 중앙 명암 보호 → 기존 GameplayViewport
                                                        │
                                          기존 HUD/판정 이펙트/일시정지
```

기존 Overlay 합성을 이용한다. 새로운 배경 전용 3D 카메라·동적 조명·실시간 그림자·후처리 스택은 첫 단계에 필요 없다. 2.5D는 각 오브젝트에 깊이와 투영을 부여하는 구현 선택이며 완성된 한 장 이미지를 네 번 미는 방식이 아니다. 바닥은 분리된 사다리꼴 메시/스프라이트 조각, 문 개구부는 고정 마스크로 처리한다. 단일 Canvas 전체를 매 프레임 재배치하지 않고 정적/움직이는 하위 Canvas의 재빌드 범위를 나눈다.

새 phase root 네 개는 한 Gameplay 안에 미리 저작한다. 활성 페이즈와 전환 중 인접 페이즈만 그린다. 배경 자산 로드·고정 파편/먼지 풀 생성은 세션 시작 전 준비한다. 소리·먼지·목인 파편을 타격마다 Instantiate하지 않는다. 누락 참조는 Validate로 명시적 오류를 내고 `Find`/AddComponent로 복구하지 않는다.

상시 호흡 시간, 성공/실패 애니메이션 시간은 `Playing && !IsPaused`에서만 전진한다. 소개 화면에서는 닫힌 문과 아주 약한 상시 호흡만 허용한다. 일시정지/포커스 상실/오디오 복구와 3초 카운트다운 중에는 p·전진·추진력·반응 나이를 고정한다. 재개 후 밀린 실시간 델타를 몰아서 적용하지 않는다. 완료 후 피니시만 기존 결과 대기와 같은 실제 시간으로 진행한다.

프레임이 건너뛰면 음악 시각으로 현재 Phase를 즉시 선택하고 목표 p로 수렴한다. 화면 충격은 프레임 의존 판정 대신 예정 시각의 동시치기 묶음/상한으로 압축한다. 계산된 성과는 빠뜨리지 않는다. 재시작/비활성/파괴 시 이벤트 구독, 전환 latch, 파편/먼지 활성 상태를 정리한다.

기존 `GameplayAtmosphere`는 HUD와 GameplayHitFeedback의 필수 참조다. 즉시 삭제하지 않는다. 통합 시 기존 타입·필드 이름/검증 계약을 유지하고 작성된 구형 레이어를 보존하는 단계부터 시작한다. 신규 배경이 활성일 때 구형 레이어의 renderer/animation 경로를 명시적으로 끄고, 현재 `React()`와 Director가 같은 새 레이어를 이중 구동하지 않게 분리한다. 이후 의존 정리는 별도 승인된 범위에서 진행한다.

## 7. 재사용·교체 및 신규 에셋 계획

### 유지/재사용

- 메뉴용 `DojoRenewal`, `TitleSky`, `AlphaDojo`, `PracticeSelection`의 기존 원본/파생 에셋은 보존한다. 새 인게임 때문에 색·필터·크롭을 덮어쓰지 않는다.
- `PracticeHall`, `HanokEntrance/HanokDoors`, `LessonRoom`, `ScribingRoom`, `Summit`은 재질·원근·색감의 레퍼런스. 원본을 잘라 쓸 수 있는 소품은 새 파생 자산으로 만들고 공유 원본의 GUID는 유지한다.
- 붓글씨 폰트, 먹 그래픽의 제작 패턴, 기존 오디오 풀, 씬 전환·일시정지·결과 계약, 실제 노트/레인 좌표는 재사용한다.
- Phase 1에서는 SongpanNote/프리팹/기존 HUD를 읽힘 검증용으로 유지한다. 최종 노트와 HUD 디자인을 확정했다는 의미는 아니다.

### 교체 대상

- Gameplay의 TrainingHall 단일 배경과 그 위의 일반 창빛/바닥 먹 장식은 새 공간 구성으로 교체한다. 원본 파일 삭제는 하지 않는다.
- 향후 레인 표면은 과한 입체 광택을 줄인 먹/목재 결, 노트는 명확한 타격 면과 Hold 연결부, 이펙트는 힘의 방향을 가진 붓/목분 표현으로 재설계한다. 타이밍/크기 읽힘 검증을 별도로 진행한다.
- 메뉴 목인/전신 StrikePoses를 1인칭 손/파괴 목인으로 그대로 쓰지 않는다. 소재 정체성만 유지한 전용 자산이 필요하다.

### 제작 목록과 순서

제안 신규 위치: `Assets/Game/Art/IngamePhases/`, `Assets/Game/Prefabs/Presentation/`, `Assets/Game/Settings/Presentation/`. 아직 생성하지 않았다.

| 제작 묶음 | 분리 제작물 | 납품/검수 |
| --- | --- | --- |
| Phase 1 우선 | 실내 좌/우 벽, 천장 보, 좌/우 기둥, 마루 근/중경, 문턱, 실내측 좌/우 문짝, 틀, 뜰, 산/하늘, 바깥 식생, 빛 마스크 3종, 천, 먼지 atlas | 닫힘·틈·절반·완전 개방·문턱 밖 5개 조립 컷. 각 PNG 투명 여백, pivot/깊이/최대 이동 범위 명세. |
| Phase 2 | 돌길 3구간, 바위/소나무 전경, 낮은 수림, 능선 3겹, 정상 전경, 아래쪽 도장 원경, 안개/구름 | 중턱과 정상 시점의 소실점 일치. 반복 이음새 노출 없음. |
| Phase 3 | 곡선 능선길 조각, 근/중경 수목, 발치 돌/풀, 얇은 먼지, 소매 가장자리 | 느림/빠름에서 형상이 읽힘. 배경 중앙의 깜박임 금지. |
| Phase 4 | 고정 밑동/축/몸통/팔/머리, 성공/실패 손 자세, 균열 3단계, 방향별 타격 프레임, 정합된 파편 8~12개 | 정상/실패/파괴의 실루엣 분리. 메뉴용 자켓 없는 전용 목인. |
| 후속 UI/노트 | 레인 표면·테두리, 탭/홀드 머리·몸통·꼬리, 판정 붓 흔적, 작은 곡 정보/체력 표현 | Phase 1 승인 이후. 진행 UI는 제작 목록에 없음. |

최종 아트는 겹침 뒤에 숨은 영역도 그려야 한다. 문이 열릴 때 생기는 빈 검정/알파 구멍, 시점 접근 시 가장자리 부족, 이동 중 서로 다른 픽셀 밀도를 검수한다. 생성형 이미지 사용 여부와 관계없이 전체 합성 한 장을 납품물로 끝내지 않고 투명 분리/픽셀 정리/실루엣 정합/원근 검수를 거친다. 이번 단계에서는 이미지 생성하지 않는다.

고정 이미지 변환은 에디터 베이크, 공유 Sprite/Material은 불변. 클립은 Sprite 교체 또는 저작된 커브를 명시적 시각으로 샘플링한다. 새 효과음은 Phase 1 검증에 필수가 아니며 기존 곡/타격 믹스를 우선한다. 문 소리가 필요하면 짧고 낮게 한 번만 재생한다.

## 8. Phase 1 독립 검증 계획

다음 구현은 먼저 `DojoRoomPhaseView` 프리팹과 Editor preview에서 **실제 제작 품질의 한 공간**을 완성한다. 성과 슬라이더는 개발용 preview에만 둘 수 있고 게임 화면에는 노출하지 않는다. 일반 플레이 씬에 즉시 네 배경을 덧붙이지 않는다.

1. 같은 아트로 닫힘→문틈→빛 확장→완전 개방→통과를 연속 캡처한다. 단순 확대와 구분되도록 문틀/문짝/뜰/산의 상대 이동을 비교한다.
2. preview에 실제 레인 투영과 현 HUD를 함께 합성한다. 완전 배경 화면만 보고 승인하지 않는다. 1280×720, 1920×1080, 16:10, 4:3, ultrawide에서 노트 진입부·판정선·자켓/체력과의 가림을 확인한다.
3. 준비한 판정 테이프로 전부 정상, Good만, 성공/실수 혼합, 전부 Miss, 빈 구간, 끝에 한 노트, 경계 Hold, 동시치기/밀집 스트림을 재생한다. 같은 음악 시각에서 같은 Phase지만 문 개방은 성과에 따라 다른지 확인한다.
4. 0.85 문턱 직전/직후 정지·재개·재시작, 피니시 중 재시작, 경계 직전의 빠른 입력/지연 판정, 낮은 FPS를 검증한다. 실패 후 p가 줄지 않고 정상 이벤트가 없으면 p가 증가하지 않아야 한다.
5. 미술 검증이 통과하면 최소 관측 계약을 연결해 실제 Gameplay에서 PUPA와 짧은 TestChart를 플레이한다. 개발 preview의 강제 성공 경로는 제품 입력으로 사용하지 않는다.

**미술 승인 기준**: 메뉴의 PracticeHall과 같은 세계의 목재/창호/석재로 보임, 문 개방의 깊이가 실제로 읽힘, 전경·중경·원경 상대 움직임이 존재, 낮은 성과에서도 공간이 살아 있음, 정상 성과 때 문턱 통과가 분명함, 연타가 밝기/먼지 폭증으로 변하지 않음. 밝은 노트가 들어오는 중앙에서는 배경보다 노트가 먼저 보이며 손/문틀/빛이 판정선을 덮지 않아야 한다.

**기능 승인 기준**: 네 구간의 고정 경계, 정확한 단위 정규화, 성공만 증가, 실수 시 누적 보존, 중복 이벤트 0회 추가 적립, 무노트 무료 성공 없음, pause/restart/re-entry 정상. 현재 노트/판정/음악 타이밍은 배경 off/on에서 동일한 이벤트 입력에 동일한 결과를 낸다.

**성능 출발 예산**(측정 결과 아님): 정적 로딩 뒤 성공 한 번당 managed allocation 0B, 신규 배경 CPU p95 0.5ms 이하, 추가 GPU p95 1.5ms 이하를 현재 개발 PC 1080p/동일 품질에서 목표로 삼는다. 기존 화면 대비 프레임 p95/p99와 GC, Canvas rebuild, 텍스처 메모리를 함께 기록한다. Phase 1 텍스처 resident 증분 목표 32MiB 이하, 효과 풀 크기 고정, 재진입 10회 후 객체/메모리가 단조 증가하지 않아야 한다. 목표를 넘으면 레이어/오버드로/atlas를 조정하고, 숫자만 맞추려고 아트 읽힘을 낮추지 않는다.

## 9. 충돌 위험과 검증 범위

| 위험 | 예방 및 필요한 검증 |
| --- | --- |
| 롱노트 중복/누락 | HoldStarted+Judged 중복 금지, shared schedule/단위 ID 사용. `Tests/Domain/PlayRulesTests.cs`, `CoordinatorTests.cs`를 우선 확장. 머리 Miss·중간 실패·Automatic 조기 release·Release 끝·변속 Hold·경계 소속을 검사. |
| 공통 판정 스케줄 추출 | 정렬/short-circuit/이벤트 순서/허용 오차를 보존. 기존 결과와 이벤트 테이프 차등 비교. 서로 다른 규칙을 하나로 합치지 않음. |
| 오입력의 잘못된 추정 | 매칭 지점의 새 관측 사실 사용. 무관한 timeout Miss가 같은 Press에서 발생하는 경우, 메뉴 입력, 중복 down, 정상 Hold 끝 release, 재개 차단 입력을 구별. |
| 종료 시간 오해 | 조정된 C를 사용. 음원 길이와 빈 outro가 다른 실제 PUPA 사례 검증. 피니시는 Completed 뒤에만 재생하고 결과 지연을 임의로 늘리지 않음. |
| 카메라/Viewport 투영 변화 | 월드 카메라/RawImage 크롭은 유지. 새 배경은 아래 Canvas에만 배치. `Tests/PlayMode/GameplayHitFeedbackTests.cs`, `GameplayHudFlowTests.cs`와 각 화면비 실제 캡처. |
| 기존 Atmosphere 필수 참조 | HUD/HitFeedback Validate 계약 보존 후 점진적 분리. 새/옛 연출의 이중 Update와 잔여 창빛 확인. `IngamePresentationTests.cs` 확장. |
| pause/recovery 이후 시간 점프 | Director는 Paused와 상태를 읽고 애니메이션 시간을 멈춤. `GameplayPauseFlowTests.cs`, `AudioRecoveryFlowTests.cs`의 해당 사례와 필요한 Hardware만 적용. |
| 결과/훈련/에디터 복귀 | `ResultsFlowTests.cs`, `GameplayFlowTests.cs`, 훈련 작업 트리의 `TrainingFlowTests.cs`에서 관련 경로. 훈련 자동 시범을 일반 곡의 무실수로 보상하지 않음. |
| 공유 프리팹/아트 오염 | 원본 메뉴 자산·메타 보존, 별도 파생 자산. `ChartGameplayPreview`는 공유 NoteView 영향 확인. 전체 Build All 대신 대상 builder만 실행. |
| 고정 구성 Runtime 재생성 | 프리팹 재오픈·초기화·재진입에서 고정 문/레이어/풀 인스턴스가 교체되지 않음을 검증. 누락 참조는 오류 처리. |
| 진행 표시가 UI로 새어 나옴 | preview 디버그 값과 제품 View 분리, 숨김 progress/Resolved Track 유지. 화면 캡처로 네 페이즈 숫자·새 게이지 없음 확인. |

테스트는 신규 사용자 동작과 실제 경계만 추가하며 기존 사례를 우선 확장한다. 순수 계획/정규화는 `Fast`, 실제 씬 연동은 `SceneIntegration`, builder 저장/재오픈은 `AssetGeneration` 중 정확히 한 비용 카테고리로 분류한다. 씬 구성 연결 시 Smoke를 추가한다. 예약 재생·완료 연결을 바꿀 때 관련 Endurance, 오디오 복구 계약을 건드릴 때 관련 Hardware를 추가한다. 공통 판정 관측/스케줄 추출의 계층 영향이 불명확하면 빌드 없는 All을 실행한다. 단순 아트 검증에 무조건 전체 테스트/빌드를 붙이지 않는다.

Unity 6000.3.23f1, Test Framework 1.6.0, 기존 Pipeline 절차를 사용한다. 소스 변경 후 recompile 완료→관련 suite 한 번씩 실행→test_status 결과 확인→Play Mode 종료와 ready 확인 순서다. 0건/skip/inconclusive를 성공으로 보고하지 않는다. 상세 절차는 [verification.md](verification.md), 판정/저장 경계는 [chart-format-v1.md](chart-format-v1.md), 계층 구조는 [team-ownership.md](team-ownership.md)를 따른다. 이 소유권 문서는 구조 참고이며 사용자가 요청한 수리에 별도 팀원 승인 대기를 추가하지 않는다.

## 10. 후속 구현 단계와 중단 지점

| 단계 | 실제 작업 범위 | 완료/다음 단계 판단 |
| --- | --- | --- |
| A. Phase 1 공간 검증 | 실내 전용 레이어 아트, 독립 프리팹/preview, 문·빛·원근/실패 반응, 기존 레인/HUD 합성 | 위 미술·읽힘 기준으로 영상/정지 컷 검토. 이 단계가 만족스럽지 않으면 다른 페이즈 제작 금지. |
| B. Phase 1 실제 연결 | shared schedule/관측 계약, 시간표·기여량 상태, Director, Gameplay builder 연결. 네 논리 구간은 검증하되 미제작 2~4를 완성 아트처럼 보고하지 않음 | 실제 곡/희소/롱노트/동시치기, pause/restart/re-entry, 기존 타이밍 회귀. Phase 1 성능 측정. |
| C. Phase 2~4 | 산길/질주/목인 아트와 각 View, 전체 곡 무실수 피니시, 네 구간 전환 | 시간표 고정, 미달성 분기, 목인 파괴 자격·결과 복귀와 긴 곡 재생 검증. |
| D. HUD·레인·노트 확장 | 메뉴 정체성과 맞는 HUD/노트/레인/판정 시각 정리 | 모든 모드·화면비·색/명암·밀집 채보에서 읽힘 검증. 새 진행 UI는 여전히 금지. |

이번 단계의 산출물은 이 문서 한 개다. 문서의 파일 근거와 링크, 진행 규칙의 경계 사례를 검토했다. Unity 런타임 검증이나 신규 미술의 시각적 완성도 검증을 완료했다고 주장하지 않는다. **여기서 작업을 종료하고, 후속 구현은 사용자의 다음 지시에서 시작한다.**

## Phase 1 구현 반영 — 2026-11-18

조사 당시의 Gameplay 조립, TrainingPlayback/편집기 경로, 조정된 CompletionTime, 기존 판정 이벤트의 체크 ID 부재, TrainingHall 단일 배경을 실제 코드와 대조했다. PracticeHall/HanokDoors 원본도 직접 확인했다. 이 조사에 필요한 정정은 없었으며, 아래 구현 사항으로 기존의 “제안” 상태를 갱신한다.

### 실제 구성과 설계 선택

- `GameplayBackgroundBuilder`가 Gameplay에 별도 배경 카메라·목재 구조·문짝·마루·뜰·천·먼지 12개를 직렬화한다. 런타임 생성은 기존 GameplayViewport의 화면 크기별 RenderTexture와 MaterialPropertyBlock 같은 세션 데이터에 한정한다. 문·소품·카메라를 런타임에 재구성하지 않는다.
- 원안의 UI 깊이 투영 대신 **배경 전용 원근 카메라와 텍스처 메시/카드의 2.5D 합성**을 사용한다. stage 원점 `(1000,0,0)`, 전용 layer 30, FOV 58. 레인 카메라에서는 해당 레이어만 제외하며 위치·FOV·입력/노트 투영은 보존한다. URP 후처리나 동적 광원은 추가하지 않았다. 문 개방에 따른 마루의 빛/창살 그림자와 공기 광선은 전용 셰이더로 표현한다.
- 좌우 문짝은 두께가 있는 별도 물체이며 벽 뒤로 들어간다. 개방 목표는 성과만으로 증가한다. 서로 다른 감쇠 시간, 0.3 이후 접근, 0.85 이상에서 짧은 예비 동작과 문턱 통과를 사용한다. 카메라 FOV와 roll은 고정한다.
- 독립 천/소나무 메시의 위상, 12개 먼지의 속도·깊이를 분리했다. 먼 산은 별도의 정적인 원경 카드다. 전체 배경 이미지를 확대/흔드는 애니메이션은 없다.
- `GameplayViewport`를 재사용하여 배경을 기존 Overlay Canvas 아래에 합성한다. Phase 2~4는 하나의 기존 배경 그룹을 공유한다. 해당 단계에 새 지형·목인·손·HUD를 만들지 않았다. 일반 곡에서는 구형 배경의 독립 unscaled 애니메이션을 고정해 일시정지 중 별도 시간이 흐르지 않게 했다. 훈련/편집기 플레이테스트는 종전 표현을 유지한다.
- 원경·문·소품 이미지에는 Point 필터를 사용한다. 실제 원근 표면인 목재/돌에는 mipmap·Trilinear·4x anisotropic을 사용해 먼 바닥의 반짝임과 mip 단계 띠를 줄인다. 이는 정적 UI 레이어를 전제로 한 초기 Point-only 제안과 의도적으로 다른 선택이다.
- 제안된 BackgroundDepthLayer, 네 개의 개별 View, 공용 표현 Profile을 모두 미리 만들지는 않았다. 현재 구현에 필요한 `DojoRoomPhaseView`와 단일 Director만 제작했다. 다음 장면은 공통 계획/성과 상태를 그대로 소비하면서 고유 View를 연결할 수 있다.

### 공통 규칙과 관측

`JudgmentSchedule`은 기존 RuleSession의 timeout 정렬·박자 기반 체크 생성에서 추출한 불변 자료다. RuleSession과 배경이 동일 자료를 공유한다. `JudgmentResolvedEvent`는 세션 ID, 고유 단위 ID, 머리/유지/끝, ordinal, 예정 시각, 실제 처리 시각, 등급을 담는다. 기존 Judged/HoldStarted/Failed의 발행 순서는 유지하며 새 관측은 기존 알림 뒤에 전달한다. 기존 콜백 중 재시작하더라도 이전 사실이 새 세션의 성공으로 바뀌지 않도록 이벤트의 세션 ID를 먼저 고정한다.

`InputResolutionEvent`는 실제 매칭 지점에서 정상 머리·유지 회복·빈 입력·중도 이탈을 관측한다. 기존 점수/체력/판정에 새 벌점을 주지 않는다. Phase 4 완벽 피니시는 이번에 구현하지 않았다.

`BackgroundPhasePlan`은 실제 controller.Chart와 오디오 길이/오프셋으로 `D=min(L,C-o)` 및 네 등분 경계를 고정한다. 단위의 소속은 입력 시각이 아닌 예정 시각이다. 탭 1, 유지 체크가 있는 Hold는 0.25/0.5/0.25, 없는 Hold는 0.5/0.5로 정규화한다. 호환 rules=null 경로도 머리/최종 Resolve를 관측한다.

`BackgroundPerformanceState`는 세션별 ID 중복 제거와 각 페이즈의 고정 기회량을 사용한다. Perfect/Good의 기여량은 같고 Miss/빈 입력은 0이다. 기회가 없는 구간은 0으로 유지한다. Director는 판정 콜백에서 사실만 큐에 넣고 LateUpdate에서 소비한다. 음악 시각 차이로 움직임을 진행하며 pause/복구/예약 재개 대기에서는 전부 고정한다. 시간표는 성과와 무관하고 GameOver는 기존 흐름을 우선한다.

### 아트·재생성·증거

신규 원본은 `Assets/Game/Art/DojoRoom/{RoomAtlas,Courtyard,DetailsAtlas}.png`다. built-in image_gen으로 제작했으며, 전체 프롬프트와 파생 방식은 [dojo-room-art-prompts.json](dojo-room-art-prompts.json)에 보관했다. 기존 메뉴 아트의 원본/GUID는 변경하지 않았다. 텍스처 자르기·고정 천 메시·재질은 Editor builder가 생성한다.

`Game Tools > Scenes > Build Gameplay`로 씬을 재생성할 수 있다. 생성된 씬에서 `Game Tools > Preview > Capture Dojo Room Motion`은 별도 PreviewScene에 복제한 배경을 24fps/120프레임으로 렌더링한다. 이 모션은 가상 성과 미술 샘플이며, 실제 판정 기반 검증은 `DojoBackgroundFlowTests`의 Gameplay 캡처를 사용한다. 새 Open Scene 메뉴는 추가하지 않았다.

시작·정상 중간·완전 개방·연속 미스·전환 직전/전환/대체 화면의 실제 Unity 렌더, 테스트 결과와 미검증 범위는 [검증 보고](../Logs/dojo-room-verification.md)에 기록한다.

## Phase 2~4 구현 반영 — 2026-11-18

기존 `DojoRoomPhaseView`와 첫 도장 아트는 유지했다. `TrainingJourneyView`가 저작된 배경 카메라·투영·환경 운동을 공유하고, `MountainPhaseView`, `RunPhaseView`, `DummyPhaseView`가 고도·질주·타격을 각각 담당한다. `GameplayBackgroundDirector`는 기존 음악 시각과 판정 큐를 사용하여 네 페이즈를 선택한다. 실제 입력, 판정창, 음원 재생, 스크롤과 레인 투영에는 새 제어를 추가하지 않았다.

- 산길은 경사 지형·암벽·나무·수풀·근거리 안개·원거리 구름을 별개로 저작했다. 정상 판정의 누적 성과만 고도 목표를 올리며 85% 이상이면 정상에 접근하여 계곡과 아래 도장을 내려다본다. 미달성 시 낮은 지형에 머문다. 원경은 전진에 따른 과도한 확대를 피하고, 가까운 지형의 깊이와 시차로 상승을 표현한다.
- 질주는 고정 크기의 길·가로수·바람 풀을 순환한다. 성공 판정의 정규화된 충격이 추진력을 높이고, Miss·빈 입력·롱노트 이탈은 짧은 감속과 작은 배경 횡변위를 만든다. 정상 플레이로 회복한다. 고성과에서는 주변 숲이 열리며 절정에 도달한다. 무입력 기본 이동은 성과가 아니다.
- 목인은 축·회전 타격목·11개 파괴 부품·좌우 손·소매·목재 먼지를 저작했다. 손의 접촉과 목재 진동은 같은 55ms 접촉 시점을 사용한다. 유지 판정은 지속 압박, 실패는 손 털기로 표현한다. 모든 구성은 초기화·재시작 때 복구하며 타격마다 생성하지 않는다.
- 피니시는 Completed 뒤 실제 FullCombo, Miss 0, 전체 고유 판정 단위 처리, 빈 입력·롱노트 이탈 0을 모두 확인한다. Good은 정상 성공이다. 충족하면 목재 부품이 중력·착지·감쇠 반동으로 무너지고, 미충족이면 목인이 선 채 경계를 풀고 고개를 숙인다. 결과 화면의 기존 대기 시간과 전환 주체는 유지했다.
- 도장 퇴장은 기존 연속 전진을 재사용한다. 짧은 곡에서만 도장 화면의 겹침 시간을 줄여 산길을 가리지 않는다. 이후 전환은 배경 겹침과 `JourneyPassageGraphic`의 먹·안개 바람으로 연결하며 검은 화면을 반복하지 않는다. 성과와 무관하게 기존 네 시간 구간을 따른다.

신규 원본 다섯 종류와 투명 파생 부품은 `Assets/Game/Art/TrainingJourney/`에 있고, 생성 프롬프트는 [training-journey-art-prompts.json](training-journey-art-prompts.json)에 보관했다. `TrainingJourneyBuilder`를 기존 `GameplayBackgroundBuilder`에 연결했다. `Game Tools > Scenes > Build Gameplay`로 재생성하고 `Game Tools > Preview > Capture Training Journey Motion`으로 미술용 가상 성과 영상을 렌더링할 수 있다.

기존 도장 회귀, 짧은 곡의 네 페이즈, 일시정지·재시작, 고성과/무입력/오입력, 전체 곡 완료 자격과 Results 연결을 검사했다. 실제 Gameplay 캡처, 별도 미술 프리뷰, 검사 결과와 남은 검증 범위는 [Phase 2~4 검증 보고](../Logs/training-journey-verification.md)에 기록한다. **네 배경 페이즈에서 작업을 마쳤으며 다음 HUD·레인·노트·이펙트 리디자인은 시작하지 않았다.**
