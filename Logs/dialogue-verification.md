# 공통 사형 대화 검증 — 2026-11-15

`emergency`에서 기존 여자 사형 반신 7종과 UI·수련 흐름을 재사용해 대화 시스템을 구현했다. 수련 시작 전에 예시 3줄을 재생하고, 성공·실패·중단으로 복귀하면 사형이 결과를 안내한다. 대화 데이터, Unity 비의존 진행 로직, 화면 표시를 분리했다. 새 채보나 플레이어 빌드는 만들지 않았다.

## 실행 근거

실행 중인 Unity **6000.3.23f1**, Pipeline **0.8.0-exp.1**, Test Framework **1.6.0**을 사용했다. `recompile` / `recompile_status`로 오류 없는 재컴파일을 확인하고, 기존 **Build Tutorial**로 대화 계층·오디오·참조가 포함된 Tutorial 씬을 저장했다. 각 검사 종료 후 Test Runner 정리를 기다리고 다음 검사를 순차 실행했다.

| 범위 | 결과 | 시간 | 로컬 상세 결과 |
| --- | --- | --- | --- |
| DialogueSessionTests / EditMode | 3 통과, 실패·skip·inconclusive 0 | 0.06초 | [JSON](dialogue-domain-results.json) |
| TrainingFlowTests / PlayMode, Endurance 포함 | 4 통과, 실패·skip·inconclusive 0 | 58.82초 | [JSON](dialogue-training-results.json) |
| 최종 레이아웃의 키보드·마우스·입력 해제 후 상태 전환 | 1 통과, 실패·skip·inconclusive 0 | 6.95초 | [JSON](dialogue-final-layout-results.json) |
| 기존 Smoke / PlayMode | 5 통과, 실패·skip·inconclusive 0 | 37.56초 | [JSON](dialogue-smoke-results.json) |
| 모드 선택에서 비급 전수 진입·복귀 / PlayMode | 1 통과, 실패·skip·inconclusive 0 | 8.14초 | [JSON](dialogue-navigation-results.json) |

대화 통합 검사에서는 다음 동작을 실제 Input System 키보드·마우스 이벤트와 저장된 씬으로 확인했다.

- 출력 도중 Enter로 현재 대사를 완성하고, 다음 확인에서만 진행한다. Space 및 실제 UI 클릭도 동일하게 동작한다.
- 설명 대사로 넘어가면 Teaching 표정으로 교체된다. 고정 계층은 진행 중 새로 생성되거나 교체되지 않는다.
- 미독 대사는 Tab으로 넘길 수 없다. 읽음 기록은 씬 재진입 이후 유지되고, 연속 읽음 건너뛰기는 첫 미독에서 멈춘다. 문구를 바꾸면 다시 미독이 된다.
- 대화 중 Escape·연주 키로 수련이나 화면 이동이 시작되지 않는다. 마지막 확인에서 Enter와 연주 키를 함께 계속 누르면 퇴장 이후에도 Gameplay 생성이 보류된다. 해제하면 수련 시도 한 번으로 전환하며 대화창은 게임플레이 씬에 남지 않는다.
- 비활성화·취소는 완료 콜백을 호출하지 않는다. 시작 요청 실패 후 수련 화면 조작이 복구되며, 모든 대사를 읽은 후 건너뛰기도 완료 콜백을 한 번만 호출한다.
- 기존 실제 채보를 실패 → 재도전 → Perfect 8개로 성공 → 다시 실패 순으로 끝까지 재생했다. 각 종료 안내, 이수 기록 유지, 새 AppFlow에서의 완료 기록 복원을 확인했다. 일시정지·재시작·중단 및 일반 자유 연주 복귀도 통과했다.

최종 시각 수정은 보조 문구 대비, 제목 먹칠 폭, 붉은 전수 표기다. 이 수정 뒤 해당 화면·입력 전환 검사를 다시 실행했다. 전체 프로젝트 검사나 Windows 플레이어 빌드의 통과를 의미하지 않는다. 효과음은 기존 클립과 작성된 AudioSource를 재사용하며 별도의 청음/출력 장치 품질 측정은 하지 않았다.

두 번째 씬 재생성 직후 Pipeline의 검사 시작 요청이 한 차례 메인 스레드 30초 제한으로 종료됐다. 실제 검사가 시작되지 않았고 이전 완료 결과가 남아 있음을 확인한 후 Editor가 응답할 때 다시 실행했으며, 위 최종 레이아웃 검사는 재시도의 실제 완료 결과다.

## 실제 화면

다음 이미지는 PlayMode에서 실행 중인 Canvas 및 실제 GameplayViewport를 캡처한 결과다.

- [대화 화면 · 1280×720](../Docs/verification-images/dialogue-teaching.png)
- [대화 화면 · 1280×800](../Docs/verification-images/dialogue-teaching-16x10.png)
- [대화 종료 후 실제 수련 채보](../Docs/verification-images/dialogue-training-playing.png)

원본 로컬 검사 캡처는 `Logs/dialogue-calm.png`, `dialogue-confident.png`, `dialogue-feedback-{Succeeded,Failed,Aborted}.png`에도 남겼다. 결과 안내 캡처는 마지막 제목·보조 문구 시각 수정 이전의 실행이다. JSON과 원본 PNG는 기존 Logs ignore 정책을 유지하고, 이 문서와 Docs의 검토 이미지는 저장소에 남길 수 있는 경로를 사용했다.

데이터 편집·다른 수련 연결·재생성 방법은 [대화 시스템 문서](../Docs/dialogue-system.md)에 정리했다.
