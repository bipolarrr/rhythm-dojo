# 플레이 키 설정

설정창 왼쪽 **게임플레이** 아래 **키 셋팅** 탭을 연다. 기본 키는 왼쪽 레인부터 **D / F / J / K**다.

1. 변경할 레인의 나무 버튼을 누른다.
2. 사용할 키보드 키를 누른다. 변경 즉시 자동 저장된다.
3. **기본값으로 복원**을 누르면 네 키 모두 D / F / J / K로 돌아간다.

다른 레인과 같은 키는 지정할 수 없다. 입력 대기 중 Esc는 변경을 취소한다. Space는 플레이 시작/재시작 전용으로 유지한다. 키 설정은 게임을 종료한 뒤에도 유지된다.

플레이 화면 중앙 하단의 네 키 안내 박스와 실제 입력은 같은 저장 설정을 사용한다. 일시정지 메뉴에서 설정을 열어 키를 변경하면 안내 박스가 바로 갱신되며, 게임을 재개할 때 바뀐 키를 사용한다. 원본 게임 모드 에셋은 수정하지 않는다. 네 레인 이외의 모드는 기존 키 설정을 유지한다.

`PlayKeyBindings`가 `RhythmDojo.PlayKeys.v1` PlayerPrefs에 설정을 저장한다. `KeySettingsController`가 키 입력 대기/취소/중복 검사를 처리하고, `LaneKeyboardInput`과 `RhythmHud`가 변경 알림을 구독한다. `SettingsLayoutBuilder.BuildKeySettings`가 새 UI를 생성하며, 기존 나무 버튼 스타일을 사용한다. 설정 씬과 공용 `SettingsPopup.prefab`에 같은 View 참조를 저장한다.

관련 검증은 `PlayKeyBindingsTests`와 `KeySettingsFlowTests`다. PlayMode 테스트는 사용자 키 설정을 백업하고 기본값으로 격리한 뒤 종료 시 복원한다.
