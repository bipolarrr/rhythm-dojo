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

            string[] values = { "1/4", "1/8", "1/12", "1/16", "1/24", "1/32", "1/48", "1/64", "1/96", "1/192" };
            int[] divisions = { 4, 8, 12, 16, 24, 32, 48, 64, 96, 192 };
            for (int i = 1; i <= values.Length; i++)
            {
                yield return ClickAt(Center(view.quantizationCycle.GetComponent<RectTransform>()));
                int index = i % values.Length;
                Assert.That(view.quantizationCycleValue.text, Is.EqualTo(values[index]));
                Assert.That(view.quantization.value, Is.EqualTo(index));
                Assert.That(timelineView.noteSnapControl.value, Is.EqualTo(index));
                Assert.That(workspace.screen.SnapBeats, Is.EqualTo(4.0 / divisions[index]).Within(.000001));
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
                WorkspaceAction.Select, WorkspaceAction.Pencil, WorkspaceAction.Eraser, WorkspaceAction.Position
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
        public IEnumerator OriginalEditorKeepsItsStandardDefault()
        {
            yield return SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath);
            yield return null;
            var screen = UnityEngine.Object.FindFirstObjectByType<ChartEditorScreen>();
            Assert.That(screen.Session.Snapshot.Charts.Single().Slot, Is.EqualTo(ChartSlot.Easy));
            Assert.That(WorkspaceEditor.ScenePath, Is.EqualTo(ChartEditorMenu.ScenePath));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<WorkspaceEditor>(), Is.Not.Null);
            var view = UnityEngine.Object.FindFirstObjectByType<WorkspaceView>();
            Assert.That(view.toolActions.Length, Is.EqualTo(4));
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
        public IEnumerator SaveAsCreatesIndependentSongAndKeepsOriginal()
        {
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
