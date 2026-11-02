# 시간 기준 그리드와 변속 간격 — 2026-11-03

- 위쪽 편집 그리드를 시간에 비례하는 좌표로 변경했다. 시작 BPM과 확대 배율이 같으면 BPM 변경점을 지나도 같은 시간 동안 같은 화면 거리를 이동한다.
- 기존 BeatProjection 생성자는 유지하고 BeatGridTiming을 받는 생성자를 추가했다. ChartBeatGrid의 선·노트·롱노트·커서·미리보기와 클릭 역변환이 같은 좌표를 사용한다.
- 화면 상단의 마지막 박을 시간 좌표에서 역변환해 구한다. 고속 BPM 구간에서 기존 visibleBeats 상한에 의해 위쪽 클릭이 잘리는 문제를 방지한다. 잡기 도구도 커서와 노트 사이의 시간 차이를 유지한다.
- 노트의 음악 시간과 게임 판정 시각은 바꾸지 않는다. 시간 기준 확대 범위는 시작 BPM 기준 박 수에 해당한다.

## 사용자 지정 음원 검증

- 같은 Saiki 음원을 사용했다. 시작 BPM 128, 7.5초부터 256, 15초부터 64를 적용했다.
- 256 구간의 박자선 간격이 128의 절반, 64 구간이 두 배인지 확인했다.
- 실제 음원을 재생하며 두 변경점을 통과할 때 위쪽 그리드의 이동 거리가 경과 음악 시간에 비례하는지 확인했다.
- 세 구간에서 화면 위쪽을 실제 InputSystem 마우스로 클릭하고 스냅된 노트 시간 및 HitTest가 일치하는지 확인했다. 기존 표시 박 수를 넘는 고속 구간의 클릭도 검증했다.
- `tempo-spacing-128-256.png`, `tempo-spacing-256-64.png`를 렌더링해 조밀해지는 구간과 넓어지는 구간을 시각적으로 확인했다.

## 실행 범위와 결과

- Unity 6000.3.23f1, 별도 임시 프로젝트에서 순차 batch 검증. 사용자 작업창은 제어하지 않았다.
- BeatProjectionTests EditMode: **4/4 passed**. 시간당 거리, 128/256/64 간격 비율, 음수 시각·그리드 원점·소수 BPM 변경점의 좌표 왕복 포함. `time-grid-projection-editmode.xml`.
- WorkspaceEditorTests PlayMode: **38/38 passed**. 실제 Saiki 변속 재생·노트 배치 및 기존 편집 기능 포함. `time-grid-workspace-playmode.xml`.
- ChartProjectionRenderingTests PlayMode: **3/3 passed**. 610×337 / 1280×720, 확대·축소, 경계 클리핑, 최대 개요 메시 검증. `time-grid-rendering-playmode.xml`.
- ChartEditorScreenTests PlayMode: **4/4 passed**. 기존 클릭·롱노트·삭제·휠·줌·배치 확인. `time-grid-input-playmode.xml`.
- 관련 테스트 총 **49 passed, 0 failed, 0 skipped**. 전체 테스트나 Windows 빌드 통과를 의미하지 않는다.
- 단독 렌더링/입력 fixture가 타이틀 강제 시작 설정을 보관·복원하도록 기존 TestRunStartup 연결을 추가했다. 처음 렌더링 실행은 타이틀에서 대기해 종료하고, 연결 후 실제 테스트를 실행했다. 검증용 프로세스만 종료했으며 사용자 Editor는 종료하지 않았다.
- 코드·테스트·문서 diff 공백 검사 통과. UI/편집 좌표 변경에 필요한 관련 fixture를 실행했으며, 탐색·씬 구성·플랫폼 설정 변경이 없어 Smoke·전체 테스트·Windows 빌드는 실행하지 않았다.
