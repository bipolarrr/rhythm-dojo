using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class ChartEditorMenuTests
    {
        private ChartEditorScreen screen;
        private ChartEditorMenu menu;
        private string root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            menu = Object.FindFirstObjectByType<ChartEditorMenu>();
            yield return new WaitUntil(() => menu.IsInitialized);
            root = Path.Combine(UnityEngine.Application.temporaryCachePath, "ChartMenuTests", Guid.NewGuid().ToString("N"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.LoadSceneAsync(AppFlowController.SelectionPath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private T Control<T>(string name) where T : Component => menu.GetComponentsInChildren<T>(true).Single(c => c.name == name);
        private void Click(string name) => Control<Button>(name).onClick.Invoke();
        private void Input(string name, string value) => Control<InputField>(name).text = value;
        private IEnumerator Idle()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (screen && screen.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(screen && screen.IsBusy, Is.False, "Menu operation timed out.");
        }

        [UnityTest]
        public IEnumerator GeneratedMenuAppliesTimingAndUndoRedoRestoresNotes()
        {
            Click("설정");
            Assert.That(Control<InputField>("BPM").gameObject.activeInHierarchy, Is.True);
            Input("Title", "Menu test"); Input("BPM", "60"); Input("Offset", "0.25"); Input("Duration", "20");
            Click("Apply Settings");
            Assert.That(screen.Session.Snapshot.Title, Is.EqualTo("Menu test"));
            Assert.That(screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(60));
            screen.Session.Edit(edit => edit.AddNote(screen.ChartId, NoteData.Tap(0, 4)));
            yield return null;
            var marker = screen.GetComponentInChildren<ChartBeatGrid>().NoteBounds(0, 4, 4);
            float height = ((RectTransform)screen.transform).rect.height;
            Assert.That(marker.yMax, Is.EqualTo(-height + height * 4 / 16 + 3.5f).Within(0.1f));
            Click("도구"); Click("Undo");
            Assert.That(screen.Session.GetNotes(screen.ChartId), Is.Empty);
            Click("Redo");
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(1));
            Click("설정"); Input("BPM", "0"); Click("Apply Settings");
            Assert.That(screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(60));
            Assert.That(Control<Text>("Progress Label").text, Does.Contain("0보다"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FileActionsSaveLoadAndPreserveCurrentDraftOnFailure()
        {
            var store = new FileSongProjectStore(root);
            menu.Initialize(store, null, null);
            var original = screen.Session;
            original.Edit(edit => edit.SetMetadata("Original draft", "Artist"));
            Click("파일"); Click("Save"); yield return Idle();
            Assert.That(original.IsDirty, Is.False);
            Assert.That(File.Exists(Path.Combine(store.GetSongFolder(original.Snapshot.Id), FileSongProjectStore.FileName)), Is.True);
            Input("Project ID", Guid.NewGuid().ToString()); Click("Load"); yield return Idle();
            Assert.That(screen.Session, Is.SameAs(original));
            var other = new SongProject { Title = "Other song", Charts = new[] {
                new ChartDocument { Name = "Normal", ModeId = screen.Mode.name, LaneCount = 4, CompletionTimeSeconds = 10 }
            }};
            var saved = store.SaveAsync(other, default);
            while (!saved.IsCompleted) yield return null;
            Assert.That(saved.IsFaulted, Is.False);
            original.Edit(edit => edit.AddNote(screen.ChartId, NoteData.Tap(1, 2)));
            Input("Project ID", other.Id.ToString()); Click("Load"); yield return Idle();
            Assert.That(screen.Session.Snapshot.Title, Is.EqualTo("Other song"), Control<Text>("Progress Label").text);
            var restored = store.LoadAsync(original.Snapshot.Id, default);
            while (!restored.IsCompleted) yield return null;
            Assert.That(restored.Result.Charts[0].Notes.Length, Is.EqualTo(1), "Replacing a dirty session saves its draft first.");
        }

        [UnityTest]
        public IEnumerator ImportedAudioPlaysAndReturnsWithUnsavedSessionAndUndoHistory()
        {
            // Use the actual composed file store and decoder, and clean only this test's song folder.
            var session = screen.Session;
            root = new FileSongProjectStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Songs")).GetSongFolder(session.Snapshot.Id);
            Directory.CreateDirectory(root);
            string wav = Path.Combine(root, "source.wav");
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                const int samples = 44100 * 3;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++) writer.Write((short)0);
            }
            Click("파일"); Input("Audio Path", wav); Click("Import Audio"); yield return Idle();
            Assert.That(session.Snapshot.Audio, Does.StartWith("audio/"));
            session.Edit(edit => { edit.SetMetadata("Playtest", "Test"); edit.AddNote(screen.ChartId, NoteData.Tap(0, 1)); });
            Click("파일");
            yield return null;
            Click("테스트");
            float deadline = Time.realtimeSinceStartup + 20;
            while (SceneManager.GetActiveScene().path != AppFlowController.GameplayPath && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.GameplayPath));
            yield return null;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.CurrentRequest.Song.Chart.Count, Is.EqualTo(1));
            flow.ReturnFromGameplay();
            while (SceneManager.GetActiveScene().path != ChartEditorMenu.ScenePath && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            Assert.That(screen.Session, Is.SameAs(session));
            Assert.That(session.IsDirty, Is.True);
            session.Undo();
            Assert.That(session.GetNotes(screen.ChartId), Is.Empty);
            Assert.That(flow.CurrentRequest, Is.Null);
        }
    }
}
