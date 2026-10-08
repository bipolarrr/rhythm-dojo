using System.Collections;
using System.Linq;
using RhythmDojo.Gameplay;
using NUnit.Framework;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    public sealed class ChartEditorScreenTests
    {
        [UnityTest]
        public IEnumerator MouseClicksAddUniqueBeatNotesAndRightClickRemovesThem()
        {
            var inputSettings = InputSystem.settings;
            var previousBehavior = inputSettings.editorInputBehaviorInPlayMode;
            var previousBackground = inputSettings.backgroundBehavior;
            Mouse mouse = null;
            Keyboard keyboard = null;
            try
            {
                inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                mouse = InputSystem.AddDevice<Mouse>();
                mouse.MakeCurrent();
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/ChartEditor.unity");
                yield return null;
                var editor = Object.FindFirstObjectByType<ChartEditorScreen>();
                Assert.That(editor, Is.Not.Null);
                Assert.That(Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Length, Is.EqualTo(4));
                var grid = (RectTransform)editor.transform;
                var notes = new TimelineNotes(grid.GetComponent<ChartEditorScreen>());
                Assert.That(grid.GetComponentInChildren<ChartBeatGrid>(), Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width * 0.125f, -grid.rect.height * 0.5f)));
                yield return Click(mouse, point, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(1));
                Assert.That(notes.Bounds(0).height, Is.EqualTo(7f).Within(0.01f));
                yield return Click(mouse, point, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(1));
                yield return Click(mouse, point, MouseButton.Right);
                Assert.That(notes.childCount, Is.Zero);

                for (int lane = 0; lane < 4; lane++)
                {
                    Vector2 lanePoint = RectTransformUtility.WorldToScreenPoint(null,
                        grid.TransformPoint(new Vector3(grid.rect.width * (lane + 0.5f) / 4f,
                            -grid.rect.height * 0.5f)));
                    yield return Click(mouse, lanePoint, MouseButton.Left);
                }
                Assert.That(notes.childCount, Is.EqualTo(4));
                yield return Click(mouse, point, MouseButton.Right);
                Assert.That(notes.childCount, Is.EqualTo(3));

                Vector2 outside = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width + 50, -grid.rect.height * 0.5f)));
                yield return Click(mouse, outside, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(3));

                float originalY = notes.Bounds(0).center.y;
                yield return Scroll(mouse, point, 120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(16), "Wheel alone must not zoom.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                yield return null;
                yield return Scroll(mouse, point, 120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(12));
                Assert.That(notes.childCount, Is.EqualTo(3));
                Assert.That(notes.Bounds(0).center.y, Is.GreaterThan(originalY));
                yield return Scroll(mouse, point, -120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(16));
                Assert.That(notes.childCount, Is.EqualTo(3));
                Assert.That(notes.Bounds(0).center.y,
                    Is.EqualTo(originalY).Within(0.01f));

                yield return Scroll(mouse, point, -120);
                yield return Scroll(mouse, point, -120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(24));
                Vector2 laterPoint = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width * 0.125f, -grid.rect.height * 0.05f)));
                yield return Click(mouse, laterPoint, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(4), "The chart must accept a note after 10 seconds.");
                yield return Scroll(mouse, point, 120);
                yield return Scroll(mouse, point, 120);
                Assert.That(notes.childCount, Is.EqualTo(3), "Zooming in only hides later notes.");
                yield return Scroll(mouse, point, -120);
                yield return Scroll(mouse, point, -120);
                Assert.That(notes.childCount, Is.EqualTo(4), "Zooming out restores later notes.");
                for (int i = 0; i < 3; i++) yield return Scroll(mouse, point, -120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(36));
                yield return Click(mouse, laterPoint, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(5), "The chart must grow beyond its old 16-second limit.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return Scroll(mouse, point, 120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(36));
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                inputSettings.editorInputBehaviorInPlayMode = previousBehavior;
                inputSettings.backgroundBehavior = previousBackground;
            }
        }

        [UnityTest]
        public IEnumerator PlainWheelScrollsTimelineWithoutChangingZoomAndCanPlaceLaterNotes()
        {
            var inputSettings = InputSystem.settings;
            var previousBehavior = inputSettings.editorInputBehaviorInPlayMode;
            var previousBackground = inputSettings.backgroundBehavior;
            Mouse mouse = null;
            Keyboard keyboard = null;
            try
            {
                inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                mouse = InputSystem.AddDevice<Mouse>();
                mouse.MakeCurrent();
                keyboard = InputSystem.AddDevice<Keyboard>();
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/ChartEditor.unity");
                yield return null;
                var editor = Object.FindFirstObjectByType<ChartEditorScreen>();
                var grid = (RectTransform)editor.transform;
                var notes = new TimelineNotes(grid.GetComponent<ChartEditorScreen>());
                Canvas.ForceUpdateCanvases();
                Vector2 middle = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width * 0.125f, -grid.rect.height * 0.5f)));
                yield return Click(mouse, middle, MouseButton.Left);
                float originalY = notes.Bounds(0).center.y;

                yield return Scroll(mouse, middle, -120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(16));
                Assert.That(editor.ScrollBeatOffset, Is.EqualTo(0.5f));
                Assert.That(notes.Bounds(0).center.y, Is.LessThan(originalY));
                yield return Scroll(mouse, middle, 120);
                Assert.That(editor.ScrollBeatOffset, Is.Zero);
                Assert.That(notes.Bounds(0).center.y,
                    Is.EqualTo(originalY).Within(0.01f));

                for (int i = 0; i < 12; i++) yield return Scroll(mouse, middle, -120);
                Assert.That(editor.VisibleBeats, Is.EqualTo(16));
                Assert.That(editor.ScrollBeatOffset, Is.EqualTo(6f));
                Vector2 later = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width * 0.125f, -grid.rect.height * 0.05f)));
                yield return Click(mouse, later, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(2), "Plain scrolling must allow a note after 10 seconds.");
                for (int i = 0; i < 12; i++) yield return Scroll(mouse, middle, 120);
                Assert.That(notes.childCount, Is.EqualTo(1), "Earlier view hides the later note without deleting it.");
                for (int i = 0; i < 12; i++) yield return Scroll(mouse, middle, -120);
                Assert.That(notes.childCount, Is.EqualTo(2));
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                inputSettings.editorInputBehaviorInPlayMode = previousBehavior;
                inputSettings.backgroundBehavior = previousBackground;
            }
        }

        [UnityTest]
        public IEnumerator DraggingUpCreatesHoldAndRightClickOnItsBodyRemovesIt()
        {
            var inputSettings = InputSystem.settings;
            var previousBehavior = inputSettings.editorInputBehaviorInPlayMode;
            var previousBackground = inputSettings.backgroundBehavior;
            Mouse mouse = null;
            try
            {
                inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                mouse = InputSystem.AddDevice<Mouse>();
                mouse.MakeCurrent();
                yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/ChartEditor.unity");
                yield return null;
                var grid = (RectTransform)Object.FindFirstObjectByType<ChartEditorScreen>().transform;
                var notes = new TimelineNotes(grid.GetComponent<ChartEditorScreen>());
                Canvas.ForceUpdateCanvases();
                float laneX = grid.rect.width * 0.375f;
                Vector2 start = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(laneX, -grid.rect.height * 0.75f)));
                Vector2 end = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(laneX, -grid.rect.height * 0.25f)));
                Vector2 middle = (start + end) * 0.5f;
                yield return Drag(mouse, start, end);
                Assert.That(notes.childCount, Is.EqualTo(1));
                Assert.That(notes.Data(0).Kind, Is.EqualTo(NoteKind.Hold));
                Assert.That(notes.Bounds(0).height, Is.GreaterThan(100f));
                yield return Click(mouse, middle, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(1), "A tap cannot overlap a hold in the same lane.");
                yield return Click(mouse, middle, MouseButton.Right);
                Assert.That(notes.childCount, Is.Zero);

                yield return Click(mouse, start, MouseButton.Left);
                yield return Click(mouse, middle, MouseButton.Left);
                yield return Click(mouse, end, MouseButton.Left);
                Vector2 otherLane = RectTransformUtility.WorldToScreenPoint(null,
                    grid.TransformPoint(new Vector3(grid.rect.width * 0.625f, -grid.rect.height * 0.5f)));
                yield return Click(mouse, otherLane, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(4));
                yield return Drag(mouse, start, end);
                Assert.That(notes.childCount, Is.EqualTo(2), "The new hold replaces all overlapping taps in its lane.");
                Assert.That(notes.Data(0).Kind, Is.EqualTo(NoteKind.Hold));
                yield return Drag(mouse, (start + middle) * 0.5f, (middle + end) * 0.5f);
                Assert.That(notes.childCount, Is.EqualTo(2), "An existing hold stays when another hold overlaps it.");
                yield return Click(mouse, middle, MouseButton.Left);
                Assert.That(notes.childCount, Is.EqualTo(2), "A tap cannot replace an existing hold.");
                yield return Click(mouse, middle, MouseButton.Right);
                Assert.That(notes.childCount, Is.EqualTo(1), "The note in another lane remains.");
            }
            finally
            {
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                inputSettings.editorInputBehaviorInPlayMode = previousBehavior;
                inputSettings.backgroundBehavior = previousBackground;
            }
        }

        [UnityTest]
        public IEnumerator LayoutFillsSixteenNineAndSixteenTen()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/ChartEditor.unity");
            yield return null;
            var canvas = Object.FindFirstObjectByType<ChartEditorScreen>().GetComponentInParent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var root = (RectTransform)canvas.transform;
            var background = (RectTransform)root.Find("Background");
            var row = (RectTransform)root.Find("Editor Content");
            var footer = (RectTransform)root.Find("Progress Space");
            var grid = (RectTransform)root.Find("Editor Content/Chart And Preview/Chart Grid");
            var preview = (RectTransform)root.Find("Editor Content/Chart And Preview/Preview Space");
            Assert.That(root.Find("Title"), Is.Null);
            foreach (float height in new[] { 720f, 800f })
            {
                root.sizeDelta = new Vector2(1280, height);
                Canvas.ForceUpdateCanvases();
                yield return null;
                Assert.That(background.rect.size, Is.EqualTo(root.rect.size));
                Assert.That(row.rect.height, Is.EqualTo(height - 241f).Within(0.01f));
                Assert.That(grid.rect.height, Is.EqualTo(row.rect.height).Within(0.01f));
                Assert.That(preview.rect.height, Is.EqualTo(row.rect.height).Within(0.01f));
                Assert.That(footer.rect.height, Is.EqualTo(151f).Within(0.01f));
                Assert.That(footer.rect.width, Is.EqualTo(row.rect.width).Within(0.01f));
            }
        }

        private sealed class TimelineNotes
        {
            private readonly ChartEditorScreen screen;
            private readonly ChartBeatGrid timeline;
            public TimelineNotes(ChartEditorScreen screen)
            { this.screen = screen; timeline = screen.GetComponentInChildren<ChartBeatGrid>(); }
            private NoteData[] Visible => screen.Session.GetNotes(screen.ChartId).Select(n => n.Data)
                .Where(n => Bounds(n).height > 0).ToArray();
            public int childCount => Visible.Length;
            public NoteData Data(int index) => Visible[index];
            public Rect Bounds(int index) => Bounds(Data(index));
            private Rect Bounds(NoteData note)
            {
                var tempo = new TempoMap(screen.Session.Snapshot.TempoPoints);
                return timeline.NoteBounds(note.Lane, tempo.BeatAtTime(note.StartTime), tempo.BeatAtTime(note.EndTime));
            }
        }

        private static IEnumerator Click(Mouse mouse, Vector2 point, MouseButton button)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            // UI release callbacks schedule note destruction at the end of the frame.
            yield return null;
        }

        private static IEnumerator Drag(Mouse mouse, Vector2 start, Vector2 end)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = (start + end) * 0.5f }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
            yield return null;
            yield return null;
        }

        private static IEnumerator Scroll(Mouse mouse, Vector2 point, float delta)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, delta) });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
        }
    }
}
