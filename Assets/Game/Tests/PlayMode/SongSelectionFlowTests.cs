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
    [Category("UI")]
    public sealed class SongSelectionFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator SongDropdownKeyboardSelectionUpdatesSelectedSong()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            yield return Press(Key.Enter);
            yield return Press(Key.DownArrow);
            yield return Press(Key.Enter);
            Assert.That(view.songList.value, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().SelectedSongIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DifficultyDropdownChangesJudgmentWithoutChangingScroll()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var mode = flow.SelectedScrollMode;
            var multiplier = flow.SelectedMultiplier;
            view.difficultyList.value = 0;
            yield return null;
            Assert.That(flow.SelectedDifficulty.DisplayName, Is.EqualTo("Easy"));
            Assert.That(flow.SelectedScrollMode, Is.EqualTo(mode));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(multiplier));
        }

        [UnityTest]
        public IEnumerator ScrollModeDropdownChangesModeWithoutChangingDifficulty()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var difficulty = flow.SelectedDifficulty;
            view.scrollModeList.value = 1;
            yield return null;
            Assert.That(flow.SelectedScrollMode, Is.EqualTo(ScrollMode.Bpm));
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(difficulty));
        }

        [UnityTest]
        public IEnumerator MultiplierDropdownChangesSpeedWithoutChangingSong()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            int song = flow.SelectedSongIndex;
            view.multiplierList.value = 4;
            yield return null;
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(song));
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
    }
}
