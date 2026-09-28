# 채보 프로젝트 v1

`Application/SongProject.cs`와 `EditorSession.cs`는 Unity/JSON 의존성이 없는 편집 API다. 기존 `SongDocument`와 `ISongDocumentStore`는 단일 채보 재생/저장 계약으로 유지한다. 실제 편집 화면, 곡 목록 등록과 외부 음원 디코딩은 이 작업에 포함하지 않는다.

## 파일 계약

UTF-8(BOM 없음), LF, 2칸 들여쓰기와 마지막 줄바꿈을 사용한다. 경로는 `<persistentDataPath>/Songs/<song UUID>/project.rdchart.json`이며 음원은 같은 폴더의 `audio/<SHA-256>.<extension>`이다. 저장 루트는 생성자에서 주입한다. 동일한 문서 내용은 동일한 JSON을 생성한다. 예제는 [완성 채보](examples/complete.rdchart.json), [초안](examples/draft.rdchart.json), 구조는 [JSON Schema](rdchart-v1.schema.json)를 참고한다. 예제의 음원은 별도로 제공해야 한다.

- `format`은 `rhythm-dojo-chart`, `version`은 정수 `1`이다. 모든 명세 필드는 필수이며, Tap의 `endTimeSeconds`만 존재해서는 안 된다.
- 곡과 채보 ID는 비어 있지 않은 UUID(D 표기)이며 문서 내에서 중복할 수 없다. 디스크 저장소는 곡 UUID와 폴더 UUID가 일치해야 한다. 이름은 ID가 아니며 채보 이름은 판정 난이도와 무관하다.
- 모든 시간과 BPM은 유한한 IEEE-754 double이다. 밀리초 반올림을 하지 않는다. 시간은 초 기준이며 BPM 변경으로 노트 시간이 이동하지 않는다. 양수 오프셋은 기존 SongClock 규칙대로 음원 대비 채보 시간을 앞당긴다.
- BPM과 오프셋은 곡 공통이다. 모드 ID, 레인 수, 종료 시간과 노트는 채보별이다. 레인은 0부터 시작한다. Hold는 시작/종료 시간, Tap은 시작 시간만 기록한다.
- 노트는 시작 시간 기준 안정 정렬한다. 동시치기는 정렬 직전 배열 순서를 유지한다. 편집 중 시간 변경으로 동시치기가 되면 변경 직전 배열의 상대 순서를 유지한다.
- 미지정 음원은 `null`이다. 지정 경로는 `/` 구분 상대 경로이며 빈 경로, 절대 경로, `.`/`..`, 역슬래시, 드라이브/스트림 구분자, Windows 금지 문자를 허용하지 않는다. 파일 작업은 심볼릭 링크/정션을 거부한다.
- 알 수 없는 필드/버전/노트 종류, 중복 JSON 키, 중복 UUID, 비유한 숫자와 잘못된 자료형은 오류다. 자동 자료형 변환이나 알 수 없는 데이터 버림은 하지 않는다.
- 노트 ID, 선택 상태, 재생 캐시, 이력, 저장 시각은 기록하지 않는다.

JSON Schema만으로는 중복 객체 키, UUID 간 중복, 유한 double 범위, 파일 존재와 경로 링크를 검사할 수 없다. 런타임 파서 및 검증기가 이 규칙과 Windows 예약 파일명 금지를 추가로 적용한다.

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
