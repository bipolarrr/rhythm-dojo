# 비급 전수: 스토리형 리듬 수련 시스템 조사 및 구현 계획

## 트릴 수직 슬라이스 (2026-11-15)

현재 제품 수련은 `Trill.asset` 하나다. 아래 최소 실행 시스템과 조사 절은 앞선 단계의 기록이다. 다른 11개 수련은 추가하지 않는다.

- 진입: Title → ModeSelection의 비급 전수 → Tutorial의 트릴 선택 → 사형 도입/설명 → 시범 → 따라 하기 → 응용 → 짧은 검정 → 사형 피드백 → 재도전 또는 수련 선택. 기존 Tutorial/Gameplay, 공통 대화와 판정/HUD/일시정지를 사용한다.
- 콘텐츠: BPM 100, 4박 준비. 따라 하기는 1-3-1-3과 2-4-2-4를 0.6초 간격으로 친다(8노트). 응용은 0.3초 간격으로 4타 두 묶음에서 8타 두 묶음으로 늘린다(24노트). 검정은 느린 4타 두 묶음과 빠른 8타 두 묶음이다(24노트). 묶음 시작은 2.4/7.2/12/16.8초이며 충분한 쉼을 둔다. 시범은 따라 하기 채보를 공유한다.
- 시범: `TrainingPlayback`이 기존 곡 시계의 현재 시각까지 도달한 노트를 채보에 저장된 시각으로 기존 엔진에 입력한다. 별도 판정창은 없다. 사용자 보정은 시범에서만 0이고, 직접 연주에는 원래 보정을 한 번 적용한다. 판정 이벤트로 기존 레인 효과와 사형의 타격 자세를 동기화한다. 일시정지하면 같은 곡 시계와 함께 멈춘다.
- 기록/피드백: 실제 플레이 중 처리한 누름의 레인·보정된 시각·노트 대응 여부를 저장한다. 기존 엔진의 성공 판정 수가 늘지 않은 누름만 추가 입력으로 센다. 대화/준비 전/일시정지/시범 입력은 제외한다. Miss는 맞히지 못한 노트에 대한 박자·횟수 조언, Good은 일정한 간격 조언, 실제 추가 입력은 노트를 정확히 보고 치라는 조언을 선택한다. Miss만으로 조기/지연 입력이나 손 동작의 원인을 단정하지 않는다.
- 통과/저장: 기존 Warmup/Gauge/Continue 판정으로 정상 종료, Miss 0, 정확도 90% 이상, 추가 입력 0회가 조건이다. 시범·따라 하기·응용의 성공은 검정 전 이수를 저장하지 않는다. 검정 성공만 `trill`/정의 버전 1의 완료를 저장한다. 실패·중단·재시작은 이전 이수를 지우지 않으며 일반 곡 기록과 분리한다.
- 재사용: `TrainingSession.Step/NextStep`, `TrainingLesson`의 단계별 곡/대화 참조, `TrainingPlayback`의 입력 관측을 분리했다. 새 UI나 판정 엔진 없이 공통 화면에서 단계별 흐름을 진행한다. 읽은 대사는 기존 Tab/건너뛰기로 넘길 수 있다.
- 저작: `TrillContentBuilder`가 채보·메트로놈·대사의 기준이다. 변경 후 Build Tutorial, Gameplay 연출은 `TrainingPlaybackLayoutBuilder` 변경 후 Build Gameplay로 반영한다. 생성된 Trill 에셋만 직접 수정하면 재생성 시 덮어쓰므로 lasting 변경은 빌더에 적용하고 채보/조건 변경 시 정의 버전을 올린다.

실행 결과와 범위는 [트릴 검증 기록](../Logs/bigup-training-verification.md)과 `Logs/trill-*-tests.json`에 기록한다. 빌드는 이번 요청의 범위에 포함하지 않는다.

## 최소 공통 실행 시스템 구현 현황 (2026-11-15)

후속 요청에 따라 아래 조사에서 제안한 전체 스토리/트릴 과정 중 **공통 수련 실행 부분만** 구현했다. 아래의 조사 당시 미구현 표기는 조사 기록이며, 현재 범위는 이 절과 [실행 검증 기록](../Logs/bigup-training-verification.md)을 기준으로 한다.

- 진입: `Title → ModeSelection → 비급 전수 → 기초 박자 수련 → 수련 시작`. 기존 `Tutorial`과 `Gameplay` 씬을 사용하며, 임시 개발 버튼이나 새 최상위 메뉴는 추가하지 않았다.
- 데이터: `Assets/Game/Settings/Training/BasicPulse.asset`의 `TrainingLesson`이 ID/제목/정의 버전/수련 또는 검정 단계/기존 `SongDefinition` 참조/허용 Miss/최소 정확도를 가진다. 샘플은 100 BPM, Tap 8개 한 항목이다. 기존 TestPulse 음원과 FourLaneMode를 참조하고 기존 검증 채보와 일반 곡 카탈로그는 변경하지 않는다. `Build Tutorial`은 누락된 샘플 자원만 생성하고 기존 정의를 덮어쓰지 않는다.
- 실행: `SongAssetAdapter → PlayRequest → GameplayCompositionRoot → RhythmSessionCoordinator → RhythmSession/RuleSession`을 그대로 사용한다. 수련의 Warmup/Gauge/Continue 규칙은 요청별로 생성하고 사용자 전역 규칙을 바꾸지 않는다. 사용자 키/입력 보정/스크롤, 기존 자동 시작/노트/판정/HUD/일시정지를 재사용한다.
- 평가: 기존 `SessionSnapshot`의 정상 종료, 전체 판정 완료, 엔진 실패 여부, Miss 수, 정확도만 읽는다. 샘플은 Miss 0개, 정확도 50% 이상이다. 정확도는 기존 Perfect/Good 가중치 계산 결과이며 별도 판정은 없다. 불필요한 추가 입력이나 트릴 교대 순서까지 평가하는 수련은 아직 아니다.
- 상태: `TrainingSession`이 `Ready → Preparing → Playing → Feedback`, 시도 번호, 성공/실패/중단, 결과 스냅샷, 이수 여부를 보유한다. 성공/실패 후 Tutorial에서 결과와 재도전을 제공한다. 일시정지 재시작은 새 시도로 취급하며 오래된 결과/중복 결과를 거부한다. 일반 모드 이탈 시 문맥과 재생 요청을 정리한다.
- 저장: `RhythmDojo.Training.v1` 전용 PlayerPrefs JSON에 ID/정의 버전별 이수를 기록한다. 완료 상태는 공유 콘텐츠 에셋을 수정하지 않고 세션/저장소에서 표현한다. 실패/중단은 새 이수를 저장하지 않으며 기존 이수를 지우지 않는다. 다른 버전의 이수는 현재 버전으로 승격하지 않는다. 일반 곡 기록과 수련 기록은 분리한다.
- 확장 지점: `PlayTraining(TrainingLesson)`, `TrainingSession.Stage/State/Outcome/Result`와 기존 `PlayResult`를 통해 이후 대화·캐릭터 표현·시범 전후·검정 화면을 연결할 수 있다. 대화 그래프, 시범 자동 연주, 캐릭터 연출, 다중 수련/해금, 트릴 추가 입력 분석, 세션 중간 복원은 미구현이다.

수련 UI는 `TutorialLayoutBuilder`와 `TutorialSceneBuilder`에서 저작한다. 조건/채보/규칙을 바꿀 때는 `definitionVersion`도 올려 이전 이수와 구분한다. 검증은 아래 원래 계획 전체의 완료가 아니라 이번 최소 실행 범위에 한정한다.

## 1. 조사 기준과 결론

- 조사일: 2026-11-15. 대상은 로컬 `emergency`의 `24748c14c152b3842b0e5de0b4f6fea2c7533db5` 및 작업 트리다. `origin`은 `https://github.com/bipolarrr/rhythm-dojo.git`이다. 원격 브랜치 최신 상태를 다시 가져온 조사는 아니다.
- 비교 기준은 로컬 `main`의 `ba43fa90a09410911f2bcb062af8e00b158eb8b5`이다. `MainMenuActions`, `GameplayCompositionRoot`, `SongDocument`, `SceneBuilder`의 main 구현을 읽었다. main의 기존 계층 분리를 따르되, emergency에 추가된 `ModeSelection`/`Tutorial` 경로와 캐릭터 연출을 유지한다. `song` 브랜치는 사용하지 않는다.
- 조사 시작 전부터 `ResultsPreview.cs`, `ResultsScreen.cs`, `ResultsFlowTests.cs`, `Docs/verification.md`, `Logs/mock-preview-verification.md`에 미커밋 변경이 있었다. 이 변경은 이번 작업의 산출물이 아니며 수정하지 않는다.
- 이번 산출물은 이 문서 하나다. 코드·씬·프리팹·채보·패키지를 변경하지 않았고, Unity 씬 재생성·테스트·플레이어 빌드는 실행하지 않았다. 아래의 **현재 상태는 소스·직렬화된 씬/에셋·테스트 코드에 대한 정적 조사 결과**이며 실제 실행 통과 보고가 아니다.

**핵심 결론:** 비급 전수의 사용자 진입점과 여자 사형의 애니메이션은 존재하지만, 현재 Tutorial은 수련 플레이가 없는 도입 화면이다. 노트·판정·롱노트·BPM·스크롤·일시정지 엔진은 이미 있으므로 그대로 사용한다. 새로 필요한 것은 짧은 이야기 진행, 수련 요청/결과/재도전 연결, 트릴 수행 평가, 수련 진도 저장이다.

첫 구현은 `Tutorial → 기존 Gameplay → Tutorial 피드백`으로 트릴 한 과정을 끝낸다. 새로운 게임플레이 씬, 대화 그래프 프레임워크, 별도 판정 엔진, 서버, 네트워크, 멀티플레이는 도입하지 않는다. 아래 신규 타입·파일·콘텐츠는 모두 **향후 구현 제안**이다.

## 2. 현재 구조 조사

### 2.1 실제 진입 경로, 씬과 UI

```text
Title: 대문 진입 영역 클릭 / Enter
  → ModeSelection: 기본 선택은 자유 연습(index 1)
  → 왼쪽 이동: 비급 전수(index 0)
  → Enter 제출 / Space / '비급 전수' 제목 클릭
  → Tutorial
  → '모드 선택' 버튼 / Escape / 게임패드 취소
  → ModeSelection
```

- [MainMenuSceneBuilder](../Assets/Game/Scripts/Editor/SceneGeneration/MainMenuSceneBuilder.cs)는 Title에 `MainMenuEntranceScreen`, ModeSelection에 `MainMenuScreen`을 조립한다. [MainMenuScreen](../Assets/Game/Scripts/Runtime/UI/MainMenuScreen.cs)의 제출/클릭은 [MainMenuActions.StartGame](../Assets/Game/Scripts/Runtime/Core/MainMenuActions.cs)으로 이어진다. index 0이면 곡 선택 사전 준비를 취소하고 Tutorial을 Single로 로드한다.
- [DojoModeView](../Assets/Game/Scripts/Runtime/UI/Views/DojoModeView.cs)의 초기 선택은 index 1이다. 비급 전수 제목 자체가 [DojoArcadeLayoutBuilder](../Assets/Game/Scripts/Editor/UILayout/DojoArcadeLayoutBuilder.cs)의 `tutorialEntry` 버튼이다. 보이지 않는 별도 시작 버튼을 추가할 필요가 없다.
- [Tutorial.unity](../Assets/Game/Scenes/Tutorial.unity)에는 `Lesson Room` 배경, 비어 있는 `Story Stage`, `Story Introduction`/제목, 복귀 버튼, 카메라, 음악, EventSystem이 저장되어 있다. [TutorialLayoutBuilder](../Assets/Game/Scripts/Editor/UILayout/TutorialLayoutBuilder.cs)에도 배우와 대화는 이후 여기에 저작한다는 주석이 있다. **현재 이 씬에는 사형, 대사 진행, 과제 선택, 레인, 노트, 수련 시작/결과 UI가 없다.**
- [TutorialScreen](../Assets/Game/Scripts/Runtime/UI/TutorialScreen.cs)은 약 0.45초 도입 표시와 복귀 입력을 처리한다. [TutorialCompositionRoot](../Assets/Game/Scripts/Runtime/Core/TutorialCompositionRoot.cs)는 AppFlow 준비 취소, `TitleMusicPlayer` 초기화, ModeSelection 복귀를 담당한다. `RhythmSession`을 생성하거나 시작하지 않는다.
- [EditorBuildSettings](../ProjectSettings/EditorBuildSettings.asset)에는 Title을 첫 씬으로 하는 9개 씬이 등록되어 있으며 ModeSelection과 Tutorial도 포함된다. 기존 씬을 재사용하면 신규 씬 등록은 필요 없다.
- Bootstrap과 SongSelection 직접 실행은 자유 연습 진입점이다. Tutorial 수련의 실제 접근성을 이 경로만으로 검증해서는 안 된다. 직접 씬을 확인할 때는 `Start Play Mode From Title`을 끈다.

### 2.2 여자 사형 에셋, 표현과 애니메이션

| 구분 | 확인한 자원과 동작 | 재사용 범위 |
| --- | --- | --- |
| 기존 자세 이미지 | `Assets/Game/Art/AlphaDojo/SeniorStudent.png`, `SeniorStudent.asset`, `SeniorStudent0.asset`~`5.asset` | 기존 메뉴 자세/이동·도착 표현의 참조를 보존한다. |
| 현재 애니메이션 원본/출력 | `Assets/Game/Art/AlphaDojo/Training/SeniorSource.png`, `SeniorFrames.png`, `SeniorFrame0.asset`~`35.asset` | 출력 시트를 직접 확인했다. 6열 × 6행, 프레임당 128px의 2D 픽셀 캐릭터다. |
| 애니메이션 자원 | 같은 폴더의 `Senior.controller`, `SeniorIdle.anim`, `SeniorPunch.anim`, `SeniorKick.anim`, `SeniorReady.anim`, `SeniorPoint.anim`, `SeniorApprove.anim` | 대기/주먹/발차기/준비/가리키기/승인 표현을 그대로 사용한다. |
| 표시 컴포넌트 | `DojoFigureGraphic : Image`, `DojoActorAnimation`, Unity `Animator` | Canvas UI 스프라이트다. 새 3D 모델이나 골격 애니메이션이 필요하지 않다. |
| 조립 위치 | `AlphaDojoLayoutBuilder`와 `DojoAnimationAssetBuilder.Configure` → ModeSelection의 사형/수련생 | Tutorial에 필요한 배우만 에디터에서 별도 배치한다. 전체 메뉴를 복제하지 않는다. |

[DojoAnimationAssetBuilder](../Assets/Game/Scripts/Editor/UILayout/DojoAnimationAssetBuilder.cs)는 원본을 고정 프레임으로 굽고 `Image.m_Sprite`를 바꾸는 AnimationClip을 만든다. [DojoActorAnimation.Sample](../Assets/Game/Scripts/Runtime/UI/Views/DojoActorAnimation.cs)은 Animator 속도를 0으로 두고 전달받은 시각으로 자세를 샘플링한다. 이는 명시적인 연출 시계로 재사용할 수 있지만 자동으로 음악 BPM에 동기화되는 수련 기능은 아니다.

[DojoLessonSequence](../Assets/Game/Scripts/Runtime/UI/Views/DojoLessonSequence.cs)는 메뉴의 `Waiting → Demonstration → Delay → Imitation → Approval` 자동 순환이다. 사형의 동작을 수련생이 약 0.35초 뒤 따라 하고 승인 애니메이션 후 `CompletedSets`가 증가한다. `Signal` 값은 호환성 때문에 남았지만 현재 진입하지 않는다. 호출자는 `DojoModeView`이고 진행 기준은 `Time.unscaledDeltaTime`이다.

**이 자동 순환은 사용자 리듬 입력을 평가하지 않는다.** `CompletedSets`도 수련 이수 기록이 아니다. 자세와 클립은 재사용하되 수련 성공 여부를 이 값이나 애니메이션 종료로 판정하지 않는다. 조사한 조립 경로에는 대사 데이터·입 모양·음성 대화 동기화가 연결되어 있지 않다. 첫 수련은 기존 자세와 짧은 텍스트만 사용한다.

### 2.3 노트 데이터와 생성/표시

- [ChartData/NoteData](../Assets/Game/Scripts/Domain/ChartData.cs): `Lane`, `Kind(Tap/Hold)`, `StartTime`, `EndTime`이다. 레인은 0부터, 시간은 double 초 단위다. 노트 배열은 시작 시각 순이며 서로 다른 레인의 같은 시각은 허용한다. 같은 레인의 겹침과 끝/다음 시작이 정확히 같은 경우는 거부한다. 마지막 노트 끝은 `CompletionTime`보다 작아야 한다.
- [RhythmChart](../Assets/Game/Scripts/Runtime/Gameplay/RhythmChart.cs)는 직렬화된 노트와 종료 시각을 가진 ScriptableObject다. [SongDefinition](../Assets/Game/Scripts/Runtime/Gameplay/SongDefinition.cs)이 음원·모드·채보·템포를 묶는다. `SongAssetAdapter`는 [PlayableSong.cs](../Assets/Game/Scripts/Runtime/Content/PlayableSong.cs)에 있으며 기존 에셋을 `SongDocument`/`PlayableSong`으로 변환한다.
- [SongDocument](../Assets/Game/Scripts/Application/SongDocument.cs)는 재생용 스냅샷 원본이고, [SongProject/ChartDocument](../Assets/Game/Scripts/Application/SongProject.cs)는 편집용 프로젝트/채보다. `ChartPlaybackAdapter`가 선택한 채보를 재생 문서로 바꾼다. [채보 계약](chart-format-v1.md)의 파일명과 달리 현재 저장 버전은 **v3**이며 `SongProjectJson`/`FileSongProjectStore`가 처리한다.
- [NotePresenter](../Assets/Game/Scripts/Runtime/Presentation/NotePresenter.cs)는 `TapNote.prefab`/`HoldNote.prefab`을 채보 수에 맞게 인스턴스화하고, 수와 종류가 맞으면 기존 뷰를 재사용한다. 노트별 위치는 `IScrollTimeline`으로 계산한다. 이는 동적 채보 수에 따른 생성이므로 별도 스포너/풀 시스템을 추가하지 않는다.
- [TestContentBuilder](../Assets/Game/Scripts/Editor/SceneGeneration/TestContentBuilder.cs)는 탭·동시치기·홀드·템포 변경 검증 채보를 이미 생성한다. 제품용 트릴 수련을 이 검증 채보에 덮어쓰지 않는다. 조사한 콘텐츠 연결에는 트릴 수련 정의나 그 정의를 재생하는 경로가 없다.

### 2.4 판정, 입력 기록, 동시치기

실제 경로는 `LaneKeyboardInput → RhythmSessionCoordinator → RhythmSession → RuleSession`이다. `RhythmSession`에는 구형 호환 생성자도 남아 있지만 현재 일반 플레이는 `PlayRules`를 전달하는 경로를 사용한다.

- [LaneKeyboardInput](../Assets/Game/Scripts/Runtime/Input/LaneKeyboardInput.cs)은 Unity Input System의 lane별 `performed/canceled` 이벤트와 `c.time`을 전달한다. 누른 채 반복되는 Press는 막고, 재시작 때 이미 눌린 키는 한 번 놓을 때까지 차단한다. [FourLaneMode.asset](../Assets/Game/Settings/FourLaneMode.asset)의 기본 키는 D/F/J/K, 시작/재시작은 Space, 복귀 입력은 Escape다. 사용자 지정 키는 `PlayKeyBindings`를 통해 적용된다.
- [SongClock](../Assets/Game/Scripts/Runtime/Audio/SongClock.cs)과 [GameplayTiming](../Assets/Game/Scripts/Application/GameplayTiming.cs)이 입력 시각을 곡의 논리 시각으로 맞춘다. 논리 시각은 곡 시각에서 입력 보정값을 뺀 값이며 입력 어댑터의 변환 결과에는 보정이 이미 포함된다. 새 수련 코드가 보정값을 다시 빼거나 `Time.time`으로 따로 판정하면 안 된다.
- [PlayRules](../Assets/Game/Scripts/Domain/PlayRules.cs)는 버전 4다. 기본 Warmup은 Perfect ±50ms/Good ±100ms, Sparring은 ±16ms/±50ms다. 이는 코드 기본값이며 저장된 사용자 설정이 항상 같다는 뜻은 아니다. 등급은 Perfect/Good/Miss이고 Good/Miss의 표시 문구는 `Hmm...`/`Nope.`다.
- [RuleSession.PressLane](../Assets/Game/Scripts/Domain/RuleSession.cs)은 해당 레인의 미판정 시작점 중 판정창 안에서 가장 가까운 노트를 고른다. 입력 없는 노트는 스케줄 sweep에서 Miss가 된다. 빈 곳의 입력을 일반 플레이에서 별도 오입력 감점으로 계산하지 않는다.
- **동시치기 자체는 지원한다.** 같은 시각의 다른 레인 노트가 각각 판정되며 홀드를 누르면서 다른 레인을 칠 수도 있다. 반면 여러 노트를 하나의 성공/실패 단위로 묶거나, 양손 입력 간 시간차를 별도로 평가하는 수련 규칙은 없다. `TimingSampleSet`의 같은 시각 표본 묶기는 보정값 추천을 위한 것으로 동시치기 합격 판정이 아니다.

입력/결과의 보존 범위를 구분해야 한다.

| 현재 자료 | 실제 포함 범위 | 포함하지 않는 것 |
| --- | --- | --- |
| `RuleSession.history` | 홀드 시점의 누름 상태를 복원하기 위한 private lane/time/down 이력. 세션 초기화 시 삭제 | 공개 리플레이 API, 디스크 저장, 수련용 전체 입력 기록 |
| `Judged`, `HoldStarted` | 실시간 노트 판정과 홀드 시작 알림 | 빈 곳/잘못된 레인 입력 전체. `JudgmentEvent`만으로 홀드 유지 tick의 부위·시각을 모두 복원하는 계약도 없음 |
| `NoteJudgmentRecord` | 노트의 Start/End 판정 | 유지 tick별 영구 기록 |
| `PlayResult`, `PlayNoteResult` | 종료 후에도 유지되는 채보·템포·노트별 시작/끝·전체 집계·최대 콤보 스냅샷 | 원시 키 입력 전체, 기본 경로에서의 유지 tick별 목록, 앱 재실행 후 결과 복원 |

트릴의 타이밍 평가는 기존 노트 결과로 충분하다. 다만 양쪽 키를 매번 함께 두드리는 입력이나 불필요한 추가 입력까지 가르치려면 **판정되지 않은 Press도 포함한 시도 한정 입력 관찰**이 추가로 필요하다. 이것을 기존 입력 기록이 이미 공개돼 있는 것으로 간주하지 않는다.

### 2.5 롱노트 시작·유지·해제

| 부분 | 현재 규칙 |
| --- | --- |
| 시작 | 탭과 같은 Perfect/Good 판정창. 시작 판정은 한 번 기록된다. |
| 유지 | `TempoMap`으로 시작 beat 이후 `HoldCheckBeats` 간격마다 검사한다. 기본 0.25 quarter-note beat다. 그 시각의 누름 상태에 따라 Perfect/Miss를 적용한다. BPM 변경을 넘는 홀드도 처리한다. |
| Automatic 끝 | 끝 시각에 해당 키가 눌려 있으면 Perfect, 아니면 Miss. 끝에서 반드시 키를 놓을 필요는 없다. 시작 Miss/중간 해제 뒤 재입력으로 이후 유지와 끝을 맞힐 수 있다. 중간 해제는 콤보를 끊는다. |
| Release 끝 | 해제 시각을 끝 판정창으로 평가한다. 너무 이른 해제는 즉시 끝 Miss를 한 번 적용하고 복구 불가 상태로 만든다. 그 뒤 남은 유지 검사도 Miss다. 시작 Miss 역시 복구할 수 없다. 정상/늦은 해제와 시간초과 처리가 있다. |
| 기록/집계 | 시작·유지·끝이 점수/콤보/체력 집계에 참여한다. 따라서 현재 규칙의 `SessionSnapshot.TotalNotes`는 화면에 보이는 노트 개수와 같다고 가정하면 안 된다. 기본 결과 스냅샷에는 시작/끝과 전체 집계가 남는다. |

근거는 `RuleSession`, `PlayRulesTests`, `CoordinatorTests`다. 구형 `RhythmSession` 호환 경로의 홀드 집계와 혼동하지 않는다. 첫 트릴은 Tap만 사용하므로 홀드 규칙을 변경하거나 새 홀드 시스템을 만드는 작업은 포함하지 않는다.

### 2.6 BPM·박자표·스크롤·정지 지원 범위

| 기능 | 확인 결과 | 첫 트릴에 대한 결정 |
| --- | --- | --- |
| 고정/변경 BPM | [TempoMap](../Assets/Game/Scripts/Domain/TempoMap.cs)이 시각↔beat 변환 및 구간 BPM을 제공한다. 첫 지점은 0초, BPM은 양수다. 재생/홀드/스크롤에서 사용한다. | 그대로 사용. 첫 과제는 고정 BPM. |
| 박자표/변박 | [BeatGridTiming](../Assets/Game/Scripts/Domain/BeatGridTiming.cs), `SongProject.Grid`, v3 JSON에 원점/박자표가 있고 편집기 마디·스냅을 지원한다. 그러나 `SongDocument`/`PlayableSong`에는 Grid가 전달되지 않고 [PlayfieldPresenter](../Assets/Game/Scripts/Runtime/Presentation/PlayfieldPresenter.cs)의 마디선은 `BeatsPerMeasure = 4` 고정이다. | 박자표 저장 기능을 재개발하지 않는다. 첫 과제는 4/4. 변박을 게임 화면에 정확히 표시하는 전달 경로는 후속 작업. |
| 스크롤 | [ScrollTimeline](../Assets/Game/Scripts/Application/ScrollTimeline.cs)의 Constant/Bpm, [ScrollSettings](../Assets/Game/Scripts/Runtime/Gameplay/ScrollSettings.cs)의 속도 배율이 있다. BPM 모드는 구간 속도를 적분한다. | 기존 설정을 사용. 속도 변경과 판정/음악 속도를 구분한다. |
| 독립 SV 이벤트/스크롤 정지 | 조사한 채보 스키마와 `SongDocument`에는 구간별 독립 속도/정지 이벤트가 없다. 현재 스크롤 속도와 BPM은 0 이하를 거부한다. | 미지원으로 기록하고 첫 과제에서 제외. BPM=0이나 multiplier=0으로 우회하지 않는다. |
| 사용자 일시정지/재개 | `RhythmSessionCoordinator`, `SongClock`, `GameplayPauseMenu`에 존재한다. 예약 재개, 키 해제, 포커스 상실 처리가 있다. | 그대로 사용. 이것을 채보의 정지 기믹으로 간주하지 않는다. |

스크롤 배율은 노트의 이동 속도이고 연습곡 재생 속도가 아니다. 편집기 미리듣기의 절반 속도/루프가 존재한다는 사실도 일반 Gameplay에서 수련 속도 조절이 완성됐다는 근거가 되지 않는다.

### 2.7 SceneBuilder와 UI 재사용 경계

| 기존 파일/구성 | 역할 및 계획 |
| --- | --- |
| `Editor/SceneGeneration/SceneBuilder.cs` | Build All에 Tutorial/ModeSelection이 이미 포함된다. 기존 Build Tutorial을 사용한다. 단독 Open 메뉴는 추가하지 않는다. |
| `Editor/UILayout/TutorialLayoutBuilder.cs` + `Runtime/UI/Views/TutorialView.cs` | 스토리 배우·대사·진행/재시도 버튼을 저작할 자리다. 고정 구성은 빌더가 생성하고 View에 명시적 참조를 둔다. |
| `Editor/SceneGeneration/TutorialSceneBuilder.cs` + `Runtime/Core/TutorialCompositionRoot.cs` | 서비스·음악·입력·화면을 연결한다. UI Screen에서 AppFlow를 새로 조립하지 않는다. |
| `Editor/UILayout/AlphaDojoLayoutBuilder.cs`, `DojoAnimationAssetBuilder.cs` | 사형/수련생 및 기존 애니메이션 조립의 원본. Tutorial은 필요한 배우만 사용한다. |
| `Editor/SceneGeneration/SceneDependencyAssembler.cs`, `GameValidators.cs` | 직렬화 참조 연결과 생성 씬 검증 패턴을 따른다. 참조 누락을 런타임 검색/생성으로 숨기지 않는다. |
| `GameplayHudLayoutBuilder`, `GameplayHudView`, `RhythmHud`, `GameplayUiController` | 이미 있는 레인 안내·판정·콤보·체력·타이밍 피드백·일시정지를 사용한다. 첫 과제에 별도 플레이 HUD는 필요하지 않다. |
| `UiElements`, `UiFontSettings`, `DojoInkGraphic`, `DojoActorAnimation` | 현재 UI/폰트/먹 배경/배우 표현을 재사용한다. 새 UI 툴킷을 도입하지 않는다. |

여기서 `Editor/`, `Runtime/`의 기준은 `Assets/Game/Scripts/`이다. 씬 YAML만 수정하면 재생성 때 사라지므로 지속되는 수정은 빌더가 원본이어야 한다. 런타임은 구독·진행·연출을 담당한다. 기존 고정 에셋을 수련 진입 때마다 다시 굽거나 생성하지 않는다.

### 2.8 현재 저장 방식

[SelectionPreferences](../Assets/Game/Scripts/Runtime/Services/SelectionPreferences.cs)는 `RhythmDojo.Selection.v1` PlayerPrefs 키에 JSON을 저장한다. 선택 옵션, 즐겨찾기, 최고 점수/정확도/콤보, 성공/실패 및 Gauge/Challenge 달성 기록이 들어간다. [RecordIdentity](../Assets/Game/Scripts/Runtime/Content/RecordIdentity.cs)는 채보 내용 revision과 판정 규칙 버전을 포함해 비교 기록을 구분한다.

[GameplayCompositionRoot.SaveRecord](../Assets/Game/Scripts/Runtime/Core/GameplayCompositionRoot.cs)는 Completed 상태에서 기록을 저장하며 Editor playtest는 제외한다. `AppFlowController.LastResult`는 메모리 값이다. `FileSongProjectStore`가 저장하는 채보 프로젝트는 수련 진도 저장소가 아니다.

조사 범위에서 **수련 ID, 이수 단계, 이야기 진행, 잠금 해제, 재실행 후 이어하기를 저장하는 타입/서비스/연결은 확인되지 않았다.** `DojoLessonSequence.CompletedSets`는 저장되지 않는다. 일반 곡 기록을 수련 완료 기록으로 대체해서는 안 된다.

### 2.9 기존 테스트와 실제 사용자 접근 검증

| 범위 | 기존 근거 | 한계/확장 지점 |
| --- | --- | --- |
| 메뉴→Tutorial→복귀 | `PlayMode/MainMenuFlowTests.LessonRespondsToRhythmAndCompletesWithoutLeavingTitle` | 이름은 과거 이름이다. 현재 본문은 제출/제목 클릭으로 Tutorial에 들어갔다가 버튼/Escape로 돌아오는 테스트다. 리듬 과제 완료 검사가 아니다. 기존 이름을 바꾸지 않고 진입 회귀를 유지한다. |
| 사형 시범/따라하기 연출 | `MainMenuFlowTests.IdleVariationsTakeTurnsRespondAndYieldToInputAndTravel`, `EditMode/DojoAnimationAssetsTests`, `DojoIdleVariationTests` | 메뉴 자동 연출과 프레임/발 고정 등을 검사한다. 사용자 수행 합격의 근거로 쓰지 않는다. |
| 노트/동시치기/홀드 | `Domain/RhythmSessionTests`, `PlayRulesTests` | 동시치기와 홀드 병행, 시작/끝 경계, 재입력, BPM 경계, 실패 처리 등을 재사용한다. |
| 보정/시각/스크롤 | `Domain/CoordinatorTests`, `ScrollTimelineTests`, `TimingSampleSetTests` | 포커스/일시정지/보정값/프레임 간격 독립성을 보존한다. |
| 저장/편집 | `EditMode/SelectionPreferencesTests`, `PlayRecordsAndChartsTests`, `SongProjectTests`, `BeatGridEditingTests` | 일반 곡 기록과 v3 계약은 있으나 수련 진도 검사는 없다. |
| Gameplay 실제 입력/결과 | `PlayMode/GameplayFlowTests`, `GameplayJudgmentTests`, `GameplayPauseFlowTests`, `ResultsFlowTests` | 현재 사용자 입력/종료 경로의 회귀 범위다. 수련 전용 복귀/재시도/기록 격리 검사가 추가로 필요하다. |
| 씬 재생성 | `EditMode/SceneRegenerationTests`, `GeneratedSceneSnapshot`, `AuthoredCompositionTests` | 기존 자원과 GUID, 반복 생성, 고정 구성을 보존하는 검증을 확장한다. |

위 테스트 경로의 기준은 `Assets/Game/Tests/`다. `RemediationPlayerProbe.VerifyTrainingModes`에도 Tutorial 진입/복귀 검증은 있지만 수련 플레이는 없다. 테스트 코드의 존재와 이번 조사에서의 실행 통과는 구분한다.

문서의 과거 설명도 주의해야 한다. [alpha-demo-ui.md](alpha-demo-ui.md)는 Space를 네 번 치는 입문 수련을 설명하지만 현재 Tutorial/진입 코드와 일치하지 않는다. 해당 기능을 현재 구현된 것으로 채택하거나 복원하지 않는다. [verification.md](verification.md)의 현재 사용자 경로와 실제 소스를 우선한다.

## 3. 재사용할 것과 실제 부족한 것

**새로 만들지 않을 것:** 레인 입력/키 재지정, DSP 곡 시계, 입력 보정, 탭·홀드 판정, 동시치기 노트, 체력·콤보·점수, TempoMap, Constant/Bpm 스크롤, 노트 프리팹/표시기, Gameplay/HUD/일시정지, 결과 스냅샷, 사형 스프라이트/애니메이션, Tutorial 진입/복귀, 채보 v3 저장.

| 우선순위 | 실제 추가할 기능 | 범위 제한 |
| --- | --- | --- |
| 첫 트릴 | 수련 정의와 도입→수행→피드백→완료/재시도 상태 | 한 수련의 선형 흐름. 노드 그래프/조건식 엔진/범용 캠페인 시스템 없음. |
| 첫 트릴 | 수련용 시작·완료·중단·재도전 경로 | 기존 Gameplay를 사용하고 Tutorial로 돌아온다. 일반 Results 화면을 개조할 필요 없음. |
| 첫 트릴 | 정해진 두 레인의 교대 수행 평가와 실패 설명 | 타이밍 등급은 기존 판정 사용. 교대 순서/추가 Press만 수련에서 확인. |
| 첫 트릴 | 시도 한정 논리 시각 Press 관찰 | pause/준비/종료/재시작 경계를 지킨다. 전체 키보드 로깅·리플레이 파일은 만들지 않음. |
| 첫 트릴 | 수련 완료의 로컬 저장과 재진입 복원 | 일반 최고 기록 키와 분리. 중간 노트부터 재개하지 않음. |
| 첫 트릴 | 사형·대사·시작/재시도·완료 표시의 authored 구성 | 기존 Tutorial 빌더/View 확장. 이번 조사에서는 실제 UI를 생성하지 않음. |
| 후속 | 여러 수련의 순서/선행 이수, 세밀한 동시치기 평가, 홀드 유지 tick별 설명 | 해당 수련을 추가할 때 필요한 계약만 확장. |
| 별도 범위 | 변박 마디선의 재생 전달, 채보 정지/SV, 속도 가변 연습, 음악 동기 시범 | 첫 트릴에 필요하지 않음. 이미 있는 BPM/박자표 저장과 구분. |

## 4. 최소 데이터와 책임 제안

기존 `SongDefinition → SongDocument → PlayableSong → PlayRequest`를 재사용한다. 새로운 수련 노트 형식이나 `rdchart` 버전을 만들지 않는다. 첫 콘텐츠는 기존 형식의 `RhythmChart`/`SongDefinition` 에셋을 직접 참조하며 일반 SongCatalog에 넣지 않는다.

| 제안 자료 | 최소 필드/값 | 소유권과 수명 |
| --- | --- | --- |
| `TrainingLesson` (Runtime의 ScriptableObject) | 안정된 `LessonId`, `DefinitionVersion`, 기존 `SongDefinition` 참조, 짧은 도입 대사 배열, 성공/재시도 대사 | Tutorial의 직렬화 참조. 노트/템포/모드/음원을 다시 저장하지 않는다. 첫 버전에는 한 과제만 참조하고 별도 카탈로그는 만들지 않는다. |
| `TrainingSession` (Application의 일반 C# 객체) | LessonId/버전, 시도 번호, `Introduction / Ready / Playing / Feedback / Completed` 상태, 이번 시도 평가값 | AppFlow가 소유해 씬 전환을 견딘다. Unity 오브젝트나 파괴된 Screen 콜백을 소유하지 않는다. 재시도마다 시도 자료를 초기화한다. |
| `TrainingPress` / `TrainingEvaluation` (Domain 값) | Press는 lane/논리 시각/순번. 평가는 합격 여부와 원인(미판정·순서·추가 입력·중단) 및 해당 노트/입력 위치 | 실제 수행 구간에서만 메모리에 수집. 저장용 원시 입력 파일은 없음. 판정 등급/오차는 기존 `NoteJudgmentRecord`를 사용한다. |
| `TrainingProgress` (Runtime 저장 DTO) | 저장 형식 버전, LessonId, DefinitionVersion, Completed | 별도 PlayerPrefs 키(제안: `RhythmDojo.Training.v1`)의 작은 JSON. 최신 도입/대사 커서는 저장하지 않고 미완료는 도입부터 재시작한다. 첫 버전에는 점수표·경험치·재화·해금 목록이 필요 없다. |

첫 트릴의 합격 규칙은 단일 순수 `TrillTrainingEvaluator`로 둔다. 입력은 기존 Domain의 `ChartData`, `NoteJudgmentRecord`, 세션 종료 상태와 수련 Press 값이다. Runtime의 `PlayResult`/Unity 타입을 Domain으로 가져오지 않는다. 패턴 플러그인 인터페이스나 추상 수련 클래스 계층은 만들지 않는다. 대상 두 레인과 순서는 채보에서 읽고, 채보가 Tap만으로 두 레인을 번갈아 사용하며 시각이 엄격히 증가하는지 콘텐츠 검증에서 확인한다. lane 목록/노트 시각을 수련 정의에 중복 저장하지 않는다.

**첫 과제의 제안값(미구현, 난이도는 플레이 후 조정):** FourLaneMode의 lane 1/2를 사용하는 120 BPM, 4/4, 8분음표 간격(0.25초) Tap 16개, 4박의 준비 구간. 안내 키 문구는 F/J로 고정하지 않고 실제 `PlayKeyBindings`에서 읽는다. 정상 종료, 모든 Tap 시작 판정이 Perfect 또는 Good, 수행 구간 Press가 채보 레인 순서와 1:1로 일치하면 합격한다. 첫 버전은 Miss/추가 Press를 허용하지 않고 실패해도 끝까지 연습한 뒤 재시도할 수 있게 한다.

판정은 기존 `PlayRules` 타입으로 구성한 수련 요청용 값을 사용한다. 첫 과제는 Warmup의 기본 판정창(±50ms/±100ms), Gauge/Continue를 명시해 사용자의 직전 Challenge 설정이나 변경한 판정창이 섞이지 않게 하고, 사용자 전역 규칙은 변경하지 않는다. 시각 보정과 키 설정은 사용자 설정을 사용한다. 스크롤은 기존 표시 설정을 사용하므로 음악 BPM이나 채보 시각을 바꾸지 않는다. 과제 내용/평가 기준 또는 적용하는 판정 규칙 버전이 바뀌면 DefinitionVersion을 올려 기존 완료가 새 기준을 자동 충족했다고 표시하지 않는다.

Press 평가 구간은 첫 노트 시작−GoodWindow부터 마지막 노트 시작+GoodWindow까지(경계 허용 오차 포함)다. 준비 구간의 예비 입력, 일시정지 중 입력, 끝난 뒤 입력은 세지 않는다. 구간 안의 **모든 플레이 레인 Press**를 기록하므로 사용하지 않는 레인/양손 난타도 추가 입력으로 드러난다. 레인 입력의 기존 중복 Press 억제는 그대로 둔다. 타이밍을 별도 재판정하거나 새 최근접 노트 매칭을 구현하지 않는다.

노트가 가까워 판정창이 겹칠 수 있으므로, 초기 채보는 인접 노트 판정창이 겹치지 않게 검증한다. 제안한 0.25초 간격은 기본 Good ±0.10초에서 이 조건을 만족한다. 높은 BPM/세분 수련은 이후 별도 경계 검토 대상으로 남긴다.

## 5. 트릴 한 과정을 먼저 완성하는 구현 순서

아래 기존 파일 경로는 `Assets/Game/Scripts/`, 테스트 경로는 `Assets/Game/Tests/` 기준이다. 신규 파일은 이름 제안이며 이번 작업에서는 생성하지 않는다. 기존 파일·필드·메서드 이름은 바꾸지 않는다.

### 단계 1 — 과제/평가 계약과 한 개의 콘텐츠 확정

- **신규 제안:** `Runtime/Content/TrainingLesson.cs`, `Domain/TrillTrainingEvaluator.cs`, `Application/TrainingSession.cs`. Press/평가 값은 evaluator 파일에 작게 함께 두어 불필요한 파일/서비스를 늘리지 않는다.
- **재사용:** `ChartData`, `PlayRules`, `SongDefinition`, `RhythmChart`, `SongAssetAdapter`, FourLaneMode. 규칙의 구현은 수정하지 않는다.
- **향후 콘텐츠:** `Assets/Game/Settings/Training/TrillIntro.asset`, `TrillSong.asset`, `TrillChart.asset`와 `.meta`. 적절한 음원은 실제 박자/길이를 검증해 연결한다. 현재 Title BGM이 120 BPM 연습음원에 적합하다고 가정하지 않는다. 음원이 필요하면 에디터에서 고정 파일로 준비하고 런타임 합성기를 만들지 않는다.
- **검증:** 신규 `Domain/TrillTrainingTests.cs`의 Fast 사례로 성공, 누락, 역순, 추가 입력, 동시 난타, 중단을 검사한다. 노트 판정 테스트를 복제하지 않고 실제 기존 세션 결과와 Press를 평가한다. 기존 `ContentTests`에 필요한 콘텐츠 유효성 사례를 확장하고 `ChartData.Validate`/`SongDefinition.Validate`를 통과시킨다.

### 단계 2 — 시도 한정 입력 관찰을 기존 판정 경로에 연결

- **변경:** `Application/RhythmSessionCoordinator.cs`, `Runtime/Gameplay/RhythmGameController.cs`, 필요한 값 선언은 단계 1 자료를 사용한다.
- 준비/일시정지/예약 재개 cutoff/비플레이 상태를 통과해 세션에 전달되는 Press의 **이미 보정된 논리 시각**을 선택적으로 알리는 작은 관찰 지점을 둔다. `ILaneInput`을 대체하거나 private `RuleSession.history`를 공개하지 않는다. observer 호출의 위치 때문에 현재 입력→판정→실패 이벤트 순서가 바뀌지 않게 한다.
- 수련 중에만 구독하고, restart/reset은 수집 자료를 지우며 dispose/씬 이탈은 구독을 해제한다. 첫 트릴은 Press 순서만 필요하므로 Release 전체 로그와 범용 리플레이 저장은 추가하지 않는다.
- **검증:** 기존 `Domain/CoordinatorTests`를 확장해 빈 레인 Press도 관찰되는지, pause/재시작/포커스/종료 후 입력이 섞이지 않는지, ±보정값과 프레임 지연에서도 판정에 사용한 시각과 동일한지 확인한다(Fast). 기존 동시치기/홀드 판정 관련 사례를 함께 실행한다. 실제 InputAction 전달은 `GameplayFlowTests`의 관련 SceneIntegration 범위에서 확인한다.

### 단계 3 — 기존 Gameplay의 수련 시작/종료/복귀 연결

- **변경:** `Runtime/Core/AppFlowController.cs`, `Runtime/Core/GameplayCompositionRoot.cs`, `Runtime/Core/TutorialCompositionRoot.cs`. `PlayRequest`와 `PlayResult`는 기존 형식을 우선 그대로 사용한다.
- AppFlow가 선택적 `TrainingSession`과 이번 수련 참조를 소유하고 수련 진입 함수를 제공한다. `SongAssetAdapter.Load`/기존 로딩 소유권/`PlayRequest`/Gameplay 초기화를 재사용한다. 선택 중인 일반 곡이나 전역 `Rules`를 바꾸는 우회 호출은 하지 않는다.
- 일반 플레이는 기존 결과 경로를 유지한다. 수련은 종료 전에 기존 `PlayResult`를 만든 뒤 평가값을 확보하고 입력/음원을 정리하여 Tutorial의 피드백 상태로 돌아온다. `SaveRecord`는 수련 요청을 제외해 일반 기록을 오염시키지 않는다. pause 메뉴에서 나가면 완료가 아닌 중단으로 돌아온다.
- `IEditorPlaytestService.PlayAsync`에는 이미 문서 재생/복귀 기능이 있으나 `returned != null`이면 `IsEditorPlaytest`가 되고 기본 결과 전환/재시도 정책도 달라진다. **수련을 에디터 테스트로 위장하지 않는다.** 기존 소유권/씬 로드 패턴을 공유하되 수련 문맥은 명시적으로 구분한다.
- 일반 모드 진입/메뉴 이탈/로드 실패 때 남은 수련 문맥을 정리한다. 실패·재시도 시 새 `PlayableSong`을 준비하고 이미 Dispose된 요청을 재사용하지 않는다. 파괴된 TutorialScreen을 복귀 콜백으로 캡처하지 않는다.
- **검증:** `GameplayFlowTests`/`GameplayPauseFlowTests`의 해당 회귀와 신규 `PlayMode/TrainingFlowTests.cs`에서 수련 시작→완료→Tutorial, 중단, 로딩 실패, 이중 클릭, 재시도, 일반 모드 재진입을 검사한다(SceneIntegration). 일반 곡/에디터 playtest의 결과·복귀가 유지되는지와 일반 기록 키 불변을 확인한다. 공통 화면 이동 변경이므로 Smoke를 추가한다.

### 단계 4 — Tutorial에 짧은 이야기와 사형을 연결

- **변경:** `Runtime/UI/TutorialScreen.cs`, `Runtime/UI/Views/TutorialView.cs`, `Editor/UILayout/TutorialLayoutBuilder.cs`, `Editor/SceneGeneration/TutorialSceneBuilder.cs`, `Runtime/Core/TutorialCompositionRoot.cs`.
- 도입 대사→시작→수행→사형 피드백→재시도/완료의 선형 UI를 만든다. 사형은 기존 Idle/Point/Ready/Approve를 사용한다. 첫 버전의 설명 동작은 연출이며, 음악에 맞춰 노트를 대신 치는 자동 시범은 구현하지 않는다.
- 배우 조립은 `DojoAnimationAssetBuilder`의 기존 구성을 읽어 재사용한다. 현재 `Configure(DojoModeView)`가 메뉴 전체에 결합되어 있으므로 필요하면 같은 클래스에 작은 배우 연결 진입점만 추가한다. 기존 Configure와 메뉴 동작은 유지하며 Tutorial에 DojoModeView 전체를 붙이지 않는다. `AlphaDojoLayoutBuilder` 수정은 실제 공통 조립 추출이 필요할 때만 한다.
- 고정 대사창/버튼/배우/Animator는 빌더에서 만들고 참조를 검증한다. Screen은 서비스에 요청하고 상태를 표시한다. 초기화 반복과 OnDestroy에서 구독을 정리한다.
- **생성 결과:** 기존 `Assets/Game/Scenes/Tutorial.unity`와 필요한 신규 에셋/`.meta`만 재생성한다. Gameplay의 기존 HUD를 사용하므로 첫 버전에서 새 Gameplay 레이아웃은 필요하지 않다. 공유 배우 조립을 변경한 경우에만 ModeSelection도 재생성한다.
- **검증:** 실제 대사 진행/시작/재시도/복귀 클릭과 키 입력을 `TrainingFlowTests` 및 기존 `MainMenuFlowTests`로 확인한다(SceneIntegration). `AuthoredCompositionTests`는 초기화/재진입 때 고정 배우/버튼이 교체·중복되지 않는 실제 회귀를 확장한다. `SceneRegenerationTests`/`GeneratedSceneSnapshot`으로 새 콘텐츠도 복원 범위에 포함하고 반복 생성/GUID/누락 참조를 확인한다(AssetGeneration). 메뉴 배우 변경 시 기존 애니메이션 검사도 실행한다.

### 단계 5 — 수련 완료 저장과 재진입 복원

- **신규 제안:** `Runtime/Services/TrainingProgressStore.cs`. **연결 변경:** `AppFlowController.cs`, `TutorialCompositionRoot.cs`, 필요하면 TutorialScreen의 상태 표시.
- `SelectionPreferences`의 작은 JSON/PlayerPrefs 패턴을 따르되 저장 키와 DTO는 수련 전용으로 둔다. 첫 버전은 별도 저장소 인터페이스나 DB가 필요하지 않다. 저장 키 주입으로 테스트 격리를 가능하게 한다.
- 현재 시도 ID와 버전을 확인한 합격 결과에만 Completed를 한 번 저장한다. 실패/중단/연출 완료는 저장 조건이 아니다. 재도전 실패가 이전 완료를 지우지 않는다. 재실행은 완료 표시 또는 도입부터 재도전이며 세션 중간 복구는 하지 않는다.
- 빈/손상 저장은 안전한 초기 상태와 필요한 안내로 처리하고 일반 곡 설정은 보존한다. 다른 DefinitionVersion의 완료를 현재 수련 완료로 자동 승격하지 않는다.
- **검증:** 신규 `EditMode/TrainingProgressTests.cs`의 Fast 사례에서 저장소 재생성 후 복원, 실패/중단 미저장, 중복 완료, 버전 차이, 손상 데이터, 일반 PlayerPrefs 보존을 검사한다. 앱/Play Mode 재시작 후 실제 화면 복원은 수동 확인하며 씬 재진입 자동 검사만으로 프로세스 재시작 검증을 대체하지 않는다.

### 단계 6 — 사용자 경로로 트릴 하나의 완료 확인

- **확장:** `PlayMode/TrainingFlowTests.cs`, 기존 `MainMenuFlowTests.cs`, 필요 시 `RemediationPlayerProbe.cs`. 사용자 경로 문서도 실제 완성된 동작에 맞춰 갱신한다. 과거 테스트 이름은 보존한다.
- 실제 Title 진입→ModeSelection의 비급 전수 선택→도입→Gameplay 시작→트릴→피드백→재시도→성공→재실행 후 완료 표시를 확인한다. 새 콘텐츠를 숨겨진 개발 호출로만 실행한 것은 완료가 아니다.
- 실제 lane 키 Press/Release를 이용하여 정상 교대, 빠름/늦음, 누락, 같은 쪽 반복, 양손 난타, 사용하지 않는 레인 입력을 검사한다. 레인 변경 설정 후 안내와 실제 입력도 일치해야 한다. pause/Escape/복귀/포커스 상실/Space 재시작에서 이전 시도 입력이나 완료가 남지 않아야 한다.
- **검증:** 관련 Fast/SceneIntegration + Smoke를 실행한다. 실제 예약 재생과 곡 종료까지 검증하는 새 트릴 시나리오는 Endurance로 분류하고, 기존 `GameplayJudgmentTests`의 관련 Endurance 범위를 선택한다. 오디오 복구/예약 재개 구현까지 변경했다면 해당 Hardware도 추가한다. 변경하지 않은 느린 프로필을 관성적으로 반복하지 않는다.
- 자동 검사 후 가상 입력 장치와 PlayerPrefs/씬/구독을 복원하고, native Keyboard/Mouse가 활성인 Game View에서 한 번 직접 수행한다. 화면 캡처와 실행 결과는 `Logs/`에 남긴다. 이 단계까지 끝나야 첫 수련을 완료한 것으로 본다.

## 6. 충돌 위험과 방지 기준

| 위험 | 구체적인 방지 기준 |
| --- | --- |
| main/과거 문서로 emergency 기능 회귀 | main의 계층/재사용 패턴만 참고한다. 현재 Tutorial 경로, ModeSelection, 사형 애니메이션을 과거 네 번 Space 수련이나 main의 직접 곡 선택 경로로 덮어쓰지 않는다. |
| 일반 플레이 결과/기록에 수련 혼입 | AppFlow의 명시적 수련 문맥, Gameplay 완료 분기, SaveRecord 제외를 함께 구현한다. 기존 ResultsScreen의 동작과 현재 미커밋 변경을 보존한다. |
| 에디터 playtest 복귀 계약 훼손 | `IsEditorPlaytest`, `returnScenePath`, `returned`, `LastResult`, 요청 해제 시점의 의미를 유지한다. 수련 복귀를 기존 에디터 콜백 필드에 얹지 않는다. |
| pause와 훈련의 입력 수집 불일치 | coordinator가 적용하는 준비/재개/보정 경계를 공유한다. 원시 이벤트 시각을 별도 보정하거나 pause 중 Press를 실수로 누적하지 않는다. |
| 메뉴 연출의 성공 오인 | `DojoLessonSequence.CompletedSets`와 실제 수련 완료를 연결하지 않는다. 수련 완료는 기존 노트 판정과 현재 시도 평가의 결과다. |
| 공유 에셋/전역 설정 변경 | `PlayRules`와 세션용 자료는 복사한다. 원본 SongDefinition/모드/일반 선택 설정/고정 클립을 런타임에 수정하지 않는다. |
| 준비음원/시각/종료 처리 충돌 | `PlayableSong`은 유효한 AudioClip을 요구한다. `GameplayCompositionRoot`는 최종 판정창과 음원 길이를 고려해 종료 시각을 보정하고 빈 아웃트로를 제한한다. 준비 구간과 마지막 GoodWindow를 실제 재생으로 검증한다. 스크롤 속도로 곡을 느리게 만들지 않는다. |
| 가짜 동시치기/트릴 합격 | 일반 엔진은 빈 입력을 감점하지 않는다. 추가 Press와 순서 평가는 수련에만 적용하고 일반 곡 판정 규칙을 변경하지 않는다. |
| 롱노트 집계 오해 | 가시 노트 수, 판정 검사 수, 결과의 시작/끝 기록을 구분한다. 첫 Tap 수련의 계산을 미래 Hold 수련에 그대로 일반화하지 않는다. |
| 박자표 지원 과장/정지 우회 | 편집 Grid와 게임 마디선의 연결 부족을 유지보수 항목으로 분리한다. BPM 0/사용자 pause를 정지 채보 형식으로 사용하지 않는다. |
| 씬 수명/음원 중복/이중 완료 | Tutorial 재생 음악은 씬 이탈 시 정리하고 Gameplay 음원 소유권을 기존 흐름에 맡긴다. 중복 시작 차단, attempt ID 확인, Reset 시 폐기, 화면 파괴 후 콜백 금지를 검증한다. |
| 생성 자원과 테스트 복원 충돌 | 신규 에셋은 `.meta`와 함께 관리한다. 콘텐츠 준비가 기존 TestChart/TempoChart를 덮어쓰는 점에 유의한다. 생성 테스트의 스냅샷/복원 범위에 새 수련 자원을 포함한다. |

## 7. 검증 실행 원칙과 남은 확인

향후 구현 시 Unity **6000.3.23f1**, Test Framework **1.6.0**과 [verification.md](verification.md)를 따른다. 기존 fixture를 먼저 확장하며 신규 기능/회귀/경계만 테스트한다. 각 사례는 비용 카테고리 `Fast`, `SceneIntegration`, `AssetGeneration`, `Hardware`, `Endurance` 중 정확히 하나를 가진다. 기능 카테고리와는 별개다.

실행 중인 Editor에서는 소스 변경 후 refresh/recompile 완료를 확인하고 한 suite씩 실행한다. 예를 들어 아래 명령의 첫 filter는 기존 fixture이며 신규 Training fixture는 실제 구현 후 선택한다.

```powershell
unity status --json
unity command run_tests --mode editor --filter CoordinatorTests --async_tests true --json
unity command test_status --json
# 결과와 정리 완료/Play Mode 종료, status의 ready를 확인한 뒤 다음 suite 실행
unity command run_tests --mode playmode --filter MainMenuFlowTests --async_tests true --json
unity command test_status --json
```

각 단계는 위에 명시한 관련 범위부터 검증한다. 공통 이동/씬에는 Smoke, 예약 재생/완주에는 관련 Endurance를 추가한다. 여러 계층에 걸친 영향이 불명확해지면 All을 빌드 없이 실행한다. 신규 씬/빌드 설정을 추가하지 않는 계획이므로 빌드는 기본 완료 조건이 아니며, 플랫폼/빌드 변경 또는 명시 요청 시에만 추가한다. 0건·skip·inconclusive를 통과로 보고하지 않는다.

이번 조사에서 아직 실증하지 않은 것은 현재 작업 트리의 Unity 컴파일/테스트 성공 여부, 실제 입력·화면·오디오 품질, 첫 과제 음원의 적합성, 120 BPM/16타의 교육적 난이도다. 향후 구현 단계에서 확인한다. 현재 확인된 코드/에셋의 존재와, 새로 제안한 수련 기능의 완성을 혼동하지 않는다.
