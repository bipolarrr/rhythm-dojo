# 에디터 오른쪽 상단 메인으로 버튼 제거 — 2026-11-03

- ChartEditorSceneBuilder에서 오른쪽 상단 `Showcase Main Menu` 버튼 생성 코드를 제거했다. 원래 ChartEditor 씬에서는 해당 버튼·텍스트·프레임의 오브젝트 3개와 연결만 제거해 나머지 씬 직렬화 변경을 피했다.
- 기존 파일 → 종료 · 타이틀로 및 저장 후 종료는 유지했다. 제거된 버튼을 사용하던 RemediationPlayerProbe의 복귀 경로도 파일 메뉴의 종료 확인과 저장 후 종료를 사용하도록 수정했다.
- 사용자 Unity 작업창은 제어하지 않았다. Unity 6000.3.23f1의 별도 임시 프로젝트에서 컴파일 및 씬 재생성을 실행하고 생성된 씬에도 버튼이 없는 것을 확인했다.
- 실제 수정된 씬을 임시 프로젝트에 복사해 렌더링했다. 오른쪽 상단 버튼 없음과 기존 파일 종료 action 참조를 확인했다. `remove-editor-main-button.png`, `remove-editor-main-button-preview.log`.
- 관련 `WorkspaceEditorTests.EscapeReturnsToTitleWhenSavedAndNoMenuIsOpen`: 1 passed, 0 failed, 0 skipped. `remove-editor-main-button-playmode.xml`.
- 전체 테스트, Smoke, Windows 빌드는 단일 에디터 버튼 제거 범위에 따라 실행하지 않았다. Player 진단 경로는 컴파일했으며 전체 진단 player는 빌드·실행하지 않았다.
