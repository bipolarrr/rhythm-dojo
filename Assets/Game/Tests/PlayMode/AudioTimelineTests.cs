using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RhythmDojo.Authoring;
using RhythmDojo.Application;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    [PrebuildSetup(typeof(TestRunStartup))]
    [PostBuildCleanup(typeof(TestRunStartup))]
    public sealed class AudioTimelineTests
    {
        [UnityTest]
        public IEnumerator ImportedWaveformSeekingZoomPlaybackAndGridEditsShareMusicTime()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            var menu = Object.FindFirstObjectByType<ChartEditorMenu>();
            var timeline = screen.GetComponent<ChartAudioTimeline>();
            string root = Path.Combine(UnityEngine.Application.temporaryCachePath,
                "AudioTimelineTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                string path = Path.Combine(root, "beats.wav");
                WriteWave(path);
                var store = new FileSongProjectStore(Path.Combine(root, "Songs"));
                menu.Initialize(store, null, null);
                var import = store.ImportAsync(screen.Session.Snapshot.Id, path, CancellationToken.None);
                while (!import.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(import.IsFaulted, Is.False);
                screen.Session.Edit(edit =>
                {
                    edit.SetAudio(import.Result);
                    edit.SetTiming(0.1, new TempoPoint(0, 120));
                });
                float deadline = Time.realtimeSinceStartup + 15;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (timeline.Peaks != null && timeline.Peaks.Ready)
                    {
                        break;
                    }

                    yield return null;
                }

                Assert.That(timeline.Peaks?.Ready, Is.True, "Imported audio should decode and generate peaks.");
                Assert.That(timeline.Duration, Is.EqualTo(4).Within(0.001));
                Assert.That(timeline.Duration, Is.EqualTo((44100.0 * 4 + 17) / 44100).Within(1e-12));
                timeline.Seek(0.75);
                Assert.That(timeline.MusicTime, Is.EqualTo(0.75).Within(0.001));
                Assert.That(screen.ScrollBeatOffset,
                    Is.EqualTo(timeline.Timing.BeatAtTime(0.85)).Within(1e-8));
                double fraction = (0.75 - timeline.WindowStart) / timeline.WindowDuration;
                timeline.Zoom(1, 0.75);
                Assert.That((0.75 - timeline.WindowStart) / timeline.WindowDuration,
                    Is.EqualTo(fraction).Within(1e-8));
                var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var play = buttons.Single(b => b.name == "Play Audio");
                play.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.18f);
                Assert.That(timeline.MusicTime, Is.GreaterThan(0.8));
                play.onClick.Invoke();
                double paused = timeline.MusicTime;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(timeline.MusicTime, Is.EqualTo(paused));

                screen.Session.Edit(edit => edit.AddNote(screen.ChartId, NoteData.Hold(0, 1, 2)));
                var before = screen.Session.GetNotes(screen.ChartId);
                for (int i = 0; i < 3; i++)
                {
                    timeline.Zoom(1, 0.75);
                }

                timeline.Pan(0.42);
                var detail = screen.GetComponentInParent<Canvas>()
                    .GetComponentsInChildren<AudioTimelineGraphic>().Single(wave => !wave.Overview);
                var originalSize = detail.rectTransform.sizeDelta;
                detail.rectTransform.sizeDelta = originalSize + new Vector2(-137.25f, 0);
                detail.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                Assert.That(timeline.MusicTime, Is.EqualTo(paused));
                Assert.That(screen.Session.GetNotes(screen.ChartId), Is.EqualTo(before));
                detail.rectTransform.sizeDelta = originalSize;
                Assert.That(detail.FirstVisibleGridTime, Is.EqualTo(0.525).Within(1e-8));
                Assert.That(buttons.Single(b => b.name == "Cursor Origin")
                    .GetComponentInChildren<Text>().text, Is.EqualTo("그리드 수정"));

                // Select between existing grid lines through the actual UI input module.
                double selectedTime = 0.737;
                Rect area = detail.rectTransform.rect;
                float x = area.xMin + (float)((selectedTime - timeline.WindowStart) / timeline.WindowDuration)
                    * area.width;
                yield return MouseClick(mouse, detail.rectTransform, new Vector2(x, area.center.y));
                Assert.That(timeline.MusicTime, Is.EqualTo(selectedTime).Within(1e-5));
                paused = timeline.MusicTime;
                timeline.Zoom(1, paused);
                timeline.Pan(0.42);
                screen.GetComponent<ChartAudioTimelineView>().displayDensityControl.value = 5;
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                yield return MouseClick(mouse, (RectTransform)buttons.Single(b => b.name == "Cursor Origin").transform);
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(0).Within(1e-8));
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                yield return MouseClick(mouse, (RectTransform)buttons.Single(b => b.name == "Cursor Origin").transform);
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(paused + timeline.AudioOffset).Within(1e-8));
                Assert.That(timeline.MusicTime, Is.EqualTo(paused));
                Assert.That(screen.Session.GetNotes(screen.ChartId), Is.EqualTo(before));
                yield return null;
                Capture(screen, "Logs/grid-cursor-origin.png");
                screen.Session.Undo();
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(0).Within(1e-8));
                screen.Session.Redo();
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(paused + timeline.AudioOffset).Within(1e-8));
                screen.Session.Undo();

                Click(play);
                yield return new WaitForSecondsRealtime(0.18f);
                double beforeClick = timeline.MusicTime;
                Click(buttons.Single(b => b.name == "Cursor Origin"));
                double afterClick = timeline.MusicTime;
                Assert.That(timeline.Timing.TimeAtBeat(0) - timeline.AudioOffset,
                    Is.InRange(beforeClick, afterClick));
                Assert.That(beforeClick, Is.GreaterThan(paused));
                Click(play);
                screen.Session.Undo();
                screen.ResetView();
                timeline.Seek(0.75);
                yield return null;
                Capture(screen, "Logs/grid-timeline.png");
                Assert.That(detail.ShowsSamples, Is.False);
                Assert.That(buttons.Any(b => b.name == "Grid Undo"), Is.False);
                Assert.That(buttons.Any(b => b.name == "Grid Redo"), Is.False);
                Assert.That(buttons.Any(b => b.name == "Advanced Grid"), Is.True);
                var canvas = screen.GetComponentInParent<Canvas>();
                var snap = canvas.GetComponentsInChildren<Dropdown>(true)
                    .Single(d => d.name == "Note Snap");
                var lockButton = buttons.Single(b => b.name == "Grid Lock");
                Assert.That(((RectTransform)lockButton.transform).anchoredPosition.x,
                    Is.LessThan(((RectTransform)snap.transform).anchoredPosition.x));
                var advancedPanel = canvas.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Advanced Grid Panel");
                Assert.That(advancedPanel.gameObject.activeSelf, Is.False);
                var advancedButton = buttons.Single(b => b.name == "Advanced Grid");
                Click(advancedButton);
                Assert.That(advancedPanel.gameObject.activeInHierarchy, Is.True);
                menu.ToggleFile();
                Assert.That(advancedPanel.gameObject.activeSelf, Is.False);
                Click(advancedButton);
                Assert.That(canvas.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "File Panel").gameObject.activeSelf, Is.False);
                yield return null;
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                buttons.Single(b => b.name == "Add Tempo Segment").onClick.Invoke();
                Assert.That(screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(1));
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                screen.IsBusy = true;
                buttons.Single(b => b.name == "Add Tempo Segment").onClick.Invoke();
                Assert.That(screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(1));
                screen.IsBusy = false;
                buttons.Single(b => b.name == "Add Tempo Segment").onClick.Invoke();
                Assert.That(screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(2));
                var fields = canvas.GetComponentsInChildren<InputField>(true);
                fields.Single(f => f.name == "Meter Numerator").text = "3";
                fields.Single(f => f.name == "Meter Denominator").text = "8";
                buttons.Single(b => b.name == "Apply Tempo Segment").onClick.Invoke();
                var settings = screen.Session.Snapshot.Grid;
                Assert.That(settings.Meters.Length, Is.EqualTo(2));
                Assert.That(settings.Meters[1].Numerator, Is.EqualTo(3));
                Assert.That(settings.Meters[1].Denominator, Is.EqualTo(8));
                double segmentBeat = timeline.Timing.BeatAtTime(timeline.MusicTime + timeline.AudioOffset);
                Assert.That(timeline.Timing.IsMeasure(segmentBeat), Is.True);
                Assert.That(timeline.Timing.SnapBeat(segmentBeat + 0.09, 0.25, 0, 16),
                    Is.EqualTo(segmentBeat).Within(1e-8));
                Assert.That(screen.Session.GetNotes(screen.ChartId), Is.EqualTo(before));
                yield return null;
                Capture(screen, "Logs/grid-timeline-advanced.png");
                var edited = screen.Session.Snapshot;
                Click(buttons.Single(b => b.name == "도구"));
                Click(buttons.Single(b => b.name == "Undo"));
                Assert.That(screen.Session.Snapshot.Grid.Meters.Length, Is.EqualTo(1));
                Click(buttons.Single(b => b.name == "Redo"));
                Click(advancedButton);
                Assert.That(ProjectValidation.ContentEquals(edited, screen.Session.Snapshot), Is.True);
                Capture(screen, "Logs/grid-timeline-small.png", 960, 540);
                var dropdown = canvas.GetComponentsInChildren<Dropdown>(true)
                    .Single(d => d.name == "Tempo Segments");
                Assert.That(dropdown.template.GetComponent<Image>().color.grayscale,
                    Is.LessThan(dropdown.itemText.color.grayscale - 0.3f), "Popup text must remain readable.");
                dropdown.Show();
                yield return new WaitForSecondsRealtime(0.2f);
                Capture(screen, "Logs/grid-timeline-dropdown.png");
                Capture(screen, "Logs/grid-timeline-dropdown-small.png", 960, 540);
                dropdown.Hide();
                var view = screen.GetComponent<ChartAudioTimelineView>();
                view.firstBeat.text = "0";
                view.secondBeat.text = "0.5";
                view.beatCount.text = "2";
                Click(view.calculateBPMControl);
                Assert.That(view.segmentBpm.text, Is.EqualTo("240"));
                view.existingNotePolicyControl.value = 1;
                var beforePolicy = screen.Session.Snapshot;
                var beforeTiming = timeline.Timing;
                Click(view.applyTempoSegmentControl);
                var afterPolicy = screen.Session.Snapshot;
                Assert.That(afterPolicy.TempoPoints[1].Bpm, Is.EqualTo(240));
                var changed = screen.Session.GetNotes(screen.ChartId)[0].Data;
                Assert.That(timeline.Timing.BeatAtTime(changed.StartTime),
                    Is.EqualTo(beforeTiming.BeatAtTime(before[0].Data.StartTime)).Within(1e-8));
                Assert.That(timeline.Timing.BeatAtTime(changed.EndTime),
                    Is.EqualTo(beforeTiming.BeatAtTime(before[0].Data.EndTime)).Within(1e-8));
                menu.Undo();
                Assert.That(ProjectValidation.ContentEquals(beforePolicy, screen.Session.Snapshot), Is.True);
                menu.Redo();
                Assert.That(ProjectValidation.ContentEquals(afterPolicy, screen.Session.Snapshot), Is.True);
                Click(view.deleteTempoSegmentControl);
                Assert.That(screen.Session.Snapshot.TempoPoints.Length, Is.EqualTo(1));
                Assert.That(screen.Session.Snapshot.Grid.Meters.Length, Is.EqualTo(1));
                menu.Undo();
                view.displayDensityControl.value = 5;
                Assert.That(timeline.DisplayStep, Is.EqualTo(1.0 / 6));
                view.noteSnapControl.value = 3;
                Assert.That(screen.SnapBeats, Is.EqualTo(0.125));
                view.noteSnapControl.value = 6;
                Assert.That(screen.SnapEnabled, Is.False);
                buttons.Single(b => b.name == "Close Advanced").onClick.Invoke();
                timeline.Resize(140);
                yield return null;
                Assert.That(timeline.GetComponentInParent<Canvas>()
                    .GetComponentsInChildren<AudioTimelineGraphic>().Single(wave => !wave.Overview)
                    .rectTransform.rect.height, Is.EqualTo(212).Within(0.01f));
                Capture(screen, "Logs/grid-timeline-resized.png");
                foreach (double value in new[] { -0.1, 0.3 })
                {
                    screen.Session.Edit(edit => edit.SetGrid(new BeatGridSettings { OriginSeconds = value }));
                    screen.ResetView();
                    Assert.That(timeline.Timing.TimeAtBeat(screen.ScrollBeatOffset),
                        Is.EqualTo(0).Within(1e-8), "The viewport must still start at valid chart time zero.");
                }

                for (int i = 0; i < 32; i++)
                {
                    timeline.Zoom(1, 0);
                }

                timeline.Seek(0.005);
                yield return null;
                Capture(screen, "Logs/waveform-detail.png");
                Assert.That(detail.ShowsSamples, Is.True);
                var beforeSampleZoom = screen.Session.Snapshot;
                for (int i = 0; i < 24; i++)
                {
                    timeline.Zoom(1, 0.005);
                }

                Assert.That(timeline.MusicTime, Is.EqualTo(0.005));
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(detail.ShowsSamples, Is.True);
                Assert.That(ProjectValidation.ContentEquals(beforeSampleZoom, screen.Session.Snapshot), Is.True);
                AssertSampleVertex(detail, timeline);
                Capture(screen, "Logs/waveform-samples.png");

                timeline.Seek(1);
                double anchor = timeline.WindowStart + timeline.WindowDuration * 0.5;
                double fractionAfterZoom = (anchor - timeline.WindowStart) / timeline.WindowDuration;
                for (int i = 0; i < 32; i++)
                {
                    timeline.Zoom(-1, anchor);
                    Assert.That((anchor - timeline.WindowStart) / timeline.WindowDuration,
                        Is.EqualTo(fractionAfterZoom).Within(1e-8));
                }

                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(detail.ShowsSamples, Is.False);
                Assert.That(timeline.MusicTime, Is.EqualTo(1));
                Assert.That(ProjectValidation.ContentEquals(beforeSampleZoom, screen.Session.Snapshot), Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                // Unload and release the clip before the fixture removes its private files.
                screen.Session.Edit(edit => edit.SetAudio(null));
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void AssertSampleVertex(AudioTimelineGraphic detail, ChartAudioTimeline timeline)
        {
            var mesh = Object.Instantiate(detail.canvasRenderer.GetMesh());
            try
            {
                int frame = (int)Math.Floor(timeline.WindowStart * timeline.Peaks.Frequency) + 3;
                // Four background vertices, then four vertices per sample-line cross section.
                // Node zero is the interpolated viewport edge; node three is this real PCM frame.
                Vector3 center = (mesh.vertices[17] + mesh.vertices[18]) * 0.5f;
                Rect area = detail.rectTransform.rect;
                double time = (double)frame / timeline.Peaks.Frequency;
                float x = area.xMin + (float)((time - timeline.WindowStart) / timeline.WindowDuration) * area.width;
                float y = area.center.y + timeline.Peaks.Sample(frame, 0) * area.height * 0.44f;
                Assert.That(center.x, Is.EqualTo(x).Within(0.001));
                Assert.That(center.y, Is.EqualTo(y).Within(0.001));
            }
            finally
            {
                Object.Destroy(mesh);
            }
        }

        private static IEnumerator MouseClick(Mouse mouse, RectTransform rect, Vector2? local = null)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(local ?? rect.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }

        private static void Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.IsInteractable(), Is.True);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
        }

        private static void WriteWave(string path)
        {
            const int frequency = 44100;
            const int frames = frequency * 4 + 17;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(frequency);
            writer.Write(frequency * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);
            for (int i = 0; i < frames; i++)
            {
                double envelope = Math.Exp(-(i % (frequency / 2)) / 1800.0);
                writer.Write((short)(Math.Sin(i * 2 * Math.PI * 220 / frequency) * envelope * 26000));
            }
        }

        private static void Capture(ChartEditorScreen screen, string path, int width = 1280, int height = 720)
        {
            var canvas = screen.GetComponentInParent<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
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
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = previousMode;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }
    }
}
