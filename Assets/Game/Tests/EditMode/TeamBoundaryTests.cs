using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.EditorTools;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    public sealed class TeamBoundaryTests
    {
        private GameSettings Settings => AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
        private SongDocument Document => SongAssetAdapter.ToDocument(Settings.catalog[0]);

        [Test]
        public void AssetAndDocumentPlaybackHaveEquivalentImmutableData()
        {
            var source = Settings.catalog[0]; var document = Document;
            using var asset = SongAssetAdapter.Load(source);
            using var authored = new PlayableSong(document, source.AudioClip, source.Mode);
            Assert.That(authored.Chart.Count, Is.EqualTo(asset.Chart.Count));
            for (int i = 0; i < asset.Chart.Count; i++) Assert.That(authored.Chart[i], Is.EqualTo(asset.Chart[i]));
            document.Notes[0] = NoteData.Tap(3, 9); document.TempoPoints[0] = new TempoPoint(0, 999); document.Title = "Edited";
            Assert.That(authored.Chart[0], Is.EqualTo(asset.Chart[0]));
            Assert.That(authored.Tempo[0], Is.EqualTo(asset.Tempo[0]));
            Assert.That(authored.Title, Is.EqualTo(source.Title));
            authored.Dispose(); Assert.That(source.AudioClip, Is.Not.Null);
        }
        [Test]
        public async Task DraftStoreCopiesAndAllowsIncompleteDocuments()
        {
            var store = new MemorySongDocumentStore(); var draft = new SongDocument { SongId = "draft" };
            await store.SaveAsync(draft, CancellationToken.None); draft.Title = "Changed";
            var loaded = await store.LoadAsync("draft", CancellationToken.None);
            Assert.That(loaded.Title, Is.Null);
            loaded.Title = "Another change";
            Assert.That((await store.LoadAsync("draft", CancellationToken.None)).Title, Is.Null);
            Assert.Throws<InvalidOperationException>(() => loaded.Validate(new GameModeRules(4)));
        }
        [TestCase("title")] [TestCase("tempo")] [TestCase("overlap")] [TestCase("lane")]
        public void InvalidDocumentsReportFieldOrNote(string kind)
        {
            var document = Document;
            if (kind == "title") document.Title = "";
            if (kind == "tempo") document.TempoPoints[0] = new TempoPoint(0, -1);
            if (kind == "overlap") document.Notes[1] = document.Notes[0];
            if (kind == "lane") document.Notes[0] = NoteData.Tap(99, 1.5);
            var error = Assert.Throws<InvalidOperationException>(() => document.Validate(new GameModeRules(4)));
            Assert.That(error.Message, Does.Match("Title|TempoPoints|Notes"));
        }
        [Test]
        public async Task CancellationAfterAudioLoadReleasesExactlyOnce()
        {
            using var cancellation = new CancellationTokenSource(); int releases = 0;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => Settings.catalog[0].Mode, (_, token) => completion.Task);
            var pending = loader.LoadAsync(Document, cancellation.Token);
            cancellation.Cancel();
            completion.SetResult(new SongAudioLease(Settings.catalog[0].AudioClip, () => releases++));
            try { await pending; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
            Assert.That(releases, Is.EqualTo(1));
        }
        [Test]
        public async Task LoaderSnapshotsBeforeAwaitAndReleasesOnDispose()
        {
            int releases = 0; var document = Document;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => Settings.catalog[0].Mode, (_, token) => completion.Task);
            var pending = loader.LoadAsync(document, CancellationToken.None);
            document.Notes[0] = NoteData.Tap(99, 0);
            completion.SetResult(new SongAudioLease(Settings.catalog[0].AudioClip, () => releases++));
            var song = await pending; Assert.That(song.Chart[0].Lane, Is.Zero);
            song.Dispose(); song.Dispose(); Assert.That(releases, Is.EqualTo(1));
        }
        [Test]
        public async Task MissingAudioAndUnknownModeFailWithoutLeaking()
        {
            int releases = 0;
            var loader = new DocumentSongLoader(_ => Settings.catalog[0].Mode,
                (_, token) => Task.FromResult(new SongAudioLease(null, () => releases++)));
            try { await loader.LoadAsync(Document, CancellationToken.None); Assert.Fail(); }
            catch (InvalidOperationException e) { Assert.That(e.Message, Does.Contain("AudioId")); }
            Assert.That(releases, Is.EqualTo(1));
            loader = new DocumentSongLoader(_ => null, (_, token) => throw new Exception("Must not load"));
            try { await loader.LoadAsync(Document, CancellationToken.None); Assert.Fail(); }
            catch (InvalidOperationException e) { Assert.That(e.Message, Does.Contain("ModeId")); }
        }
        private sealed class EntriesProvider : ISongProvider
        {
            public SongEntry[] Entries;
            public IReadOnlyList<SongEntry> GetEntries() => Entries;
            public Task<PlayableSong> LoadAsync(string id, CancellationToken token) => throw new InvalidOperationException("Audio unavailable");
        }
        [Test]
        public void SelectionSurvivesReorderAndDuplicateIdsCannotLoad()
        {
            var a = new SongEntry("a", "A", "", 1, 1, 4, 120, 120);
            var b = new SongEntry("b", "B", "", 1, 1, 4, 120, 120);
            var provider = new EntriesProvider { Entries = new[] { a, b } };
            var library = new SongLibrary(); library.Register(provider);
            var selection = new SongSelectionService(Settings, library);
            selection.UpdateSelection("b", Settings.defaultDifficulty, ScrollMode.Constant, 1);
            provider.Entries = new[] { b, a }; library.Refresh();
            Assert.That(selection.SelectedSongId, Is.EqualTo("b"));
            var duplicate = new EntriesProvider { Entries = new[] { b } }; library.Register(duplicate);
            Assert.That(library.Entries.Where(e => e.Id == "b").All(e => e.Error.Contains("Duplicate")), Is.True);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await library.LoadAsync("b", CancellationToken.None));
            library.Unregister(duplicate); Assert.That(library.Find("b").Error, Is.Null);
        }
        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void HudLayoutSupportsLaneCountsWithoutController(int lanes)
        {
            var view = GameplayHudLayoutBuilder.Build(); var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            try
            {
                mode.SetGeneratedDefaults(Enumerable.Range(0, lanes).Select(i => new LaneDefinition("key"+i,"K"+i,Color.white)).ToArray());
                var images = view.lanes.Build(mode, Settings.presentation);
                Assert.That(images.Length, Is.EqualTo(mode.LaneCount));
                view.gameObject.name = "Renamed HUD"; view.Validate();
                Assert.That(images[0].rectTransform.anchoredPosition.x, Is.EqualTo(-images[images.Length-1].rectTransform.anchoredPosition.x));
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); UnityEngine.Object.DestroyImmediate(mode); }
        }
        [Test]
        public void SceneGenerationDoesNotWriteContentAssets()
        {
            var files = new[] { "Assets/Game/Settings", "Assets/Game/Audio", "Assets/Game/Materials", "Assets/Game/Prefabs" }
                .SelectMany(p => Directory.GetFiles(p, "*", SearchOption.AllDirectories)).ToDictionary(p => p, File.ReadAllBytes);
            SceneBuilder.BuildAll(); SceneBuilder.BuildAll();
            foreach (var pair in files) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
        }
    }
}
