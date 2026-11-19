# 네 페이즈 수련 여정 — 2026-11-18

`emergency`에서 기존 도장 수련실을 유지하고 산 오르기·능선 질주·목인 수련을 추가했다. 각 페이즈는 상시 환경 운동, 정상 판정 반응, 누적 성과에 따른 절정을 가진다. 곡의 실제 재생 구간을 네 등분하는 기존 `BackgroundPhasePlan`과 판정 관측을 재사용했다. 레인·노트·HUD 디자인, 음악 재생, 판정창, 스크롤 속도는 변경하지 않았다. 다음 UI/이펙트 리디자인은 수행하지 않았다.

## 구현

- **산 오르기:** 고정 경사 지형, 근거리 암벽·수풀·소나무, 독립 안개·구름, 먼 계곡을 별개 메시로 구성했다. 정상 판정만 고도 목표를 올린다. 성과 85% 이상에서는 정상 접근 후 아래 계곡과 작은 도장을 내려다본다. 저성과에서는 낮은 고도에 남는다. 원경은 전진에 따른 과도한 확대를 피하며 지형과 수목은 실제 깊이에 따라 시차를 만든다.
- **질주:** 미리 배치한 길·수목·수풀·바람 흔적을 재사용한다. 노트 단위로 정규화한 성공 충격이 추진력을 올리고, Miss·빈 입력·롱노트 이탈이 이를 낮춘다. 이후 정상 입력이 속도를 회복한다. 성과 85%에서는 숲 가장자리가 열리고 시야가 넓어진다. 무입력 기본 이동은 환경 운동이며 성과를 만들지 않는다. 카메라의 실패 반응은 배경 전용 작은 횡변위다.
- **목인:** 고정 밑동, 축, 두 회전 타격목, 목재 몸통 조각, 좌우 주먹과 소매, 재사용 목재 먼지를 저작했다. 머리·끝 판정은 손과 목재 충격을 같은 55ms 접촉 시점으로 맞추고, 유지 체크는 압박·떨림으로 잇는다. 실패는 짧은 손 털기이며 입력을 잠그지 않는다. 누적 성과는 나뭇결 균열에 반영한다.
- **피니시:** `Completed`, 실제 `SessionSnapshot.IsFullCombo`, Miss 없음, 모든 고유 판정 단위 관측 완료, 전체 곡의 빈 입력·롱노트 중도 이탈 없음이 모두 필요하다. Good은 허용한다. 마지막 노트나 페이즈 진행도만으로 발동하지 않는다. 성공 시 실제 몸통·팔·머리 11개 조각이 중력 곡선과 감쇠 착지로 무너진다. 실패 기록이 있으면 목인을 세운 채 손을 내리고 짧게 고개를 숙인다. 기존 결과 대기 1초/2초와 전환 소유권을 유지한다.
- **전환:** 첫 도장의 문 개방·연속 전진은 기존 뷰 그대로 유지했다. 이후 배경 투영을 겹치고 바람에 실린 먹·안개 띠를 흘려 공간을 연결한다. 짧은 곡은 도장 퇴장 화면의 연결 시간을 줄여 다음 구간을 가리지 않는다. 음악상 페이즈는 항상 기존 네 구간을 따른다. 프레임보다 짧은 구간을 충분한 길이로 보여주겠다는 보장은 하지 않는다.

고정 구성은 `TrainingJourneyBuilder`에서 작성하고 `GameplayBackgroundBuilder`가 연결한다. `Game Tools > Scenes > Build Gameplay`로 재생성할 수 있다. 이번에는 전체 씬 재생성 후 후속 조정에서 Gameplay의 새 배경 구성만 재생성했다. 별도 Open Scene 메뉴는 추가하지 않았다. 런타임에는 기존 `GameplayViewport`의 화면 크기별 렌더 타깃과 상태 자료만 할당하며, 타격마다 오브젝트·재질을 생성하지 않는다.

## 검증 근거

Unity 6000.3.23f1에서 재컴파일 후 관련 검사들을 순차 실행했으며 아래 범위가 모두 통과했다(실패·건너뜀·불확정 0). 기존 도장의 문·전진·정지/재개 회귀, 16초 곡의 네 구간, 정상/무입력/오입력, 고도와 속도의 차이, 타격과 손 위치, 일시정지, 재시작 후 파편 복구, 레인 카메라와 스크롤 고정, 전체 곡 피니시 자격을 확인했다. 실제 음악 시계로 무입력 곡 종료 후 Results로 이동하는 Endurance와 종료 대기 중 재시작도 통과했다. 전체 테스트와 Player 빌드는 실행하지 않았다. 별도 GPU/메모리 성능 측정, 장시간 실기 연주 및 모든 화면비의 수동 검토를 완료했다는 의미는 아니다.

| 범위 | 결과 파일 |
| --- | --- |
| 진행도·완료 자격, 13 사례 | [Domain 결과](training-journey/progress-tests.json) |
| 기존 도장 포함 PlayMode, 3 사례 | [첫 실행](training-journey/playmode-first.json) |
| 화면 캡처·타격 확인을 보강한 여정 검사 | [최종 실행](training-journey/journey-final-tests.json) |
| 종료 대기 중 재시작 | [결과](training-journey/results-restart.json) |
| 실제 음악 시계 완료/Results, Endurance | [결과](training-journey/results-clock.json) |
| Smoke, 5 사례 | [결과](training-journey/smoke-tests.json) |

처음 EditMode 실행은 Unity의 수정 씬 저장 확인 창에서 진행되지 않아 취소했다. 그 실행을 통과로 집계하지 않았다. 당시 열린 ModeSelection의 메모리 상태를 별도 사본으로 보존하고 빈 편집 씬에서 검사를 실행했다. 기존 작업 트리의 다른 수정은 유지했다. 초기 컴파일의 필드/지역 변수 이름 충돌은 기존 이름을 바꾸지 않고 `this.passage`로 명확히 지정해 해결했다.

검증 뒤 원래 ModeSelection을 다시 열고 사본과 비교하여 동일한 씬 내용을 확인했다. Play Mode와 컴파일이 끝난 ready 상태이며 임시 Assets 내 백업 씬은 제거했다. 미술 프리뷰의 카메라 해제 전에 렌더 타깃을 분리하도록 보완하고, 별도 캡처에서 렌더 타깃 경고나 예외 없이 정리됨을 [확인](training-journey/preview-cleanup.log)했다.

## 화면과 아트

실제 판정 테이프와 Gameplay HUD를 사용한 캡처는 [gameplay 폴더](training-journey/gameplay)에 있다. 테스트 시계로 16초 채보를 재생하며 실제 Coordinator/Domain 판정을 거친 증거이고, 실시간 사람이 연주한 영상은 아니다. 미술용 모션 프리뷰는 별도 PreviewScene에서 가상 성과를 넣어 Unity로 렌더링한 것으로 판정 검사와 구분한다.

| 실제 Gameplay 화면 | 비교 자료 |
| --- | --- |
| 산 정상 조망 | [고성과](../Docs/verification-images/training-mountain-summit.png) · [무입력](../Docs/verification-images/training-mountain-low.png) |
| 질주 공간 | [고성과](../Docs/verification-images/training-run-high.png) · [무입력](../Docs/verification-images/training-run-low.png) |
| 목인 타격과 종료 분기 | [타격](../Docs/verification-images/training-dummy-hit.png) · [무실수 붕괴](../Docs/verification-images/training-dummy-collapse.png) · [앞 구간 오입력 후 온전한 종료](../Docs/verification-images/training-dummy-standing.png) |

미술용 모션 프리뷰(각 15초, 960×540, 30fps): [산 오르기](../Docs/verification-images/training-mountain-motion.mp4), [질주](../Docs/verification-images/training-run-motion.mp4), [목인 타격·파괴](../Docs/verification-images/training-dummy-motion.mp4). 이 프리뷰는 HUD를 제외하고 환경·타격 움직임을 보기 위한 합성 성과 샘플이다. 캡처 요청은 Pipeline의 30초 응답 제한에 걸렸지만 Editor의 렌더링은 계속되므로, 최종 완료 표식과 각 451프레임을 확인하여 영상을 만들었다.

새 원본 다섯 종류는 built-in `image_gen`으로 제작하여 [TrainingJourney 아트](../Assets/Game/Art/TrainingJourney)에 저장했다. [전체 프롬프트](../Docs/training-journey-art-prompts.json)를 보관했다. 투명 부품 추출, 목재 분할 메시, 소매 연결, 재질은 에디터에서 결정적으로 생성하며 원본 알파를 유지한다. 기존 첫 도장 원본과 `DojoRoomPhaseView`는 수정하지 않았다.
