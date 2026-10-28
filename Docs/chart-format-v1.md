# 채보 프로젝트 v1 / v2 / v3

현재 저장 형식은 **v3**입니다. v1 파일은 파일 메뉴의 **v1 프로젝트 가져오기**로 명시적으로 가져옵니다. 곡·채보 UUID, 음원 경로, 노트와 템포는 보존하며 저장 시 v3가 됩니다. 단일 채보는 Standard로 연결합니다. 여러 기존 채보는 슬롯 미지정으로 보존하므로 이름이나 패턴으로 난이도를 추측하지 않습니다. 에디터의 다음 채보 버튼으로 미지정 채보를 순회하고 빈 슬롯 버튼을 누르면 현재 미지정 채보를 그 슬롯에 연결합니다.

v2는 각 채보에 필수 `slot` 필드를 추가합니다. 값은 `Easy`, `Standard`, `Hard` 또는 슬롯 미지정 `null`이며 한 프로젝트에서 지정된 슬롯은 중복할 수 없습니다. [v2 JSON Schema](rdchart-v2.schema.json)를 참고하세요. 아래 v1 예제와 스키마는 가져오기용 계약으로 유지합니다.


v3는 `timing.grid`를 필수로 추가합니다. [v3 JSON Schema](rdchart-v3.schema.json)를 참고하세요. 기본 그리드도 생략하지 않으며 `originSeconds: 0`, `meters: [{timeSeconds: 0, numerator: 4, denominator: 4}]`로 기록합니다. 그리드 원점은 오디오 오프셋과 독립적인 채보 초 단위 좌표입니다. 박자표는 0초에서 시작하는 비어 있지 않은 배열이고 시간이 엄격히 증가해야 합니다. 분자는 1~32, 분모는 1, 2, 4, 8, 16, 32 중 하나입니다. 모든 시간은 유한한 double입니다. 그리드 밀도·스냅 선택·미리듣기 상태는 저장하지 않습니다.

정식 v2는 일반 불러오기로 읽으며 메모리에 기본 그리드를 보충합니다. 읽기만으로 원본 파일은 변경하지 않습니다. v2 스키마는 그대로 유지하며 `timing.grid`가 있는 확장 v2는 일반 로드에서 거부합니다. showcase 확장 v2는 파일 메뉴의 별도 가져오기와 `SongProjectJson.ImportShowcaseVersion2` / `FileSongProjectStore.ReadShowcaseVersion2`로 읽습니다. 해당 API는 v2만 허용하고 선택적 `timing.grid`의 정보를 보존하며, 이후 저장하면 v3가 됩니다. v1 가져오기도 기본 그리드를 보충하고 이후 저장은 v3로 기록합니다.

에디터 상단의 Easy·Standard·Hard 버튼은 해당 채보로 전환하며, 빈 슬롯은 빈 채보를 추가합니다. 활성 채보 표시가 상단에 나옵니다. 전환은 노트/종료 시간/ID와 Undo·Redo 이력을 유지하고, 테스트 플레이 복귀도 같은 편집 세션을 복원합니다. 저장한 프로젝트는 시작 시 곡 목록에서 탐색하며 저장 완료 후 목록을 갱신합니다. 없는 슬롯은 곡 선택에서 비활성화하고 초안의 재생 오류는 선택한 채보별로 표시합니다.

`Application/SongProject.cs`와 `EditorSession.cs`는 Unity/JSON 의존성이 없는 편집 API다. 기존 `SongDocument`와 `ISongDocumentStore`는 단일 채보 재생/저장 계약으로 유지한다.

## 차트 에디터 화면

**Game Tools > Scenes > Build Chart Editor** 또는 **Build All Scenes**로 씬을 생성한 뒤 `Assets/Game/Scenes/ChartEditor.unity`를 열고 Play Mode에서 사용한다. 메뉴의 영구 이벤트 연결과 입력 필드는 `ChartEditorSceneBuilder`에서 생성한다.

- **파일**: 현재 프로젝트 저장, 곡 UUID로 저장한 프로젝트 불러오기, WAV/OGG/MP3 전체 파일 경로로 음원 가져오기. 저장 완료 시 실제 경로를 표시한다. 불러오기 전 현재 변경 내용을 자동 저장하며, 불러오기 실패 시 현재 세션을 유지한다.
- **파일 → PUPA 음원 + 채보**: 프로젝트 루트의 `LocalAudio/PUPA.wav`와 포함된 PUPA Standard 채보(929노트: 탭 900·롱노트 29, 202 BPM, 오디오 오프셋 -0.02초)를 새 곡으로 가져온다. 현재 초안은 자동 저장하고, 새 곡은 가져온 뒤 저장 버튼으로 보관한다. 반복 가져오기도 새 곡 ID를 사용하므로 기존 PUPA 편집본을 덮어쓰지 않는다. 음원이 없으면 현재 작업을 유지하고 필요한 전체 경로를 표시한다.
- **설정**: 제목, 아티스트, 시작 BPM, 오디오 오프셋, 채보 종료 시간을 입력한 뒤 적용한다. 시작 BPM을 바꿔도 이후 템포 변경점과 기존 노트의 초 단위 시간은 유지한다. 그리드는 전체 템포 맵을 반영한다.
- **도구**: 실행 취소, 다시 실행, 처음 위치와 기본 확대 배율로 복귀.
- **테스트**: 제목·음원·노트 등 기존 재생 규칙을 검증한 뒤 실제 Gameplay로 전환한다. 게임의 복귀 버튼/Escape로 돌아오면 미저장 편집 세션과 Undo 이력이 유지된다. Play Mode를 종료하면 메모리 세션은 사라지므로 보존할 작업은 먼저 저장한다.

현재 화면은 활성 4레인 채보를 편집한다. 고급 GRID 패널에서 템포 변경점과 박자표를 편집한다. 파일 선택 대화상자는 제공하지 않는다. 여러 채보가 있는 프로젝트를 열면 첫 채보에서 시작하며 상단 슬롯 버튼으로 전환한다. 템포 맵이 유효하지 않거나 첫 채보가 4레인 모드와 일치하지 않는 초안은 화면에서 열 수 없으며 파일 저장 API 자체는 기존 초안 계약을 유지한다.

표시는 `TempoMap`의 double 시간↔누적 박자 변환과 `BeatProjection`의 `y = 하단 + (박자 - 하단 박자) × 박자당 길이`를 사용한다. 줌은 박자당 길이만, 휠 이동은 하단 박자만 바꾼다. `ChartBeatGrid` 한 렌더러가 같은 영역에서 정수 박자선, 4박 강조선, 노트, 롱노트, 배치 미리보기를 그린다. 기존 노트는 스냅하지 않으며 새 배치 입력만 역변환 후 1박으로 스냅한다. 삭제는 경계에서 잘린 실제 노트 사각형으로 판정한다. 작은 화면에서는 선의 소수 픽셀 커버리지를 표현하며 음악 좌표 자체를 픽셀에 맞춰 이동하지 않는다.

`ChartProjectionRenderingTests`는 610×337과 1280×720에서 여러 줌 및 0.5박 이동의 실제 Unity 렌더링을 `Logs/chart-projection/`에 저장한다. `BeatProjectionTests`는 시간↔박자 및 화면↔박자 왕복을 검증하고, `ChartEditorScreenTests`와 `ChartEditorMenuTests`는 입력 및 저장·불러오기·테스트 플레이 복귀를 검증한다.

오디오 파형은 공유 정점을 연결한 연속 min/max 외곽선과 그라데이션 채움, 화면 1픽셀의 알파 경계로 그린다. 64프레임 단위 피크 피라미드는 넓은 구간 조회를 가속하고, 모든 배율에서 [시작, 끝) 구간에 해당하는 샘플 시각 n/샘플레이트를 조회한다. 구간 내부의 완전한 피크 묶음만 집계하고 잘린 양 끝은 PCM 읽기 캐시로 확인한다. 전체 보기와 상세 보기의 표시 열은 음원/생성 버전/표시 구간/해상도를 키로 캐시하므로 재생 커서만 이동할 때는 재분석하지 않는다. 한 표시 픽셀당 샘플이 하나 이하이면 채움 대신 실제 샘플을 연결한 채널별 선으로 전환한다. 화면 양 끝은 인접 샘플 사이에서 선형 보간하고 음원 범위 밖의 샘플을 만들지 않는다. 최소 확대 구간은 16샘플이다. 범위 밖의 상위 피크 묶음을 포함하지 않는다. 주파수 색상은 64프레임 집계이며 진폭과 별개다. 피크 채움은 채널 전체의 진폭 범위를, 샘플 연결선은 채널별 값을 표시하므로 스테레오 반대 위상도 상쇄되지 않는다. 파형의 표시 열이나 화면 픽셀은 음악 시간의 저장 단위가 아니다. 음원 길이는 AudioClip.length 대신 샘플 수/샘플레이트로 계산한다. 미리듣기는 DSP 시계와 샘플 위치, 채보와 그리드는 double 초·박자 좌표를 사용한다. `WaveformPeaksTests`는 경계 피크·부분 마지막 블록·색상을, `AudioTimelineTests`는 탐색·확대·재생 및 화면 폭 변경 시 시간과 노트 보존을 검증한다.

## 파일 계약

### 기본 제공 PUPA 채보

Discord로 `PUPA.wav`를 별도 전달한다. 팀원은 저장소를 최신 상태로 받은 뒤 `Assets`, `Packages`, `ProjectSettings`와 같은 위치에 `LocalAudio` 폴더를 만들고 `PUPA.wav`를 넣는다. 예: `C:/Projects/RhythmDojo/LocalAudio/PUPA.wav`. 이 PC에서는 `C:/Users/song/RhythmDojo/LocalAudio/PUPA.wav`다. 파일 확장자만 바꾸거나 다른 버전의 음원으로 교체하면 채보와 맞지 않을 수 있으므로 전달받은 WAV를 그대로 사용한다. 기준 파일의 SHA-256은 `14bf278429c22b3d543666032dcb83f4ed82be811a4fd8e80457d43008ad36c0`이다.

ChartEditor를 실행하고 **파일 → PUPA 음원 + 채보**를 누른다. `LocalAudio/` 전체와 음원 확장자는 `.gitignore`로 제외되며 음원은 `Assets`나 빌드에 포함하지 않는다. 채보 원본은 `Assets/Game/Resources/Authoring/Pupa.json`에 포함된다. 가져온 음원은 기존 곡 저장소의 `Songs/<새 곡 ID>/audio/`에 복사된다. Windows 플레이어에서 사용할 때도 실행 파일 옆 `LocalAudio/PUPA.wav`를 사용한다.

채보는 BROKENPASTEL의 [PUPA 4B HD 12](https://www.youtube.com/watch?v=oeoJT20YcGM) 영상에서 기본 4레인을 옮긴 것이다. 영상의 총 932노트 중 사이드 노트 3개를 제외한다. 영상 시각에서 1.2초를 빼 WAV 기준 시각으로 저장했으며, 원본 60fps의 한 프레임 단위로 판독했다. 그리드 원점은 0.4833333333333334초이고 노트에 박자 스냅이나 동시치기 제한을 적용하지 않았다. 게임 내 Standard 슬롯은 이 게임의 저장 슬롯이며 원본 HD 난이도를 낮춘 편곡이라는 의미가 아니다.

[기본 노트 판독 목록](pupa-note-provenance.csv)과 [제외한 사이드 노트](pupa-excluded-side-notes.csv)를 함께 보관한다. 프레임 번호는 위 영상의 첫 프레임을 0으로 한 60fps 기준이며, `time`·`end`는 WAV 기준 초다. `readable_frame`은 노트 형태가 보이는 프레임, `head_frame`·`tail_frame`은 판정선 도달 프레임이다. 원본 영상·스크린샷·음원은 Git에 포함하지 않는다.

2026-10-28 싱크 확인에 사용한 로컬 WAV의 SHA-256은 `108f2b729b9ed635c04856e7d15e4ec8283b8640323b66cda7d335d9d6c13a10`이며, 48 kHz 스테레오·131.5410625초다. 첫 타격음은 약 0.503초이고 기존 첫 노트는 0.4833333333333334초이므로 기본 오디오 오프셋을 -0.02초로 보정했다. 원본 판독 시각과 노트·롱노트 길이는 유지하며 표시와 입력 판정에 같은 보정을 한 번 적용한다. 다른 WAV 버전은 파형을 확인한 뒤 오프셋을 조절한다. 이전에 가져와 저장한 PUPA는 편집기의 오디오 오프셋을 -0.02초로 바꾸고 저장하면 된다.

게임 시계는 DSP 시각을 실시간으로 보간하고 DSP와의 차이는 1초 시정수로 보정한다. 버퍼 갱신 간격이 키 이벤트 시각 변환에 그대로 섞이지 않도록 하며 사용자 입력 보정값은 별도로 한 번 차감한다.

### JSON 저장 규칙

UTF-8(BOM 없음), LF, 2칸 들여쓰기와 마지막 줄바꿈을 사용한다. 경로는 `<persistentDataPath>/Songs/<song UUID>/project.rdchart.json`이며 음원은 같은 폴더의 `audio/<SHA-256>.<extension>`이다. 저장 루트는 생성자에서 주입한다. 동일한 문서 내용은 동일한 JSON을 생성한다. 예제는 [완성 채보](examples/complete.rdchart.json), [초안](examples/draft.rdchart.json), 구조는 [JSON Schema](rdchart-v1.schema.json)를 참고한다. 예제의 음원은 별도로 제공해야 한다.

- `format`은 `rhythm-dojo-chart`, 새 저장의 `version`은 정수 `3`이다. 아래 v1 예제는 명시적 가져오기용이다. 모든 명세 필드는 필수이며, Tap의 `endTimeSeconds`만 존재해서는 안 된다.
- 곡과 채보 ID는 비어 있지 않은 UUID(D 표기)이며 문서 내에서 중복할 수 없다. 디스크 저장소는 곡 UUID와 폴더 UUID가 일치해야 한다. 이름은 ID가 아니며 채보 이름은 판정 난이도와 무관하다.
- 모든 시간과 BPM은 유한한 IEEE-754 double이다. 밀리초 반올림을 하지 않는다. 시간은 초 기준이며 BPM 변경으로 노트 시간이 이동하지 않는다. 양수 오프셋은 기존 SongClock 규칙대로 음원 대비 채보 시간을 앞당긴다.
- BPM과 오프셋은 곡 공통이다. 모드 ID, 레인 수, 종료 시간과 노트는 채보별이다. 레인은 0부터 시작한다. Hold는 시작/종료 시간, Tap은 시작 시간만 기록한다.
- 노트는 시작 시간 기준 안정 정렬한다. 동시치기는 정렬 직전 배열 순서를 유지한다. 편집 중 시간 변경으로 동시치기가 되면 변경 직전 배열의 상대 순서를 유지한다.
- 미지정 음원은 `null`이다. 지정 경로는 `/` 구분 상대 경로이며 빈 경로, 절대 경로, `.`/`..`, 역슬래시, 드라이브/스트림 구분자, Windows 금지 문자를 허용하지 않는다. 파일 작업은 심볼릭 링크/정션을 거부한다.
- 알 수 없는 필드/버전/노트 종류, 중복 JSON 키, 중복 UUID, 비유한 숫자와 잘못된 자료형은 오류다. 자동 자료형 변환이나 알 수 없는 데이터 버림은 하지 않는다.
- 노트 ID, 선택 상태, 재생 캐시, 이력, 저장 시각은 기록하지 않는다.

JSON Schema만으로는 중복 객체 키, UUID 간 중복, 유한 double 범위, 박자표 첫 시간 0과 엄격한 시간 증가, 파일 존재와 경로 링크를 검사할 수 없다. 런타임 파서 및 검증기가 이 규칙과 Windows 예약 파일명 금지를 추가로 적용한다.

## 초안과 재생

저장은 파일 구조만 검증한다. 빈 제목/채보/템포, 음원 미지정, 잘못된 레인 배치, 음수 시간, 겹침, 미완성 종료 시간도 초안으로 보존한다. NaN/Infinity와 정의되지 않은 노트 종류는 보존하지 않는다. Tap의 내부 종료 시간은 시작 시간과 같아야 한다.

재생 변환은 제목, 음원 존재, 실제 모드 ID와 레인 수 일치, Domain의 템포/노트/겹침/종료 시간 규칙을 검증한다. `ProjectValidationException`은 `FieldPath`, 노트 오류의 `ChartId`/`NoteIndex`를 제공한다. 세션 스냅샷을 검증한 직후 `ResolveNoteId`로 오류를 편집 ID에 연결한다. 이후 편집된 스냅샷에 이전 오류를 적용하지 않는다.

## 편집 API

```csharp
var store = new FileSongProjectStore(Path.Combine(Application.persistentDataPath, "Songs"));
var session = new EditorSession(new SongProject());
Guid chartId = default;
long noteId = 0;
session.Edit(edit => {
    edit.SetMetadata("제목", "작곡가");
    edit.SetTiming(0.05, new TempoPoint(0, 120));
    chartId = edit.AddChart("Normal", "FourLaneMode", 4, 12);
    noteId = edit.AddNote(chartId, NoteData.Hold(1, 3, 4.5));
});
session.SelectChart(chartId);
session.Edit(edit => edit.UpdateNote(chartId, noteId, NoteData.Hold(1, 3.25, 4.75)));
session.Undo();
session.Redo();
var relative = await store.ImportAsync(session.Snapshot.Id, sourceAudioPath, cancellationToken);
session.Edit(edit => edit.SetAudio(relative));
await session.SaveAsync(store, cancellationToken);
// 실패하면 기존 session 변수는 그대로 유지된다.
session = await EditorSession.LoadAsync(store, session.Snapshot.Id, cancellationToken);
```

`Edit` 콜백 하나가 한 Undo 단위다. 실패한 일괄 변경은 반영하지 않는다. 콜백 종료 뒤 편집 객체는 사용할 수 없고 중첩 편집은 거부한다. `Snapshot`, `GetNotes`는 독립 복사본이다. 편집 ID는 세션 내 단조 증가 정수이며 이동/정렬/삭제 Undo에서도 유지된다. 로드한 새 세션은 ID를 새로 부여한다. 선택은 문서 변경이나 Undo 이력에 포함하지 않는다. 활성 채보가 삭제되면 첫 채보 또는 null로 대체한다.

Undo는 100개, 새 편집은 Redo를 비운다. `Changed`에서 문서/선택/저장 상태를 다시 조회한다. 저장한 내용과 비교하므로 저장 지점으로 Undo하면 `IsDirty`가 해제된다. 저장 중 추가 편집은 저장된 스냅샷에 포함되지 않으며 변경 표시가 유지된다. 새 세션은 저장 전 dirty이고 로드 세션은 clean이다. 세션은 소유 UI 스레드에서 호출하며 비동기 호출도 그 동기화 컨텍스트를 유지한다. 파일 저장소는 여러 호출/인스턴스에서 사용할 수 있다.

## 기존 재생 및 에셋 연결

```csharp
var snapshot = session.Snapshot;
var document = ChartPlaybackAdapter.ToDocument(snapshot, session.ActiveChartId.Value,
    actualMode.name, actualMode.ToRules(), audio => store.AudioExists(snapshot.Id, audio));
// 기존 DocumentSongLoader/테스트 플레이 진입점에 document를 전달한다.
// 주입하는 loadAudio는 store.ResolveAudioPath(snapshot.Id, document.AudioId)를 사용한다.
// 외부 파일의 AudioClip 디코더는 후속 UI/재생 통합 작업에서 제공한다.
var projectCopy = SongProjectAssetAdapter.Copy(existingSongDefinition);
```

에셋 복사는 새 곡/채보 UUID를 생성하며 원본 ScriptableObject를 수정하지 않는다. AudioClip은 파일 경로가 아니므로 음원을 따로 import하여 `SetAudio`하거나 복사 API에 이미 import된 경로를 지정한다.

## 저장 보장

호출 시 스냅샷을 확보하고 동일한 곡 폴더의 임시 파일에 전체 내용을 기록/flush한 다음 원자적으로 교체한다. 교체 전 실패/취소는 기존 파일을 보존한다. 교체가 성공한 뒤에는 취소를 다시 검사하지 않는다. 프로젝트별 저장/음원 import는 프로세스 내에서 직렬화된다(여러 저장소 인스턴스 포함). 외부 프로세스의 동시 편집/쓰기 충돌 병합은 지원하지 않는다.

음원 import는 원본을 수정하지 않고 복사한 바이트를 SHA-256으로 해시한다. 이전 음원은 Undo/실패 복구를 위해 보존한다. 자동 가비지 수집은 하지 않는다. 곡 UUID 폴더를 다른 Songs 루트로 옮겨도 상대 음원 경로는 유지된다. 음원이 없어도 문서 로드는 성공하고 재생 변환에서 오류가 난다.

검증 테스트: `Assets/Game/Tests/EditMode/SongProjectTests.cs`. Unity 실행 시 `-runTests -testPlatform EditMode`와 `PlayMode`를 각각 사용한다. Windows 빌드는 기존 `RhythmDojo.EditorTools.FoundationVerification.BuildPlayer` 진입점을 사용한다.
