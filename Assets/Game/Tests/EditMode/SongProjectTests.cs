using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class SongProjectTests
    {
        private string root;
        private FileSongProjectStore store;
        private readonly SongProjectJson json = new SongProjectJson();
        private static SongProject Example() => new SongProject { Title = "한글 곡", Artist = "작곡가", Audio = "audio/music.ogg",
            AudioOffsetSeconds = .12345678901234567, TempoPoints = new[] { new TempoPoint(0, 120.12345678901234), new TempoPoint(6, 180) },
            Charts = new[] { new ChartDocument { Name = "Normal", ModeId = "FourLaneMode", LaneCount = 4, CompletionTimeSeconds = 12,
                Notes = new[] { NoteData.Tap(2, 1.1234567890123457), NoteData.Tap(0, 1.1234567890123457), NoteData.Hold(1, 3, 4.567890123456789) } },
                new ChartDocument { Name = "초안", ModeId = "FourLaneMode", LaneCount = 4 } } };
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "RhythmDojoTests-" + Guid.NewGuid().ToString("N")); store = new FileSongProjectStore(root); }
        [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        [Test] public void RoundTripPreservesDoublesOrderAndMultipleCharts()
        {
            var p = Example(); string text = json.Serialize(p); var loaded = json.Deserialize(text);
            Assert.That(ProjectValidation.ContentEquals(p, loaded), Is.True);
            Assert.That(json.Serialize(loaded), Is.EqualTo(text));
            Assert.That(text, Does.Not.Contain("noteId").And.Not.Contain("savedAt"));
            Assert.That(loaded.Charts[0].Notes.Select(n => n.Lane), Is.EqualTo(new[] { 2, 0, 1 }));
            var before = ChartPlaybackAdapter.ToDocument(p, p.Charts[0].Id, "FourLaneMode", new GameModeRules(4), _ => true);
            var after = ChartPlaybackAdapter.ToDocument(loaded, loaded.Charts[0].Id, "FourLaneMode", new GameModeRules(4), _ => true);
            Assert.That(after.Notes, Is.EqualTo(before.Notes)); Assert.That(after.TempoPoints, Is.EqualTo(before.TempoPoints));
            Assert.That(after.AudioOffset, Is.EqualTo(before.AudioOffset));
        }
        [Test] public async Task EmptyDraftSavesButCannotPlay()
        {
            var p = new SongProject { Charts = new[] { new ChartDocument() } };
            await store.SaveAsync(p, default); var loaded = await store.LoadAsync(p.Id, default);
            Assert.That(ProjectValidation.ContentEquals(p, loaded), Is.True);
            Assert.Throws<ProjectValidationException>(() => ChartPlaybackAdapter.ToDocument(loaded, p.Charts[0].Id, "FourLaneMode", new GameModeRules(4), _ => false));
        }
        [Test] public void EditorIdsSnapshotsBatchesAndHistory()
        {
            var p = Example(); var session = new EditorSession(p, true); var c = p.Charts[0].Id;
            var original = session.GetNotes(c); long id = original[0].Id;
            session.Edit(e => { e.UpdateNote(c, id, NoteData.Tap(2, 8)); e.SetTiming(.5, new TempoPoint(0, 90)); });
            Assert.That(session.GetNotes(c).Last().Id, Is.EqualTo(id)); Assert.That(session.IsDirty, Is.True);
            session.Edit(e => e.DeleteNote(c, id)); session.Undo();
            Assert.That(session.GetNotes(c).Last().Id, Is.EqualTo(id)); session.Undo();
            Assert.That(session.IsDirty, Is.False); Assert.That(session.GetNotes(c), Is.EqualTo(original));
            session.Redo(); session.Edit(e => e.RenameChart(c, "새 이름")); Assert.That(session.CanRedo, Is.False);
            var snapshot = session.Snapshot; snapshot.Charts[0].Notes[0] = NoteData.Tap(99, 99); snapshot.Title = "outside";
            Assert.That(session.Snapshot.Title, Is.EqualTo(p.Title));
            Assert.Throws<ProjectValidationException>(() => session.Edit(e => { e.SetMetadata("bad", ""); e.AddNote(c, NoteData.Tap(0, double.NaN)); }));
            Assert.That(session.Snapshot.Title, Is.EqualTo(p.Title));
            EditorEdit escaped = null; session.Edit(e => escaped = e);
            Assert.Throws<InvalidOperationException>(() => escaped.SetMetadata("outside", ""));
            for (int i = 0; i < 110; i++) { int value = i; session.Edit(e => e.SetMetadata(value.ToString(), "")); }
            int undos = 0; while (session.CanUndo) { session.Undo(); undos++; } Assert.That(undos, Is.EqualTo(100));
        }
        [Test] public void ChartLifecycleAndEqualTimeOrder()
        {
            var session = new EditorSession(new SongProject()); Guid chart = default; long a = 0, b = 0;
            session.Edit(e => { chart = e.AddChart("A", "FourLaneMode", 4); a = e.AddNote(chart, NoteData.Tap(2, 2)); b = e.AddNote(chart, NoteData.Tap(0, 1)); });
            session.SelectChart(chart);
            session.Edit(e => { e.UpdateNote(chart, a, NoteData.Tap(2, 1)); e.RenameChart(chart, "B"); });
            Assert.That(session.GetNotes(chart).Select(n => n.Id), Is.EqualTo(new[] { b, a }));
            session.Edit(e => e.DeleteChart(chart)); Assert.That(session.ActiveChartId, Is.Null);
            session.Undo(); Assert.That(session.GetNotes(chart).Select(n => n.Id), Is.EqualTo(new[] { b, a }));
        }
        private sealed class DelayedStore : ISongProjectStore
        {
            public TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
            public SongProject Captured;
            public async Task SaveAsync(SongProject p, CancellationToken token) { Captured = p.Copy(); await Completion.Task; }
            public Task<SongProject> LoadAsync(Guid id, CancellationToken token) => throw new IOException("load failed");
        }
        [Test] public async Task SaveTracksCapturedContentAndFailurePreservesSession()
        {
            var session = new EditorSession(Example()); var delayed = new DelayedStore();
            var pending = session.SaveAsync(delayed); session.Edit(e => e.SetMetadata("Later", ""));
            delayed.Completion.SetResult(true); await pending;
            Assert.That(session.IsDirty, Is.True); session.Undo(); Assert.That(session.IsDirty, Is.False);
            session.Edit(e => e.SetMetadata("unsaved", ""));
            delayed = new DelayedStore(); pending = session.SaveAsync(delayed); delayed.Completion.SetException(new IOException("disk full"));
            try { await pending; Assert.Fail(); } catch (IOException) { }
            Assert.That(session.IsDirty, Is.True); Assert.That(session.Snapshot.Title, Is.EqualTo("unsaved"));
            try { await EditorSession.LoadAsync(delayed, session.Snapshot.Id); Assert.Fail(); } catch (IOException) { }
            Assert.That(session.Snapshot.Title, Is.EqualTo("unsaved"));
        }
        [TestCase("version")] [TestCase("field")] [TestCase("duplicateKey")] [TestCase("kind")]
        [TestCase("duplicateId")] [TestCase("id")] [TestCase("nan")] [TestCase("type")] [TestCase("tapEnd")]
        public void MalformedFilesFail(string failure)
        {
            var p = Example(); string text = json.Serialize(p);
            switch (failure)
            {
                case "version": text = text.Replace("\"version\": 1", "\"version\": 2"); break;
                case "field": text = text.Replace("\"artist\":", "\"unknown\":"); break;
                case "duplicateKey": text = text.Replace("\"version\": 1", "\"version\": 1, \"version\": 1"); break;
                case "kind": text = text.Replace("\"tap\"", "\"mine\""); break;
                case "duplicateId": text = text.Replace(p.Charts[1].Id.ToString(), p.Charts[0].Id.ToString()); break;
                case "id": text = text.Replace(p.Id.ToString(), "bad"); break;
                case "nan": text = text.Replace("\"completionTimeSeconds\": 12.0", "\"completionTimeSeconds\": NaN"); break;
                case "type": text = text.Replace("\"laneCount\": 4", "\"laneCount\": \"4\""); break;
                case "tapEnd": text = text.Replace("\"kind\": \"tap\"", "\"kind\": \"tap\", \"endTimeSeconds\": 3"); break;
            }
            Assert.Throws<ProjectValidationException>(() => json.Deserialize(text));
        }
        [TestCase("../outside.ogg")] [TestCase("/absolute.ogg")] [TestCase("C:/outside.ogg")]
        [TestCase("audio\\file.ogg")] [TestCase("audio//file.ogg")] [TestCase("audio/../file.ogg")]
        [TestCase("audio/CON.ogg")] [TestCase("audio/")]
        public void InvalidAudioPathsFail(string path)
        { var p = Example(); p.Audio = path; Assert.Throws<ProjectValidationException>(() => json.Serialize(p)); }
        [Test] public void DraftLayoutsAndTempoEditsPreserveSeconds()
        {
            var p = Example(); var c = p.Charts[0];
            c.Notes = new[] { NoteData.Hold(-1, -2, -3), NoteData.Tap(50, -2) };
            p.TempoPoints = new[] { new TempoPoint(-1, -120) };
            Assert.That(ProjectValidation.ContentEquals(p, json.Deserialize(json.Serialize(p))), Is.True);
            var session = new EditorSession(p); var notes = session.GetNotes(c.Id);
            session.Edit(e => e.SetTiming(20, new TempoPoint(0, 999)));
            Assert.That(session.GetNotes(c.Id), Is.EqualTo(notes));
        }
        [Test] public void PublishedExamplesLoad()
        {
            foreach (var path in Directory.GetFiles("Docs/examples", "*.rdchart.json"))
                Assert.That(json.Deserialize(File.ReadAllText(path)), Is.Not.Null);
        }
        [Test] public async Task CancellationFailureAndConcurrentStoresPreserveCommittedFiles()
        {
            var p = Example(); await store.SaveAsync(p, default);
            string path = Path.Combine(store.GetSongFolder(p.Id), FileSongProjectStore.FileName), original = File.ReadAllText(path);
            p.Title = "changed"; using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await store.SaveAsync(p, cancel.Token); Assert.Fail(); } catch (OperationCanceledException) { }
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            { try { await store.SaveAsync(p, default); Assert.Fail(); } catch (IOException) { } }
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
            var first = store.SaveAsync(p, default); p.Title = "last";
            var second = new FileSongProjectStore(root).SaveAsync(p, default); await Task.WhenAll(first, second);
            Assert.That((await store.LoadAsync(p.Id, default)).Title, Is.EqualTo("last"));
            Assert.That(Directory.GetFiles(store.GetSongFolder(p.Id), "*.tmp"), Is.Empty);
        }
        [Test] public async Task ImportedAudioIsPortableAndOldAudioSurvives()
        {
            Directory.CreateDirectory(root); string source = Path.Combine(root, "source.ogg"); File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
            var p = Example(); p.Audio = await store.ImportAsync(p.Id, source, default); string old = p.Audio;
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(await store.ImportAsync(p.Id, source, default), Is.EqualTo(old));
            File.WriteAllBytes(source, new byte[] { 4, 5, 6 }); p.Audio = await store.ImportAsync(p.Id, source, default);
            Assert.That(store.AudioExists(p.Id, old), Is.True); await store.SaveAsync(p, default);
            string movedRoot = Path.Combine(root, "moved"); Directory.CreateDirectory(movedRoot);
            Directory.Move(store.GetSongFolder(p.Id), Path.Combine(movedRoot, p.Id.ToString("D")));
            var moved = new FileSongProjectStore(movedRoot); var loaded = await moved.LoadAsync(p.Id, default);
            Assert.That(moved.AudioExists(p.Id, loaded.Audio), Is.True);
            File.Delete(moved.ResolveAudioPath(p.Id, loaded.Audio)); loaded = await moved.LoadAsync(p.Id, default);
            Assert.Throws<ProjectValidationException>(() => ChartPlaybackAdapter.ToDocument(loaded, p.Charts[0].Id, "FourLaneMode", new GameModeRules(4), a => moved.AudioExists(p.Id, a)));
        }
        [Test] public void PlaybackErrorsMapToEditorIdsAndModesMustMatch()
        {
            var p = Example(); var c = p.Charts[0]; c.Notes = new[] { NoteData.Tap(0, 1), NoteData.Hold(0, 1, 3) };
            var session = new EditorSession(p);
            var error = Assert.Throws<ProjectValidationException>(() => ChartPlaybackAdapter.ToDocument(session.Snapshot, c.Id, c.ModeId, new GameModeRules(4), _ => true));
            Assert.That(session.ResolveNoteId(error), Is.EqualTo(session.GetNotes(c.Id)[1].Id));
            Assert.Throws<ProjectValidationException>(() => ChartPlaybackAdapter.ToDocument(p, c.Id, "other", new GameModeRules(4), _ => true));
            Assert.Throws<ProjectValidationException>(() => ChartPlaybackAdapter.ToDocument(p, c.Id, c.ModeId, new GameModeRules(5), _ => true));
        }
        [Test] public void AssetCopyIsIndependentAndUsesNewUuids()
        {
            var source = AssetDatabase.LoadAssetAtPath<SongDefinition>("Assets/Game/Settings/TestSong.asset");
            var p = SongProjectAssetAdapter.Copy(source); string title = source.Title;
            p.Title = "changed"; p.Charts[0].Notes[0] = NoteData.Tap(99, 99);
            Assert.That(source.Title, Is.EqualTo(title)); Assert.That(source.Chart[0].Lane, Is.Not.EqualTo(99));
            Assert.That(p.Id, Is.Not.EqualTo(SongProjectAssetAdapter.Copy(source).Id)); Assert.That(p.Audio, Is.Null);
        }
    }
}
