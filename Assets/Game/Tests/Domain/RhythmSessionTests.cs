using System;
using NUnit.Framework;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class RhythmSessionTests
    {
        private static RhythmSession Session(params NoteData[] notes) => new RhythmSession(new ChartData(notes, 12),
            new JudgmentSettings(.05, .11), new GameModeRules(4));
        [TestCase(-.05, Judgment.Perfect)] [TestCase(.05, Judgment.Perfect)]
        [TestCase(-.05001, Judgment.Good)] [TestCase(.05001, Judgment.Good)]
        [TestCase(-.11, Judgment.Good)] [TestCase(.11, Judgment.Good)]
        [TestCase(-.11001, Judgment.Miss)] [TestCase(.11001, Judgment.Miss)]
        public void GradesInclusiveBoundaries(double error, Judgment expected)
        { Assert.That(Session(NoteData.Tap(0, 1)).Grade(error), Is.EqualTo(expected)); }
        [TestCase(-.08)] [TestCase(.08)]
        public void SignedErrorAndDuplicatePrevention(double error)
        {
            var s = Session(NoteData.Tap(0, 1)); int events = 0; JudgmentEvent last = default;
            s.Judged += e => { events++; last = e; }; s.Start();
            s.PressLane(0, 1 + error); s.PressLane(0, 1); s.ReleaseLane(0, 1.09); s.PressLane(0, 1.10);
            Assert.That(s.Good, Is.EqualTo(1)); Assert.That(events, Is.EqualTo(1));
            Assert.That(last.ErrorSeconds, Is.EqualTo(error).Within(1e-9));
        }
        [Test]
        public void NearestCandidateAndStableTieOrder()
        {
            var s = Session(NoteData.Tap(0, 6.5), NoteData.Tap(0, 6.68)); s.Start(); s.PressLane(0, 6.59);
            Assert.That(s.GetState(0), Is.EqualTo(NoteState.Completed));
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Pending));
            s.ReleaseLane(0, 6.60); s.PressLane(0, 6.68); Assert.That(s.Resolved, Is.EqualTo(2));
            s.Start(); s.PressLane(0, 6.61);
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Completed));
        }
        [TestCase(0, .08, Judgment.Good)] [TestCase(.08, 0, Judgment.Good)]
        [TestCase(0, 0, Judgment.Perfect)] [TestCase(0, -1, Judgment.Miss)]
        public void HoldUsesWorseHeadAndTail(double headError, double tailError, Judgment expected)
        {
            var s = Session(NoteData.Hold(0, 2, 4)); int heads = 0;
            s.HoldStarted += e => { heads++; Assert.That(e.Head.NoteIndex, Is.Zero); };
            s.Start(); s.PressLane(0, 2 + headError);
            Assert.That(s.Resolved, Is.Zero); Assert.That(heads, Is.EqualTo(1));
            s.ReleaseLane(0, 4 + tailError);
            Assert.That(s.Resolved, Is.EqualTo(1));
            Assert.That(s.GetState(0), Is.EqualTo(expected == Judgment.Miss ? NoteState.Missed : NoteState.Completed));
            Assert.That(expected == Judgment.Perfect ? s.Perfect : expected == Judgment.Good ? s.Good : s.Miss, Is.EqualTo(1));
        }
        [Test]
        public void AutomaticHoldCompletionKeepsHeadGradeAndIgnoresLateRelease()
        {
            var s = Session(NoteData.Hold(0, 2, 4)); s.Start(); s.PressLane(0, 2.08); s.Advance(4.12);
            s.ReleaseLane(0, 5); Assert.That(s.Good, Is.EqualTo(1)); Assert.That(s.Resolved, Is.EqualTo(1));
        }
        [Test]
        public void ChordsAndOtherLaneDuringHoldWorkAndEmptyPressDoesNotResetCombo()
        {
            var s = Session(NoteData.Hold(0, 2, 4), NoteData.Tap(1, 3), NoteData.Tap(2, 3)); s.Start();
            s.PressLane(0, 2); s.PressLane(1, 3); s.PressLane(2, 3); s.PressLane(3, 3.5);
            Assert.That(s.Combo, Is.EqualTo(2)); s.ReleaseLane(0, 4); Assert.That(s.Combo, Is.EqualTo(3));
        }
        [Test]
        public void TimeoutBoundaryAndResetClearAllState()
        {
            var s = Session(NoteData.Tap(0, 1), NoteData.Hold(1, 2, 4)); s.Start(); s.Advance(1.11);
            Assert.That(s.Miss, Is.Zero); s.Advance(1.111); Assert.That(s.Miss, Is.EqualTo(1));
            s.Advance(12); Assert.That(s.State, Is.EqualTo(SessionState.Completed)); Assert.That(s.Miss, Is.EqualTo(2));
            s.Start(); Assert.That(s.Resolved, Is.Zero); Assert.That(s.Combo, Is.Zero);
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Pending)); Assert.That(s.IsHeld(1), Is.False);
        }
        [Test]
        public void SixLanesAndDefensiveCopy()
        {
            var notes = new[] { NoteData.Tap(5, 1) }; var chart = new ChartData(notes, 3); notes[0] = NoteData.Tap(10, 1);
            var s = new RhythmSession(chart, new JudgmentSettings(.05,.11), new GameModeRules(6));
            s.Start(); s.PressLane(5, 1); Assert.That(s.Perfect, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.PressLane(6, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(double.NaN));
        }
        [Test]
        public void MalformedChartAndSettingsFailBeforePlay()
        {
            foreach (var notes in new[] {
                new[] { NoteData.Hold(0,1,1) }, new[] { NoteData.Tap(4,1) },
                new[] { NoteData.Tap(0,2), NoteData.Tap(1,1) },
                new[] { NoteData.Hold(0,1,2), NoteData.Tap(0,2) },
                new[] { NoteData.Tap(0,double.NaN) }, Array.Empty<NoteData>() })
                Assert.Throws<InvalidOperationException>(() => new ChartData(notes,12).Validate(new GameModeRules(4)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JudgmentSettings(.1,.05));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JudgmentSettings(double.NaN,.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameModeRules(0));
        }
    }
}
