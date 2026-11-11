# 알파 시연용 타이틀·모드 공간

## 실행과 수정

`emergency`는 작업 시작 시 최신 `origin/main`에서 생성했다. 기존 미커밋 변경은 보존했다. 재생성 전 Title과 작업 트리 diff는 `Logs/emergency-backup/`에 보관했다. 다른 씬과 SettingsPopup의 기존 수정은 이번 작업에서 재생성하지 않았다.

Project 창에서 `Assets/Game/Scenes/Title.unity`를 열고 Play 한다. 대문 또는 현판 클릭 / Enter → 모드 공간 → 좌우 화살표 또는 화면 양쪽 버튼 → Enter / 수련 시작. 게임패드 D-pad도 좌우 이동을 지원한다. Esc는 대문으로 돌아간다. 설정·종료·채보 공방은 기존 기능을 유지한다.

| 항목 | 실제 기능 |
| --- | --- |
| 비급 전수 | 사부의 손짓과 소리에 맞춰 Space 또는 버튼을 네 번 두드리는 짧은 입문 수련. 성공/교정 피드백과 완료 상태가 있다. |
| 자유 연습 | 기존 곡 선택과 Gauge 규칙으로 연결. |
| 실력 검정 | 기존 곡 선택과 Challenge 규칙으로 연결. 기존 프리셋과 판정 설정을 유지. |
| 새로운 수련 | 네 번째 플레이 모드는 확인되지 않아 준비 중으로 비활성화. |
| 채보 공방 버튼 | 기존 ChartEditor로 진입하는 별도 제작 도구. 네 번째 모드로 간주하지 않는다. |

비급 전수는 시연용 기초 박자 연습이며, 새로운 시나리오 목록이나 단위 인정 커리큘럼을 구현하지 않는다. 실력 검정 역시 새 인증 시스템이 아니라 기존 Challenge 진입점이다.

## 작성 구조

`MainMenuSceneBuilder` → `MainMenuLayoutBuilder` → `AlphaDojoLayoutBuilder.Apply` 순서로 기존 화면 계약을 유지하며 씬을 작성한다. **Game Tools > Scenes > Build Title**로 재생성한다. Build All Scenes에서도 같은 경로가 실행된다.

에디터에서 모드 배치를 보려면 `Menu Buttons`를 활성화하고 CanvasGroup alpha를 1로, `Dojo Entrance`를 비활성화한다. 각 `Mode Stages/Stage`의 CanvasGroup alpha로 원하는 배경을 확인한다. 영구 수정은 빌더에 반영한다.

Title 씬에는 대문 두 장, 현판 Text, 빛 레이어, 네 개 배경, 사부·수련생·목인·설명판, 모드 정보와 버튼이 저장되어 있다. `DojoModeView`는 직렬화된 오브젝트의 자세·프레임·색·위치만 변경한다. 고정 UI/배경/캐릭터의 런타임 생성은 없다. 배우 스프라이트와 선인 옷자락의 분할도 에디터가 `.asset`으로 저장한다.

모드 이동은 0.6초이며 두 달리기 프레임, 방향 반전, 배경 이동과 교차 페이드, 정보 페이드가 연결된다. 이동 중 선택·설정·중복 방향 입력을 차단한다. 자유 연습의 타격 프레임과 목인 반응은 같은 접촉 이벤트에서 시작한다. 선인의 옷자락은 상체와 별도 이미지다. 문 열기는 0.85초이며 기존 문 미끄러짐 효과음을 사용한다.

곡 선택은 기존 `PreparedSelectionScene`을 재사용한다. 모드 선택 시 기존 Rules 서비스의 Gauge/Challenge 값만 갱신한다. 준비 실패 시 수련 시작 버튼을 복구하고 재시도를 안내한다. 새 경로에서는 곡 선택 EventSystem 활성화 이후 선택 행의 포커스를 명시적으로 복원한다. 기존 도장 전환 프리팹은 호환 경로를 위해 유지하지만 새 모드 진입 때 문을 다시 여는 연출은 실행하지 않는다.

## 아트와 폰트

최상위 레퍼런스는 `Art/DojoEntrance/SelectionBackground.png`. 적갈색 목재의 거의 검은 윤곽, 황금색 등불과 종이, 청록/청색 산수, 세밀한 계단형 픽셀 군집을 기준으로 삼았다. 원근과 배경의 복잡도는 기존 화면을 따르며 본문 뒤에는 어두운 단색 패널을 둔다. 기존 TrainingInterior, DoorPanel, EntranceFrame, SettingsPanel 및 효과음을 재사용한다.

기존 프로젝트 전체에서 플레이어 캐릭터 프레임과 Animator/AnimationClip은 발견되지 않았다. 새 수련생은 대기·달리기 두 장·타격 총 네 프레임을 사용한다. 완성 화면을 통째로 생성하지 않았다.

새 원본은 `Assets/Game/Art/AlphaDojo/Actors.png`와 `Summit.png`. 배우 아틀라스는 4×2로 나누며, 실제 alpha=0 배경을 확인했다. 필터는 Point, mipmap 없음, 비압축. 기존 이미지 및 meta는 삭제하지 않았다.

현판과 모드 제목은 기존 `Dokdo-Regular.ttf`를 사용한다. 라이선스는 `Assets/Game/Fonts/Dokdo-OFL.txt`의 SIL OFL 1.1. 본문은 기존 UiFontSettings/Maru Buri 체계를 유지한다. 모든 글자는 Unity Text이며 생성 이미지의 글자를 UI로 사용하지 않는다.

## 검증 근거

Unity 6000.3.23f1의 실행 중인 Editor/Pipeline에서 컴파일·씬 생성과 PlayMode 검증을 수행했다. MainMenuTests EditMode, MainMenuFlowTests PlayMode에 문 입력, 연출 중 중복 입력, 모드 이동, 실제 목인 반응, 입문 연습 완료, Challenge 전달, 준비 중 차단, 곡 목록 실패 후 재시도를 포함한다. 기존 설정·종료·에디터·사전 로딩 검증도 유지한다.

상세 결과는 `Logs/emergency-edit-tests.json`, `Logs/emergency-main-tests.json`, `Logs/emergency-smoke-tests.json`, `Logs/emergency-preparation-final-tests.json`에 보관한다. MainMenuTests 5건과 MainMenuFlowTests 22건, Smoke 4건은 통과했다. 사전 로딩의 초기 검증 실패 2건은 구형 전환 대기 조건과 새 경로의 포커스 복원 순서 문제로 수정했다. 수정 후 ScenePreparationFlowTests 12건이 모두 통과했다. 마지막 배치 수정 후 캡처 검증도 통과했다. 초기 실패와 부분 재검증은 별도 JSON에 보존한다. Editor에는 기존 커서 텍스처 설정 경고와 기존 코드의 컴파일 경고가 남아 있다. 신규 컴파일 오류는 없다. 전체 테스트/Windows 빌드/장시간 곡 완주/실제 패드 조작 검증은 별개이며 이번 UI 검증을 이들 전체의 통과로 간주하지 않는다.

실제 씬 렌더 캡처: `Logs/main-menu.png`, `Logs/main-menu-expanded.png`, `Logs/emergency-lesson.png`, `Logs/emergency-summit.png`. 4:3, 16:10, ultrawide 캡처도 `Logs/main-menu-*`에 저장한다. Pipeline의 일반 screenshot 명령은 Overlay Canvas를 담지 않아 검증에서는 기존 테스트의 임시 ScreenSpaceCamera 캡처 방식을 사용하고 원래 렌더 상태를 복원한다.

## 생성 프롬프트

내장 `image_gen` 사용. 외부 API/CLI 사용 없음. 최종 Actors는 아래 첫 프롬프트 후 두 번째 투명 배경 편집을 적용한 결과다.

### Actors

```text
Create a production Unity game sprite atlas on transparent background, 1536x1024. No text. Modern detailed 16/32 bit pixel art, warm rust brown outlines, ivory hanbok, muted jade and brick red accents, Korean Joseon martial arts rhythm dojo. Precisely a 4 column by 2 row grid, each cell equally 384x512, all subjects fully separated, centered, with transparent padding. Top row: same original young Korean trainee with black topknot and red headband, ivory jeogori and dark teal trousers, side view facing RIGHT: cell1 idle guard pose, cell2 running stride left leg forward, cell3 running opposite stride, cell4 punching extended right fist. Bottom row: cell1 elderly dojo teacher holding bamboo pointer facing LEFT, cell2 same elderly sage in long ivory robes with teal sash facing LEFT, cell3 wooden standing martial arts training dummy, cell4 freestanding blank parchment instruction board on dark timber easel with two rolled scrolls at its feet. Characters full body, feet at same baseline, consistent sizes. Clean transparent background, no shadows outside subjects, no lettering, no scenery, no UI. Crisp pixel clusters with expressive silhouettes, not vector, not 8bit, not blurry.
```

```text
Remove the entire brown gradient background to genuine alpha transparency. Keep all eight subjects in their exact grid positions and the same canvas size, preserve all pixel artwork and poses. Transparent background sprite sheet for Unity, absolutely no colored background, no backdrop shadows, no matte.
```

### Summit

```text
Standalone background layer for a Unity side-view Korean Joseon martial arts game. Landscape 1536x1024. Detailed modern 32-bit pixel art, crisp pixel clusters, dark warm brown outlines, matching warm timber dojo against blue jade mountains. A high mountain training terrace at dawn, flat ochre stone ground across bottom quarter, a tall rock outcrop on the RIGHT for an elder character to stand upon (no character drawn), distant layered blue and teal Korean mountain peaks above a sea of ivory clouds, warm amber sunlight from left, a small Korean tiled pavilion at far left edge, calm spacious central area for animated characters. Natural rugged pine silhouette at edge. No humans, no lettering, no symbols, no UI, no flags, no floating particles. Strong readable stage silhouette, restrained detail, sophisticated 16-32bit pixel game art, not photoreal, not vector, not Japanese shrine.
```
