# 에디터 기본 커서·클릭 효과·글꼴 복원 — 2026-11-03

- ChartEditor에서는 시스템 기본 화살표 커서를 사용한다. 지속 객체인 DojoCursor가 활성 씬 전환을 감지해 에디터에서 주먹 커서·자국·클릭음을 비활성화하고, 다른 화면으로 돌아가면 기존 스타일을 복원한다. 에디터 음원과 노트 미리듣기는 이 클릭음 소스와 별개다.
- UiFontScope에 스타일 설정을 추가했다. 기존 화면의 기본값은 Bold를 유지하며, 에디터 캔버스는 이전 일반 글꼴인 NotoSansKR-Regular 및 Normal을 사용한다. 숨겨진 메뉴·팝업·드롭다운 텍스트도 동일하게 적용했다.
- ChartEditorSceneBuilder에 범위를 반영했다. 현재 씬은 기존 버튼 제거 상태에서 글꼴과 범위만 갱신해 저장했다. 앞서 제거한 오른쪽 상단 메인으로 버튼은 계속 제거되어 있다.
- Unity 6000.3.23f1의 별도 임시 프로젝트에서 검증했다. 사용자 Unity 작업창은 제어하지 않았다.

## 결과

- 실제 씬의 모든 Text 글꼴·스타일 검사 및 렌더링 확인: `editor-default-presentation.png`, `editor-default-presentation-preview.log`.
- `WorkspaceEditorTests.EditorUsesDefaultPresentationAndRestoresThemedPointerOnTitle`: **1/1 passed**. 에디터 기본 커서 상태, 클릭음·자국 비활성화, 숨겨진 텍스트, 타이틀 이동 후 커서·클릭음 복원, 에디터 재진입을 확인했다. `editor-default-presentation-playmode.xml`.
- `UiTypographyTests` EditMode: **9/9 passed**. 다른 화면의 기존 글꼴·스타일 범위와 에디터 Normal 범위를 확인했다. `editor-default-typography-editmode.xml`.
- `PixelDojoCursorTests` PlayMode: **5/5 passed**. 다른 화면의 기존 주먹 커서·애니메이션·클릭 자국 회귀 검증. `editor-default-cursor-regression.xml`.
- 관련 테스트 총 **15 passed, 0 failed, 0 skipped**. 각 검증은 순차 실행했고 마지막 검증 프로세스 종료를 확인했다.
- 소스·테스트 diff 공백 검사 통과. 화면 스타일 변경에 필요한 관련 범위만 실행했으며 전체 테스트·Smoke·Windows 빌드는 실행하지 않았다.

## 실제 작업창 반영

- 사용자 요청으로 실행 중인 Unity를 새로고침하고 타이틀 메뉴에서 에디터를 직접 열었다.
- 일반 글꼴의 줄 높이 때문에 작은 입력칸·드롭다운 값이 잘리는 것을 발견해, Normal 글꼴 범위의 해당 텍스트만 세로 Overflow를 허용했다. 다른 화면의 Bold 범위는 변경하지 않았다.
- 기존 전환 테스트에 입력 숫자와 드롭다운의 실제 생성 글자 수 검사를 추가해 재실행했고 1/1 통과했다.
- Unity 새로고침 후 에디터를 다시 열어 BPM 120, 첫 박 위치 0, 미세 이동량 1, 스냅 1/4가 실제 화면에 정상 표시되는 것을 확인했다. 에디터를 열린 상태로 두었다.
