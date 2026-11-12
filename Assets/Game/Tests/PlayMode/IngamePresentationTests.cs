using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    public sealed class IngamePresentationTests : GameplayTestContext
    {
        protected override bool UsePracticeSelection => true;

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator IntroductionBlocksEarlyStartThenSchedulesMusicAndNotesTogether()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<GameplayHudView>());
            var view = Object.FindFirstObjectByType<GameplayHudView>();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var root = Object.FindFirstObjectByType<GameplayCompositionRoot>();
            var clock = Object.FindFirstObjectByType<SongClock>();
            yield return Await(() => view.introduction.activeInHierarchy);
            double shownAt = Time.realtimeSinceStartupAsDouble;
            double songTime = game.ReadModel.Snapshot.SongTime;
            Assert.That(view.album.sprite, Is.Not.Null);
            Assert.That(view.songTitle.text, Is.EqualTo(flow.CurrentRequest.Song.Title));
            Assert.That(view.songDetails.text, Is.EqualTo(flow.CurrentRequest.Difficulty.DisplayName));
            Assert.That(view.combo.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.judgmentRecordPanel.activeInHierarchy, Is.False);
            Assert.That(view.healthFill.gameObject.activeInHierarchy, Is.True);
            var mode = flow.CurrentRequest.Song.Mode;
            var presentation = flow.Settings.presentation;
            float edge = view.gameplayCamera.WorldToViewportPoint(new Vector3(
                mode.LaneCount * mode.LaneSpacing * .5f, 0,
                presentation.highwayCenter - presentation.highwayLength * .5f)).x;
            Assert.That(view.healthGauge.anchorMin.x - edge, Is.EqualTo(.012f).Within(.002f));
            Assert.That(view.lanes.laneRoot.gameObject.activeInHierarchy, Is.True);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.D));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            while (Time.realtimeSinceStartupAsDouble - shownAt < 1.8)
            {
                Assert.That(clock.Running, Is.False);
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready));
                Assert.That(game.Session.Resolved, Is.Zero);
                Assert.That(game.ReadModel.Snapshot.SongTime, Is.EqualTo(songTime));
                Assert.That(view.introduction.activeInHierarchy, Is.True);
                yield return null;
            }
            Capture("ingame-introduction");
            yield return Await(() => !root.IntroductionPending);
            Assert.That(Time.realtimeSinceStartupAsDouble - shownAt, Is.GreaterThanOrEqualTo(1.95));
            Assert.That(clock.Running, Is.True);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(view.introduction.activeSelf, Is.False);
            Assert.That(view.lanes.laneRoot.gameObject.activeSelf, Is.False);
            Assert.That(clock.ScheduledStartDspTime, Is.GreaterThan(AudioSettings.dspTime - .2));
            yield return null;
            Capture("ingame-playing");
            game.RestartSession();
            yield return null;
            Assert.That(view.introduction.activeSelf, Is.False, "Restart must not revive hidden help.");
            Assert.That(view.lanes.laneRoot.gameObject.activeSelf, Is.False);
            root.ReturnToSelection();
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(Object.FindObjectsByType<GameplayHudView>(FindObjectsSortMode.None), Is.Empty);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<GameplayCompositionRoot>());
            root = Object.FindFirstObjectByType<GameplayCompositionRoot>();
            Assert.That(root.IntroductionPending, Is.True);
        }

        private static void Capture(string name)
        {
            var texture = GameplayHudFlowTests.CaptureGameplayDisplay(Object.FindFirstObjectByType<GameplayViewport>());
            try
            {
                System.IO.File.WriteAllBytes("Logs/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        [Category("Hardware")]
        public IEnumerator LaneInputPlaysWhooshWithoutAHitAndPauseSuppressesIt()
        {
            Object.FindFirstObjectByType<AppFlowController>().PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<GameplayCompositionRoot>());
            var root = Object.FindFirstObjectByType<GameplayCompositionRoot>();
            yield return Await(() => !root.IntroductionPending);
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var feedback = Object.FindFirstObjectByType<RhythmDojo.Presentation.GameplayHitFeedback>();
            var impact = feedback.GetComponent<AudioSource>();
            var input = feedback.GetComponentsInChildren<AudioSource>().Single(source => source != impact);
            // No note is in the window during the existing DSP preroll.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            Assert.That(input.isPlaying, Is.True);
            Assert.That(impact.isPlaying, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            game.PauseSession();
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            Assert.That(input.isPlaying, Is.False);
            Assert.That(impact.isPlaying, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
