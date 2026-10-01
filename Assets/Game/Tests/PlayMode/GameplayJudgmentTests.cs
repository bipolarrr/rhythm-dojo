using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class GameplayJudgmentTests : GameplayTestContext
    {
        private RhythmGameController game;
        private readonly List<(double time, Key key, bool press)> events = new List<(double, Key, bool)>();
        private readonly HashSet<Key> held = new HashSet<Key>();
        private int nextEvent;
        private bool hitchEnabled, hitched;

        [UnityTearDown]
        public IEnumerator StopFeeding()
        {
            InputSystem.onBeforeUpdate -= FeedInput;
            events.Clear();
            held.Clear();
            yield return null;
        }

        private IEnumerator LoadSong(int index = 0, ScrollMode scroll = ScrollMode.Constant)
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.UpdateSelection(index, flow.Settings.defaultDifficulty, scroll, 1);
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready));
            nextEvent = 0;
            hitched = hitchEnabled = false;
            held.Clear();
            events.Clear();
        }

        private void PrepareInput(bool perfect)
        {
            events.Clear();
            nextEvent = 0;
            var keys = new[] { Key.D, Key.F, Key.J, Key.K };
            for (int i = 0; i < game.Chart.Count; i++)
            {
                var note = game.Chart[i];
                if (!perfect && (i == 3 || i == 14)) continue;
                double time = note.StartTime;
                if (!perfect && i == 0) time -= .08;
                if (!perfect && i == 1) time += .08;
                double duration = note.Kind == NoteKind.Hold ? note.EndTime - time : .025;
                if (!perfect && i == 11) duration = .3;
                events.Add((time, keys[note.Lane], true));
                if (!perfect && i == 15) continue; // Hold through completion; automatic resolution.
                events.Add((time + duration, keys[note.Lane], false));
            }
            events.Sort((a, b) => a.time.CompareTo(b.time));
            InputSystem.onBeforeUpdate += FeedInput;
        }

        private void FeedInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || !game ||
                game.Session.State != SessionState.Playing) return;
            if (hitchEnabled && !hitched && game.Clock.SongTime > 6.40)
            {
                hitched = true;
                System.Threading.Thread.Sleep(180);
            }
            double time = game.Clock.SongTime;
            while (nextEvent < events.Count && time >= events[nextEvent].time)
            {
                var input = events[nextEvent++];
                if (input.press) held.Add(input.key); else held.Remove(input.key);
                double timestamp = Time.realtimeSinceStartupAsDouble - (game.Clock.SongTime - input.time);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()), timestamp);
            }
        }

        private IEnumerator Completed()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + game.Chart.CompletionTime + 10;
            while (game.Session.State != SessionState.Completed)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Song did not complete.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TimestampedLaneActionsJudgeMixedRunDespiteFrameHitch()
        {
            yield return LoadSong();
            PrepareInput(false);
            hitchEnabled = true;
            yield return Press(Key.Space);
            yield return Await(() => game.Clock.SongTime > 4.4);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying, Is.True);
            var note = UnityEngine.Object.FindObjectsByType<NoteView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(view => view.name.StartsWith("Note 04", StringComparison.Ordinal));
            Assert.That(note.transform.Find("Head").position.z, Is.EqualTo(0).Within(.001));
            CaptureSelection("playmode");
            yield return Completed();
            Assert.That(hitched, Is.True);
            Assert.That(game.Session.Perfect, Is.EqualTo(13));
            Assert.That(game.Session.Good, Is.EqualTo(2));
            Assert.That(game.Session.Miss, Is.EqualTo(3));
            Assert.That(game.Session.Combo, Is.EqualTo(3));
            Assert.That(game.Session.IsHeld(1), Is.False);
        }

        [UnityTest]
        public IEnumerator SpaceRestartClearsHeldLaneAndScoresThenAllowsPerfectRun()
        {
            yield return LoadSong();
            yield return Press(Key.Space);
            yield return Await(() => game.Session.Resolved > 0); // A missed note gives the restart something to clear.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            Assert.That(game.Session.IsHeld(1), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F, Key.Space));
            yield return null;
            Assert.That(game.Session.Resolved, Is.Zero);
            Assert.That(game.Session.IsHeld(1), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            Assert.That(game.Session.IsHeld(1), Is.False, "Held input stays blocked until released.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            PrepareInput(true);
            yield return Completed();
            Assert.That(game.Session.Perfect, Is.EqualTo(18));
            Assert.That(game.Session.Resolved, Is.EqualTo(18));
            Assert.That(game.Session.Combo, Is.EqualTo(18));
        }

        [UnityTest]
        public IEnumerator UnplayedTapsAndHoldStartsAllBecomeMisses()
        {
            yield return LoadSong();
            yield return Press(Key.Space);
            yield return Completed();
            Assert.That(game.Session.Miss, Is.EqualTo(18));
            Assert.That(game.Session.Combo, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TempoBoundaryHoldIsPerfectWithConstantScroll()
        {
            yield return TempoRun(ScrollMode.Constant);
        }

        [UnityTest]
        public IEnumerator TempoBoundaryHoldIsPerfectWithBpmScroll()
        {
            yield return TempoRun(ScrollMode.Bpm);
        }

        private IEnumerator TempoRun(ScrollMode scroll)
        {
            yield return LoadSong(1, scroll);
            Assert.That(game.Chart.Count, Is.EqualTo(8));
            PrepareInput(true);
            yield return Press(Key.Space);
            yield return Completed();
            Assert.That(game.Session.Perfect, Is.EqualTo(8));
            Assert.That(game.Session.Combo, Is.EqualTo(8));
            Assert.That(game.ReadModel.Snapshot.SongTime, Is.GreaterThanOrEqualTo(12));
        }
    }
}
