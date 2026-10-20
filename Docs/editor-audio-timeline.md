# 채보 에디터 파형과 GRID

`Assets/Game/Scenes/ChartEditor.unity`를 Project 창에서 열고 Play Mode로 실행한다. 기존 파일 메뉴에서 WAV/OGG/MP3를 가져오면 전체 파형과 확대 파형이 생성된다. UI는 `ChartEditorSceneBuilder`와 `ChartAudioTimelineLayoutBuilder`가 생성한다. 빌더 수정 후 **Build Chart Editor**로 씬을 재생성한다. 런타임은 직렬화된 View 참조로 이벤트와 표시를 갱신한다.

- 전체 파형 클릭/드래그: 음악 위치 이동. 확대 파형 클릭: 탐색, 드래그: 구간 이동, Ctrl+휠: 마우스 시점 중심 확대/축소. 휠: 구간 이동.
- 재생/일시정지: DSP 시계를 기준으로 탐색한 위치에서 재생. `재생 따라가기`는 수동 탐색 후 자동 추적을 다시 켠다. 파형 탐색은 위 채보의 스크롤도 이동한다.
- BPM과 첫 박 위치(ms)를 입력하고 `적용`. 첫 박 위치는 음원 시작 기준이며 양수는 뒤쪽이다. `그리드 수정`은 재생 커서 대신 상세 파형에서 가장 왼쪽에 보이는 그리드선을 첫 마디 첫 박으로 지정한다. 확대율과 표시 밀도에 따라 실제로 표시한 선을 기준으로 하며 화면 안에 선이 없으면 화면 왼쪽 시점을 사용한다. ms/박 단위 미세 이동과 모든 구간의 BPM ×2/÷2도 지원한다.
- 노트 스냅은 1/4, 1/8, 1/16, 1/32, 1/8T, 1/16T, OFF. 기존 노트는 스냅하지 않는다.
- 기본 정책은 기존 노트의 실제 음악 시간 유지다. 전체 변경은 기존 EditorSession의 한 Undo 단위다.
- GRID 잠금은 그리드 변경을 차단한다. 도구 행의 `고급 GRID` 버튼으로 템포 구간 추가·수정·삭제, 박자표, 두 지점 BPM 계산, 기존 노트 유지 정책, 표시 밀도를 조작한다. 파일·설정·도구 패널과 동시에 열리지 않으며 닫기 버튼으로 닫는다. 잠금과 작업 중 상태는 기본·고급 편집 모두에 적용된다. Undo/Redo는 기존 편집 메뉴를 사용한다. 패널 위쪽 가로 막대를 드래그해 높이를 조절한다.

`BeatGridTiming`이 위 노란 마디선, 아래 빨간 마디선, 노트 입력을 위한 공통 음악 시간↔4분음표 박 변환을 제공한다. 각 뷰의 화면 좌표는 독립적이다. `AudioOffsetSeconds`는 기존 재생 규칙을 유지하며 **채보 시간 = 음원 시간 + 오디오 오프셋**이다. 그리드 원점은 별도 값이며 재생 속도나 음원을 수정하지 않는다. 전체 원점을 이동하면 첫 고정 변경점 이외의 템포·박자표 변경점도 같은 초 단위 이동량으로 이동한다. 시간 0 이전으로 변경점이 넘어가는 작업은 거부한다. 원점보다 앞에 있는 기존 노트는 음수 박 좌표로 계속 편집할 수 있다.

새 저장은 `timing.grid`가 필수인 v3이다. `originSeconds`는 채보 시간 기준이며 `meters`에는 `timeSeconds`, `numerator`, `denominator`가 들어간다. 첫 박자표 시점은 0이고 이후 시점은 중복 없이 증가한다. 분자는 1–32, 분모는 1/2/4/8/16/32다. 정식 v2는 원본 수정 없이 기본 그리드로 읽는다. v1 및 showcase 확장 v2는 파일 메뉴의 별도 가져오기를 사용한다. showcase 가져오기는 그리드를 보존하며 다음 저장부터 v3로 기록한다.

확대율, 표시 밀도, 스냅, 잠금, 자동 추적, 패널 높이는 현재 편집 화면의 상태이며 음악 파일에 저장하지 않는다. 파형 캐시는 채널의 min/max를 64샘플 단위로 수집하고 해상도별 피크 피라미드를 만든다. 생성은 프레임별로 나누며 표시 중 전체 PCM 재읽기나 파형 UI 객체 대량 생성은 하지 않는다.

전체·상세 파형은 RGB 색상을 기본으로 사용한다. 저음은 빨강, 중음은 초록, 고음은 파랑이며 같은 구간에 여러 음역이 있으면 색을 혼합한다. 250 Hz와 4 kHz를 기준으로 한 완만한 필터로 음역별 에너지를 분석한다. 이는 자체 구현한 주파수 표시이며 rekordbox의 고유 분석 알고리즘을 복제한 것은 아니다. 스테레오 채널의 에너지를 각각 누적하고 해상도별 캐시에 저장하므로 역위상 음원이 상쇄되지 않고 확대·축소 시에도 색을 재사용한다. 색상은 음역 비율, 파형 높이는 기존 min/max 진폭을 나타내며 재생·노트·그리드 타이밍은 바꾸지 않는다.

검증: `BeatGridEditingTests`, `WaveformPeaksTests`, `AudioTimelineTests`, 기존 `ChartEditorScreenTests` / `ChartEditorMenuTests` / `ChartProjectionRenderingTests`. UI 검증 이미지는 `Logs/grid-timeline.png`, `Logs/grid-timeline-advanced.png`에 생성된다.

## 구현 파일

- 새 타이밍·편집 규칙: `Domain/BeatGridTiming.cs`, `Application/BeatGridEditing.cs`.
- 씬 UI 생성·참조: `Editor/UILayout/ChartAudioTimelineLayoutBuilder.cs`, `Runtime/Authoring/ChartAudioTimelineView.cs`.
- 새 파형·UI: `Runtime/Authoring/ChartAudioTimeline.cs`, `AudioTimelineGraphic.cs`, `WaveformPeaks.cs`, `AudioTimelineUi.cs`, `AudioTimelineResize.cs`, `AudioTimelineTooltip.cs`.
- 기존 연결부: `Application/SongProject.cs`, `EditorSession.cs`, `Runtime/Authoring/SongProjectJson.cs`, `Runtime/UI/ChartEditorScreen.cs`, `ChartBeatGrid.cs`, `ChartEditorMenu.cs`. 기존 변수·파일 이름과 공개 API는 유지한다.
- 새 검사: `BeatGridEditingTests.cs`, `WaveformPeaksTests.cs`, `AudioTimelineTests.cs`. 기존 화면 배치 검사에는 추가 패널의 높이를 반영했다.

위 코드 경로는 `Assets/Game/Scripts` 기준이며 테스트는 `Assets/Game/Tests`에 있다. 저장 계약은 `Docs/rdchart-v3.schema.json`에 정의한다. 실제 WAV 파일 로딩과 렌더링을 자동 확인했으며 OGG/MP3 개별 파일 재생과 사람의 실청 평가는 별도로 진행하지 않았다.
