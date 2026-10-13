using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SongSelectionFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator ModePopupSelectsNormalAndHardWithoutChangingGameplayOptions()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var song = flow.Selection.SelectedSongId;
            var difficulty = flow.SelectedDifficulty;
            double speed = flow.SelectedMultiplier;
            int timing = flow.Preferences.TimingOffsetMs;
            Assert.That(screen.HardModeSelected, Is.False);
            Assert.That(view.modeLabel.text, Is.EqualTo("Gauge"));
            yield return Click(view.modeButton);
            Assert.That(view.modePopup.activeSelf, Is.True);
            Assert.That(view.settingsButton.interactable, Is.False);
            Assert.That(view.play.interactable, Is.False);
            view.play.onClick.Invoke();
            view.settingsButton.onClick.Invoke();
            Assert.That(flow.Transitioning, Is.False);
            yield return Click(view.hardModeButton);
            Assert.That(screen.HardModeSelected, Is.True);
            Assert.That(view.modeLabel.text, Is.EqualTo("Challenge 1"));
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.modeButton.gameObject)
            );
            yield return Click(view.modeButton);
            yield return Press(Key.Escape);
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(view.backPopup.activeSelf, Is.False);
            Assert.That(screen.HardModeSelected, Is.True);
            yield return Click(view.modeButton);
            yield return Click(view.cancelMode);
            Assert.That(screen.HardModeSelected, Is.True);
            yield return Click(view.modeButton);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.hardModeButton.gameObject)
            );
            yield return Press(Key.LeftArrow);
            yield return Press(Key.Enter);
            Assert.That(screen.HardModeSelected, Is.False);
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(song));
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(difficulty));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(speed));
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(timing));

            var modeRect = (RectTransform)view.modeButton.transform;
            var settingsRect = (RectTransform)view.settingsButton.transform;
            Assert.That(modeRect.anchorMax.x, Is.LessThanOrEqualTo(settingsRect.anchorMin.x));
        }

        [UnityTest]
        public IEnumerator SongRowsKeyboardSelectionUpdatesSelectedSong()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            EventSystem.current.SetSelectedGameObject(
                view.Rows.First(r => r.Entry.Id == "test-pulse").gameObject
            );
            yield return Press(Key.DownArrow);
            Assert.That(
                UnityEngine
                    .Object.FindFirstObjectByType<AppFlowController>()
                    .Selection.SelectedSongId,
                Is.EqualTo("tempo-pulse")
            );
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<AppFlowController>().SelectedSongIndex,
                Is.EqualTo(1)
            );
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
            Assert.That(flow.SelectedDifficulty.DisplayName, Is.EqualTo("Standard"));
            Assert.That(view.difficultyButtons[0].interactable, Is.False);
            Assert.That(view.difficultyButtons[2].interactable, Is.False);
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
            Assert.That(
                view.difficultyButtons.Length,
                Is.EqualTo(flow.Settings.difficulties.Length)
            );

            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                if (i != 1)
                {
                    Assert.That(view.difficultyButtons[i].interactable, Is.False);
                    continue;
                }

                yield return Click(view.difficultyButtons[i]);
                Assert.That(view.difficultyList.value, Is.EqualTo(i));
                Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[i]));
                Assert.That(
                    flow.Preferences.Difficulty,
                    Is.EqualTo(flow.Settings.difficulties[i].name)
                );
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
            Assert.That(view.difficultyButtons[1].interactable, Is.True);
            view.difficultyList.value = 0;
            yield return null;
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[1]));
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
            yield return Press(Key.RightArrow);
            yield return Press(Key.RightArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            yield return Press(Key.LeftArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            yield return Click(view.back);
            yield return Press(Key.RightArrow);
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
            yield return Click(view.cancelBack);
            yield return Click(view.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            yield return null;

            var settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            yield return Click(settingsView.gameplayTab);
            EventSystem.current.SetSelectedGameObject(settingsView.speedSlider.gameObject);

            float speed = settingsView.speedSlider.value;
            yield return Press(Key.RightArrow);
            Assert.That(settingsView.speedSlider.value, Is.EqualTo(speed + .1f).Within(.0001));
            Assert.That(view.difficultyList.value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SpeedSliderSupportsTenthsAndSavedSpeedIsAppliedToGameplay()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(view.scrollModeList.gameObject.activeSelf, Is.False);
            Assert.That(view.multiplierList.gameObject.activeSelf, Is.False);
            Assert.That(view.speedSlider.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.speedLabel.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.timingOffset.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.timingLabel.gameObject.activeInHierarchy, Is.False);
            yield return Click(view.settingsButton);
            yield return Await(
                () => UnityEngine.Object.FindFirstObjectByType<SettingsView>(),
                "Speed test: enter settings"
            );
            yield return null;

            var settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            yield return Click(settingsView.gameplayTab);
            Assert.That(settingsView.speedSlider.minValue, Is.EqualTo(1));
            Assert.That(settingsView.speedSlider.maxValue, Is.EqualTo(10));

            for (int i = 10; i <= 100; i++)
            {
                settingsView.speedSlider.value = i / 10f;
                Assert.That(flow.SelectedMultiplier, Is.EqualTo(i / 10.0).Within(.0001));
                Assert.That(flow.Preferences.Multiplier, Is.EqualTo(i / 10.0).Within(.0001));
            }

            settingsView.speedSlider.value = 6.74f;
            Assert.That(settingsView.speedSlider.value, Is.EqualTo(6.7f).Within(.0001));
            Assert.That(settingsView.speedLabel.text, Does.Contain("6.7"));
            settingsView.timingOffset.value = 125;
            yield return Click(settingsView.back);
            yield return Await(
                () => !UnityEngine.Object.FindFirstObjectByType<SettingsView>(),
                "Speed test: return to selection"
            );
            yield return null;
            Assert.That(view.speedSlider.value, Is.EqualTo(6.7f).Within(.0001));

            // Selecting another song or difficulty must not overwrite the popup's saved speed.
            UnityEngine
                .Object.FindFirstObjectByType<SongSelectionScreen>()
                .SelectSong("tempo-pulse");
            view.difficultyList.value = 0;
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(6.7).Within(.0001));
            yield return Click(view.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            yield return null;
            settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            Assert.That(settingsView.speedSlider.value, Is.EqualTo(6.7f).Within(.0001));
            Assert.That(settingsView.timingOffset.value, Is.EqualTo(125));
            yield return Click(settingsView.back);
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            yield return null;
            yield return Click(view.play);
            yield return Await(
                () => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(),
                "Speed test: enter gameplay; " + view.details.text
            );
            yield return null;
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
            flow.Selection.UpdateSelection(
                flow.Selection.SelectedSongId,
                flow.SelectedDifficulty,
                flow.SelectedScrollMode,
                1.5
            );
            yield return null;
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(song));
        }

        [UnityTest]
        public IEnumerator RenamedLayoutStillLoadsSongThroughExplicitViewReferences()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();

            foreach (var child in view.GetComponentsInChildren<Transform>(true))
            {
                child.name = "Reorganized visual";
            }

            view.play.transform.SetAsFirstSibling();
            yield return Click(view.play);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
        }

        [UnityTest]
        public IEnumerator MouseClickUsesUiInputModuleToLoadSong()
        {
            CaptureSelection();

            var button = UnityEngine
                .Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(b => b.name == "Play");
            Canvas.ForceUpdateCanvases();

            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center)
            );
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(
                mouse,
                new MouseState { position = point }.WithButton(MouseButton.Left)
            );
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
            yield return null;
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().Chart.Count,
                Is.EqualTo(18)
            );
        }

        [UnityTest]
        public IEnumerator SelectionKeepsSpeedIndependentAndBothSongsLoad()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            dropdowns.Single(d => d.name == "Scroll mode").value = 1;
            flow.Selection.UpdateSelection(
                flow.Selection.SelectedSongId,
                flow.SelectedDifficulty,
                flow.SelectedScrollMode,
                1.5
            );
            dropdowns.Single(d => d.name == "Judgment difficulty").value = 0;
            UnityEngine
                .Object.FindFirstObjectByType<SongSelectionScreen>()
                .SelectSong("tempo-pulse");
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(flow.SelectedScrollMode, Is.EqualTo(ScrollMode.Constant));
            UnityEngine
                .Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(b => b.name == "Play")
                .onClick.Invoke();
            flow.PlaySelected();

            // A duplicate start during transition is ignored.
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
            yield return null;

            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.Chart.Count, Is.EqualTo(8));
            Assert.That(game.ReadModel.Snapshot.ScrollMultiplier, Is.EqualTo(1.5));
            Assert.That(game.ReadModel.Snapshot.DifficultyName, Does.Contain("Standard"));
            Assert.That(
                UnityEngine
                    .Object.FindObjectsByType<NoteView>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    )
                    .Length,
                Is.EqualTo(8)
            );
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            yield return null;
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(1));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>(), Is.Null);
            flow.UpdateSelection(0, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
            yield return null;
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().Chart.Count,
                Is.EqualTo(18)
            );
        }
    }
}
