# 도장 수련실 Phase 1 구현과 검증

2026-11-18, `emergency`, Unity 6000.3.23f1. 일반 곡의 첫 구간에 앉은 시점의 도장 공간을 구현했다. 정상 판정이 쌓이면 두께가 있는 문짝이 벽 뒤로 열리고, 바닥의 빛과 창살 그림자가 넓어지며, 충분한 성과에서 문턱을 통과한다. 음악 시각은 성과와 독립적으로 네 구간을 선택한다. Phase 2~4는 기존 TrainingHall 배경을 표시한다.

기존 PracticeHall과 HanokDoors의 목재·창호·청록 원경을 이어받은 신규 텍스처를 입체 구조와 분리된 원경/소품에 배치했다. 전경 기둥·문틀, 중경 문짝·마루, 뜰과 먼 산은 서로 다른 깊이에 있다. 천의 변형, 소나무의 움직임, 고정 풀의 먼지는 독립 위상으로 움직인다. 카메라는 고정 FOV로 부드럽게 접근하고, 성과 0.85에서 예비 동작 후 문턱을 넘는다. 마루 빛과 그림자는 셰이더 표현이며 물리 기반 실시간 그림자는 아니다.

## 실제 렌더 확인

아래 화면은 실제 Gameplay 씬에서 Controller와 RuleSession에 입력을 전달해 만든 Unity 렌더다. 재현 가능한 80초 테스트 채보와 통제된 음악 시계를 사용했다. 사람이 실제 곡을 끝까지 연주한 기록으로 해석하지 않는다. 게임 화면은 1920×1080, 배경 단독 캡처는 1280×720이다. 다음 화면을 직접 열어 확인했다.

| 상태 | 확인한 결과 | 실제 게임 화면 |
| --- | --- | --- |
| 시작 직후 | 문이 닫히고 가느다란 문틈과 따뜻한 실내가 보인다. | [시작](dojo-room/start-game.png) |
| 정상 판정 중간 | 첫 구간 기회량의 45%를 획득하면 문이 부분 개방되고 바닥 빛이 넓어진다. | [중간](dojo-room/middle-game.png) |
| 연속 미스 | 20회 Miss 후에도 성과 0, 문 닫힘, 전진 0을 유지한다. | [미스](dojo-room/misses-game.png) |
| 높은 성과 | 성과 1에서 문이 완전히 열리고 시점이 문턱을 지나 뜰·산·소나무로 확장된다. | [완전 개방](dojo-room/open-game.png) |
| 전환 직전 | 성공 경로는 외부 시야를, 미스 경로는 닫힌 실내를 그대로 유지한다. | [성공](dojo-room/before-transition-game.png), [미스](dojo-room/miss-before-transition-game.png) |
| 시간 경계와 다음 구간 | 20초 경계 뒤 기존 배경으로 합성 전환하며 빈 화면이 발생하지 않는다. | [전환](dojo-room/transition-game.png), [기존 배경](dojo-room/fallback-game.png) |

[5초 모션 영상](dojo-room/dojo-motion.mp4)은 별도 PreviewScene에서 가상 성과를 올리며 렌더한 960×540, 24fps 미술 확인용 샘플이다. 실제 플레이 녹화와 구분한다. 연속 프레임을 열어 문틀이 시야에서 빠져나가는 상대 이동과 유지되는 원경을 확인했다. 전체 영상의 실시간 재생을 통한 감상 평가는 수행하지 않았다.

## 자동 검증

관련 순수 규칙, 실제 씬 연동, 기존 플레이 흐름과 타격 피드백, 공용 화면 이동 Smoke가 통과했다. 테스트 사이 Play Mode 종료와 Editor ready를 확인했다. 아래 실행에는 실패·skip·inconclusive가 없다. Smoke의 EditMode는 해당 사례가 없어 실행하지 않았으며 별도의 성공으로 계산하지 않는다.

| 범위 | 통과 사례 | 실행 시간 | 원본 결과 |
| --- | ---: | ---: | --- |
| Game.Domain.Tests 전체 | 237 | 1.90초 | [JSON](dojo-domain-test-status.json) |
| BackgroundProgressTests 최종 | 9 | 0.39초 | [JSON](dojo-background-test-status.json) |
| GameplayFlowTests | 4 | 17.76초 | [JSON](dojo-gameplay-test-status.json) |
| DojoBackgroundFlowTests | 1 | 6.71초 | [JSON](dojo-scene-test-status.json) |
| GameplayHitFeedbackTests | 5 | 9.04초 | [JSON](dojo-hit-feedback-test-status.json) |
| Smoke PlayMode | 5 | 37.84초 | [보고](dojo-smoke-verification.txt), [XML](dojo-smoke-playmode.xml) |

Domain 전체 실행 뒤 재진입 콜백에 대한 두 회귀 사례를 추가했다. 기존 Judged 콜백에서 세션이 재시작되어도 이전 판정을 새 세션에 적립하지 않도록 처리한 최종 구현은 BackgroundProgressTests 9건에서 검증했다. 합계는 중복 사례가 있으므로 전체 고유 테스트 수로 사용하지 않는다.

규칙 검증은 Perfect/Good 동등 적립, 예정 시각에 따른 페이즈 소속, 중복·이전 세션 판정 차단, 빈 입력·Miss, 변속 Hold의 머리/유지/끝 정규화, Automatic/Release 끝 규칙, 중도 이탈과 재잡기, rules=null 호환 경로, 짧은 곡·오프셋·빈 구간을 포함한다. 씬 검증은 실제 판정 기반 개방, pause와 예약 재개 대기 중 정지, 재시작 시 저작 오브젝트 유지, 실패해도 시간에 따른 전환, 마지막 구간 선택, 기존 레인 카메라 위치·회전 보존을 포함한다.

중간 GameplayFlow 실행 한 번은 Unity Test Runner의 시작/정리 오류로 중단했다. 취소·재컴파일·빈 테스트 씬 준비 후 해당 suite를 다시 실행해 위 결과를 얻었다. 중단한 실행과 잘못된 필터로 0건이 선택된 조회는 성공 근거에 포함하지 않는다. 최종 검증 후 필수 참조 조건을 두 개의 짧은 guard로 나눈 변경은 재컴파일로 확인했다.

이번에는 Windows Player 빌드, Hardware/Endurance 전체, 실제 PUPA 완주, 낮은 FPS, 16:10·4:3·ultrawide의 합성 화면을 확인하지 않았다. 계획 문서의 CPU/GPU p95·p99, GC, 메모리 및 재진입 10회 성능 예산도 측정하지 않았다. 따라서 해당 환경의 시각적 품질이나 성능 목표 달성을 검증했다고 주장하지 않는다.

## 주요 파일과 확장 지점

| 파일 | 역할 |
| --- | --- |
| [JudgmentSchedule.cs](../Assets/Game/Scripts/Domain/JudgmentSchedule.cs) | 기존 판정 스케줄을 공유 불변 데이터로 추출하고 세션·단위 ID가 있는 관측 사실을 정의한다. |
| [RuleSession.cs](../Assets/Game/Scripts/Domain/RuleSession.cs), [RhythmSession.cs](../Assets/Game/Scripts/Domain/RhythmSession.cs) | 기존 판정 처리 뒤 체크 결과와 입력 매칭 사실을 전달한다. 점수·체력 규칙은 유지한다. |
| [RhythmSessionCoordinator.cs](../Assets/Game/Scripts/Application/RhythmSessionCoordinator.cs) | 읽기 전용 관측 계약을 노출하고 생명주기에 맞춰 구독을 해제한다. |
| [BackgroundPhasePlan.cs](../Assets/Game/Scripts/Application/BackgroundPhasePlan.cs) | 오디오/채보 종료를 조정한 네 등분 시간표와 각 구간 기회량, 중복 제거·성과 누적을 Unity와 분리한다. |
| [GameplayBackgroundDirector.cs](../Assets/Game/Scripts/Runtime/Presentation/GameplayBackgroundDirector.cs) | 음악 시각·상태·관측 큐를 소비하고 도장/기존 배경 전환과 pause를 제어한다. |
| [DojoRoomPhaseView.cs](../Assets/Game/Scripts/Runtime/UI/Views/DojoRoomPhaseView.cs) | 저작된 문·카메라·천·먼지의 포즈와 개별 셰이더 상태만 갱신한다. |
| [GameplayBackgroundBuilder.cs](../Assets/Game/Scripts/Editor/UILayout/GameplayBackgroundBuilder.cs) | 고정 공간·공유 재질·메시·참조를 생성하고 배경 전용 카메라를 기존 Viewport에 연결한다. |
| [GameplayBackgroundPreview.cs](../Assets/Game/Scripts/Editor/SceneGeneration/GameplayBackgroundPreview.cs) | 임시 PreviewScene에서 120프레임의 미술 확인용 모션을 캡처하고 자원을 정리한다. |
| [GameplayCompositionRoot.cs](../Assets/Game/Scripts/Runtime/Core/GameplayCompositionRoot.cs), [PixelGameplayStyle.cs](../Assets/Game/Scripts/Editor/UILayout/PixelGameplayStyle.cs) | 일반 곡의 초기화/종료와 Gameplay 씬 조립에 연결한다. 훈련·편집기 경로는 기존 표현을 사용한다. |
| [GameplayWindowLight.cs](../Assets/Game/Scripts/Runtime/UI/Views/GameplayWindowLight.cs) | 일반 곡의 대체 배경에서 독립 unscaled 애니메이션이 pause 중 흐르지 않게 한다. |
| [Gameplay.unity](../Assets/Game/Scenes/Gameplay.unity), [DojoRoom 아트](../Assets/Game/Art/DojoRoom/) | 재생성한 고정 공간과 원본·파생 텍스처·재질·셰이더다. |
| [BackgroundProgressTests.cs](../Assets/Game/Tests/Domain/BackgroundProgressTests.cs), [DojoBackgroundFlowTests.cs](../Assets/Game/Tests/PlayMode/DojoBackgroundFlowTests.cs) | 판정 경계 회귀와 실제 씬의 렌더/생명주기 검증이다. |

후속 페이즈는 `BackgroundPhasePlan`과 `BackgroundPerformanceState`를 재사용하고 고유 View를 Director에 연결할 수 있다. Domain/Application은 Unity API를 참조하지 않는다. 런타임에서 고정 UI·문·소품을 재생성하지 않으며, 기존 화면 크기별 RenderTexture와 세션 상태만 동적으로 관리한다. 나머지 세 페이즈와 레인·노트·판정 UI의 새 디자인은 만들지 않았다.

신규 아트는 built-in `image_gen` 모드로 생성한 [RoomAtlas.png](../Assets/Game/Art/DojoRoom/RoomAtlas.png), [Courtyard.png](../Assets/Game/Art/DojoRoom/Courtyard.png), [DetailsAtlas.png](../Assets/Game/Art/DojoRoom/DetailsAtlas.png)다. [전체 생성 프롬프트](../Docs/dojo-room-art-prompts.json)에 기준 이미지와 파생 과정을 보관했다. 고정 crop·메시·재질은 에디터에서 굽는다. 기존 메뉴 아트의 원본과 GUID는 보존했다.

재생성은 `Game Tools > Scenes > Build Gameplay`, 미술 모션 캡처는 생성된 씬에서 `Game Tools > Preview > Capture Dojo Room Motion`을 사용한다. 실제 판정 캡처는 `DojoBackgroundFlowTests`를 실행하면 `Logs/dojo-room/`에 다시 기록한다. 설계와 실제 구현의 차이는 [인게임 시각 계획의 구현 반영](../Docs/ingame-visual-plan.md)에 기록했다.
