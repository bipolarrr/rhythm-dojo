# 네 영역 분업 안내

## 담당 경계

| 담당 | 소유 영역 | 연결 규약 |
| --- | --- | --- |
| ① 게임 UI 배치·스타일 | `Scripts/Editor/UILayout`, `Scripts/Runtime/UI/Views` | 레이아웃 빌더가 View의 명시적 참조를 채운다. 컨트롤러나 서비스를 생성하지 않는다. |
| ② 게임 UI 동작 | `Scripts/Runtime/UI`의 View 이외 파일 | View와 주입된 Services 인터페이스를 사용한다. 화면 전환·음원 로딩·판정을 직접 구현하지 않는다. |
| ③ 게임 내 곡·채보 에디터 | `Scripts/Runtime/Authoring`, 향후 에디터 전용 화면과 레이아웃 | `SongDocument`, `ISongDocumentStore`, `ISongDocumentLoader`, `IEditorPlaytestService`를 사용한다. |
| ④ 공통 기반·게임플레이·통합 | Domain, Application, Runtime의 Core/Services/Content/Audio/Input/Gameplay/Presentation, Editor의 SceneGeneration | 공통 계약, 조립, 게임플레이, 곡 공급자 통합과 씬 등록을 소유한다. |

경로는 `Assets/Game` 기준이다. 3D 노트·플레이필드·카메라는 ④, HUD의 반복 레인 UI 생성·배치는 ①이다. 에디터 화면은 ③이 전담한다. `Scripts/Editor`는 Unity Editor 전용 개발 도구이며, 게임 내 채보 에디터 코드를 넣지 않는다.

각 담당은 자기 기능의 테스트와 문서도 수정한다. 공통 계약 변경은 ④와 사용하는 담당이 함께 검토한다. 기존 어셈블리 경계는 유지하므로 같은 Runtime 안의 의존성 방향은 코드 리뷰로 지킨다.

## 메인 화면 UI 작업 시작하기

메인 화면의 씬 이름은 **Title**입니다. `main`을 받은 뒤 Project 창에서 `Assets/Game/Scenes/Title.unity`를 열면 볼 수 있습니다. Windows 빌드도 이 화면에서 시작합니다.

| 작업 | 수정할 파일 (`Assets/Game` 기준) | 담당 |
| --- | --- | --- |
| 제목, 안내 문구, 색상, 크기, 배치, 종료 팝업 디자인 | `Scripts/Editor/UILayout/MainMenuLayoutBuilder.cs` | ① UI 배치·스타일 |
| 화면 요소의 명시적 참조 | `Scripts/Runtime/UI/Views/MainMenuView.cs` | ① UI 배치·스타일 |
| 설정·전원 아이콘 모양 | `Scripts/Runtime/UI/Views/MainMenuIcon.cs` | ① UI 배치·스타일 |
| 클릭, 팝업 열기/닫기, 중복 입력 방지 | `Scripts/Runtime/UI/MainMenuScreen.cs` | ② UI 동작 |
| 실제 게임 시작·종료, 씬 조립 | `Scripts/Runtime/Core/MainMenuActions.cs`, `MainMenuCompositionRoot.cs`, `Scripts/Editor/SceneGeneration/MainMenuSceneBuilder.cs` | ④ 공통 기반·통합 |

1. `MainMenuLayoutBuilder`에서 디자인을 수정합니다. 중앙 로고, 펼쳐지는 플레이/설정/종료 버튼, 팝업의 View 참조를 유지하세요.
2. **Game Tools > Scenes > Build Title**을 실행합니다. 다른 화면을 재생성하지 않고 Title을 저장하고 엽니다.
3. Play Mode에서 중앙 원 클릭 → 오른쪽에 가로 메뉴 펼치기, 원 다시 클릭 → 접기를 확인합니다. 플레이 → 곡 선택, 설정 → 설정 화면 → 뒤로 → 타이틀, 종료 → 팝업 → 아니요 → 닫기를 확인합니다. 빈 배경을 클릭해도 게임은 시작되지 않습니다.
4. **Game Tools > Verification > Verify UI Actions**로 화면 동작을 확인합니다. 타이틀만 확인하려면 Test Runner에서 `MainMenuTests`(EditMode)와 `MainMenuFlowTests`(PlayMode)를 선택합니다.
5. 빌더/관련 View 변경, 재생성된 `Title.unity`, 새 에셋과 `.meta`, 화면 스크린샷을 함께 제출합니다. 이름·위치·문구의 사양을 의도적으로 바꾸면 해당 레이아웃 테스트도 맞추고, 버튼 동작 테스트는 유지합니다.

씬에서 직접 바꾼 배치는 **Build Title** 실행 시 덮어써집니다. 미리보기로 실험한 값도 최종적으로 빌더에 옮겨야 다른 팀원이 같은 화면을 재생성할 수 있습니다. 새 이미지/프리팹을 쓰면 빌더에서 로드하고 View에 연결하세요. 다른 화면을 직접 실행하려면 **Game Tools > Scenes > Start Play Mode From Title**을 끄면 됩니다.

중앙 로고는 `LogoCircleGraphic`으로 그리며 원 내부만 클릭할 수 있습니다. 제목과 글로우는 원에 포함되어 음악 BPM에 맞춰 함께 바운스합니다. 로고 클릭 시 0.25초 동안 왼쪽으로 이동하고 플레이·설정·종료 사각형 버튼이 오른쪽에 가로로 나타납니다. 메뉴가 펼쳐지기 전과 종료 팝업이 열려 있을 때는 게임 시작/설정 입력을 차단합니다.

### 타이틀 음악 넣기

1. 음원 파일을 `Assets/Game/Audio/`에 가져옵니다.
2. `Assets/Game/Settings/TitleMusic.asset`을 선택하고 **Music Clip**에 음원을 드래그합니다.
3. **Beats Per Minute**에 음악 BPM, **First Beat Seconds**에 음원에서 첫 박자가 시작되는 시간(초), **Volume**에 음량을 설정합니다.
4. Title 씬에서 Play Mode를 시작하면 반복 재생되며 제목과 시작 문구가 같은 박자로 바운스합니다. BPM을 자동 분석하지 않으므로 음원의 값을 직접 입력합니다. 음원을 바꿀 때는 Play Mode를 종료한 뒤 설정합니다.

음원을 비워두면 이 설정의 BPM으로 바운스만 실행합니다. 음악은 타이틀 씬을 벗어나면 정지하고 종료 팝업에서는 계속 재생됩니다. 루프마다 음원 시작점을 기준으로 박자를 다시 맞추므로 자연스럽게 반복되도록 편집한 음원을 권장합니다. **Build Title**은 이 설정 에셋을 다시 연결하므로 음원 지정이 유지됩니다. `MainMenuScreen`의 BPM은 음악 설정이 연결되지 않은 화면의 대체 값이며, 바운스 강도는 계속 `MainMenuScreen`에서 조절합니다.

## UI 작업 방법

- 배치: `MainMenuLayoutBuilder`, `SongSelectionLayoutBuilder`, `SettingsLayoutBuilder`, `GameplayHudLayoutBuilder`에서 좌표·크기·문구·계층을 수정한다. `HudLaneLayout`은 런타임 레인 반복 요소를 생성한다.
- 참조: `MainMenuView`, `SongSelectionView`, `SettingsView`, `GameplayHudView`의 참조 필드가 화면 계약이다. 객체 이름과 계층 경로는 연결 규약이 아니다. 신규 필수 컨트롤은 View의 검증에도 추가한다.
- 동작: Screen의 `Initialize`가 서비스 구독을 시작한다. 재초기화와 파괴 시 구독을 해제한다. 컨트롤러에서 배치 코드를 만들거나 `AppFlowController.Create`를 호출하지 않는다.
- 조립: `SceneDependencyAssembler`가 컨트롤러와 View를 연결하고, `ScreenCompositionRoot`가 런타임 서비스를 주입한다. 직접 씬을 열어 실행하는 경우도 이 경로를 사용한다.
- 게임플레이 복귀 버튼: 표시·클릭은 `GameplayUiController`, 세션 종료·입력 해제·씬 이동 순서는 `GameplayCompositionRoot`와 AppFlow가 담당한다. Escape 입력 콜백 안에서 입력 액션을 해제하지 않는다.

`IAppNavigation`은 화면 이동, `ISongSelectionService`는 곡 ID 기반 선택, `IAudioSettingsService`는 실제 오디오 상태와 버퍼 적용을 제공한다. AppFlow의 이전 메서드는 기존 통합 호출자를 위한 얇은 호환 진입점이다. 새 UI에서는 인터페이스를 사용한다.

## 곡·에디터 연결 방법

1. 편집 세션은 변경 가능한 `SongDocument`를 소유한다. 저장 전 불완전한 문서도 허용한다. 한 곡에 한 채보이며 시간 단위는 초, 레인은 0부터 시작한다. 오프셋의 양수는 오디오보다 채보 시간을 앞당긴다.
2. `ISongDocumentStore`는 문서 저장·불러오기 계약이다. `MemorySongDocumentStore`는 복사와 취소 동작을 보여 주는 기준 구현이며 디스크 저장이 아니다. 파일 형식·버전·음원 가져오기 UX는 에디터 기능 작업에서 정한다. `SongDocument`는 특정 JSON 직렬화기의 wire format을 보장하지 않는다.
3. 기본 곡은 `SongAssetAdapter`로 문서가 된다. 기본 모드 ID는 현재 모드 에셋 이름이다. 에디터의 모드 레지스트리는 이 식별자를 해석해야 한다. 외부 곡의 AudioId와 ModeId 해석은 주입한 로더가 담당한다.
4. `DocumentSongLoader`에 모드 조회 함수와 비동기 음원 로딩 함수를 전달한다. 음원 로딩 함수는 `SongAudioLease`를 반환한다. 기본 제공 AudioClip은 해제 콜백 없이 빌리고, 직접 가져온 AudioClip은 해제 콜백에서 파괴한다. 로더와 서비스는 Unity 메인 스레드에서 호출한다.
5. `PlayableSong`은 문서와 모드를 복사하고 기존 Domain 검증을 실행한다. 노트·BPM·오프셋이 잘못되면 필드 또는 노트 정보를 가진 예외가 발생한다. UI는 이 오류를 표시한다. 판정용 별도 검증 규칙은 만들지 않는다.
6. 곡 목록 공급자는 `ISongProvider`를 구현해 `flow.Library.Register(provider)`로 등록한다. 저장 완료 후 `Refresh()`를 호출한다. 공급자는 목록용 메타데이터와 로딩을 제공한다. 같은 ID가 여러 공급자에 있으면 해당 항목들은 재생 불가이며 우선순위로 덮어쓰지 않는다.
7. 미저장 테스트 플레이는 `IEditorPlaytestService.PlayAsync(document, loader, sessionId, returnScenePath, returned, token)`을 호출한다. 반환 씬은 Build Settings에 등록돼 있어야 한다. 에디터는 씬이 바뀌어도 유지되는 편집 세션을 소유하고, 복귀 콜백에서 sessionId로 이를 복구한다. 콜백이 파괴될 화면 컴포넌트를 캡처하지 않도록 한다.
8. Task 완료는 로딩 및 씬 전환 요청이 성공했다는 의미다. Gameplay 준비는 씬 로드 시 진행되고, 복귀 콜백은 반환 씬이 로드된 뒤 호출된다. 취소는 씬 전환을 요청하기 전까지 적용된다. 이미 요청한 Unity 씬 전환은 되돌리지 않는다. 곡 ID와 재생 옵션은 로딩 요청 시점에 고정하며 게임 화면은 로딩 중 선택 컨트롤을 잠근다.

라이브러리 로더는 반환한 `PlayableSong`의 소유권을 호출자에게 넘긴다. AppFlow에 넘긴 재생 자료는 AppFlow가 반환 씬 로드 뒤 해제한다. 별도로 로더를 사용하는 호출자는 `Dispose`해야 한다. 로딩 취소 또는 실패로 전달하지 못한 음원은 로더가 해제한다. 기존 ScriptableObject 곡 에셋은 수정하지 않는다.

## 생성과 통합

- 일반 UI 작업: **Game Tools > Scenes > Build All Scenes**. 기존 리소스를 읽고 씬을 생성하며 검증용 채보를 다시 쓰지 않는다.
- 최초 준비·검증 곡 재생성: **Game Tools > Content > Prepare Default and Verification Content**. 이 명령은 이름이 정해진 두 검증 채보를 재생성한다. `DefaultSettingsBuilder`, `TestContentBuilder`, `PresentationAssetBuilder`가 각각 설정·검증 곡·표현 리소스를 준비한다.
- 생성된 `.unity`는 빌더 출력물이다. 수동 편집을 원본으로 삼지 않는다. 공통 `SceneBuilder`, `SceneDependencyAssembler`, 씬 등록은 ④가 통합한다.
- 파일 이동 시 `.meta`를 함께 이동한다. 기존 씬 경로와 에셋 경로는 유지한다.
- Unity 에디터를 닫고 README의 배치 검증을 실행한다. 새 회귀 테스트는 `TeamBoundaryTests`와 `GameplayFlowTests`에 있다.

## 이번 범위 밖

채보 편집 화면과 메뉴 연결은 `ChartEditorSceneBuilder`, `ChartEditorScreen`, `ChartEditorMenu`에 있다. `ChartEditorCompositionRoot`가 파일 저장소, 외부 음원 디코더, 테스트 플레이 서비스를 조립한다. 저장 형식과 사용법은 `chart-format-v1.md`를 참고한다. 사용자 음원 파일 선택 대화상자와 에디터 전용 미리듣기는 아직 구현하지 않았으며, 음원은 전체 경로 입력으로 가져온다.
