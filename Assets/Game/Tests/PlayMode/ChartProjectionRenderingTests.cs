using System.Collections;
using System.IO;
using NUnit.Framework;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class ChartProjectionRenderingTests
    {
        [UnityTest]
        public IEnumerator RenderAtSmallAndLargeResolutionsAcrossZoomScrollAndTempoBoundary()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            var timeline = screen.GetComponentInChildren<ChartBeatGrid>();
            var tempo = new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(2, 180) });
            screen.Session.Edit(edit =>
            {
                edit.SetTiming(0, tempo[0], tempo[1]);
                edit.AddNote(screen.ChartId, NoteData.Tap(0, tempo.TimeAtBeat(3)));
                edit.AddNote(screen.ChartId, NoteData.Tap(1, tempo.TimeAtBeat(4)));
                edit.AddNote(screen.ChartId, NoteData.Tap(0, tempo.TimeAtBeat(13)));
                edit.AddNote(screen.ChartId, NoteData.Tap(2, tempo.TimeAtBeat(6.375)));
                edit.AddNote(screen.ChartId, NoteData.Hold(3, tempo.TimeAtBeat(1.25), tempo.TimeAtBeat(10.5)));
            });
            var original = screen.Session.GetNotes(screen.ChartId);
            var originalTiming = screen.Session.Snapshot.TempoPoints;
            var canvas = timeline.canvas;
            var camera = Camera.main;
            RenderTexture target = null;
            Directory.CreateDirectory("Logs/chart-projection");
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                foreach (var size in new[] { new Vector2Int(610, 337), new Vector2Int(1280, 720) })
                {
                    target = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = target;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    foreach (double scroll in new[] { 0.0, 0.5 })
                    {
                        screen.ResetView();
                        if (scroll > 0) screen.Scroll(scroll);
                        foreach (int beats in new[] { 16, 12, 32, 36, 64, 68, 128, 132, 16 })
                        {
                            screen.Zoom((screen.VisibleBeats - beats) / 4);
                            Assert.That(screen.VisibleBeats, Is.EqualTo(beats));
                            yield return null;
                            Canvas.ForceUpdateCanvases();
                            var projection = timeline.Projection;
                            Assert.That(screen.ScrollBeatOffset, Is.EqualTo(scroll));
                            Assert.That(projection.Project(scroll), Is.EqualTo(timeline.rectTransform.rect.yMin));
                            if (beats >= 16) Assert.That(timeline.NoteBounds(0, 13, 13).center.y,
                                Is.EqualTo(projection.Project(13)).Within(0.001));
                            Assert.That(timeline.NoteBounds(2, 6.375, 6.375).center.y,
                                Is.EqualTo(projection.Project(6.375)).Within(0.001), "Existing fractional note must not snap.");
                            Assert.That(screen.Session.GetNotes(screen.ChartId), Is.EqualTo(original));
                            Assert.That(screen.Session.Snapshot.TempoPoints, Is.EqualTo(originalTiming));
                            Capture(camera, target, $"Logs/chart-projection/{size.x}x{size.y}-beats{beats}-scroll{scroll:0.0}.png");
                        }
                    }
                    camera.targetTexture = null;
                    target.Release(); Object.Destroy(target); target = null;
                }
            }
            finally
            {
                camera.targetTexture = null;
                if (target) { target.Release(); Object.Destroy(target); }
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
        }

        [UnityTest]
        public IEnumerator DeletionUsesClippedVisibleBoundsWithoutBeatSnappingAndPreviewSurvivesZoom()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            var timeline = screen.GetComponentInChildren<ChartBeatGrid>();
            screen.Session.Edit(edit => {
                edit.AddNote(screen.ChartId, NoteData.Tap(0, 1.123456789));
                edit.AddNote(screen.ChartId, NoteData.Hold(1, -1, 20));
            });
            var tap = timeline.NoteBounds(0, 2.246913578, 2.246913578);
            var point = tap.center;
            long id = screen.Session.GetNotes(screen.ChartId)[1].Id;
            Assert.That(timeline.HitTest(point), Is.EqualTo(id));
            Assert.That(timeline.HitTest(new Vector2(point.x, tap.yMax + 0.1f)), Is.Zero);
            Assert.That(timeline.HitTest(new Vector2(tap.xMin - 0.1f, point.y)), Is.Zero);
            var hold = timeline.NoteBounds(1, -2, 40);
            Assert.That(hold.yMin, Is.EqualTo(timeline.rectTransform.rect.yMin));
            Assert.That(hold.yMax, Is.EqualTo(timeline.rectTransform.rect.yMax));
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right,
                position = RectTransformUtility.WorldToScreenPoint(null, timeline.rectTransform.TransformPoint(point)) };
            screen.OnPointerClick(click);
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(1));
            screen.Session.Undo();
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(2));
            screen.Session.Redo();
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(1));
            var down = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, timeline.rectTransform.TransformPoint(
                    new Vector2(timeline.rectTransform.rect.xMin + timeline.rectTransform.rect.width * 0.625f,
                        (float)timeline.Projection.Project(4)))) };
            screen.OnPointerDown(down);
            screen.Zoom(-1);
            screen.Scroll(0.5);
            yield return null;
            screen.OnDrag(down);
            screen.OnPointerUp(down);
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MaximumOverviewIncludesEveryBeatAndManyNotesWithoutExceedingMeshCapacity()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            var timeline = screen.GetComponentInChildren<ChartBeatGrid>();
            screen.Session.Edit(edit => {
                for (int i = 0; i < 1000; i++) edit.AddNote(screen.ChartId, NoteData.Tap(i % 4, i * 3.5));
            });
            screen.Zoom(-10000);
            Assert.That(screen.VisibleBeats, Is.EqualTo(7200));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(timeline.canvasRenderer.GetMesh().vertexCount, Is.LessThan(65000));
            Assert.That(screen.Session.GetNotes(screen.ChartId).Length, Is.EqualTo(1000));
            // Each beat still contributes coverage; no zoom-dependent stride/measure change.
            Assert.That(timeline.Projection.Project(4) - timeline.Projection.Project(3),
                Is.EqualTo(timeline.Projection.UnitsPerBeat).Within(1e-9));
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
    }
}
