# 변속 버튼 및 그리드 세부 설정 이동 — 2026-11-03

- 하단 고급 GRID 버튼을 변속 버튼으로 대체했다. 내부 필드·오브젝트 이름은 기존 이름을 유지했다.
- 파형 커서 위치를 현재 스냅 단위에 맞춰 고정하고 마디·박 / 음원 시간 / 현재 BPM을 표시하는 모달 창을 추가했다. 새 BPM을 Enter 또는 적용으로 추가하며, 같은 위치의 변경점은 수정한다. 기존 변경점 삭제와 Esc·취소를 지원한다. 첫 BPM은 삭제할 수 없고 유한한 양수 BPM만 허용한다.
- 변속 창에서는 기존 고급 설정의 노트 유지 정책과 관계없이 모든 기존 노트의 음악 시간을 유지한다. 박자표는 그대로 보존한다. 기존 템포 맵 저장 형식을 사용해 Undo/Redo와 저장을 지원한다.
- 상세 파형에 청록색 변경점 선과 `BPM 128 → 160` 표시를 추가했다.
- 박자표·변경점 직접 편집·BPM 계산·노트 유지 정책·표시 밀도는 설정 → 그리드 세부 설정으로 옮겼다. 씬 빌더를 수정하고 ChartEditor.unity를 재생성했다.

## 검증

- 현재 작업창의 CLI 연결이 끊겨 별도 임시 프로젝트 `Temp/negative-cursor-verification`에서 Unity 6000.3.23f1로 컴파일·씬 생성·PlayMode 검증을 수행했다. 화면 제어는 하지 않았다.
- WorkspaceEditorTests: **37/37 passed**, skipped 0. 실제 Saiki 음원을 가져와 BPM 128 적용 후 새 변속 창의 위치 스냅, 실제 버튼 클릭·Enter·Esc, 추가·수정·삭제, 노트/박자표 보존, Undo/Redo, 저장 내용을 확인했다. `tempo-dialog-workspace-tests.xml`.
- 시각 검증용 캡처를 추가한 뒤 해당 변속 테스트를 재실행해 **1/1 passed**. `tempo-dialog-visual-tests.xml`. `tempo-change-dialog.png`, `tempo-change-marker.png`에서 실제 Saiki 파형과 팝업·BPM 전환 표시 배치를 확인했다.
- Smoke: **3/4 passed, 1 failed**. `MainMenuFlowTests.StartLoadsBootstrapThenExistingSongSelection`이 타이틀 입장 배경과 곡 선택 배경 구도 비교에서 68.2326355px 차이로 실패했다. 단독 재실행에서도 같은 값으로 재현됐다. 이번 작업에서 해당 화면 코드와 Title 씬을 수정하지 않았으며, 이 결과를 전체 통과로 간주하지 않는다. `tempo-dialog-smoke-tests.xml`, `tempo-dialog-smoke-recheck.xml`.
- 씬을 원래 프로젝트로 복사한 뒤 검증한 씬과 SHA-256 일치를 확인했다. 기존 사용자의 다른 씬·프리팹 변경은 유지했다.
- 소스·테스트·문서에 한정한 diff 공백 검사 통과. 전체 테스트 및 Windows 빌드는 기능/UI 변경 범위에 따라 실행하지 않았다.
