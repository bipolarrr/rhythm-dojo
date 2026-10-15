using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using RhythmDojo.Authoring;
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
                Assert.That(detail.FirstVisibleGridTime, Is.EqualTo(0.525).Within(1e-8));
                Assert.That(buttons.Single(b => b.name == "Cursor Origin")
                    .GetComponentInChildren<Text>().text, Is.EqualTo("그리드 수정"));
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                buttons.Single(b => b.name == "Cursor Origin").onClick.Invoke();
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(0).Within(1e-8));
                buttons.Single(b => b.name == "Grid Lock").onClick.Invoke();
                buttons.Single(b => b.name == "Cursor Origin").onClick.Invoke();
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(0.625).Within(1e-8));
                Assert.That(timeline.MusicTime, Is.EqualTo(paused));
                Assert.That(detail.FirstVisibleGridTime, Is.EqualTo(0.525).Within(1e-8));
                Assert.That(screen.Session.GetNotes(screen.ChartId), Is.EqualTo(before));
                screen.Session.Undo();
                Assert.That(timeline.Timing.TimeAtBeat(0), Is.EqualTo(0).Within(1e-8));
                screen.ResetView();
                timeline.Seek(0.75);
                yield return null;
                Capture(screen, "Logs/grid-timeline.png");
                Assert.That(buttons.Any(b => b.name == "Grid Undo"), Is.False);
                Assert.That(buttons.Any(b => b.name == "Grid Redo"), Is.False);
                Assert.That(buttons.Any(b => b.name == "Advanced Grid"), Is.False);
                var canvas = screen.GetComponentInParent<Canvas>();
                var snap = canvas.GetComponentsInChildren<Dropdown>(true)
                    .Single(d => d.name == "Note Snap");
                var lockButton = buttons.Single(b => b.name == "Grid Lock");
                Assert.That(((RectTransform)lockButton.transform).anchoredPosition.x,
                    Is.LessThan(((RectTransform)snap.transform).anchoredPosition.x));
                var advancedPanel = canvas.GetComponentsInChildren<RectTransform>(true)
                    .Single(rect => rect.name == "Advanced Grid Panel");
                Assert.That(advancedPanel.gameObject.activeSelf, Is.False);
                advancedPanel.gameObject.SetActive(true);
                yield return null;
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
                var dropdown = canvas.GetComponentsInChildren<Dropdown>(true)
                    .Single(d => d.name == "Tempo Segments");
                Assert.That(dropdown.template.GetComponent<Image>().color.grayscale,
                    Is.LessThan(dropdown.itemText.color.grayscale - 0.3f), "Popup text must remain readable.");
                dropdown.Show();
                yield return new WaitForSecondsRealtime(0.2f);
                Capture(screen, "Logs/grid-timeline-dropdown.png");
                dropdown.Hide();
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

            }
            finally
            {
                // Unload and release the clip before the fixture removes its private files.
                screen.Session.Edit(edit => edit.SetAudio(null));
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void WriteWave(string path)
        {
            const int frequency = 44100;
            const int frames = frequency * 4;
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

        private static void Capture(ChartEditorScreen screen, string path)
        {
            var canvas = screen.GetComponentInParent<Canvas>();
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
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
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
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
