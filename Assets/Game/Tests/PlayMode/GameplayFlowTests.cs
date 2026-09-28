using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Tests
{
    public sealed class GameplayFlowTests
    {
        [UnityTest]
        public IEnumerator UnsavedDocumentPlaytestReturnsSessionAndReleasesAudio()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; var document = SongAssetAdapter.ToDocument(source);
            document.SongId = "unsaved"; document.Title = "Unsaved song";
            int releases = 0; string returned = null;
            var audio = UnityEngine.Object.Instantiate(source.AudioClip);
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => Task.FromResult(
                new SongAudioLease(audio, () => { releases++; UnityEngine.Object.Destroy(audio); })));
            var task = flow.PlayAsync(document, loader, "editor-session-42", AppFlowController.SelectionPath,
                id => returned = id, CancellationToken.None);
            yield return Await(() => task.IsCompleted); Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            document.Title = "Edited after request"; document.Notes[0] = NoteData.Tap(99, 0);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.ReadModel.Snapshot.SongTitle, Is.EqualTo("Unsaved song"));
            Assert.That(flow.CurrentRequest.Song.Chart[0].Lane, Is.Zero);
            Assert.That(releases, Is.Zero);
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(() => returned != null); yield return null;
            Assert.That(returned, Is.EqualTo("editor-session-42"));
            Assert.That(releases, Is.EqualTo(1)); Assert.That(audio == null, Is.True);
            Assert.That(source.AudioClip, Is.Not.Null); Assert.That(flow.CurrentRequest, Is.Null);
        }
        [UnityTest]
        public IEnumerator CanceledEditorLoadStaysOnScreenAndAllowsRetry()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; int releases = 0;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => completion.Task);
            using var cancellation = new CancellationTokenSource();
            var task = flow.PlayAsync(SongAssetAdapter.ToDocument(source), loader, "cancelled", AppFlowController.SelectionPath,
                _ => Assert.Fail("Canceled load must not return a playtest"), cancellation.Token);
            Assert.That(flow.Transitioning, Is.True); cancellation.Cancel();
            completion.SetResult(new SongAudioLease(source.AudioClip, () => releases++));
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsCanceled, Is.True); Assert.That(releases, Is.EqualTo(1));
            Assert.That(flow.Transitioning, Is.False); Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>(), Is.Not.Null);
            var retry = flow.PlaySelectedAsync(CancellationToken.None);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(retry.IsFaulted, Is.False);
        }
        [UnityTest]
        public IEnumerator RenamedLayoutStillLoadsSongThroughExplicitViewReferences()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            foreach (var child in view.GetComponentsInChildren<Transform>(true)) child.name = "Reorganized visual";
            view.play.transform.SetAsFirstSibling();
            yield return Click(view.play);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
        }
        [UnityTest]
        public IEnumerator DelayedPlaytestKeepsOptionsCapturedAtRequest()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; var selectedDifficulty = flow.SelectedDifficulty;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => completion.Task);
            var task = flow.PlayAsync(SongAssetAdapter.ToDocument(source), loader, "delayed", AppFlowController.SelectionPath,
                _ => { }, CancellationToken.None);
            flow.Selection.UpdateSelection(source.SongId, flow.Settings.difficulties[0], ScrollMode.Bpm, 1.5);
            completion.SetResult(new SongAudioLease(source.AudioClip));
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.Difficulty, Is.EqualTo(selectedDifficulty));
            Assert.That(flow.CurrentRequest.ScrollMode, Is.EqualTo(ScrollMode.Constant));
            Assert.That(flow.CurrentRequest.Multiplier, Is.EqualTo(1));
        }
        private InputSettings original;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;
        private Keyboard keyboard;
        private Mouse mouse;
        private AudioConfiguration originalAudio;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalAudio = AudioSettings.GetConfiguration();
            original = InputSystem.settings;
            priorEditorBehavior = original.editorInputBehaviorInPlayMode; priorBackgroundBehavior = original.backgroundBehavior;
            original.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Bootstrap.unity");
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Assert.That(AudioSettings.Reset(originalAudio), Is.True, "Restore original audio configuration.");
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            original.editorInputBehaviorInPlayMode = priorEditorBehavior; original.backgroundBehavior = priorBackgroundBehavior;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>(); if (flow) UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
        }
        private static IEnumerator Await(System.Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Scene transition timed out.");
                yield return null;
            }
        }
        private static Button ButtonNamed(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        [UnityTest]
        public IEnumerator SettingsMouseAndKeyboardApplyActualBufferAndKeepSelection()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.DefaultBufferSize, Is.EqualTo(originalAudio.dspBufferSize));
            var before = AudioSettings.GetConfiguration();
            Assert.That(flow.ApplyBufferSize(), Is.False);
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(before.dspBufferSize));
            yield return Click(ButtonNamed("Settings"));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            CaptureSelection("settings");
            var dropdown = UnityEngine.Object.FindFirstObjectByType<Dropdown>();
            Assert.That(dropdown.options.Count, Is.EqualTo(8));
            yield return Press(Key.Enter); yield return Press(Key.DownArrow); yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(.25f); // uGUI restores selection after closing its popup.
            Assert.That(flow.SelectedBufferOption, Is.EqualTo(1));
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(before.dspBufferSize), "Selection alone must not reset audio.");
            yield return Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonNamed("Apply").gameObject));
            yield return Press(Key.Enter);
            Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t => t.name == "Actual Audio State").text,
                Does.Contain($"{AudioSettings.GetConfiguration().dspBufferSize} samples"));
            Assert.That(flow.BufferStatus, Does.Contain("Actual:"));
            yield return Press(Key.RightArrow); yield return Press(Key.Enter);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
            EventSystem.current.SetSelectedGameObject(ButtonNamed("Settings").gameObject);
            yield return Press(Key.Enter);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            dropdown = UnityEngine.Object.FindFirstObjectByType<Dropdown>();
            Assert.That(dropdown.value, Is.EqualTo(1));
            dropdown.value = 0; yield return Click(ButtonNamed("Apply"));
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(flow.DefaultBufferSize));
            yield return Click(ButtonNamed("Back"));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
        [UnityTest]
        public IEnumerator AllBufferRequestsReportActualConfigurationAndChangedAudioPlaysAndRestarts()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowSettings();
            Assert.That(flow.ApplyBufferSize(), Is.False, "Apply during transition must be rejected.");
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            System.IO.File.WriteAllText("Logs/audio-buffers.txt", $"Startup: {originalAudio.dspBufferSize} samples / {originalAudio.sampleRate} Hz\n");
            for (int option = 1; option < AppFlowController.BufferOptionCount; option++)
            {
                flow.SelectBufferOption(option);
                bool accepted = flow.ApplyBufferSize();
                var actual = AudioSettings.GetConfiguration();
                AudioSettings.GetDSPBufferSize(out int size, out int count);
                yield return new WaitForSecondsRealtime(.3f);
                double start = AudioSettings.dspTime;
                double realStart = Time.realtimeSinceStartupAsDouble;
                yield return new WaitForSecondsRealtime(1);
                double elapsed = Time.realtimeSinceStartupAsDouble - realStart;
                double dspElapsed = AudioSettings.dspTime - start;
                System.IO.File.AppendAllText("Logs/audio-buffers.txt", $"Requested {flow.RequestedBufferSize}: accepted={accepted}, actual={size} x {count}, rate={actual.sampleRate}, real={elapsed:F4}s, DSP={dspElapsed:F4}s, ratio={dspElapsed/elapsed:F3}\n");
                Assert.That(size, Is.EqualTo(actual.dspBufferSize));
                Assert.That(size, Is.GreaterThan(0));
                Assert.That(AudioSettings.dspTime, Is.GreaterThan(start));
            }
            flow.SelectBufferOption(4); Assert.That(flow.ApplyBufferSize(), Is.True);
            flow.ShowSelection(); yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
            flow.PlaySelected(); yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var playingConfiguration = AudioSettings.GetConfiguration();
            Assert.That(flow.ApplyBufferSize(), Is.False, "Gameplay must reject buffer reset.");
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(playingConfiguration.dspBufferSize));
            game.StartSession(); yield return new WaitForSecondsRealtime(2);
            Assert.That(game.Clock.SongTime, Is.GreaterThan(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying, Is.True);
            game.StartSession(); Assert.That(game.Session.Resolved, Is.Zero);
            yield return new WaitForSecondsRealtime(2);
            Assert.That(game.Clock.SongTime, Is.GreaterThan(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying, Is.True);
            System.IO.File.AppendAllText("Logs/audio-buffers.txt", "PASS: requested 256 samples; scheduled playback and restart advance song time and report AudioSource playing. Audible dropouts were not measured.\n");
        }
        [UnityTest]
        public IEnumerator MouseClickUsesUiInputModuleToLoadSong()
        {
            CaptureSelection();
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Play");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }); yield return null;
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().Chart.Count, Is.EqualTo(18));
        }
        private static void CaptureSelection(string imageName = "selection")
        {
            var camera = Camera.main; var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var target = new RenderTexture(1280,720,24); var previous = RenderTexture.active;
            var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=1; Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes($"Logs/{imageName}.png",texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
                RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }
        [UnityTest]
        public IEnumerator SixLaneDefinitionDrivesAllUnityAdapters()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>(); var originalCatalog = flow.Settings.catalog;
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            var chart = ScriptableObject.CreateInstance<RhythmChart>();
            var song = ScriptableObject.CreateInstance<SongDefinition>();
            var catalog = ScriptableObject.CreateInstance<SongCatalog>();
            try
            {
                string[] keys = { "a","s","d","f","j","k" };
                mode.SetGeneratedDefaults(keys.Select(k=>new LaneDefinition("<Keyboard>/"+k,k.ToUpperInvariant(),Color.cyan)).ToArray());
                chart.SetGeneratedContent(new[] { NoteData.Tap(5,1),NoteData.Hold(0,2,4) },6);
                var source = originalCatalog[0];
                song.SetGeneratedDefaults("six-lane-test","Six lane test",source.AudioClip,chart,mode,new SongTiming());
                mode.name = "six-lane-mode";
                catalog.SetGeneratedDefaults(new[] { song });
                flow.Library.Register(new BuiltInSongProvider(catalog));
                flow.Selection.UpdateSelection(song.SongId,flow.Settings.defaultDifficulty,ScrollMode.Constant,1); flow.PlaySelected();
                yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
                var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
                Assert.That(game.Input.LaneCount, Is.EqualTo(6)); Assert.That(game.ReadModel.LaneCount, Is.EqualTo(6));
                var field = UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>().transform;
                Assert.That(field.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Lane ")), Is.EqualTo(6));
                Assert.That(field.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Rail ")), Is.EqualTo(7));
                var hud = UnityEngine.Object.FindFirstObjectByType<RhythmHud>();
                Assert.That(hud.GetComponentsInChildren<Image>().Count(i=>i.name.StartsWith("Lane ")), Is.EqualTo(6));
                game.StartSession();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.K)); yield return null;
                Assert.That(game.Session.IsHeld(5), Is.True);
            }
            finally
            {
                flow.Settings.catalog=originalCatalog;
                UnityEngine.Object.Destroy(mode); UnityEngine.Object.Destroy(chart);
                UnityEngine.Object.Destroy(song); UnityEngine.Object.Destroy(catalog);
            }
        }
        [UnityTest]
        public IEnumerator SelectionKeepsSpeedIndependentAndBothSongsLoad()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
            dropdowns.Single(d=>d.name=="Scroll mode").value = 1;
            dropdowns.Single(d=>d.name=="Scroll multiplier").value = 4;
            dropdowns.Single(d=>d.name=="Judgment difficulty").value = 0;
            dropdowns.Single(d=>d.name=="Song").value = 1;
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5)); Assert.That(flow.SelectedScrollMode, Is.EqualTo(ScrollMode.Bpm));
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name=="Play").onClick.Invoke();
            flow.PlaySelected(); // A duplicate start during transition is ignored.
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.Chart.Count, Is.EqualTo(8)); Assert.That(game.ReadModel.Snapshot.ScrollMultiplier, Is.EqualTo(1.5));
            Assert.That(game.ReadModel.Snapshot.DifficultyName, Is.EqualTo("Easy"));
            Assert.That(UnityEngine.Object.FindObjectsByType<NoteView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length, Is.EqualTo(8));
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(1)); Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>(), Is.Null);
            flow.UpdateSelection(0,flow.Settings.defaultDifficulty,ScrollMode.Constant,1); flow.PlaySelected();
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().Chart.Count, Is.EqualTo(18));
        }
        [UnityTest]
        public IEnumerator KeyboardNavigationAndStartRestartFocusLossUseActualActions()
        {
            var dropdown = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsSortMode.None).Single(d=>d.name=="Song");
            int value = dropdown.value;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.DownArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            Assert.That(dropdown.value, Is.EqualTo(value+1));
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.UpdateSelection(0,flow.Settings.defaultDifficulty,ScrollMode.Constant,1); flow.PlaySelected();
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space)); yield return null;
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing)); Assert.That(game.Clock.Running, Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F)); yield return null;
            game.StartSession(); Assert.That(game.Session.IsHeld(1), Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F)); yield return null;
            Assert.That(game.Session.IsHeld(1), Is.False);
            game.SendMessage("OnApplicationFocus",false);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready)); Assert.That(game.Clock.Running, Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null;
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
