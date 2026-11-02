using System.Collections;
using NUnit.Framework;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
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
        public IEnumerator PauseSettingsCountdownRestartAndExitPreserveTheSongUntilRequested()
        {
            yield return LoadPlayingSong();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            yield return Press(Key.Escape);
            Assert.That(menu.panel.activeSelf, Is.True);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Clock.Running, Is.False);
            double time = game.Clock.SongTime;
            int resolved = game.Session.Resolved;
            yield return new WaitForSecondsRealtime(.3f);
            yield return Press(Key.D);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
            yield return Click(menu.settingsButton);
            var settings = Object.FindFirstObjectByType<SettingsView>();
            Assert.That(settings, Is.Not.Null);
            Assert.That(game.IsPaused, Is.True);
            yield return Click(settings.graphicsTab);
            settings.bufferList.value = 1;
            yield return Click(settings.apply);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return Await(() => !flow.Audio.IsBufferValidationPending);
            Assert.That(flow.Audio.LastDspRealtimeRatio, Is.InRange(.85, 1.15));
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
            yield return null;
            yield return Click(settings.back);
            Assert.That(menu.isActiveAndEnabled, Is.True);
            Assert.That(menu.panel.activeSelf, Is.True);
            Assert.That(game.Clock.SongTime, Is.EqualTo(time));
            yield return Click(menu.resumeButton);
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
