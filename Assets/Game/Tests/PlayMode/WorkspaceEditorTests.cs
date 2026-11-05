using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Core;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [PrebuildSetup(typeof(TestRunStartup))]
    [PostBuildCleanup(typeof(TestRunStartup))]
    [Category("Authoring")]
    public sealed class WorkspaceEditorTests
    {
        private WorkspaceEditor workspace;
        private string root;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings input;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private InputSettings.BackgroundBehavior backgroundBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input = InputSystem.settings;
            editorBehavior = input.editorInputBehaviorInPlayMode;
            backgroundBehavior = input.backgroundBehavior;
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            yield return SceneManager.LoadSceneAsync(WorkspaceEditor.ScenePath);
            yield return null;
            workspace = UnityEngine.Object.FindFirstObjectByType<WorkspaceEditor>();
            yield return new WaitUntil(() => workspace.menu.IsInitialized);
            yield return null;
            root = Path.Combine(UnityEngine.Application.temporaryCachePath, "WorkspaceTests", Guid.NewGuid().ToString("N"));
            workspace.menu.Initialize(new FileSongProjectStore(root), null, null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added)
            {
                InputSystem.RemoveDevice(mouse);
            }

            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }

            input.editorInputBehaviorInPlayMode = editorBehavior;
            input.backgroundBehavior = backgroundBehavior;
            yield return SceneManager.LoadSceneAsync(AppFlowController.SelectionPath);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private IEnumerator Idle()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (workspace.IsWorking)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
        }

        private static Vector2 Center(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private IEnumerator MoveTo(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }

        private IEnumerator ClickAt(Vector2 point)
        {
            yield return MoveTo(point);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            yield return null;
            yield return MoveTo(point);
        }

        private IEnumerator EnterText(InputField field, string value)
        {
            yield return ClickAt(Center(field.GetComponent<RectTransform>()));
            Assert.That(field.isFocused, Is.True, field.name + " must receive focus from the click.");
            field.ProcessEvent(Event.KeyboardEvent("^a"));
            foreach (char character in value)
            {
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
            }
            field.ForceLabelUpdate();
            yield return null;
            Assert.That(field.text, Is.EqualTo(value));
        }

        private string WriteTempoWave()
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "Tempo128.wav");
            const int rate = 8000;
            const int frames = rate * 30;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);
            for (int i = 0; i < frames; i++)
            {
                double time = (double)i / rate;
                double beatTime = time % (60.0 / 128);
                double sample = Math.Sin(2 * Math.PI * 440 * time) * Math.Exp(-beatTime * 40);
                writer.Write((short)(sample * 4000));
            }
            return path;
        }

        [UnityTest]
        public IEnumerator WorkspaceMenusKeepLegacyImportsChartDurationAndViewResetAccessible()
        {
            var original = workspace.screen.Session;
            yield return ClickAt(Center(workspace.view.menus[0].trigger.GetComponent<RectTransform>()));
            var files = workspace.view.actions.Single(binding => binding.action == WorkspaceAction.ProjectFiles).button;
            yield return ClickAt(Center(files.GetComponent<RectTransform>()));
            var panel = workspace.transform.Find("File Panel");
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("Import Legacy").gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Find("Import Showcase").gameObject.activeInHierarchy, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.False);

            workspace.Run(WorkspaceAction.ChartSettings);
            yield return Idle();
            panel = workspace.transform.Find("Settings Panel");
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            var duration = panel.GetComponentsInChildren<InputField>().Single(field => field.name == "Duration");
            duration.text = "42";
            panel.Find("Apply Settings").GetComponent<Button>().onClick.Invoke();
            Assert.That(original.Snapshot.Charts.Single(chart => chart.Id == workspace.screen.ChartId)
                .CompletionTimeSeconds, Is.EqualTo(42));
            workspace.menu.ClosePanels();
            workspace.screen.Scroll(8);
            workspace.Run(WorkspaceAction.ResetView);
            yield return Idle();
            Assert.That(workspace.screen.ScrollBeatOffset, Is.Zero);
            Assert.That(workspace.screen.Session, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator SaikiAt128BpmCanDragAndWheelBeforeFirstMeasure()
        {
            string path = WriteTempoWave();

            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (workspace.timeline.Peaks == null || !workspace.timeline.Peaks.Ready)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            workspace.view.bpm.text = "128";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(128));
            var detail = workspace.timeline.GetComponent<ChartAudioTimelineView>().detail;
            Vector2 point = Center(detail.rectTransform);
            yield return MoveTo(point);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            Vector2 destination = point + new Vector2(detail.rectTransform.rect.width * 0.25f, 0);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = destination, buttons = 1 });
            yield return null;
            yield return null;
            yield return MoveTo(destination);
            Assert.That(workspace.timeline.WindowStart, Is.LessThan(0),
                "Dragging the imported waveform right must reveal the pre-song grid.");

            for (int i = 0; i < 40; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState
                {
                    position = point,
                    scroll = new Vector2(0, 120)
                });
                yield return null;
                yield return null;
            }

            double expected = -44 * 60.0 / 128;
            Assert.That(workspace.timeline.WindowStart, Is.EqualTo(expected).Within(1e-8));
            Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(-44).Within(1e-8));
            detail.RefreshLabels();
            Assert.That(detail.GetComponentsInChildren<Text>()
                .Any(label => label.text == "-10.1"), Is.True);
            Assert.That(workspace.timeline.MusicTime, Is.Zero);

            yield return ClickAt(point);
            Assert.That(workspace.timeline.WindowStart, Is.EqualTo(expected).Within(1e-8),
                "Clicking before the audio begins must not reset the viewport to measure 1.");
            Assert.That(workspace.timeline.MusicTime,
                Is.EqualTo(expected + workspace.timeline.WindowDuration * 0.5).Within(0.05),
                "The playhead must move to the clicked negative time.");
            yield return null;
            Assert.That(workspace.grid.PlayheadTime,
                Is.EqualTo(workspace.timeline.MusicTime + workspace.timeline.AudioOffset).Within(1e-8));

            workspace.timeline.Seek(expected - 10);
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(expected).Within(1e-8));
            workspace.timeline.Seek(-0.5);
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(-0.5).Within(1e-8));
            workspace.timeline.PlayPreview();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(workspace.timeline.MusicTime, Is.InRange(-0.5, -0.1));
            Assert.That(workspace.timeline.GetComponent<AudioSource>().timeSamples, Is.Zero);
            workspace.timeline.PausePreview();
            double paused = workspace.timeline.MusicTime;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(paused).Within(1e-8));

            workspace.timeline.SetPreviewSpeed(0.5f);
            workspace.timeline.PlayPreview();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(0));
            Assert.That(workspace.timeline.GetComponent<AudioSource>().timeSamples, Is.GreaterThan(0));
            workspace.timeline.StopPreview();
            workspace.timeline.SetPreviewSpeed(1);
            Assert.That(workspace.timeline.MusicTime, Is.Zero);

            workspace.timeline.Seek(0.75);
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(0.75).Within(1e-8));
            Assert.That(workspace.screen.ScrollBeatOffset,
                Is.EqualTo(workspace.timeline.Timing.BeatAtTime(0.75)).Within(1e-8));
        }

        [UnityTest]
        public IEnumerator Saiki128To256To64KeepsScrollSpeedAndPlacesNotesOnResizedBeatSpacing()
        {
            string path = WriteTempoWave();

            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (workspace.timeline.Peaks == null || !workspace.timeline.Peaks.Ready)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            workspace.screen.Session.Edit(edit => edit.SetTiming(0,
                new TempoPoint(0, 128), new TempoPoint(7.5, 256), new TempoPoint(15, 64)));
            workspace.screen.ResetView();
            var timing = workspace.timeline.Timing;
            var grid = workspace.grid;
            var area = grid.rectTransform.rect;
            double unitsPerSecond = area.height / workspace.screen.VisibleBeats * 128 / 60;
            foreach (double boundary in new[] { 7.5, 15.0 })
            {
                workspace.timeline.Seek(boundary - 0.25);
                workspace.timeline.GetComponent<ChartAudioTimelineView>().followAudioControl.onClick.Invoke();
                yield return null;
                double startTime = timing.TimeAtBeat(workspace.screen.ScrollBeatOffset);
                double startY = grid.Projection.Project(timing.BeatAtTime(20));
                workspace.timeline.PlayPreview();
                yield return new WaitForSecondsRealtime(0.6f);
                workspace.timeline.PausePreview();
                yield return null;
                double endTime = timing.TimeAtBeat(workspace.screen.ScrollBeatOffset);
                double endY = grid.Projection.Project(timing.BeatAtTime(20));
                Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(boundary));
                Assert.That(endTime, Is.GreaterThan(boundary));
                Assert.That(startY - endY, Is.EqualTo((endTime - startTime) * unitsPerSecond).Within(0.001),
                    "Playback follow must keep the same pixel speed through both BPM transitions.");
            }

            double previousY = 0;
            foreach (double time in new[] { 7.3, 7.4, 7.5, 7.6, 14.8, 14.9, 15.0, 15.1 })
            {
                workspace.screen.Scroll(timing.BeatAtTime(time) - workspace.screen.ScrollBeatOffset);
                yield return null;
                var projection = grid.Projection;
                double y = projection.Project(timing.BeatAtTime(20));
                if (time != 7.3 && time != 14.8)
                {
                    Assert.That(previousY - y, Is.EqualTo(unitsPerSecond * 0.1).Within(0.001),
                        "An equal elapsed time must move the grid by equal pixels across BPM changes.");
                }

                Assert.That(timing.TimeAtBeat(projection.Unproject(area.yMax)) - time,
                    Is.EqualTo(workspace.screen.VisibleBeats * 60.0 / 128).Within(1e-8));
                previousY = y;
            }

            foreach (double start in new[] { 0.0, 7.5, 15 })
            {
                workspace.timeline.Pan(start);
                yield return null;
                var projection = grid.Projection;
                double beat = timing.BeatAtTime(start + 0.5);
                double spacing = projection.Project(beat + 1) - projection.Project(beat);
                Assert.That(spacing, Is.EqualTo(projection.UnitsPerBeat * 128 / timing.BpmAt(start + 0.5)).Within(0.001));
                Vector2 local = new Vector2(area.xMin + area.width * 0.125f, area.yMin + area.height * 0.8f);
                double expectedBeat = timing.SnapBeat(projection.Unproject(local.y), workspace.screen.SnapBeats,
                    workspace.screen.ScrollBeatOffset, projection.Unproject(area.yMax));
                double expectedTime = timing.TimeAtBeat(expectedBeat);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, grid.rectTransform.TransformPoint(local));
                yield return ClickAt(point);
                Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId)
                    .Any(note => Math.Abs(note.Data.StartTime - expectedTime) < 1e-8), Is.True,
                    "A click must use the tempo-dependent grid spacing, including beyond the old visible-beat limit.");
                Assert.That(grid.HitTest(new Vector2(local.x, (float)projection.Project(expectedBeat))), Is.Not.Zero);
            }

            workspace.timeline.Pan(5);
            yield return null;
            CaptureTempoPreview("tempo-spacing-128-256.png");
            workspace.timeline.Pan(12);
            yield return null;
            CaptureTempoPreview("tempo-spacing-256-64.png");
        }

        [UnityTest]
        public IEnumerator TempoButtonEditsCursorBpmAndSettingsKeepsDetailedGridControls()
        {
            string path = WriteTempoWave();

            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (workspace.timeline.Peaks == null || !workspace.timeline.Peaks.Ready)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            workspace.view.bpm.text = "128";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            Assert.That(timelineView.advancedGridControl.GetComponentInChildren<Text>().text, Is.EqualTo("변속"));
            yield return ClickAt(Center(workspace.view.menus[1].trigger.GetComponent<RectTransform>()));
            workspace.view.menus[1].panel.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            yield return ClickAt(Center(timelineView.gridSettingsControl.GetComponent<RectTransform>()));
            Assert.That(timelineView.advanced.activeSelf, Is.True);
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            Assert.That(timelineView.numerator.gameObject.activeInHierarchy, Is.True);
            yield return ClickAt(Center(timelineView.closeAdvancedControl.GetComponent<RectTransform>()));

            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 8, 10));
            });
            var original = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            var grid = workspace.screen.Session.Snapshot.Grid;
            timelineView.existingNotePolicyControl.value = 1;
            workspace.timeline.Seek(7.51);
            yield return ClickAt(Center(timelineView.advancedGridControl.GetComponent<RectTransform>()));
            Assert.That(timelineView.tempoChangePopup.activeSelf, Is.True);
            Assert.That(workspace.screen.IsBusy, Is.True);
            StringAssert.Contains("5.1", timelineView.tempoChangePosition.text);
            Assert.That(timelineView.tempoChangeBpm.text, Is.EqualTo("128"));
            timelineView.tempoChangeBpm.text = "160";
            CaptureTempoPreview("tempo-change-dialog.png");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(timelineView.tempoChangePopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);
            var points = workspace.screen.Session.Snapshot.TempoPoints;
            Assert.That(points.Length, Is.EqualTo(2));
            Assert.That(points[1].StartTimeSeconds, Is.EqualTo(7.5).Within(1e-8));
            Assert.That(points[1].Bpm, Is.EqualTo(160));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.EqualTo(original));
            Assert.That(workspace.screen.Session.Snapshot.Grid.Meters, Is.EqualTo(grid.Meters));
            workspace.timeline.Pan(7);
            yield return null;
            Assert.That(timelineView.detail.GetComponentsInChildren<Text>()
                .Any(label => label.text == "BPM 128 → 160"), Is.True);
            CaptureTempoPreview("tempo-change-marker.png");

            workspace.screen.Session.Undo();
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(1));
            workspace.screen.Session.Redo();
            workspace.timeline.Seek(7.5);
            yield return ClickAt(Center(timelineView.advancedGridControl.GetComponent<RectTransform>()));
            Assert.That(timelineView.tempoChangeBpm.text, Is.EqualTo("160"));
            Assert.That(timelineView.deleteTempoChangeControl.interactable, Is.True);
            timelineView.tempoChangeBpm.text = "192";
            yield return ClickAt(Center(timelineView.applyTempoChangeControl.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(2));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints[1].Bpm, Is.EqualTo(192));

            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            var saved = workspace.menu.Store.Read(workspace.screen.Session.Snapshot.Id);
            Assert.That(saved.TempoPoints[1].Bpm, Is.EqualTo(192));
            workspace.timeline.Seek(7.5);
            yield return ClickAt(Center(timelineView.advancedGridControl.GetComponent<RectTransform>()));
            yield return ClickAt(Center(timelineView.deleteTempoChangeControl.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(1));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator TempoDialogRejectsInvalidBpmAndCancelsWithoutChangingTiming()
        {
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            var original = workspace.screen.Session.Snapshot.TempoPoints;
            yield return ClickAt(Center(timelineView.advancedGridControl.GetComponent<RectTransform>()));
            Assert.That(timelineView.deleteTempoChangeControl.interactable, Is.False);
            foreach (string value in new[] { "0", "-5", "NaN", "abc" })
            {
                timelineView.tempoChangeBpm.text = value;
                yield return ClickAt(Center(timelineView.applyTempoChangeControl.GetComponent<RectTransform>()));
                Assert.That(timelineView.tempoChangePopup.activeSelf, Is.True);
                Assert.That(timelineView.tempoChangeError.text, Is.Not.Empty);
                Assert.That(workspace.screen.Session.Snapshot.TempoPoints, Is.EqualTo(original));
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(timelineView.tempoChangePopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints, Is.EqualTo(original));
        }

        private void CaptureTempoPreview(string name)
        {
            var canvas = workspace.screen.GetComponentInParent<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var previousMode = canvas.renderMode;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(Path.Combine("Logs", name), texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator QuantizationButtonStartsAtQuarterCyclesAndSynchronizesAllControls()
        {
            var view = workspace.view;
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            Assert.That(view.quantization.value, Is.Zero);
            Assert.That(timelineView.noteSnapControl.value, Is.Zero);
            Assert.That(view.quantizationCycleValue.text, Is.EqualTo("1/4"));
            Assert.That(workspace.screen.SnapBeats, Is.EqualTo(1));
            var toolLabel = view.quantizationCycle.transform.parent.Find("Tool Space Label");
            Assert.That(toolLabel.gameObject.activeSelf, Is.False);

            string[] values = { "1/4", "1/8", "1/12", "1/16", "1/24", "1/32", "1/48", "1/64", "1/96", "1/192", "OFF" };
            int[] divisions = { 4, 8, 12, 16, 24, 32, 48, 64, 96, 192 };
            for (int i = 1; i <= values.Length; i++)
            {
                yield return ClickAt(Center(view.quantizationCycle.GetComponent<RectTransform>()));
                int index = i % values.Length;
                Assert.That(view.quantizationCycleValue.text, Is.EqualTo(values[index]));
                Assert.That(view.quantization.value, Is.EqualTo(index));
                Assert.That(timelineView.noteSnapControl.value, Is.EqualTo(index));
                Assert.That(workspace.screen.SnapEnabled, Is.EqualTo(index < divisions.Length));
                if (workspace.screen.SnapEnabled)
                {
                    Assert.That(workspace.screen.SnapBeats, Is.EqualTo(4.0 / divisions[index]).Within(.000001));
                }
            }

            view.quantization.value = 1;
            Assert.That(view.quantizationCycleValue.text, Is.EqualTo("1/8"));
            timelineView.noteSnapControl.value = 3;
            Assert.That(view.quantization.value, Is.EqualTo(3));
            Assert.That(view.quantizationCycleValue.text, Is.EqualTo("1/16"));
            Assert.That(workspace.screen.SnapBeats, Is.EqualTo(.25));
            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            if (view.newPopup.activeSelf)
            {
                workspace.Run(WorkspaceAction.DiscardNew);
                yield return Idle();
            }
            Assert.That(view.quantizationCycleValue.text, Is.EqualTo("1/4"));
            Assert.That(workspace.screen.SnapBeats, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShortcutKeyEventsResolveEveryCommandWithExactModifiers()
        {
            workspace.enabled = false;
            EventSystem.current.SetSelectedGameObject(null);
            try
            {
                foreach (var binding in WorkspaceShortcuts.Bindings)
                {
                    var keys = new System.Collections.Generic.List<Key> { binding.Key };
                    if (binding.Control)
                    {
                        keys.Add(Key.RightCtrl);
                    }
                    if (binding.Shift)
                    {
                        keys.Add(Key.RightShift);
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                    yield return null;
                    Assert.That(WorkspaceShortcuts.TryGetAction(keyboard, out var action), Is.True, binding.Label);
                    Assert.That(action, Is.EqualTo(binding.Action), binding.Label);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                var slotKeys = new[] { Key.Digit1, Key.Digit2, Key.Digit3 };
                for (int i = 0; i < slotKeys.Length; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftAlt, slotKeys[i]));
                    yield return null;
                    Assert.That(WorkspaceShortcuts.TryGetSlot(keyboard, out int slot), Is.True);
                    Assert.That(slot, Is.EqualTo(i));
                    Assert.That(WorkspaceShortcuts.TryGetAction(keyboard, out _), Is.False);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                foreach (Key key in new[] { Key.Home, Key.B, Key.Q })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null;
                    Assert.That(WorkspaceShortcuts.TryGetAction(keyboard, out _), Is.False);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.Z));
                yield return null;
                Assert.That(WorkspaceShortcuts.TryGetAction(keyboard, out _), Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally
            {
                workspace.enabled = true;
            }
        }

        [UnityTest]
        public IEnumerator ShortcutLabelsAndToolDifficultyEditingCommandsStayInSync()
        {
            foreach (var binding in workspace.view.actions)
            {
                string shortcut = WorkspaceShortcuts.Label(binding.action);
                if (!string.IsNullOrEmpty(shortcut))
                {
                    var label = binding.button.transform.Find("Shortcut").GetComponent<Text>();
                    Assert.That(label.text, Is.EqualTo(shortcut));
                    Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleRight));
                    Assert.That(label.raycastTarget, Is.False);
                }
                else
                {
                    Assert.That(binding.button.transform.Find("Shortcut"), Is.Null);
                }
            }
            foreach (var binding in workspace.view.toolActions)
            {
                var label = binding.button.transform.Find("Shortcut").GetComponent<Text>();
                Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleRight));
                Assert.That(label.text, Is.EqualTo(WorkspaceShortcuts.Label(binding.action)));
            }
            foreach (var button in workspace.view.difficulty)
            {
                Assert.That(button.GetComponentInChildren<Text>().text, Does.Not.Contain("Alt"));
            }
            Assert.That(workspace.view.actions.Any(binding => binding.action == WorkspaceAction.EstimateBpm), Is.True);
            yield return PressKeys(Key.A, Key.LeftCtrl);
            yield return PressKeys(Key.A, Key.LeftCtrl, Key.LeftShift);
            foreach (Key key in new[] { Key.V, Key.P, Key.G, Key.E })
            {
                yield return PressKeys(key);
                Assert.That(workspace.view.selection.text, Does.Contain("도구"));
            }
            yield return PressKeys(Key.Digit2, Key.LeftAlt);
            Assert.That(workspace.screen.Session.Snapshot.Charts.Single(item => item.Id == workspace.screen.ChartId).Slot,
                Is.EqualTo(ChartSlot.Standard));
            yield return PressKeys(Key.Digit3, Key.LeftAlt);
            Assert.That(workspace.screen.Session.Snapshot.Charts.Single(item => item.Id == workspace.screen.ChartId).Slot,
                Is.EqualTo(ChartSlot.Hard));
            yield return PressKeys(Key.Digit1, Key.LeftAlt);

            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
            });
            yield return PressKeys(Key.Digit1, Key.LeftCtrl);
            Assert.That(workspace.Editing.Selected.Count, Is.EqualTo(1));
            yield return PressKeys(Key.M, Key.LeftCtrl);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(note => note.Data.Kind == NoteKind.Tap).Data.Lane,
                Is.EqualTo(3));
            yield return PressKeys(Key.Z, Key.LeftCtrl);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(note => note.Data.Kind == NoteKind.Tap).Data.Lane,
                Is.Zero);
            yield return PressKeys(Key.Y, Key.LeftCtrl);
            yield return PressKeys(Key.Digit2, Key.LeftCtrl);
            Assert.That(workspace.Editing.Selected.Count, Is.EqualTo(1));
            yield return PressKeys(Key.A, Key.LeftCtrl, Key.LeftShift);
            Assert.That(workspace.Editing.Selected, Is.Empty);
            yield return PressKeys(Key.A, Key.LeftCtrl);
            Assert.That(workspace.Editing.Selected.Count, Is.EqualTo(2));
            yield return PressKeys(Key.E);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
            yield return PressKeys(Key.Z, Key.LeftCtrl);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FileShortcutsAndEscapeRespectMenusDialogsAndTyping()
        {
            workspace.view.OpenMenu(workspace.view.menus[0]);
            yield return PressKeys(Key.Escape);
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.False);
            yield return PressKeys(Key.S, Key.LeftCtrl, Key.LeftShift);
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.True);
            yield return EnterText(workspace.view.saveAsName, "Keyboard Copy");
            yield return PressKeys(Key.Enter);
            yield return Idle();
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session.Snapshot.Title, Is.EqualTo("Keyboard Copy"));
            workspace.screen.Session.Edit(edit => edit.SetMetadata("Keyboard saved", "Test"));
            EventSystem.current.SetSelectedGameObject(null);
            yield return PressKeys(Key.S, Key.LeftCtrl);
            yield return Idle();
            Assert.That(workspace.HasUnsavedChanges, Is.False);
            Assert.That(workspace.menu.Store.Read(workspace.screen.Session.Snapshot.Id).Title, Is.EqualTo("Keyboard saved"));

            workspace.view.OpenMenu(workspace.view.menus[1]);
            yield return EnterText(workspace.view.title, "Typed title");
            yield return PressKeys(Key.S, Key.LeftCtrl, Key.LeftShift);
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
            yield return PressKeys(Key.Escape);
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            EventSystem.current.SetSelectedGameObject(null);
            yield return PressKeys(Key.N, Key.LeftCtrl);
            Assert.That(workspace.view.newPopup.activeSelf, Is.True);
            var original = workspace.screen.Session;
            yield return PressKeys(Key.Escape);
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session, Is.SameAs(original));
            yield return PressKeys(Key.Escape);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.True);
            yield return PressKeys(Key.Escape);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(WorkspaceEditor.ScenePath));
        }

        [UnityTest]
        public IEnumerator EditorUsesDefaultPresentationAndRestoresThemedPointerOnTitle()
        {
            var cursor = UnityEngine.Object.FindFirstObjectByType<DojoCursor>();
            var feedback = UnityEngine.Object.FindFirstObjectByType<DojoClickFeedback>(FindObjectsInactive.Include);
            var impact = UnityEngine.Object.FindFirstObjectByType<DojoPunchImpact>(FindObjectsInactive.Include);
            Assert.That(DojoCursor.UsesDefaultPointer, Is.True);
            Assert.That(cursor, Is.Not.Null);
            Assert.That(feedback.enabled, Is.False);
            Assert.That(impact.enabled, Is.False);
            var texture = typeof(DojoCursor).GetField("currentTexture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(texture.GetValue(cursor), Is.Null);
            feedback.PlayAt(new Vector2(100, 100));
            Assert.That(feedback.GetComponent<AudioSource>().isPlaying, Is.False);

            var canvas = workspace.GetComponent<Canvas>();
            var scope = canvas.GetComponent<UiFontScope>();
            Assert.That(scope.font.name, Does.Contain("NotoSansKR"));
            Assert.That(scope.style, Is.EqualTo(FontStyle.Normal));
            UiFontSettings.ApplyTo(canvas.transform);
            Canvas.ForceUpdateCanvases();
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            Assert.That(timelineView.bpm.textComponent.cachedTextGenerator.characterCountVisible, Is.GreaterThan(0),
                "The regular font must keep numeric input values visible in compact fields.");
            Assert.That(timelineView.noteSnapControl.captionText.cachedTextGenerator.characterCountVisible, Is.GreaterThan(0));
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                Assert.That(text.font, Is.SameAs(scope.font), text.name);
                Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), text.name);
            }

            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            Assert.That(DojoCursor.UsesDefaultPointer, Is.False);
            Assert.That(feedback.enabled, Is.True);
            Assert.That(impact.enabled, Is.True);
            Assert.That(texture.GetValue(cursor), Is.Not.Null);
            feedback.PlayAt(new Vector2(100, 100));
            Assert.That(feedback.GetComponent<AudioSource>().isPlaying, Is.True);

            yield return SceneManager.LoadSceneAsync(WorkspaceEditor.ScenePath);
            yield return null;
            Assert.That(DojoCursor.UsesDefaultPointer, Is.True);
            Assert.That(feedback.enabled, Is.False);
            Assert.That(impact.enabled, Is.False);
            Assert.That(texture.GetValue(cursor), Is.Null);
        }

        [UnityTest]
        public IEnumerator EscapeReturnsToTitleWhenSavedAndNoMenuIsOpen()
        {
            yield return PressKeys(Key.S, Key.LeftCtrl);
            yield return Idle();
            Assert.That(workspace.HasUnsavedChanges, Is.False);
            yield return PressKeys(Key.Escape);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!SceneManager.GetActiveScene().path.EndsWith("/Title.unity"))
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ControlEnterAppliesSettingsWithoutSubmittingSelectedToolButton()
        {
            long id = 0;
            workspace.screen.Session.Edit(edit => id = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.Editing.Selected.Add(id);
            var button = workspace.view.toolActions.Single(item => item.action == WorkspaceAction.Eraser).button;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return PressKeys(Key.Enter, Key.LeftCtrl);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(1));
            Assert.That(workspace.Editing.Selected, Does.Contain(id));
        }

        private IEnumerator PressKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EraserButtonDeletesSelectionAndUndoRestoresWholeGroup()
        {
            long tap = 0;
            long hold = 0;
            long other = 0;
            workspace.screen.Session.Edit(edit =>
            {
                tap = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                hold = edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
                other = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(3, .5));
            });
            workspace.Run(WorkspaceAction.Select);
            yield return Idle();
            yield return ClickAt(GridPoint(0, 1));
            yield return ClickAt(GridPoint(1, 2.5));
            var button = workspace.view.toolActions.Single(item => item.action == WorkspaceAction.Eraser).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Select(note => note.Id),
                Is.EqualTo(new[] { other }));
            Assert.That(workspace.Editing.Selected, Is.Empty);

            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes.Select(note => note.Id), Is.EquivalentTo(new[] { tap, hold, other }));
            Assert.That(notes.Single(note => note.Id == hold).Data.EndTime, Is.EqualTo(3));
            workspace.Run(WorkspaceAction.Redo);
            yield return Idle();
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(1));

            yield return ClickAt(GridPoint(3, .5));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
        }

        [UnityTest]
        public IEnumerator EraserMenuAndClickDeleteSelectedGroup()
        {
            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
            });
            workspace.Run(WorkspaceAction.SelectAll);
            yield return Idle();
            workspace.view.OpenMenu(workspace.view.menus[3]);
            var button = workspace.view.actions.Single(item => item.action == WorkspaceAction.Eraser).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            workspace.Run(WorkspaceAction.SelectAll);
            yield return Idle();
            yield return ClickAt(GridPoint(0, 1));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator GrabSelectedNoteMovesWholeSelectionAndLeavesOthersAlone()
        {
            long tap = 0;
            long hold = 0;
            long other = 0;
            workspace.screen.Session.Edit(edit =>
            {
                tap = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                hold = edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
                other = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(3, .5));
            });
            workspace.Run(WorkspaceAction.Select);
            yield return Idle();
            yield return ClickAt(GridPoint(0, 1));
            yield return ClickAt(GridPoint(1, 2.5));
            Assert.That(workspace.Editing.Selected, Is.EquivalentTo(new[] { tap, hold }));
            var button = workspace.view.toolActions.Single(item => item.action == WorkspaceAction.Position).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            yield return DragNote(GridPoint(0, 1), GridPoint(1, 1.5));
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes.Single(note => note.Id == tap).Data.Lane, Is.EqualTo(1));
            Assert.That(notes.Single(note => note.Id == tap).Data.StartTime, Is.EqualTo(1.5).Within(.001));
            Assert.That(notes.Single(note => note.Id == hold).Data.Lane, Is.EqualTo(2));
            Assert.That(notes.Single(note => note.Id == hold).Data.StartTime, Is.EqualTo(2.5).Within(.001));
            Assert.That(notes.Single(note => note.Id == hold).Data.EndTime, Is.EqualTo(3.5).Within(.001));
            Assert.That(notes.Single(note => note.Id == other).Data.Lane, Is.EqualTo(3));
            Assert.That(notes.Single(note => note.Id == other).Data.StartTime, Is.EqualTo(.5));
            Assert.That(workspace.Editing.Selected, Is.EquivalentTo(new[] { tap, hold }));
            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes.Single(note => note.Id == tap).Data.StartTime, Is.EqualTo(1));
            Assert.That(notes.Single(note => note.Id == hold).Data.StartTime, Is.EqualTo(2));

            yield return DragNote(GridPoint(3, .5), GridPoint(2, .75));
            notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes.Single(note => note.Id == other).Data.Lane, Is.EqualTo(2));
            Assert.That(notes.Single(note => note.Id == tap).Data.Lane, Is.Zero);
            Assert.That(notes.Single(note => note.Id == hold).Data.Lane, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GrabSelectionClampsAsAGroupAtFirstLaneAndTimeZero()
        {
            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, .5));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 1.5, 2.5));
            });
            workspace.Run(WorkspaceAction.SelectAll);
            yield return Idle();
            workspace.Run(WorkspaceAction.Position);
            yield return Idle();
            yield return DragNote(GridPoint(1, 2), GridPoint(0, .5));
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes[0].Data.Lane, Is.Zero);
            Assert.That(notes[0].Data.StartTime, Is.Zero.Within(.001));
            Assert.That(notes[1].Data.Lane, Is.EqualTo(1));
            Assert.That(notes[1].Data.StartTime, Is.EqualTo(1).Within(.001));
            Assert.That(notes[1].Data.EndTime, Is.EqualTo(2).Within(.001));
        }

        [UnityTest]
        public IEnumerator LeftToolIconsSelectDrawEraseAndGrabWithVisibleSelection()
        {
            var bindings = workspace.view.toolActions;
            Assert.That(bindings.Select(item => item.action), Is.EqualTo(new[]
            {
                WorkspaceAction.Select, WorkspaceAction.Pencil, WorkspaceAction.Eraser, WorkspaceAction.Position,
                WorkspaceAction.Scroll
            }));
            foreach (var binding in bindings)
            {
                var icon = binding.button.GetComponentInChildren<WorkspaceToolIcon>();
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.raycastTarget, Is.False);
            }
            Assert.That(bindings[1].button.colors.normalColor.grayscale,
                Is.LessThan(bindings[0].button.colors.normalColor.grayscale));

            yield return ClickAt(Center(bindings[1].button.GetComponent<RectTransform>()));
            yield return ClickAt(GridPoint(0, 1));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(1));
            yield return ClickAt(Center(bindings[0].button.GetComponent<RectTransform>()));
            yield return ClickAt(GridPoint(0, 1));
            Assert.That(workspace.Editing.Selected.Count, Is.EqualTo(1));
            Assert.That(bindings[0].button.colors.normalColor.grayscale,
                Is.LessThan(bindings[1].button.colors.normalColor.grayscale));

            yield return ClickAt(Center(bindings[2].button.GetComponent<RectTransform>()));
            yield return ClickAt(GridPoint(0, 1));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
            yield return ClickAt(Center(bindings[1].button.GetComponent<RectTransform>()));
            yield return ClickAt(GridPoint(0, 1));
            yield return ClickAt(Center(bindings[3].button.GetComponent<RectTransform>()));
            yield return DragNote(GridPoint(0, 1), GridPoint(2, 1.5));
            var moved = workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single();
            Assert.That(moved.Data.Lane, Is.EqualTo(2));
            Assert.That(moved.Data.StartTime, Is.EqualTo(1.5).Within(.001));

            workspace.view.OpenMenu(workspace.view.menus[3]);
            var button = workspace.view.actions.Single(item => item.action == WorkspaceAction.Select).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            Assert.That(bindings[0].button.colors.normalColor.grayscale,
                Is.LessThan(bindings[3].button.colors.normalColor.grayscale));
        }

        [UnityTest]
        [Category("BpmSuggestion")]
        public IEnumerator ImportAudioAutomaticallyEstimatesBpmWithoutChangingSavedTiming()
        {
            string path = Path.GetFullPath("LocalAudio/Saiki.wav");
            if (!File.Exists(path))
            {
                path = WriteTempoWave();
            }
            double original = workspace.screen.Session.Snapshot.TempoPoints[0].Bpm;
            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();

            Assert.That(workspace.timeline.PreviewClip, Is.Not.Null);
            Assert.That(workspace.timeline.IsLoadingAudio, Is.False);
            Assert.That(double.Parse(workspace.view.bpm.text), Is.EqualTo(original));
            Assert.That(workspace.timeline.GetComponent<ChartAudioTimelineView>().bpm.text,
                Is.EqualTo(workspace.view.bpm.text));
            Assert.That(workspace.view.bpmSuggestion.text, Does.StartWith("자동 분석된 BPM은 "));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(original));
            string suggestion = workspace.view.bpmSuggestion.text;
            var session = workspace.screen.Session;
            session.Edit(edit =>
            {
                edit.SetMetadata("Suggestion survives editing", "Artist");
                edit.SetTiming(.25, new TempoPoint(0, original), new TempoPoint(4, 256));
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
            });
            workspace.view.difficulty[2].onClick.Invoke();
            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            Assert.That(workspace.view.bpmSuggestion.text, Is.EqualTo(suggestion));
            workspace.view.applyEstimatedBpm.onClick.Invoke();
            yield return Idle();
            Assert.That(session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(128).Within(2));
            Assert.That(session.Snapshot.TempoPoints[1].Bpm, Is.EqualTo(256));
            Assert.That(session.Snapshot.AudioOffsetSeconds, Is.EqualTo(.25));
            Assert.That(workspace.view.bpmSuggestion.text, Is.Empty);
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);
            session.Undo();
            Assert.That(session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(original));
            Assert.That(session.Snapshot.TempoPoints[1].Bpm, Is.EqualTo(256));

            workspace.Run(WorkspaceAction.EstimateBpm);
            Assert.That(workspace.view.bpmSuggestion.text, Is.EqualTo("BPM 분석 중…"));
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);
            yield return Idle();
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.True);
            session.Edit(edit => edit.SetAudio(null));
            Assert.That(workspace.view.bpmSuggestion.text, Is.Empty);
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("BpmSuggestion")]
        public IEnumerator BpmAnalysisIgnoresReplacedAudioAndProject()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            workspace.Run(WorkspaceAction.EstimateBpm);
            workspace.screen.Session.Edit(edit => edit.SetAudio(null));
            yield return Idle();
            Assert.That(workspace.view.bpmSuggestion.text, Is.Empty);
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);

            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            workspace.Run(WorkspaceAction.EstimateBpm);
            var replacement = new EditorSession(new SongProject());
            replacement.Edit(edit => edit.AddChartSlot(ChartSlot.Easy, workspace.screen.Mode.name, 4, 8.5));
            workspace.menu.ReplaceSession(replacement);
            yield return Idle();
            yield return null;
            Assert.That(workspace.view.bpmSuggestion.text, Is.Empty);
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("BpmSuggestion")]
        public IEnumerator SpaceTogglesPlaybackButNotWhileTypingOrConfirming()
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "Space preview.wav");
            const int rate = 8000;
            const int frames = rate * 8;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + frames * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(frames * 2);
                writer.Write(new byte[frames * 2]);
            }

            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            Assert.That(workspace.view.status.text, Does.Contain("BPM 자동 분석 실패"),
                "Silent audio should remain imported even when BPM cannot be estimated.");
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!workspace.timeline.PreviewClip || workspace.timeline.IsBusy)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            var actions = workspace.view.actions.Where(item =>
                item.button.transform.IsChildOf(workspace.view.menus[4].panel.transform)).ToArray();
            Assert.That(actions.Select(item => item.action), Is.EqualTo(new[]
            {
                WorkspaceAction.PlayPause, WorkspaceAction.Stop, WorkspaceAction.Test,
                WorkspaceAction.TestHere, WorkspaceAction.AutoTest, WorkspaceAction.AutoTestHere
            }));
            Assert.That(actions[0].button.transform.Find("Shortcut").GetComponent<Text>().text, Is.EqualTo("Space"));
            Assert.That(actions[1].button.GetComponentInChildren<Text>().text, Is.EqualTo("처음으로"));
            Assert.That(actions[1].button.transform.Find("Shortcut").GetComponent<Text>().text, Is.EqualTo("0"));
            Assert.That(workspace.view.loopStart, Is.Null);
            Assert.That(workspace.view.loopEnd, Is.Null);
            Assert.That(workspace.view.playback, Is.Null);

            workspace.view.OpenMenu(workspace.view.menus[4]);
            yield return ClickAt(Center(actions[0].button.GetComponent<RectTransform>()));
            Assert.That(workspace.timeline.IsPlaying, Is.True);
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.False);
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.True);

            yield return PressKeys(Key.Digit0);
            yield return Idle();
            Assert.That(workspace.timeline.IsPlaying, Is.False);
            Assert.That(workspace.timeline.MusicTime, Is.Zero.Within(.001));
            workspace.view.OpenMenu(workspace.view.menus[1]);
            yield return ClickAt(Center(workspace.view.title.GetComponent<RectTransform>()));
            Assert.That(workspace.view.title.isFocused, Is.True);
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.False);

            workspace.view.CloseMenus();
            EventSystem.current.SetSelectedGameObject(null);
            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            Assert.That(workspace.view.newPopup.activeSelf, Is.True);
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.False);
        }

        [UnityTest]
        [Category("PreviewLoop")]
        public IEnumerator ControlSpaceReturnsToPreviewStartAndOrdinarySpaceStillPauses()
        {
            string path = Path.GetFullPath("LocalAudio/Saiki.wav");
            if (!File.Exists(path))
            {
                path = WriteTempoWave();
            }

            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (workspace.timeline.Peaks == null || !workspace.timeline.Peaks.Ready)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            workspace.view.bpm.text = "128";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            foreach (double start in new[] { 3.0, -1.5, 6.0 })
            {
                workspace.timeline.Seek(start);
                EventSystem.current.SetSelectedGameObject(null);
                yield return PressKeys(Key.Space, Key.LeftCtrl);
                Assert.That(workspace.timeline.IsPlaying, Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(start + .1));
                yield return PressSpace();
                Assert.That(workspace.timeline.IsPlaying, Is.False);
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(start).Within(.001));
            }

            yield return PressSpace();
            yield return new WaitForSecondsRealtime(.3f);
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.False);
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(6.1));

            yield return PressKeys(Key.Space, Key.LeftCtrl);
            yield return PressKeys(Key.Digit0);
            Assert.That(workspace.timeline.MusicTime, Is.Zero.Within(.001));
            yield return PressSpace();
            yield return new WaitForSecondsRealtime(.3f);
            yield return PressSpace();
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(.1));
        }

        [UnityTest]
        [Category("PreviewLoop")]
        public IEnumerator PreviewLoopPreservesReturnPositionAcrossBoundariesAndSpeedChanges()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            var loopStart = new GameObject("Test loop start", typeof(RectTransform), typeof(InputField));
            loopStart.transform.SetParent(workspace.transform, false);
            workspace.view.loopStart = loopStart.GetComponent<InputField>();
            workspace.view.loopStart.text = (workspace.timeline.Duration - .3).ToString("R",
                System.Globalization.CultureInfo.InvariantCulture);
            double start = workspace.timeline.Duration - .15;
            workspace.timeline.Seek(start);
            workspace.Run(WorkspaceAction.Loop);
            yield return PressKeys(Key.Space, Key.LeftCtrl);

            for (int iteration = 0; iteration < 4; iteration++)
            {
                if (iteration == 2)
                {
                    workspace.timeline.SetPreviewSpeed(.5f);
                }
                double previous = workspace.timeline.MusicTime;
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                do
                {
                    previous = workspace.timeline.MusicTime;
                    yield return null;
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                }
                while (workspace.timeline.MusicTime >= previous);
                Assert.That(workspace.timeline.IsPlaying, Is.True);
            }
            yield return PressSpace();
            Assert.That(workspace.timeline.IsPlaying, Is.False);
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(start).Within(.001));

            workspace.Run(WorkspaceAction.Loop);
            workspace.timeline.Seek(2);
            workspace.timeline.TogglePreview(true);
            workspace.timeline.RestartPreviewLoop(1);
            workspace.timeline.Seek(3);
            yield return new WaitForSecondsRealtime(.15f);
            workspace.timeline.TogglePreview();
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(3));
            workspace.timeline.TogglePreview(true);
            workspace.timeline.StopPreview();
            workspace.timeline.TogglePreview();
            yield return new WaitForSecondsRealtime(.15f);
            workspace.timeline.TogglePreview();
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(0));

            workspace.timeline.Seek(2);
            workspace.timeline.TogglePreview();
            workspace.timeline.RestartPreviewLoop(1);
            yield return new WaitForSecondsRealtime(.15f);
            workspace.timeline.TogglePreview();
            Assert.That(workspace.timeline.MusicTime, Is.InRange(1.01, 1.3));
        }

        private IEnumerator PressSpace()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrabMovesTapAndHoldWithPointerDragAndUndo()
        {
            long tap = 0;
            long hold = 0;
            workspace.screen.Session.Edit(edit =>
            {
                tap = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                hold = edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
            });
            var button = workspace.view.actions.Single(item => item.action == WorkspaceAction.Position).button;
            Assert.That(button.GetComponentInChildren<Text>().text, Is.EqualTo("잡기"));
            Assert.That(button.transform.Find("Shortcut").GetComponent<Text>().text, Is.EqualTo("G"));
            workspace.view.OpenMenu(workspace.view.menus[3]);
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            yield return DragNote(GridPoint(0, 1), GridPoint(2, 1.5));
            var moved = workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(item => item.Id == tap);
            Assert.That(moved.Data.Lane, Is.EqualTo(2));
            Assert.That(moved.Data.StartTime, Is.EqualTo(1.5).Within(.001));
            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(item => item.Id == tap).Data.Lane,
                Is.EqualTo(0));

            yield return DragNote(GridPoint(1, 2.5), GridPoint(3, 3.5));
            moved = workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(item => item.Id == hold);
            Assert.That(moved.Data.Lane, Is.EqualTo(3));
            Assert.That(moved.Data.StartTime, Is.EqualTo(3).Within(.001));
            Assert.That(moved.Data.EndTime, Is.EqualTo(4).Within(.001));
            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single(item => item.Id == hold).Data.StartTime,
                Is.EqualTo(2));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator GrabCancelsOutsideGridAndDoesNotDrawOnEmptySpace()
        {
            long id = 0;
            workspace.screen.Session.Edit(edit => id = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.Run(WorkspaceAction.Position);
            yield return Idle();
            yield return DragNote(GridPoint(0, 1), new Vector2(-100, -100));
            yield return DragNote(GridPoint(2, 1), GridPoint(3, 2));
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes.Length, Is.EqualTo(1));
            Assert.That(notes[0].Id, Is.EqualTo(id));
            Assert.That(notes[0].Data.Lane, Is.Zero);
            Assert.That(notes[0].Data.StartTime, Is.EqualTo(1));
        }

        private Vector2 GridPoint(int lane, double time)
        {
            Canvas.ForceUpdateCanvases();
            var rect = workspace.grid.rectTransform;
            float x = rect.rect.xMin + rect.rect.width * (lane + .5f) / 4;
            float y = (float)workspace.grid.Projection.Project(workspace.timeline.Timing.BeatAtTime(time));
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector2(x, y)));
        }

        [UnityTest]
        [Category("PlaybackSeek")]
        public IEnumerator ControlClickSeeksNearestVisibleBeatLineWithoutEditingNotes()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();

            long id = 0;
            workspace.screen.Session.Edit(edit =>
            {
                edit.SetTiming(.25, new TempoPoint(0, 120), new TempoPoint(1.2, 240));
                id = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 2.13));
            });
            workspace.screen.SnapEnabled = false;
            workspace.Run(WorkspaceAction.Pencil);
            yield return null;

            double scrollOffset = workspace.screen.ScrollBeatOffset;
            double windowStart = workspace.timeline.WindowStart;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return ClickAt(GridPoint(1, 1.18));
            Assert.That(workspace.grid.PlayheadTime, Is.EqualTo(1.35).Within(.001),
                "Across the BPM change, screen distance is closer to beat 3 than beat 2.");
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(1.10).Within(.001));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single().Id, Is.EqualTo(id));

            workspace.Run(WorkspaceAction.Eraser);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightCtrl));
            yield return ClickAt(GridPoint(0, 2.13));
            Assert.That(workspace.grid.PlayheadTime, Is.EqualTo(2.10).Within(.001));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single().Id, Is.EqualTo(id));
            Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(scrollOffset));
            Assert.That(workspace.timeline.WindowStart, Is.EqualTo(windowStart));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return ClickAt(GridPoint(0, 2.13));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
        }

        [UnityTest]
        [Category("PlaybackSeek")]
        public IEnumerator ControlClickSeeksNearestVisibleWaveformLine()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();

            workspace.screen.Session.Edit(edit => edit.SetTiming(.25, new TempoPoint(0, 128)));
            workspace.screen.SnapEnabled = false;
            workspace.timeline.Zoom(1, 0);
            yield return null;

            Canvas.ForceUpdateCanvases();
            var timeline = workspace.timeline;
            var detail = timeline.GetComponent<ChartAudioTimelineView>().detail.rectTransform;
            Assert.That(timeline.WindowDuration, Is.EqualTo(6.4).Within(.001));
            Assert.That(detail.GetComponentsInChildren<Text>().Select(label => label.text),
                Does.Contain("1.2").And.Contain("1.3").And.Contain("1.4").And.Contain("2.2"));
            double windowStart = timeline.WindowStart;
            double scrollOffset = workspace.screen.ScrollBeatOffset;
            const double clickTime = 2.04;
            float fraction = (float)((clickTime - windowStart) / timeline.WindowDuration);
            float x = Mathf.Lerp(detail.rect.xMin, detail.rect.xMax, fraction);
            var point = RectTransformUtility.WorldToScreenPoint(null, detail.TransformPoint(x, detail.rect.center.y, 0));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            yield return ClickAt(point);
            Assert.That(timeline.MusicTime, Is.EqualTo(2.09375).Within(.001));
            Assert.That(workspace.grid.PlayheadTime, Is.EqualTo(2.34375).Within(.001));
            Assert.That(timeline.WindowStart, Is.EqualTo(windowStart));
            Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(scrollOffset));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return ClickAt(point);
            Assert.That(timeline.MusicTime, Is.EqualTo(clickTime).Within(.005));
        }

        [UnityTest]
        [Category("PlaybackSeek")]
        public IEnumerator PlaybackPositionToolSeeksClickedGridLineWithoutEditingNotes()
        {
            string path = Path.GetFullPath("LocalAudio/Saiki.wav");
            if (!File.Exists(path))
            {
                path = WriteTempoWave();
            }
            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            double deadline = Time.realtimeSinceStartupAsDouble + 60;
            while (workspace.timeline.Peaks == null || !workspace.timeline.Peaks.Ready)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            workspace.screen.Session.Edit(edit => edit.SetTiming(.25,
                new TempoPoint(0, 128), new TempoPoint(7.5, 256), new TempoPoint(15, 64)));

            long id = 0;
            workspace.screen.Session.Edit(edit => id = edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.Editing.Selected.Add(id);
            yield return null;
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            var binding = workspace.view.toolActions.Single(item => item.action == WorkspaceAction.Scroll);
            var grab = workspace.view.toolActions.Single(item => item.action == WorkspaceAction.Position);
            Assert.That(binding.button.GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(grab.button.GetComponent<RectTransform>().anchoredPosition.y));
            Assert.That(binding.button.GetComponentInChildren<WorkspaceToolIcon>().kind,
                Is.EqualTo(WorkspaceToolIcon.IconKind.Scroll));
            Assert.That(workspace.view.quantizationCycle.GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(binding.button.GetComponent<RectTransform>().anchoredPosition.y));

            workspace.view.OpenMenu(workspace.view.menus[3]);
            var menuButton = workspace.view.actions.Single(item => item.action == WorkspaceAction.Scroll).button;
            Assert.That(menuButton.transform.IsChildOf(workspace.view.menus[3].panel.transform), Is.True);
            Assert.That(menuButton.GetComponentInChildren<Text>().text, Is.EqualTo("재생 위치"));
            yield return ClickAt(Center(menuButton.GetComponent<RectTransform>()));
            Assert.That(binding.button.colors.normalColor.grayscale,
                Is.LessThan(grab.button.colors.normalColor.grayscale));

            int visibleBeats = workspace.screen.VisibleBeats;
            foreach (double time in new[] { 1.875, 9.375, 16.875 })
            {
                double beat = workspace.timeline.Timing.BeatAtTime(time);
                workspace.screen.Scroll(beat - 4 - workspace.screen.ScrollBeatOffset);
                yield return null;
                double original = workspace.screen.ScrollBeatOffset;
                double windowStart = workspace.timeline.WindowStart;
                yield return ClickAt(GridPoint(2, time));
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(time - .25).Within(.001));
                Assert.That(workspace.grid.PlayheadTime, Is.EqualTo(time).Within(.001));
                Assert.That(workspace.timeline.IsPlaying, Is.False);
                Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(original).Within(.001));
                Assert.That(workspace.timeline.WindowStart, Is.EqualTo(windowStart).Within(.001));
                Assert.That(workspace.screen.VisibleBeats, Is.EqualTo(visibleBeats));

                yield return PressSpace();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(workspace.timeline.IsPlaying, Is.True);
                Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(time - .15));
                var source = workspace.timeline.GetComponent<AudioSource>();
                Assert.That((double)source.timeSamples / source.clip.frequency,
                    Is.GreaterThan(time - .25));
                yield return PressSpace();
            }

            yield return ClickAt(Center(binding.button.GetComponent<RectTransform>()));
            workspace.screen.ResetView();
            yield return null;
            yield return ClickAt(GridPoint(0, 1));
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(.6875).Within(.001),
                "The playhead snaps to beat 2 at 128 BPM, then subtracts the audio offset.");
            yield return DragNote(GridPoint(2, .9375), GridPoint(2, 1.875));
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(1.625).Within(.001));
            workspace.screen.Scroll(-4 - workspace.screen.ScrollBeatOffset);
            yield return null;
            yield return ClickAt(GridPoint(2, -.9375));
            Assert.That(workspace.timeline.MusicTime, Is.EqualTo(-1.1875).Within(.001));
            Assert.That(workspace.grid.PlayheadTime, Is.EqualTo(-.9375).Within(.001));

            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Single().Data,
                Is.EqualTo(notes.Single().Data));
            Assert.That(workspace.Editing.Selected, Is.EquivalentTo(new[] { id }));
        }

        [UnityTest]
        public IEnumerator GameplayPreviewFollowsAudioEditsAndChartSelectionAndReleasesTexture()
        {
            string path = Path.GetFullPath("LocalAudio/Saiki.wav");
            if (!File.Exists(path))
            {
                path = WriteTempoWave();
            }
            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            workspace.screen.Session.Edit(edit =>
            {
                edit.SetTiming(0, new TempoPoint(0, 128));
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 4));
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(3, 5));
            });
            yield return null;
            yield return null;
            var preview = UnityEngine.Object.FindFirstObjectByType<ChartGameplayPreview>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.Target.IsCreated(), Is.True);
            Assert.That(preview.image.raycastTarget, Is.False);
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(3));
            var camera = preview.playfield.GetComponentInChildren<Camera>();
            Assert.That(camera.GetComponent<AudioListener>(), Is.Null);
            Assert.That(camera.cullingMask, Is.EqualTo(1 << 31));
            Assert.That(Camera.main.cullingMask & (1 << 31), Is.Zero);

            workspace.timeline.Seek(3);
            yield return null;
            yield return null;
            Assert.That(preview.SongTime, Is.EqualTo(3).Within(.001));
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(2));
            Assert.That(preview.playfield.GetComponentsInChildren<RhythmDojo.Presentation.NoteView>()
                .Count(view => view.HasBody), Is.EqualTo(1));
            var previous = RenderTexture.active;
            var snapshot = new Texture2D(preview.Target.width, preview.Target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = preview.Target;
                snapshot.ReadPixels(new Rect(0, 0, snapshot.width, snapshot.height), 0, 0);
                snapshot.Apply();
                Assert.That(snapshot.GetPixels32().Select(pixel => pixel.r).Distinct().Count(), Is.GreaterThan(5));
                string evidence = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Logs"));
                Directory.CreateDirectory(evidence);
                File.WriteAllBytes(Path.Combine(evidence, "editor-gameplay-preview.png"), snapshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(snapshot);
            }

            workspace.timeline.PlayPreview();
            yield return new WaitForSecondsRealtime(.3f);
            workspace.timeline.PausePreview();
            Assert.That(preview.SongTime, Is.GreaterThan(3.1));
            workspace.screen.Session.Edit(edit => edit.AddNote(workspace.screen.ChartId, NoteData.Tap(2, 6)));
            yield return null;
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(3));
            workspace.screen.Session.Undo();
            yield return null;
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(2));
            workspace.screen.SelectSlot((int)ChartSlot.Hard);
            yield return null;
            Assert.That(preview.VisibleNoteCount, Is.Zero);
            workspace.screen.SelectSlot((int)ChartSlot.Easy);
            yield return null;
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(2));

            var target = preview.Target;
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            Assert.That(target == null, Is.True);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<ChartGameplayPreview>(), Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Any(item => item.name == "Editor Preview Camera"), Is.False);
        }

        [UnityTest]
        public IEnumerator GameplayPreviewNoteSpeedAcceptsDecimalsAndRejectsOutOfRangeValues()
        {
            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(1, 5));
            });
            yield return null;

            var preview = UnityEngine.Object.FindFirstObjectByType<ChartGameplayPreview>();
            var field = workspace.view.noteSpeed;
            Assert.That(field.text, Is.EqualTo("1.0"));
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(2));

            yield return EnterText(field, "5.5");
            yield return PressKeys(Key.Enter);
            Assert.That(field.text, Is.EqualTo("5.5"));
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(1));
            Assert.That(workspace.timeline.PreviewSpeed, Is.EqualTo(1));

            var warning = workspace.view.noteSpeedWarning;
            var confirm = workspace.view.actions.Single(item => item.action == WorkspaceAction.CloseNoteSpeedWarning).button;
            foreach (string invalidSpeed in new[] { "0.9", "10.1" })
            {
                yield return EnterText(field, invalidSpeed);
                yield return PressKeys(Key.Enter);
                Assert.That(warning.activeInHierarchy, Is.True);
                Assert.That(warning.GetComponentsInChildren<Text>().Any(label =>
                    label.text == "속도는 1.0 ~ 10.0 안에서만 조절 할 수 있습니다"), Is.True);
                Assert.That(field.text, Is.EqualTo("5.5"));
                Assert.That(preview.VisibleNoteCount, Is.EqualTo(1));
                yield return ClickAt(Center(confirm.GetComponent<RectTransform>()));
                Assert.That(warning.activeInHierarchy, Is.False);
            }

            foreach (string validSpeed in new[] { "2.0", "10.0", "1.0" })
            {
                yield return EnterText(field, validSpeed);
                yield return ClickAt(Center(preview.clock.rectTransform));
                Assert.That(field.text, Is.EqualTo(validSpeed));
                Assert.That(warning.activeInHierarchy, Is.False);
            }
            Assert.That(preview.VisibleNoteCount, Is.EqualTo(2));
        }

        [UnityTest]
        [Category("PlaybackSeek")]
        public IEnumerator PlaybackPositionDragFreezesViewportAndRestoresOnlyEnabledFollow()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            workspace.screen.Session.Edit(edit => edit.SetTiming(.25,
                new TempoPoint(0, 128), new TempoPoint(7.5, 256), new TempoPoint(15, 64)));
            workspace.Run(WorkspaceAction.Scroll);
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();

            foreach (double time in new[] { -.9375, 9.375, 16.875 })
            {
                workspace.timeline.PausePreview();
                workspace.screen.Scroll(workspace.timeline.Timing.BeatAtTime(time) - 4
                    - workspace.screen.ScrollBeatOffset);
                yield return null;
                double windowStart = workspace.timeline.WindowStart;
                double scroll = workspace.screen.ScrollBeatOffset;
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = GridPoint(2, time),
                    button = PointerEventData.InputButton.Left,
                    pointerId = 55
                };
                workspace.screen.OnPointerDown(pointer);
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(time - .25).Within(.001));
                workspace.timeline.PlayPreview();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(workspace.timeline.IsSeeking, Is.True);
                Assert.That(workspace.timeline.IsPlaying, Is.True);
                Assert.That(workspace.timeline.WindowStart, Is.EqualTo(windowStart));
                Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(scroll));
                workspace.screen.OnDrag(pointer);
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(time - .25).Within(.001));
                pointer.position = new Vector2(-100, -100);
                workspace.screen.OnPointerUp(pointer);
                Assert.That(workspace.timeline.IsSeeking, Is.False);
                yield return null;
                yield return null;
                Assert.That(workspace.screen.ScrollBeatOffset, Is.Not.EqualTo(scroll));
            }

            workspace.timeline.PausePreview();
            workspace.timeline.Pan(1);
            double original = workspace.timeline.WindowStart;
            double originalBeat = workspace.screen.ScrollBeatOffset;
            var start = GridPoint(2, 2);
            yield return MoveTo(start);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start, buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(workspace.timeline.IsSeeking, Is.True);
            workspace.timeline.PlayPreview();
            yield return new WaitForSecondsRealtime(.2f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-100, -100) });
            yield return null;
            yield return null;
            Assert.That(workspace.timeline.IsSeeking, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(workspace.timeline.WindowStart, Is.EqualTo(original));
            Assert.That(workspace.screen.ScrollBeatOffset, Is.EqualTo(originalBeat));
            timelineView.followAudioControl.onClick.Invoke();
            yield return null;
            Assert.That(workspace.screen.ScrollBeatOffset, Is.Not.EqualTo(originalBeat));
        }

        [UnityTest]
        [Category("PlaybackSeek")]
        public IEnumerator PlaybackPositionDragSuspendsLoopsAndCleansUpCancellationPaths()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            workspace.Run(WorkspaceAction.Scroll);
            var loopEnd = new GameObject("Test loop end", typeof(RectTransform), typeof(InputField));
            loopEnd.transform.SetParent(workspace.transform, false);
            workspace.view.loopEnd = loopEnd.GetComponent<InputField>();
            workspace.view.loopEnd.text = "1";
            workspace.Run(WorkspaceAction.Loop);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = GridPoint(2, 2),
                button = PointerEventData.InputButton.Left,
                pointerId = 55
            };
            workspace.screen.OnPointerDown(pointer);
            workspace.timeline.PlayPreview();
            double dsp = AudioSettings.dspTime;
            Assert.That(workspace.timeline.IsPlaying, Is.True);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(workspace.timeline.IsPlaying, Is.True, "Preview stopped while seeking");
            Assert.That(workspace.timeline.MusicTime, Is.GreaterThan(2),
                "DSP delta: " + (AudioSettings.dspTime - dsp) + "; seeking: " + workspace.timeline.IsSeeking);

            workspace.screen.OnPointerUp(pointer);
            yield return null;
            yield return null;
            Assert.That(workspace.timeline.MusicTime, Is.LessThan(1));
            workspace.Run(WorkspaceAction.Loop);
            workspace.timeline.PausePreview();

            for (int cancellation = 0; cancellation < 5; cancellation++)
            {
                workspace.Run(WorkspaceAction.Scroll);
                workspace.timeline.Pan(0);
                yield return null;
                pointer.position = GridPoint(2, 2);
                workspace.screen.OnPointerDown(pointer);
                Assert.That(workspace.timeline.IsSeeking, Is.True, "Cancellation case " + cancellation);
                switch (cancellation)
                {
                    case 0:
                        yield return PressKeys(Key.Escape);
                        break;
                    case 1:
                        workspace.Run(WorkspaceAction.Pencil);
                        break;
                    case 2:
                        workspace.SendMessage("OnApplicationFocus", false);
                        break;
                    case 3:
                        workspace.enabled = false;
                        workspace.enabled = true;
                        break;
                    case 4:
                        workspace.screen.enabled = false;
                        yield return null;
                        workspace.screen.enabled = true;
                        break;
                }
                Assert.That(workspace.timeline.IsSeeking, Is.False);
                workspace.timeline.GetComponent<ChartAudioTimelineView>().followAudioControl.onClick.Invoke();
                double scroll = workspace.screen.ScrollBeatOffset;
                workspace.timeline.PlayPreview();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(workspace.screen.ScrollBeatOffset, Is.Not.EqualTo(scroll));
                workspace.timeline.PausePreview();
            }
        }

        private IEnumerator DragNote(Vector2 start, Vector2 end)
        {
            yield return MoveTo(start);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start, buttons = 1 });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end, buttons = 1 });
            yield return null;
            yield return null;
            yield return MoveTo(end);
        }

        [UnityTest]
        [Category("BpmSuggestion")]
        public IEnumerator BpmFieldsSynchronizeAndCommitBothWays()
        {
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            workspace.view.OpenMenu(workspace.view.menus[1]);
            yield return EnterText(workspace.view.bpm, "175");
            Assert.That(timelineView.bpm.text, Is.EqualTo("175"));
            yield return ClickAt(Center(workspace.view.volume.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(175));

            workspace.view.CloseMenus();
            yield return EnterText(timelineView.bpm, "142.5");
            Assert.That(workspace.view.bpm.text, Is.EqualTo("142.5"));
            yield return ClickAt(Center(workspace.view.menus[1].trigger.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.Snapshot.TempoPoints[0].Bpm, Is.EqualTo(142.5));

            workspace.Run(WorkspaceAction.Undo);
            yield return Idle();
            Assert.That(workspace.view.bpm.text, Is.EqualTo("175"));
            Assert.That(timelineView.bpm.text, Is.EqualTo("175"));
        }

        [UnityTest]
        public IEnumerator VolumeDragUpdatesPercentFillAndSavedValue()
        {
            workspace.view.OpenMenu(workspace.view.menus[1]);
            yield return null;
            var slider = workspace.view.volumeControl;
            var rect = slider.GetComponent<RectTransform>();
            Vector2 left = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector2(rect.rect.xMin + rect.rect.width * .2f, rect.rect.center.y)));
            Vector2 right = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector2(rect.rect.xMin + rect.rect.width * .8f, rect.rect.center.y)));
            yield return MoveTo(left);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = left, buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(slider.value, Is.EqualTo(20).Within(1));
            Color low = slider.fillRect.GetComponent<Image>().color;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = right, buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(slider.value, Is.EqualTo(80).Within(1));
            Assert.That(slider.fillRect.anchorMax.x, Is.EqualTo(.8f).Within(.01f));
            Assert.That(low, Is.EqualTo(new Color(.6f, .64f, .7f)));
            Assert.That(slider.fillRect.GetComponent<Image>().color, Is.EqualTo(low));
            Assert.That(workspace.view.volume.textComponent.text, Does.EndWith("%"));
            Assert.That(workspace.timeline.PreviewVolume, Is.EqualTo(.8f).Within(.01f));
            yield return MoveTo(left);
            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            string path = Path.Combine(workspace.menu.Store.GetSongFolder(workspace.screen.Session.Snapshot.Id), "workspace.json");
            var saved = JsonUtility.FromJson<WorkspaceEditor.ResourcesSettings>(File.ReadAllText(path));
            Assert.That(saved.volume, Is.EqualTo(80).Within(1));
        }

        [UnityTest]
        public IEnumerator SettingsTitleAndArtistCommitWhenFocusMoves()
        {
            yield return ClickAt(Center(workspace.view.menus[1].trigger.GetComponent<RectTransform>()));
            yield return EnterText(workspace.view.title, "입력한 제목");
            yield return EnterText(workspace.view.artist, "입력한 작곡가");
            Assert.That(workspace.screen.Session.Snapshot.Title, Is.EqualTo("입력한 제목"));
            yield return ClickAt(Center(workspace.view.volume.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session.Snapshot.Artist, Is.EqualTo("입력한 작곡가"));
            Assert.That(workspace.view.menus[1].panel.activeSelf, Is.True);

            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            var saved = workspace.menu.Store.Read(workspace.screen.Session.Snapshot.Id);
            Assert.That(saved.Title, Is.EqualTo("입력한 제목"));
            Assert.That(saved.Artist, Is.EqualTo("입력한 작곡가"));
        }

        [UnityTest]
        public IEnumerator SettingsPathsImportOnEndEditAndKeepSettingsOpen()
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "Jacket input.png");
            var image = new Texture2D(2, 2);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            string audio = Path.Combine(root, "Audio input.wav");
            using (var writer = new BinaryWriter(File.Create(audio)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + 16000);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(8000);
                writer.Write(16000);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(16000);
                writer.Write(new byte[16000]);
            }

            yield return ClickAt(Center(workspace.view.menus[1].trigger.GetComponent<RectTransform>()));
            yield return EnterText(workspace.view.jacketPath, path);
            yield return ClickAt(Center(workspace.view.artist.GetComponent<RectTransform>()));
            yield return Idle();
            Assert.That(workspace.view.jacket.enabled, Is.True);
            Assert.That(workspace.view.menus[1].panel.activeSelf, Is.True);
            yield return EnterText(workspace.view.audioPath, audio);
            yield return ClickAt(Center(workspace.view.artist.GetComponent<RectTransform>()));
            yield return Idle();
            Assert.That(workspace.screen.Session.Snapshot.Audio, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(workspace.menu.Store.ResolveAudioPath(
                workspace.screen.Session.Snapshot.Id, workspace.screen.Session.Snapshot.Audio)), Is.True);
            Assert.That(workspace.view.menus[1].panel.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator NewFilesStartWithEasySelected()
        {
            var chart = workspace.screen.Session.Snapshot.Charts.Single();
            Assert.That(chart.Slot, Is.EqualTo(ChartSlot.Easy));
            Assert.That(workspace.screen.ChartId, Is.EqualTo(chart.Id));
            Assert.That(workspace.view.difficulty[0].colors.normalColor.grayscale,
                Is.LessThan(workspace.view.difficulty[1].colors.normalColor.grayscale));

            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            workspace.Run(WorkspaceAction.DiscardNew);
            yield return Idle();
            chart = workspace.screen.Session.Snapshot.Charts.Single();
            Assert.That(chart.Slot, Is.EqualTo(ChartSlot.Easy));
            Assert.That(workspace.screen.ChartId, Is.EqualTo(chart.Id));
            Assert.That(workspace.view.difficulty[0].colors.normalColor.grayscale,
                Is.LessThan(workspace.view.difficulty[2].colors.normalColor.grayscale));
        }

        [UnityTest]
        public IEnumerator OnAirRecordsConfiguredLaneKeysAndReturnsToStart()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();

            var mode = workspace.screen.Mode;
            string savedKeys = PlayerPrefs.GetString(PlayKeyBindings.ModeStorageKey, null);
            bool hadSavedKeys = PlayerPrefs.HasKey(PlayKeyBindings.ModeStorageKey);
            try
            {
                PlayKeyBindings.Reset(mode.name);
                Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, Key.S, out _), Is.True);
                Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 1, Key.D, out _), Is.True);
                Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 3, Key.L, out _), Is.True);
                Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 2, Key.K, out _), Is.True);

                var view = workspace.view;
                var chart = workspace.screen.ChartId;
                var session = workspace.screen.Session;
                var toolbarButton = view.toolActions.Single(item => item.action == WorkspaceAction.OnAir).button;
                var menuButton = view.actions.Single(item => item.action == WorkspaceAction.OnAir).button;
                var scrollButton = view.actions.Single(item => item.action == WorkspaceAction.Scroll).button;
                var yes = view.actions.Single(item => item.action == WorkspaceAction.ConfirmOnAir).button;
                var no = view.actions.Single(item => item.action == WorkspaceAction.CancelOnAir).button;
                Assert.That(menuButton.transform.GetSiblingIndex(), Is.EqualTo(scrollButton.transform.GetSiblingIndex() + 1));
                Assert.That(view.toolActions.All(item => ((RectTransform)item.button.transform).sizeDelta == new Vector2(38, 38)), Is.True);
                workspace.timeline.Seek(2);

                yield return PressKeys(Key.F8);
                Assert.That(view.onAirPopup.activeSelf, Is.True);
                yield return ClickAt(Center((RectTransform)no.transform));
                Assert.That(view.onAirPopup.activeSelf, Is.False);
                Assert.That(workspace.timeline.IsPlaying, Is.False);

                yield return ClickAt(Center((RectTransform)toolbarButton.transform));
                yield return ClickAt(Center((RectTransform)yes.transform));
                Assert.That(view.onAirCountdown.text, Is.EqualTo("3"));
                yield return PressKeys(Key.S);
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(2).Within(.001));
                Assert.That(session.GetNotes(chart), Is.Empty);
                yield return ClickAt(Center((RectTransform)toolbarButton.transform));
                Assert.That(view.onAirCountdown.gameObject.activeSelf, Is.False);

                view.OpenMenu(view.menus[3]);
                yield return ClickAt(Center((RectTransform)menuButton.transform));
                yield return ClickAt(Center((RectTransform)yes.transform));
                Assert.That(view.onAirCountdown.gameObject.activeSelf, Is.True);
                yield return new WaitForSecondsRealtime(3.1f);
                Assert.That(workspace.timeline.IsPlaying, Is.True);
                Assert.That(view.onAirCountdown.gameObject.activeSelf, Is.False);

                double before = workspace.timeline.MusicTime;
                yield return PressKeys(Key.S, Key.K);
                var notes = session.GetNotes(chart);
                Assert.That(notes.Select(note => note.Data.Lane), Is.EquivalentTo(new[] { 0, 2 }));
                Assert.That(notes.All(note => note.Data.Kind == NoteKind.Tap), Is.True);
                Assert.That(notes[0].Data.StartTime, Is.InRange(before, workspace.timeline.MusicTime));
                Assert.That(notes[1].Data.StartTime, Is.EqualTo(notes[0].Data.StartTime));

                double beforeHold = workspace.timeline.MusicTime;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return null;
                yield return null;
                double afterHoldPress = workspace.timeline.MusicTime;
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(session.GetNotes(chart).Length, Is.EqualTo(2));

                double beforeRelease = workspace.timeline.MusicTime;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                var hold = session.GetNotes(chart).Single(note => note.Data.Lane == 1).Data;
                Assert.That(hold.Kind, Is.EqualTo(NoteKind.Hold));
                Assert.That(hold.StartTime, Is.InRange(beforeHold, afterHoldPress));
                Assert.That(hold.EndTime, Is.InRange(beforeRelease, workspace.timeline.MusicTime));

                yield return PressKeys(Key.LeftCtrl, Key.Z);
                Assert.That(session.GetNotes(chart).Length, Is.EqualTo(3));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.L));
                yield return new WaitForSecondsRealtime(.2f);
                yield return PressKeys(Key.L, Key.Escape);
                Assert.That(workspace.timeline.IsPlaying, Is.False);
                Assert.That(workspace.timeline.MusicTime, Is.EqualTo(2).Within(.001));
                yield return PressKeys(Key.S);
                Assert.That(session.GetNotes(chart).Length, Is.EqualTo(3));
                yield return PressKeys(Key.LeftCtrl, Key.Z);
                Assert.That(session.GetNotes(chart).Length, Is.EqualTo(2));
                yield return PressKeys(Key.LeftCtrl, Key.Z);
                Assert.That(session.GetNotes(chart), Is.Empty);
            }
            finally
            {
                if (hadSavedKeys)
                {
                    PlayerPrefs.SetString(PlayKeyBindings.ModeStorageKey, savedKeys);
                }
                else
                {
                    PlayerPrefs.DeleteKey(PlayKeyBindings.ModeStorageKey);
                }
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator OriginalEditorKeepsItsStandardDefault()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = UnityEngine.Object.FindFirstObjectByType<ChartEditorScreen>();
            Assert.That(screen.Session.Snapshot.Charts.Single().Slot, Is.EqualTo(ChartSlot.Easy));
            Assert.That(WorkspaceEditor.ScenePath, Is.EqualTo(ChartEditorMenu.ScenePath));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<WorkspaceEditor>(), Is.Not.Null);
            var view = UnityEngine.Object.FindFirstObjectByType<WorkspaceView>();
            Assert.That(view.toolActions.Length, Is.EqualTo(6));
            Assert.That(view.quantizationCycleValue.text, Is.EqualTo("1/4"));
        }

        [UnityTest]
        public IEnumerator DifficultyButtonsLiveInSettingsAndSelectAllSlots()
        {
            var settings = workspace.view.menus[1];
            Assert.That(workspace.view.soundPath, Is.Null);
            Assert.That(settings.panel.GetComponentsInChildren<Text>(true).Any(text => text.text == "메타 정보"), Is.False);
            yield return ClickAt(Center(settings.trigger.GetComponent<RectTransform>()));
            for (int i = 0; i < workspace.view.difficulty.Length; i++)
            {
                var button = workspace.view.difficulty[i];
                Assert.That(button.transform.IsChildOf(settings.panel.transform), Is.True);
                yield return ClickAt(Center(button.GetComponent<RectTransform>()));
                var chart = workspace.screen.Session.Snapshot.Charts.Single(item => item.Id == workspace.screen.ChartId);
                Assert.That(chart.Slot, Is.EqualTo((ChartSlot)i));
                Assert.That(settings.panel.activeSelf, Is.True);
                foreach (var other in workspace.view.difficulty)
                {
                    if (other != button)
                    {
                        Assert.That(button.colors.normalColor.grayscale, Is.LessThan(other.colors.normalColor.grayscale));
                        Assert.That(button.colors.highlightedColor.grayscale, Is.LessThan(other.colors.normalColor.grayscale));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator MenusOpenOnClickSwitchOnHoverAndCloseAfterAnAction()
        {
            var menu = workspace.view.menus[0];
            yield return MoveTo(Center(menu.trigger.GetComponent<RectTransform>()));
            Assert.That(menu.panel.activeSelf, Is.False, "Hover alone must not open menus.");
            yield return ClickAt(Center(menu.trigger.GetComponent<RectTransform>()));
            Assert.That(menu.panel.activeSelf, Is.True);

            var edit = workspace.view.menus[2];
            yield return MoveTo(Center(edit.trigger.GetComponent<RectTransform>()));
            Assert.That(menu.panel.activeSelf, Is.False);
            Assert.That(edit.panel.activeSelf, Is.True);
            var action = workspace.view.actions.Single(binding => binding.action == WorkspaceAction.SelectAll).button;
            yield return ClickAt(Center(action.GetComponent<RectTransform>()));
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            Assert.That(workspace.view.menuDismissArea.activeSelf, Is.False);

            yield return ClickAt(Center(menu.trigger.GetComponent<RectTransform>()));
            Assert.That(menu.panel.activeSelf, Is.True);
            yield return ClickAt(Center(menu.trigger.GetComponent<RectTransform>()));
            Assert.That(workspace.view.HasOpenMenu, Is.False);
        }

        [UnityTest]
        public IEnumerator OutsideClickAndEscapeDismissMenusWithoutEditingTheChart()
        {
            var menu = workspace.view.menus[0];
            yield return ClickAt(Center(menu.trigger.GetComponent<RectTransform>()));
            var rect = workspace.grid.rectTransform;
            var point = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(new Vector3(rect.rect.xMax - 12, rect.rect.center.y, 0)));
            yield return MoveTo(point);
            Assert.That(menu.panel.activeSelf, Is.True, "Leaving a menu must not close it.");
            yield return ClickAt(point);
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);

            yield return ClickAt(Center(menu.trigger.GetComponent<RectTransform>()));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            Assert.That(workspace.view.menuDismissArea.activeSelf, Is.False);
            yield return MoveTo(Center(workspace.view.menus[1].trigger.GetComponent<RectTransform>()));
            Assert.That(workspace.view.HasOpenMenu, Is.False);
        }

        [UnityTest]
        public IEnumerator NewWithUnsavedChangesWaitsForNoAndDoesNotSave()
        {
            var original = workspace.screen.Session;
            original.Edit(edit => edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.view.actions.Single(binding => binding.action == WorkspaceAction.New).button.onClick.Invoke();
            yield return Idle();

            Assert.That(workspace.view.newPopup.activeSelf, Is.True);
            Assert.That(workspace.screen.Session, Is.SameAs(original));
            Assert.That(workspace.menu.Store.ListSongIds(), Is.Empty);
            Assert.That(workspace.screen.IsBusy, Is.True);
            workspace.Run(WorkspaceAction.Delete);
            Assert.That(original.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(1));

            workspace.view.actions.Single(binding => binding.action == WorkspaceAction.DiscardNew).button.onClick.Invoke();
            yield return Idle();
            Assert.That(workspace.screen.Session.Snapshot.Id, Is.Not.EqualTo(original.Snapshot.Id));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId), Is.Empty);
            Assert.That(workspace.menu.Store.ListSongIds(), Is.Empty);
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator NewWithYesSavesCurrentNotesAndResourcesBeforeReplacing()
        {
            workspace.screen.Session.Edit(edit => edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.view.volume.text = "35";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            Guid original = workspace.screen.Session.Snapshot.Id;
            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            workspace.view.actions.Single(binding => binding.action == WorkspaceAction.SaveNew).button.onClick.Invoke();
            yield return Idle();

            Assert.That(workspace.menu.Store.Read(original).Charts.Single().Notes.Length, Is.EqualTo(1));
            string path = Path.Combine(workspace.menu.Store.GetSongFolder(original), "workspace.json");
            var resources = JsonUtility.FromJson<WorkspaceEditor.ResourcesSettings>(File.ReadAllText(path));
            Assert.That(resources.volume, Is.EqualTo(35));
            Assert.That(workspace.screen.Session.Snapshot.Id, Is.Not.EqualTo(original));
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator NewAfterSavingDoesNotAskUntilResourcesChange()
        {
            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            Guid original = workspace.screen.Session.Snapshot.Id;
            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session.Snapshot.Id, Is.Not.EqualTo(original));

            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            workspace.Run(WorkspaceAction.ImportJacket);
            yield return Idle();
            Assert.That(workspace.screen.Session.IsDirty, Is.False);
            Assert.That(workspace.HasUnsavedChanges, Is.True);
            workspace.Run(WorkspaceAction.New);
            yield return Idle();
            Assert.That(workspace.view.newPopup.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator NewSaveFailureKeepsCurrentWorkAndAllowsNo()
        {
            var original = workspace.screen.Session;
            original.Edit(edit => edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            Directory.CreateDirectory(Path.GetDirectoryName(root));
            File.WriteAllText(root, "Block the song folder to simulate a save failure.");
            try
            {
                workspace.Run(WorkspaceAction.New);
                yield return Idle();
                workspace.Run(WorkspaceAction.SaveNew);
                yield return Idle();
                Assert.That(workspace.screen.Session, Is.SameAs(original));
                Assert.That(workspace.view.newPopup.activeSelf, Is.True);
                Assert.That(workspace.screen.IsBusy, Is.True);
                Assert.That(workspace.HasUnsavedChanges, Is.True);
            }
            finally
            {
                File.Delete(root);
            }

            workspace.Run(WorkspaceAction.DiscardNew);
            yield return Idle();
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session, Is.Not.SameAs(original));
        }

        [UnityTest]
        [Category("BpmSuggestion")]
        public IEnumerator SaveAsCreatesIndependentSongAndKeepsOriginal()
        {
            workspace.view.audioPath.text = WriteTempoWave();
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.True);

            workspace.view.title.text = "Original";
            workspace.view.artist.text = "Team";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            workspace.screen.Session.Edit(edit => edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1)));
            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            Guid original = workspace.screen.Session.Snapshot.Id;
            workspace.Run(WorkspaceAction.SaveAs);
            yield return Idle();
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.True);
            Assert.That(workspace.screen.Session.Snapshot.Id, Is.EqualTo(original));
            Assert.That(workspace.view.saveAsName.text, Is.EqualTo("Original"));
            workspace.view.saveAsName.text = "Copy";
            var button = workspace.view.actions.Single(binding => binding.action == WorkspaceAction.ConfirmSaveAs).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            yield return Idle();
            Assert.That(workspace.screen.Session.Snapshot.Id, Is.Not.EqualTo(original));
            Assert.That(workspace.view.bpmSuggestion.text, Is.Empty);
            Assert.That(workspace.view.applyEstimatedBpm.gameObject.activeSelf, Is.False);
            Assert.That(workspace.menu.Store.Read(original).Title, Is.EqualTo("Original"));
            Assert.That(workspace.screen.Session.Snapshot.Title, Is.EqualTo("Copy"));
            Assert.That(workspace.screen.Session.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(1));
            Assert.That(workspace.HasUnsavedChanges, Is.False);
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator SaveAsRequiresANameAndCancelKeepsCurrentWork()
        {
            var original = workspace.screen.Session;
            workspace.Run(WorkspaceAction.SaveAs);
            yield return Idle();
            workspace.view.saveAsName.text = "   ";
            workspace.Run(WorkspaceAction.ConfirmSaveAs);
            yield return Idle();
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.True);
            Assert.That(workspace.view.saveAsError.text, Is.EqualTo("이름을 입력하세요."));
            Assert.That(workspace.menu.Store.ListSongIds(), Is.Empty);
            workspace.Run(WorkspaceAction.New);
            Assert.That(workspace.view.newPopup.activeSelf, Is.False);
            workspace.view.menus[0].trigger.onClick.Invoke();
            Assert.That(workspace.view.HasOpenMenu, Is.False);
            var button = workspace.view.actions.Single(binding => binding.action == WorkspaceAction.CancelSaveAs).button;
            yield return ClickAt(Center(button.GetComponent<RectTransform>()));
            Assert.That(workspace.screen.Session, Is.SameAs(original));
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.IsBusy, Is.False);

            workspace.Run(WorkspaceAction.SaveAs);
            yield return Idle();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
            Assert.That(workspace.screen.Session, Is.SameAs(original));
        }

        [UnityTest]
        public IEnumerator SaveAsFailureKeepsCurrentSessionAndNameForRetry()
        {
            var original = workspace.screen.Session;
            Directory.CreateDirectory(Path.GetDirectoryName(root));
            File.WriteAllText(root, "Block saving.");
            try
            {
                workspace.Run(WorkspaceAction.SaveAs);
                yield return Idle();
                workspace.view.saveAsName.text = "Retry Copy";
                workspace.Run(WorkspaceAction.ConfirmSaveAs);
                yield return Idle();
                Assert.That(workspace.screen.Session, Is.SameAs(original));
                Assert.That(workspace.view.saveAsPopup.activeSelf, Is.True);
                Assert.That(workspace.view.saveAsName.text, Is.EqualTo("Retry Copy"));
                Assert.That(workspace.view.saveAsError.text, Is.Not.Empty);
            }
            finally
            {
                File.Delete(root);
            }

            workspace.Run(WorkspaceAction.ConfirmSaveAs);
            yield return Idle();
            Assert.That(workspace.screen.Session.Snapshot.Title, Is.EqualTo("Retry Copy"));
            Assert.That(workspace.view.saveAsPopup.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator MenusPreviewOnHoverPinOnClickAndExitCanBeCancelled()
        {
            var menu = workspace.view.menus[0];
            var data = new PointerEventData(EventSystem.current);
            menu.OnPointerEnter(data);
            Assert.That(menu.panel.activeSelf, Is.False);
            menu.trigger.onClick.Invoke();
            menu.OnPointerExit(data);
            Assert.That(menu.panel.activeSelf, Is.True);
            workspace.view.menus[1].OnPointerEnter(data);
            Assert.That(menu.panel.activeSelf, Is.False);
            workspace.Run(WorkspaceAction.Exit);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.True);
            workspace.Run(WorkspaceAction.CancelExit);
            Assert.That(workspace.view.exitPopup.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(WorkspaceEditor.ScenePath));
            yield return null;
        }

        [UnityTest]
        public IEnumerator KindSelectionMirrorAndQuantizationUseTheExistingSession()
        {
            workspace.screen.Session.Edit(edit =>
            {
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, 1));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(1, 2, 3));
            });
            workspace.Run(WorkspaceAction.SelectHolds);
            workspace.Run(WorkspaceAction.Mirror);
            var notes = workspace.screen.Session.GetNotes(workspace.screen.ChartId);
            Assert.That(notes[0].Data.Lane, Is.Zero);
            Assert.That(notes[1].Data.Lane, Is.EqualTo(2));
            workspace.view.quantization.value = 9;
            Assert.That(workspace.screen.SnapBeats, Is.EqualTo(4.0 / 192));
            workspace.Run(WorkspaceAction.Slow);
            Assert.That(workspace.timeline.PreviewSpeed, Is.EqualTo(.5f));
            workspace.Run(WorkspaceAction.Slow);
            Assert.That(workspace.timeline.PreviewSpeed, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator JacketAndVolumeSurviveSavingAndReloadingWithoutOptionalSound()
        {
            workspace.view.OpenMenu(workspace.view.menus[1]);
            Canvas.ForceUpdateCanvases();
            var notice = workspace.view.menus[1].panel.GetComponentsInChildren<Text>()
                .Single(label => label.text == "임시, 자켓 정보만 받고 표시하는 엔드포인트가 없음.");
            Assert.That(notice.preferredWidth, Is.LessThanOrEqualTo(notice.rectTransform.rect.width));
            Assert.That(notice.preferredHeight, Is.LessThanOrEqualTo(notice.rectTransform.rect.height));
            workspace.view.CloseMenus();

            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "Jacket.png");
            var image = new Texture2D(2, 2);
            image.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red });
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            workspace.view.jacketPath.text = path;
            workspace.Run(WorkspaceAction.ImportJacket);
            yield return Idle();
            Assert.That(workspace.view.status.text,
                Is.EqualTo("자켓 정보를 반영했습니다. 이미지 표시는 아직 지원하지 않습니다."));
            workspace.view.volume.text = "35";
            workspace.Run(WorkspaceAction.ApplySettings);
            yield return Idle();
            workspace.Run(WorkspaceAction.Save);
            yield return Idle();
            Guid id = workspace.screen.Session.Snapshot.Id;
            workspace.menu.ReplaceSession(new EditorSession(workspace.menu.Store.Read(id), true));
            yield return null;
            yield return null;
            Assert.That(workspace.view.jacket.enabled, Is.True);
            Assert.That(workspace.view.jacket.texture.width, Is.EqualTo(2));
            Assert.That(workspace.timeline.PreviewVolume, Is.EqualTo(.35f).Within(.001f));
            Assert.That(workspace.view.status.text, Does.Not.StartWith("리소스:"));
            Assert.That(workspace.HasUnsavedChanges, Is.False);
        }

        [UnityTest]
        public IEnumerator CurrentPositionAutoTestUsesTemporaryNotesAndReturnsToExtendedEditor()
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "Preview.wav");
            const int rate = 8000;
            const int frames = rate * 4;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + frames * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(frames * 2);
                writer.Write(new byte[frames * 2]);
            }

            var store = workspace.menu.Store;
            var mode = workspace.screen.Mode;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            workspace.menu.Initialize(store, flow, project => new DocumentSongLoader(
                id => mode, (audio, token) => ChartEditorCompositionRoot.LoadAudio(
                    store.ResolveAudioPath(project.Id, audio), token)));
            workspace.view.audioPath.text = path;
            workspace.Run(WorkspaceAction.ImportAudio);
            yield return Idle();
            yield return new WaitUntil(() => workspace.timeline.PreviewClip && !workspace.timeline.IsBusy);
            workspace.screen.Session.Edit(edit =>
            {
                edit.SetMetadata("Preview", "Test");
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(0, .25));
                edit.AddNote(workspace.screen.ChartId, NoteData.Tap(1, 1.25));
                edit.AddNote(workspace.screen.ChartId, NoteData.Hold(2, 1.6, 2.1));
            });
            var original = workspace.screen.Session;
            workspace.timeline.Seek(1);
            workspace.Run(WorkspaceAction.AutoTestHere);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().path.EndsWith("/Gameplay.unity"));
            yield return null;
            var controller = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return new WaitUntil(() => controller.Clock != null && controller.Clock.Running);
            Assert.That(controller.Chart.Count, Is.EqualTo(2));
            Assert.That(controller.Clock.SongTime, Is.GreaterThan(.5));
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(controller.Session.Snapshot.Miss, Is.Zero);
            Assert.That(controller.Session.Snapshot.Combo, Is.GreaterThanOrEqualTo(2));
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().path == WorkspaceEditor.ScenePath);
            yield return null;
            workspace = UnityEngine.Object.FindFirstObjectByType<WorkspaceEditor>();
            yield return new WaitUntil(() => workspace.menu.IsInitialized);
            Assert.That(workspace.screen.Session, Is.SameAs(original));
            Assert.That(original.GetNotes(workspace.screen.ChartId).Length, Is.EqualTo(3));
            Assert.That(original.CanUndo, Is.True);
        }
    }
}
