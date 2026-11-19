# 공통 사형 대화

`emergency`의 기존 사형 반신 일러스트 7종, 수련방 배경, 한지 패널 내부 질감, 먹칠 그래픽과 효과음을 재사용한다. 비급 전수의 **수련 시작 / 재도전**을 누르면 시작 대화를 재생한다. 마지막 대사 확인 → 퇴장 → 누르고 있던 키와 마우스 버튼 해제 → 기존 Gameplay 씬 순서로 진행한다. 성공·실패·중단으로 Tutorial에 복귀하면 해당 안내 대화를 먼저 보여 준다.

## 조작

- Enter, 숫자패드 Enter, Space 또는 대화 종이 클릭: 출력 중인 대사 완성. 이미 완성된 경우 다음 대사.
- Tab 또는 **읽은 대사 건너뛰기**: 연속해서 읽은 대사만 넘기며 첫 미독 대사에서 멈춘다. 모두 읽었다면 퇴장 후 다음 상태로 이동한다.
- 대화 중 수련 시작, 뒤로 가기, Escape와 연주 키는 수련이나 화면 이동을 실행하지 않는다. 대화 종료에는 키 해제가 필요하다.

## 데이터와 재사용

`Assets/Game/Settings/Training/Dialogue/Trill*.asset`에 트릴의 도입·단계 설명·성공·실패·중단 대사를 저장한다. `TrainingLesson`이 단계별 대화와 실제 결과에 해당하는 조언만 선택하여 공통 `DialogueScreen`에 전달한다. 기존 기초 박자 에셋은 앞선 공통 시스템의 샘플이며, 제품 진입은 트릴 하나다. 상세 흐름은 [트릴 구현 기록](bigup-training-plan.md)을 따른다.

새 대화는 **Create > Rhythm Dojo > Dialogue Sequence**로 만든다. `sequenceId`는 에셋마다 고유하게, 각 줄의 `id`는 해당 시퀀스 안에서 고유하게 지정한다. `speaker`, `text`, `expression`, `motion`을 지정한다. 표정은 Calm / Playful / Serious / Confident / Exasperated / Teaching / Praising, 작은 동작은 Breathe / Nod / Lean을 지원한다. 현재 레이아웃은 간결한 1~2줄 대사에 맞췄으므로 긴 설명은 여러 대사로 나눈다.

- Domain의 `DialogueLine`: Unity와 무관한 불변 대사와 표정·동작 값.
- Application의 `DialogueSession`: 문자열 요소 단위 출력, 확인, 읽음 기록 계약, 미독 경계와 종료 상태. 화면·입력·오디오에 의존하지 않는다.
- Runtime의 `DialogueSequenceAsset`: Inspector 데이터 → 불변 대사 변환.
- `DialogueReadStore`: PlayerPrefs의 `RhythmDojo.DialogueRead.v1`에 읽음 키의 SHA-256 해시를 저장한다. 완전히 출력하거나 즉시 완성한 대사만 기록한다. 시퀀스 ID·버전·줄 ID·화자·문구가 달라지면 다시 미독으로 취급한다. 도중 취소한 부분 출력은 읽음으로 기록하지 않는다.
- `DialogueScreen` / `DialogueView`: 입력, 타이핑 표시, 두 기존 Image의 표정 교차 페이드, 작은 동작, 등장·퇴장 easing, 기존 AudioSource의 효과음 재생을 담당한다. 게임 상태 전환은 완료 콜백으로 호스트에 돌려준다. `Cancel`과 비활성화는 완료 콜백을 호출하지 않는다.

다른 화면에 붙일 때는 Editor builder에서 `DialogueLayoutBuilder.Build(parent)`로 고정 계층을 만들고, `DialogueScreen`과 AudioSource·클립 참조를 연결한다. 호스트는 대화 전에 자신의 입력·이동 컨트롤을 차단하고 `Play(sequence, onCompleted)`를 호출한다. 완료 콜백에서 다음 상태로 이동하거나 컨트롤을 복구한다. 런타임에 고정 UI·오디오·배우를 생성하거나 검색해서 복구하지 않는다.

## 씬과 연출 수정

레이아웃은 `DialogueLayoutBuilder`, 연출 값은 `DialogueView`, Tutorial 조립은 `TutorialSceneBuilder`에서 수정한다. **Game Tools > Scenes > Build Tutorial**로 씬을 재생성한다. 트릴 대사는 `TrillContentBuilder`가 기준이며 Build Tutorial에서 재생성한다. 트릴 문구를 지속적으로 수정하려면 이 빌더를 변경한다. 앞선 `DialogueContentBuilder`의 기초 박자 샘플만 최초 생성 시 작성하는 기존 동작을 유지한다.

대화의 자체 입력 처리와 EventSystem Submit가 한 번의 확인을 중복 처리하지 않도록 대화 버튼의 navigation을 끄고 프레임당 입력을 한 번만 허용한다. 퇴장 중에도 전체 화면 raycast 차단을 유지하며 키·마우스가 풀린 뒤 한 프레임을 지나 완료시킨다. 실제 채보는 그 뒤 생성되므로 대화 확인 입력이 노트 판정이나 일시정지로 전달되지 않는다.

검증 근거: [dialogue-verification.md](../Logs/dialogue-verification.md).
