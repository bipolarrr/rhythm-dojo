using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class GameplayPauseFlowTests : GameplayTestContext
    {
        private IEnumerator LoadPlayingSong()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            yield return Press(Key.Space);
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PauseSettingsCountdownRestartAndExitPreserveTheSongUntilRequested()
        {
            yield return LoadPlayingSong();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            yield return Press(Key.Escape);
            Assert.That(menu.panel.activeSelf, Is.True);
            Assert.That(menu.resumeButton.transform.Find("Dojo Focus Border")?.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Clock.Running, Is.False);
            double time = game.Clock.SongTime;
            int resolved = game.Session.Resolved;
            yield return new WaitForSecondsRealtime(.3f);
            yield return Press(Key.D);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(menu.settingsButton.gameObject.activeSelf, Is.False);
            menu.settingsButton.onClick.Invoke();
            flow.ShowGameplaySettings();
            flow.ShowSettings();
            flow.ShowTitleSettings();
            Assert.That(Object.FindFirstObjectByType<SettingsView>(), Is.Null);
            Assert.That(flow.ApplyBufferSize(), Is.False);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            yield return Press(Key.Escape);
            Assert.That(menu.panel.activeSelf, Is.False);
            Assert.That(menu.countdownText.text, Is.EqualTo("3"));
            Assert.That(game.IsPaused, Is.True);
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(menu.countdownText.text, Is.EqualTo("2"));
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(menu.countdownText.text, Is.EqualTo("1"));
            Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
            yield return new WaitForSecondsRealtime(1.05f);
            if (game.IsPaused)
            {
                Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
            }
            yield return Await(() => !game.IsPaused && !menu.countdown.activeSelf);
            Assert.That(menu.countdown.activeSelf, Is.False);
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.Clock.Running, Is.True);
            Assert.That(((Component)game.Clock).GetComponent<AudioSource>().isPlaying, Is.True);
            Assert.That(game.Clock.SongTime, Is.GreaterThan(time));
            Assert.That(game.Clock.SongTime, Is.LessThan(time + .5));
            yield return Press(Key.Escape);
            yield return Click(menu.restartButton);
            Assert.That(menu.panel.activeSelf, Is.False);
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.Session.Resolved, Is.Zero);
            Assert.That(game.Clock.SongTime, Is.LessThan(.5));
            yield return Press(Key.Escape);
            yield return Click(menu.exitButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(Object.FindFirstObjectByType<AppFlowController>().LastResult, Is.Null);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PauseSettingsChangesCurrentNoteAndMeasureSpeedWithoutResettingTheSong()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var selection = flow.Selection;
            selection.UpdateSelection(
                selection.SelectedSongId,
                selection.SelectedDifficulty,
                ScrollMode.Bpm,
                .75
            );
            yield return LoadPlayingSong();

            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            yield return Press(Key.Escape);

            var before = game.ReadModel.Snapshot;
            var session = game.Session;
            var firstNote = Object.FindObjectsByType<NoteView>(FindObjectsSortMode.None)
                .Single(note => note.name.StartsWith("Note 00 -", System.StringComparison.Ordinal));
            var head = firstNote.transform.Find("Head");
            var playfield = Object.FindFirstObjectByType<PlayfieldPresenter>();
            var measure = playfield.transform.Find("Measure Line");
            double noteDistance = head.position.z;
            double measureDistance = measure.position.z;

            // Existing pause regression now enforces immutable settings, including direct callers.
            var stored = PlayerPrefs.GetString(SelectionPreferences.StorageKey);
            int offset = flow.Preferences.TimingOffsetMs;
            flow.ShowGameplaySettings();
            Assert.That(Object.FindFirstObjectByType<SettingsView>(), Is.Null);
            flow.Preferences.Multiplier = 2;
            flow.Preferences.TimingOffsetMs = 100;
            flow.Preferences.Save();
            flow.Selection.UpdateSelection(selection.SelectedSongId, flow.SelectedDifficulty, ScrollMode.Constant, 2);
            Object.FindFirstObjectByType<GameplayCompositionRoot>().SetScrollMultiplier(2);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(offset));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(.75));
            Assert.That(PlayerPrefs.GetString(SelectionPreferences.StorageKey), Is.EqualTo(stored));

            var after = game.ReadModel.Snapshot;

            Assert.That(after.ScrollMultiplier, Is.EqualTo(.75));
            Assert.That(after.ScrollMode, Is.EqualTo(ScrollMode.Bpm));
            Assert.That(after.SongTime, Is.EqualTo(before.SongTime));
            Assert.That(game.Session, Is.SameAs(session));
            Assert.That(after.Session.Resolved, Is.EqualTo(before.Session.Resolved));
            Assert.That(head.position.z, Is.EqualTo(noteDistance).Within(.001));

            Assert.That(measure.position.z, Is.EqualTo(measureDistance).Within(.001));

            yield return Click(menu.resumeButton);
            yield return Await(() => !game.IsPaused && !menu.countdown.activeSelf);
            Assert.That(game.ReadModel.Snapshot.ScrollMultiplier, Is.EqualTo(.75));
            Assert.That(game.Clock.SongTime, Is.GreaterThan(before.SongTime));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EscapeDuringCountdownReopensPauseWithoutResumingTheClock()
        {
            yield return LoadPlayingSong();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            yield return Press(Key.Escape);
            double time = game.Clock.SongTime;
            yield return Click(menu.resumeButton);
            yield return new WaitForSecondsRealtime(.3f);
            yield return Press(Key.Escape);
            Assert.That(menu.CountingDown, Is.False);
            Assert.That(menu.panel.activeSelf, Is.True);
            Assert.That(menu.countdown.activeSelf, Is.False);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            yield return Click(menu.exitButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
