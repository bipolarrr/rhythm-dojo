# 비주얼 재구성 검증 — 2026-11-11

사용자 예시의 한지문·자주색 장식·붓글씨를 반영한 입구와, 별도의 밝은 비급 전수 수업방을 Title에 작성했다. 플레이어·사부·선인·목인·두루마리 원본과 GUID는 보존했다. 고정 요소는 SceneBuilder가 생성한 씬에 저장되고 런타임에서는 위치·회전·프레임·색만 변한다.

## 최종 검증

Unity 6000.3.23f1 / 실행 중인 Editor의 Unity Pipeline / Test Framework 1.6.0.

| 범위 | 최종 결과 | 실행 시간 | 근거 |
| --- | --- | --- | --- |
| MainMenuTests, EditMode | 5 passed, 실패·skip·inconclusive 0 | 0.40초 | [결과](renewal-edit-final-tests.json) |
| MainMenuFlowTests, PlayMode | 22 passed, 실패·skip·inconclusive 0 | 63.41초 | [결과](renewal-main-final-tests.json) |
| Smoke, PlayMode | 4 passed, 실패·skip·inconclusive 0 | 8.20초 | [결과](renewal-smoke-tests.json) |

Smoke에 MainMenuFlowTests 사례 하나가 포함되므로 위 수를 고유 사례 총합으로 합산하지 않는다. 문 클릭·역전·중복 입력 차단, 등불 이동과 재입장 시 같은 오브젝트 유지, 장소별 수련생 위치와 동일 Image 유지, 네 박자 수련 및 표식 완료, 기존 설정·종료·에디터·곡 선택/게임플레이 진입을 검증했다. 기존 캡처 검증에 여러 화면비에서 현판과 입장 안내가 화면 안에 있는지 확인하는 회귀 검사를 추가했다.

초기 관련 검사도 통과했으나 화면 검토에서 울트라와이드 잘림과 서로 다른 배율의 인물/배경 문제를 발견해 구도 전체를 맞추는 방식으로 보정했다. 후속 실행 한 번은 Play Mode 전환 상태와 겹쳐 씬 생성이 거부되고 Test Runner 시작이 중단됐다. 해당 실행은 통과 근거로 사용하지 않는다([기록](renewal-interrupted-run.json)). Editor의 실제 Play/전환 상태가 모두 false임을 확인하고, 씬의 새 컴포넌트 저장을 확인한 뒤 위 최종 검사를 수행했다.

최종 검사 및 영상 촬영 이후 새 런타임/컴파일 오류 0. 기존 커서 관련 경고 등은 남아 있다. 코드 diff 공백 검사 통과. Unity가 생성한 YAML의 빈 필드 뒤 공백은 유지했다. 전체 테스트, Hardware/Endurance, Windows 빌드는 이번 UI 작업에서 실행하지 않았다.

## 시각 확인

- [메인 입구](renewal-title.png)
- [문 열기 중간 상태](renewal-door-opening.png)
- [비급 전수](renewal-lesson.png)
- [수련 완료](emergency-lesson.png)
- [실력 검정](renewal-summit.png)
- [4:3 입구](main-menu-4x3.png), [울트라와이드 입구](main-menu-ultrawide.png)
- [4:3 모드 공간](main-menu-expanded-4x3.png), [울트라와이드 모드 공간](main-menu-expanded-ultrawide.png)
- [15초 모션 미리보기](renewal-motion.mp4)

영상은 실제 Play Mode에서 12fps로 캡처한 180프레임을 H.264로 인코딩했다. 입구 대기 → 문 열림 → 자유 연습 → 비급 전수 안내 → 자유 연습 → 실력 검정의 흐름이다. 영상 전환에는 버튼 콜백을 사용했으며 실제 입력 검증은 위 PlayMode 테스트가 담당한다. 영상의 박자 안내는 관찰용이고, 네 번 수련 완료는 별도 실제 클릭 테스트로 검증했다. 녹화 스크립트는 [RenewalReview.cs](RenewalReview.cs)다.

## 보존과 인계

`git diff -- Assets/Game/Art/AlphaDojo Assets/Game/Art/DojoEntrance` 결과 변경 없음. 보존 자산 SHA-256은 [renewal-preserved-assets.json](renewal-preserved-assets.json)에 남겼다. 기존 다른 씬·SettingsPopup·채보 에디터·AGENTS.md의 미커밋 수정은 보존했다.

새 에셋: `Assets/Game/Art/DojoRenewal/`의 EntranceFrame, SlidingDoors, LessonRoom, Lantern, BrushTitle PNG 및 full/left/right Sprite 자산. [재사용 분류·씬 작성 안내](../Docs/dojo-visual-renewal.md), [내장 image_gen 최종 프롬프트](../Docs/dojo-renewal-prompts.json).

최종 Editor는 Play Mode를 종료하고 Title 씬을 열어 둔 상태다. 영구 구성은 **Game Tools > Scenes > Build Title**에서 재생성할 수 있다. 로컬 `main` 작업이며 커밋·푸시는 실행하지 않았다.
