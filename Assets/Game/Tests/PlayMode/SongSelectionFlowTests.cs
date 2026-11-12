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
        private sealed class DelayedSongProvider : ISongProvider
        {
            public const string SongId = "delayed-alpha-test";
            public int LoadCount { get; private set; }
            public TaskCompletionSource<PlayableSong> Pending { get; private set; }

            public System.Collections.Generic.IReadOnlyList<SongEntry> GetEntries() =>
                new[] { new SongEntry(SongId, "Delayed Alpha Test", "", 12, 8, 4, 120, 120) };

            public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken)
            {
                LoadCount++;
                Pending = new TaskCompletionSource<PlayableSong>(TaskCreationOptions.RunContinuationsAsynchronously);
                return Pending.Task;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SongPreviewStopsOnSelectionChangeAndGameplayEntry()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            yield return Click(view.previewButton);
            yield return Await(() => view.previewSource.isPlaying, "Preview did not play.");
            Assert.That(view.previewButton.GetComponentInChildren<Text>().text, Does.Contain("멈추기"));
            Assert.That(view.previewSource.time, Is.GreaterThan(0));

            screen.SelectSong("tempo-pulse");
            Assert.That(view.previewSource.isPlaying, Is.False);
            Assert.That(view.previewSource.clip, Is.Null);
            Assert.That(view.previewButton.GetComponentInChildren<Text>().text, Does.Contain("미리듣기"));

            screen.SelectSong("test-pulse");
            yield return Click(view.previewButton);
            yield return Await(() => view.previewSource.isPlaying, "Preview did not restart.");
            view.play.onClick.Invoke();
            Assert.That(view.previewSource.isPlaying, Is.False);
            Assert.That(view.previewSource.clip, Is.Null);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LoadingLocksEntryAndRestoresNavigationWithActionableErrors()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var provider = new DelayedSongProvider();
            flow.Library.Register(provider);
            screen.SelectSong(DelayedSongProvider.SongId);

            yield return Click(view.play);
            Assert.That(provider.LoadCount, Is.EqualTo(1));
            Assert.That(view.play.interactable, Is.False);
            Assert.That(screen.State == SongSelectionState.Loading, Is.True);
            Assert.That(view.practice.status.text, Does.StartWith("불러오는 중"));
            Assert.That(view.songTitle.text, Is.EqualTo("Delayed Alpha Test"));
            Assert.That(view.libraryPanel.activeSelf, Is.False);

            string loadingCaption = view.practice.status.text;
            double deadline = Time.realtimeSinceStartupAsDouble + 3;
            while (view.practice.status.text == loadingCaption)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Loading indicator did not animate.");
                yield return null;
            }

            yield return Press(Key.Enter);
            Assert.That(provider.LoadCount, Is.EqualTo(1));

            provider.Pending.SetException(new System.IO.IOException("Temporary load failure."));
            yield return Await(() => !string.IsNullOrEmpty(view.practice.status.text), "Retryable playback error did not appear.");
            Assert.That(view.practice.status.text, Does.Contain("다시 시도"));
            Assert.That(view.play.GetComponentInChildren<Text>().text, Is.EqualTo("다시 시도"));
            Assert.That(view.play.interactable, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(view.play.gameObject));

            yield return Press(Key.Enter);
            Assert.That(provider.LoadCount, Is.EqualTo(2), "Enter should retry the selected song.");
            Assert.That(screen.State, Is.EqualTo(SongSelectionState.Loading));
            provider.Pending.SetException(new SongLoadException(new SongLoadFailure(
                SongLoadFailureKind.MissingAudio, "Missing audio.",
                new System.IO.FileNotFoundException("Missing audio."))));
            yield return Await(() => !string.IsNullOrEmpty(view.practice.status.text), "Missing audio error did not appear.");
            Assert.That(view.practice.status.text, Does.Contain("음원 파일을 찾을 수 없어요"));
            CaptureSelection("selection-missing-audio-1920", 1920, 1080);
            Assert.That(view.play.interactable, Is.False);
            yield return Click(view.practice.library);

            var row = view.Rows.Single(item => item.Entry.Id == "test-pulse");
            yield return Click(row.select);
            yield return Click(view.closeLibrary);
            Assert.That(!string.IsNullOrEmpty(view.practice.status.text), Is.False);
            Assert.That(screen.State == SongSelectionState.Loading, Is.False);
            Assert.That(view.play.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.play.interactable, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LatePreviewCompletionIsReleasedAndRefreshClearsTheFailure()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var screen = UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var provider = new DelayedSongProvider();
            flow.Library.Register(provider);
            screen.SelectSong(DelayedSongProvider.SongId);
            yield return Click(view.previewButton);
            var pending = provider.Pending;
            screen.SelectSong("test-pulse");
            int releases = 0;
            var source = flow.Settings.catalog[0];
            pending.SetResult(new PlayableSong(SongAssetAdapter.ToDocument(source), source.AudioClip,
                source.Mode, () => releases++));
            yield return Await(() => releases == 1);
            Assert.That(view.previewSource.clip, Is.Null);
            Assert.That(view.previewSource.isPlaying, Is.False);

            screen.SelectSong(DelayedSongProvider.SongId);
            yield return Click(view.play);
            provider.Pending.SetException(new SongLoadException(new SongLoadFailure(
                SongLoadFailureKind.MissingChart, "missing chart")));
            yield return Await(() => !string.IsNullOrEmpty(view.practice.status.text));
            Assert.That(view.practice.status.text, Does.Contain("채보 파일"));
            Assert.That(view.play.interactable, Is.False);
            flow.Library.Refresh();
            Assert.That(view.play.interactable, Is.True);
            Assert.That(!string.IsNullOrEmpty(view.practice.status.text), Is.False);
            Assert.That(releases, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("SceneIntegration")]
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
            Assert.That(view.modeLabel.text, Does.Contain("Gauge"));
            yield return Click(view.practice.library);
            yield return Click(view.modeButton);
            Assert.That(view.modePopup.activeSelf, Is.True);
            Assert.That(view.settingsButton.interactable, Is.False);
            Assert.That(view.play.interactable, Is.False);
            view.play.onClick.Invoke();
            view.settingsButton.onClick.Invoke();
            Assert.That(flow.Transitioning, Is.False);
            yield return Click(view.hardModeButton);
            Assert.That(screen.HardModeSelected, Is.True);
            Assert.That(view.modeLabel.text, Does.Contain("Challenge"));
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.modeButton.gameObject)
            );
            yield return Click(view.modeButton);
            yield return Press(Key.Escape);
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(view.libraryPanel.activeSelf, Is.True);
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

            Assert.That(view.practice.heading.text, Is.EqualTo("자유 연습"));
            yield return Click(view.closeLibrary);

        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SongRowsKeyboardSelectionUpdatesSelectedSong()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            yield return Click(view.practice.library);
            flow.Preferences.ToggleFavorite("test-pulse");
            flow.Preferences.ToggleFavorite("tempo-pulse");
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows.Select(row => row.Entry.Id),
                Is.EquivalentTo(new[] { "test-pulse", "tempo-pulse" }));

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
                flow.Library.Entries[flow.SelectedSongIndex].Id,
                Is.EqualTo("tempo-pulse")
            );
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator HorizontalDifficultyButtonsKeepSelectionJudgmentAndOptionsInSync()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var song = flow.Selection.SelectedSongId;
            var mode = flow.SelectedScrollMode;
            var multiplier = flow.SelectedMultiplier;
            Assert.That(view.difficultyButtons.Length, Is.EqualTo(flow.Settings.difficulties.Length));
            yield return Click(view.difficultyButtons[1]);
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[1]));
            Assert.That(flow.Preferences.Difficulty, Is.EqualTo(flow.SelectedDifficulty.name));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(song));
            Assert.That(flow.SelectedScrollMode, Is.EqualTo(mode));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(multiplier));
            Assert.That(view.difficultyButtons[0].interactable, Is.False);
            Assert.That(view.difficultyButtons[2].interactable, Is.False);
            yield return Click(view.practice.library);
            Assert.That(view.difficultyButtons.All(button => !button.interactable), Is.True);
            yield return Click(view.closeLibrary);
            Assert.That(view.difficultyButtons[1].interactable, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LeftAndRightChangeDifficultyAndRespectPopupAndSliderFocus()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            yield return Press(Key.UpArrow);
            yield return Press(Key.DownArrow);
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[1]));
            yield return Click(view.practice.library);
            var selected = flow.Selection.SelectedSongId;
            int contacts = view.practice.ContactCount;
            EventSystem.current.SetSelectedGameObject(view.speedSlider.gameObject);
            float speed = view.speedSlider.value;
            yield return Press(Key.RightArrow);
            Assert.That(view.speedSlider.value, Is.EqualTo(speed + .1f).Within(.0001));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(selected));
            Assert.That(view.practice.ContactCount, Is.EqualTo(contacts));
            yield return Press(Key.Escape);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.practice.library.gameObject));
            yield return Click(view.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsView>());
            var settingsView = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            yield return Click(settingsView.gameplayTab);
            EventSystem.current.SetSelectedGameObject(settingsView.speedSlider.gameObject);
            speed = settingsView.speedSlider.value;
            yield return Press(Key.RightArrow);
            Assert.That(settingsView.speedSlider.value, Is.EqualTo(speed + .1f).Within(.0001));
            Assert.That(flow.SelectedDifficulty, Is.EqualTo(flow.Settings.difficulties[1]));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SpeedSliderSupportsTenthsAndSavedSpeedIsAppliedToGameplay()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(view.scrollModeList.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.multiplierList.gameObject.activeInHierarchy, Is.False);
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
            yield return Press(Key.UpArrow);
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
        [Category("SceneIntegration")]
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
        [Category("Smoke")]
        [Category("SceneIntegration")]
        public IEnumerator MouseClickUsesUiInputModuleToLoadSong()
        {
            var button = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().play;
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
        [Category("SceneIntegration")]
        public IEnumerator SelectionKeepsSpeedIndependentAndBothSongsLoad()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.library);
            yield return Choose(view.scrollModeList, 1);
            view.speedSlider.value = 1.5f;
            UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong("tempo-pulse");
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(flow.SelectedScrollMode, Is.EqualTo((ScrollMode)1));
            yield return Click(view.closeLibrary);
            yield return Click(view.play);

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
