# 비급 전수 최소 실행 시스템 검증

2026-11-15 · `emergency` · Unity 6000.3.23f1 / Unity Test Framework 1.6.0

## 트릴 수직 슬라이스 완료 검증 (2026-11-15)

현재 비급 전수에는 **트릴 한 수련**이 연결되어 있다. 기존 판정, Gameplay, 사형 반신/전신 에셋, 공통 대화와 Tutorial 컨트롤을 재사용한다. BPM 100에서 느린 1-3/2-4 4타를 따라 한 뒤 간격을 0.6초에서 0.3초로 줄이고 8타로 확장한다. 시범은 실제 따라 하기 채보의 시각으로 기존 엔진에 입력하며 판정 이벤트와 사형의 타격 자세를 함께 표시한다. 다른 11개 수련은 추가하지 않았다.

실제 Input System 이벤트를 사용해 Title → 비급 전수 → 트릴 → 도입/설명 → 시범 → 따라 하기 → 응용 → 검정 → 피드백 → 저장/재도전 → 수련 선택 복귀를 검증했다. 무입력 누락, Good 타이밍 오차, 쉼표의 실제 추가 입력은 서로 다른 조언을 선택했다. 시범/연습 성공은 이수를 저장하지 않았고, 검정 성공만 저장했다. 이후 검정 실패에도 이수는 유지됐으며 AppFlow 제거/재생성 및 저장소 재로드 후 완료 표시가 복원됐다. 이 검사는 결과를 강제로 대입하거나 곡 시계를 끝으로 건너뛰지 않는다.

| 최종 실행 범위 | 결과 | 시간 | 근거 |
| --- | --- | --- | --- |
| 수련 Domain/EditMode (`Training`) | 14 통과 | 0.72초 | [JSON](trill-Training-tests.json) |
| 기존 조정기 회귀 (`CoordinatorTests`) | 108 통과 | 0.63초 | [JSON](trill-CoordinatorTests-tests.json) |
| 공통 대화 규칙 | 3 통과 | 0.21초 | [JSON](trill-DialogueSessionTests-tests.json) |
| 수련 PlayMode: 대화/입력 차단/재시작/중단/전체 실제 완주 | 4 통과 | 212.73초 | [JSON](trill-flow-tests.json) |
| 공통 화면 이동 Smoke | 5 통과 | 37.54초 | [JSON](trill-smoke-tests.json) |

위 모든 실행에서 실패, skip, inconclusive는 0이다. 필터 사이에 일부 사례가 중복되므로 총 사례 수로 합산하지 않는다. 모든 suite는 순차 실행했다. Tutorial/Gameplay는 최신 빌더로 재생성하고 직렬화된 참조와 guided 설정을 확인했다. 대화의 읽음/미독 경계, 이미 읽은 설명 건너뛰기, 누른 채인 키의 다음 화면 유출 방지, 취소/실패한 시작 복구, 일시정지 재시작, 고정 구성 재사용도 포함한다. 일반 곡의 판정·설정·결과 이동을 확인하는 Smoke가 함께 통과했다.

[시범 화면](../Docs/verification-images/trill-demonstration.png) · [완료 후 수련 선택](../Docs/verification-images/trill-selection-completed.png) · [새 세션 완료 복원](../Docs/verification-images/trill-restored.png)

화면은 1920×1080 Gameplay와 1280×720/1280×800 공통 대화를 확인했다. 종료 시 Play Mode와 컴파일이 끝났고 컴파일 오류가 없으며, 원래 ModeSelection 편집 씬이 복원됐다. native Keyboard/Mouse는 활성 상태이고 가상 키보드/마우스는 남아 있지 않다. [최종 상태](trill-final-state.json)

검증 과정에서 타이틀의 additive 전환 중 테스트가 이전 화면의 View를 찾던 부분을 실제 모드 화면 참조로 수정했다. 여러 실제 재도전을 포함한 검사는 기본 180초 제한을 넘어 300초로 명시했다. 지연 빌더 콜백이 반영되지 않은 실행은 유효한 최종 결과에서 제외하고, 생성 완료 파일과 Trill의 guided 직렬화 값을 확인한 뒤 다시 검증했다. 위 표는 모두 수정/재생성 이후의 통과 결과다.

범위는 Unity Editor의 자동 입력과 실제 재생/씬 이동이다. 사람이 물리 키보드로 연주하는 수동 검증, Editor 프로세스 재시작, Windows 플레이어 빌드는 실행하지 않았다. 저장 복원은 새 AppFlow와 새 저장소 인스턴스로 확인했다. 오디오 장치 설정/복구 구현은 변경하지 않아 별도 Hardware 프로필은 실행하지 않았다. 전체 저장소 테스트를 모두 실행했다는 뜻은 아니다.

아래는 앞선 **기초 실행 시스템** 단계의 기록이며 현재 트릴 범위와 구분한다.

---

## 구현 결과

기존 비급 전수 화면에서 데이터로 정의한 `기초 박자 수련` 하나를 시작하고, 기존 Gameplay에서 연주한 뒤 성공/실패/중단 결과와 재도전을 제공한다. 수련 정의, 실행 상태, 이수 저장을 분리했다. 엔진 판정 결과를 평가하며 입력·노트·시계·판정·인게임 HUD·일시정지는 기존 경로를 그대로 사용한다. 일반 곡 기록/선택/전역 규칙은 수련 결과로 변경하지 않는다.

- 경로: Title → ModeSelection → 비급 전수 → 수련 시작 → Gameplay → Tutorial 결과 → 재도전.
- 데이터: `Assets/Game/Settings/Training/BasicPulse.asset` (ID `basic-pulse`, 버전 1, Practice, SongDefinition 참조, Miss 0개 이하/정확도 50% 이상).
- 샘플: 100 BPM, 4레인 Tap 8개, 기존 TestPulse 음원. 테스트용 항목 하나만 추가하며 일반 곡 카탈로그에는 등록하지 않는다.
- 상태: Ready/Preparing/Playing/Feedback, 시도 번호, Succeeded/Failed/Aborted, 결과 스냅샷, 별도 누적 이수 여부. 재시작/중단/중복 시작/오래된 결과를 처리한다.
- 저장: `RhythmDojo.Training.v1` PlayerPrefs JSON. 성공만 ID와 정의 버전별로 기록하며 재도전 실패/중단은 이전 이수를 지우지 않는다. 버전이 다르면 미이수로 취급한다.
- 고정 UI는 빌더와 Tutorial 씬에 직렬화했다. 런타임 UI 재생성이나 임시 최상위 개발 메뉴는 추가하지 않았다.

## 실행 근거

실행 중인 Editor를 refresh/recompile한 뒤 Pipeline으로 한 suite씩 실행했다. 각 실행 완료 뒤 Play Mode 정리와 Editor 준비 상태를 확인했다. 처음 `|`로 연결한 필터는 0건이라 폐기했고, 아래 표에는 실제 실행된 결과만 포함한다.

| 범위 | 결과 | 근거 |
| --- | --- | --- |
| TrainingSession/TrainingProgress Fast | 10 통과, 실패/skip/inconclusive 0 | [JSON](training-fast-results.json) |
| TrainingFlow: 실제 재생/입력 및 중단·재시작 통합 | 2 통과, 실패/skip/inconclusive 0 | [JSON](training-flow-results.json) |
| 기존 Smoke: 메뉴·곡 선택·게임 시작/재시작·복귀·결과/재도전 | 5 통과, 실패/skip/inconclusive 0 | [JSON](training-smoke-results.json) |
| 실제 재생 시나리오와 Gameplay 표시 재확인 | 1 통과, 실패/skip/inconclusive 0 | [JSON](training-playback-results.json) |
| 기존 비급 전수 메뉴 진입/복귀 | 1 통과, 실패/skip/inconclusive 0 | [JSON](training-menu-results.json) |
| 채보 에디터 테스트 플레이 결과/복귀/음원 해제 | 1 통과, 실패/skip/inconclusive 0 | [JSON](training-authoring-results.json) |
| 기존 일시정지/카운트다운/재시작/복귀 | 3 통과, 실패/skip/inconclusive 0 | [JSON](training-pause-results.json) |
| 씬 반복 생성/GUID·자원 보존/스냅샷 복원 | 4 통과, 실패/skip/inconclusive 0 | [JSON](training-generation-results.json) |

실제 재생 검사는 가상 Keyboard의 Press/Release를 Input System 이벤트로 전달했다. 노트 판정 결과를 강제로 대입하거나 곡 시계를 끝으로 건너뛰지 않았다. 무입력 실패 → 재도전 → 8 Perfect 성공 → 재도전 실패 → 이수 유지 → AppFlow 제거/재생성 후 저장 복원까지 검사했다. 해당 시나리오는 Endurance로 분류했다. 별도 SceneIntegration은 UI 클릭, 잘못된 데이터의 로드 거부, 중복 시작 차단, UI 재초기화/재활성화 시 고정 구성 보존, pause 재시작/나가기, 일반 연습 재진입을 검사한다.

수련 Gameplay 캡처는 기존 렌더 타깃을 사용하는 `CaptureGameplayDisplay`를 사용했다. 일반 메뉴용 캡처 도구는 Gameplay에서 검은 프레임을 반환해 교체했고, 표시 내용이 있는지 검사한 뒤 실제 캡처를 확인했다.

화면: [시작](training-ready.png) · [플레이](training-playing.png) · [실패/재도전](training-failed.png) · [성공](training-succeeded.png) · [저장 복원](training-restored.png)

씬 전체 재생성은 기존 검사의 두 차례 생성/복원 경로로 확인했다. 새 Training 자원은 기존 `GeneratedSceneSnapshot`의 Settings 재귀 복원 범위에 포함되어 별도 스냅샷 경로 확장이 필요하지 않다. 최종 변경 씬은 Tutorial 하나이며, 다른 생성 씬/자원은 복원되었다. `git diff --check`도 통과했다.

종료 시 Play Mode stopped / Editor ready / 컴파일 중 아님을 확인했다. 원래 Results 씬을 dirty 없이 복원했고 AppFlow 객체는 0개다. native Keyboard/Mouse/Xbox Controller는 활성 상태이며 가상 장치는 남아 있지 않았다. [정리 상태](training-cleanup.txt)

## 범위와 제한

대화/스토리 진행, 캐릭터 연출, 자동 시범, 별도 검정 콘텐츠, 다중 수련/해금, 트릴 교대 순서·추가 입력 평가, 세션 중간 복원은 미구현이다. Practice/Assessment 단계 값과 실행/결과 API만 확장 지점으로 남겼다. 일반 리듬 엔진은 빈 입력을 감점하지 않으므로 이 샘플을 트릴 수행 검증 완료로 해석하지 않는다.

검증은 Editor Play Mode의 자동 입력 및 실제 재생/씬 전환이다. 저장소 재생성과 AppFlow 수명 교체를 확인했으며 Editor 프로세스 재시작, 사람이 물리 키보드로 연주한 수동 검증, 플레이어 빌드는 실행하지 않았다. 오디오 장치 복구/스케줄링 구현을 변경하지 않아 별도 Hardware 프로필은 실행하지 않았다. 전체 테스트 통과 보고가 아니다.

작업 시작 전부터 있던 ResultsPreview.cs, ResultsScreen.cs, ResultsFlowTests.cs, Docs/verification.md, Logs/mock-preview-verification.md의 변경은 보존했다. 원래 계획 문서에는 이번 축소 범위의 구현 현황을 추가했다.
