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
        public IEnumerator SongRowsKeyboardSelectionUpdatesSelectedSong()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            EventSystem.current.SetSelectedGameObject(view.Rows.First(r => r.Entry.Id == "test-pulse").gameObject);
            yield return Press(Key.DownArrow);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
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
        public IEnumerator HorizontalDifficultyButtonsKeepSelectionJudgmentAndOptionsInSync()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var song = flow.Selection.SelectedSongId;
            var mode = flow.SelectedScrollMode;
            var multiplier = flow.SelectedMultiplier;
            Assert.That(view.difficultyButtons.Length, Is.EqualTo(flow.Settings.difficulties.Length));
            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                yield return Click(view.difficultyButtons[i]);
                Assert.That(view.difficultyList.value, Is.EqualTo(i));
                Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[i]));
                Assert.That(flow.Preferences.Difficulty, Is.EqualTo(flow.Settings.difficulties[i].name));
                Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(song));
                Assert.That(flow.SelectedScrollMode, Is.EqualTo(mode));
                Assert.That(flow.SelectedMultiplier, Is.EqualTo(multiplier));
                Assert.That(view.difficultyButtons[i].GetComponent<Outline>().enabled, Is.True);
                if (i > 0)
                {
                    var previous = (RectTransform)view.difficultyButtons[i - 1].transform;
                    var current = (RectTransform)view.difficultyButtons[i].transform;
                    Assert.That(current.anchorMin.x, Is.GreaterThan(previous.anchorMax.x));
                    Assert.That(current.anchorMin.y, Is.EqualTo(previous.anchorMin.y));
                }
            }
            yield return Click(view.back);
            Assert.That(view.difficultyButtons.All(button => !button.interactable), Is.True);
            yield return Click(view.cancelBack);
            Assert.That(view.difficultyButtons.All(button => button.interactable), Is.True);
            view.difficultyList.value = 0; yield return null;
            Assert.That(view.difficultyButtons[0].GetComponent<Outline>().enabled, Is.True);
        }
        [UnityTest]
        public IEnumerator LeftAndRightChangeDifficultyAndRespectPopupAndSliderFocus()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            view.difficultyList.value = 0;
            EventSystem.current.SetSelectedGameObject(view.Rows[0].gameObject);
            yield return Press(Key.RightArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[1]));
            yield return Press(Key.RightArrow); yield return Press(Key.RightArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(2));
            yield return Press(Key.LeftArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            yield return Click(view.back); yield return Press(Key.RightArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            yield return Click(view.cancelBack);
            EventSystem.current.SetSelectedGameObject(view.speedSlider.gameObject);
            float speed = view.speedSlider.value;
            yield return Press(Key.RightArrow);
            Assert.That(view.speedSlider.value, Is.EqualTo(speed + .1f).Within(.0001));
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator SpeedSliderSupportsTenthsAndSavedSpeedIsAppliedToGameplay()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(view.scrollModeList.gameObject.activeSelf, Is.False);
            Assert.That(view.multiplierList.gameObject.activeSelf, Is.False);
            Assert.That(view.speedSlider.minValue, Is.EqualTo(1)); Assert.That(view.speedSlider.maxValue, Is.EqualTo(10));
            for (int i = 10; i <= 100; i++)
            {
                view.speedSlider.value = i / 10f;
                Assert.That(flow.SelectedMultiplier, Is.EqualTo(i / 10.0).Within(.0001));
                Assert.That(flow.Preferences.Multiplier, Is.EqualTo(i / 10.0).Within(.0001));
            }
            view.speedSlider.value = 6.74f;
            Assert.That(view.speedSlider.value, Is.EqualTo(6.7f).Within(.0001));
            Assert.That(view.speedLabel.text, Does.Contain("6.7"));
            view.timingOffset.value = 125;
            yield return Click(view.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsView>(), "Speed test: enter settings"); yield return null;
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionView>(), "Speed test: return to selection"); yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(view.speedSlider.value, Is.EqualTo(6.7f).Within(.0001));
            yield return Click(view.play);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(), "Speed test: enter gameplay; " + view.details.text); yield return null;
            Assert.That(flow.CurrentRequest.Multiplier, Is.EqualTo(6.7).Within(.0001));
            Assert.That(flow.CurrentRequest.ScrollMode, Is.EqualTo(ScrollMode.Constant));
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.EqualTo(125));
        }
        [UnityTest]
        public IEnumerator ScrollModeDropdownChangesModeWithoutChangingDifficulty()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var difficulty = flow.SelectedDifficulty;
            view.scrollModeList.value = 1;
            yield return null;
            Assert.That(flow.SelectedScrollMode, Is.EqualTo(ScrollMode.Constant));
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
            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            dropdowns.Single(d=>d.name=="Scroll mode").value = 1;
            dropdowns.Single(d=>d.name=="Scroll multiplier").value = 4;
            dropdowns.Single(d=>d.name=="Judgment difficulty").value = 0;
            UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong("tempo-pulse");
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5)); Assert.That(flow.SelectedScrollMode, Is.EqualTo(ScrollMode.Constant));
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
