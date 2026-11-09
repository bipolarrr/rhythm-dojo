using System;
using NUnit.Framework;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class RhythmSessionTests
    {
        private static RhythmSession Session(params NoteData[] notes) =>
            new RhythmSession(
                new ChartData(notes, 12),
                new GameModeRules(4),
                new TempoMap(new[] { new TempoPoint(0, 120) }),
                new PlayRules { WarmupGood = .11, HoldEnd = HoldEndMode.Release,
                    GaugeFailure = GaugeFailureMode.Continue, RecoveryCombo = 1 }
            );

        [Test]
        [Category("Fast"), Category("Compatibility")]
        public void LegacyHoldRecordsStartAndFinalWithoutChangingScoreCount()
        {
            foreach (var example in new[] { (head: 0d, tail: .08, grade: Judgment.Good),
                (head: .08, tail: 0d, grade: Judgment.Good),
                (head: 0d, tail: 0d, grade: Judgment.Perfect),
                (head: 0d, tail: -1d, grade: Judgment.Miss) })
            {
                var legacy = new RhythmSession(new ChartData(new[] { NoteData.Hold(0, 2, 4) }, 12),
                    new JudgmentSettings(.05, .11), new GameModeRules(4));
                legacy.Start();
                legacy.PressLane(0, 2 + example.head);
                Assert.That(legacy.Resolved, Is.Zero);
                legacy.ReleaseLane(0, 4 + example.tail);
                Assert.That(legacy.Resolved, Is.EqualTo(1));
                Assert.That(legacy.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(example.grade));
            }

            var health = new RhythmSession(new ChartData(new[] { NoteData.Tap(0, 1),
                NoteData.Tap(0, 2), NoteData.Tap(0, 3), NoteData.Hold(1, 4, 5) }, 12),
                new JudgmentSettings(.05, .11), new GameModeRules(4));
            health.Start();
            health.Advance(1.2);
            Assert.That(health.Health, Is.EqualTo(90));
            health.PressLane(0, 2);
            health.ReleaseLane(0, 2.01);
            Assert.That(health.Health, Is.EqualTo(92));
            health.PressLane(0, 3.08);
            health.ReleaseLane(0, 3.09);
            Assert.That(health.Health, Is.EqualTo(93));
            health.PressLane(1, 4);
            Assert.That(health.Health, Is.EqualTo(93));
            health.ReleaseLane(1, 5);
            health.ReleaseLane(1, 5);
            Assert.That(health.Health, Is.EqualTo(95));
            Assert.That(health.Resolved, Is.EqualTo(4));

            var current = Session(NoteData.Hold(0, 1, 2));
            current.Start();
            current.PressLane(0, 1.08);
            Assert.That(current.GetNoteJudgments(0).Start.Value.Grade, Is.EqualTo(Judgment.Good));
            current.ReleaseLane(0, 2);
            Assert.That(current.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Perfect));
            Assert.That(current.Resolved, Is.EqualTo(current.Snapshot.TotalNotes));
            current.Reset();
            Assert.That(current.GetNoteJudgments(0).Start, Is.Null);
            Assert.That(current.GetNoteJudgments(0).End, Is.Null);

            // The public legacy constructor still aggregates one score per hold.
            var s = new RhythmSession(new ChartData(new[] { NoteData.Hold(0, 1, 2), NoteData.Hold(1, 3, 4) }, 12),
                new JudgmentSettings(.05, .11), new GameModeRules(4));
            s.Start();
            s.PressLane(0, 1.08);
            Assert.That(s.Resolved, Is.Zero);
            Assert.That(s.GetNoteJudgments(0).Start.Value.Grade, Is.EqualTo(Judgment.Good));
            Assert.That(s.GetNoteJudgments(0).End, Is.Null);
            s.ReleaseLane(0, 2);
            Assert.That(s.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Good));
            Assert.That(s.Resolved, Is.EqualTo(1));
            s.Advance(5);
            Assert.That(s.GetNoteJudgments(1).Start.Value.Grade, Is.EqualTo(Judgment.Miss));
            Assert.That(s.GetNoteJudgments(1).End, Is.Null);
            s.Reset();
            Assert.That(s.GetNoteJudgments(0).Start, Is.Null);
            Assert.That(s.GetNoteJudgments(0).End, Is.Null);
        }

        [Test]
        [Category("Fast")]
        public void HealthRecoversOnJudgmentsAndHoldIsCountedOnlyOnce()
        {
            var s = Session(NoteData.Tap(0, 1), NoteData.Tap(0, 2), NoteData.Tap(0, 3), NoteData.Hold(1, 4, 5));
            s.Start();
            s.Advance(1.2);
            Assert.That(s.Snapshot.Health, Is.EqualTo(90));
            s.PressLane(0, 2);
            s.ReleaseLane(0, 2.01);
            Assert.That(s.Health, Is.EqualTo(92));
            s.PressLane(0, 3.08);
            s.ReleaseLane(0, 3.09);
            Assert.That(s.Health, Is.EqualTo(92), "Current rules do not recover health on Good.");
            s.PressLane(1, 4);
            s.ReleaseLane(1, 5);
            int resolved = s.Resolved;
            int health = s.Health;
            s.ReleaseLane(1, 5);
            s.Advance(12);
            Assert.That(s.Resolved, Is.EqualTo(resolved));
            Assert.That(s.Health, Is.EqualTo(health));
            Assert.That(health, Is.EqualTo(100));
            s.Start();
            Assert.That(s.Health, Is.EqualTo(100));
            Assert.That(s.Resolved, Is.Zero);
        }

        [Test]
        [Category("Fast")]
        public void ZeroHealthFreezesRemainingNotesAndRestartRestoresFullHealth()
        {
            var notes = new NoteData[12];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = NoteData.Tap(i % 4, .5 + i * .5);
            }

            var s = new RhythmSession(new ChartData(notes, 12), new GameModeRules(4),
                new TempoMap(new[] { new TempoPoint(0, 120) }),
                new PlayRules { WarmupGood = .11, GaugeFailure = GaugeFailureMode.Stop });
            s.Start();
            s.Advance(12);
            Assert.That(s.Health, Is.Zero);
            Assert.That(s.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(s.Miss, Is.EqualTo(10));
            Assert.That(s.GetState(10), Is.EqualTo(NoteState.Pending));
            s.Advance(20);
            s.PressLane(2, 5.5);
            s.ReleaseLane(2, 5.6);
            Assert.That(s.Miss, Is.EqualTo(10));
            Assert.That(s.Health, Is.Zero);
            s.Start();
            Assert.That(s.Health, Is.EqualTo(100));
            Assert.That(s.Resolved, Is.Zero);
            Assert.That(s.State, Is.EqualTo(SessionState.Playing));
        }

        [TestCase(-.05, Judgment.Perfect)]
        [TestCase(.05, Judgment.Perfect)]
        [TestCase(-.05001, Judgment.Good)]
        [TestCase(.05001, Judgment.Good)]
        [TestCase(-.11, Judgment.Good)]
        [TestCase(.11, Judgment.Good)]
        [TestCase(-.11001, Judgment.Miss)]
        [TestCase(.11001, Judgment.Miss)]
        [Category("Fast")]
        public void GradesInclusiveBoundaries(double error, Judgment expected)
        {
            Assert.That(Session(NoteData.Tap(0, 1)).Grade(error), Is.EqualTo(expected));
        }

        [TestCase(-.08)]
        [TestCase(.08)]
        [Category("Fast")]
        public void SignedErrorAndDuplicatePrevention(double error)
        {
            var s = Session(NoteData.Tap(0, 1));
            int events = 0;
            JudgmentEvent last = default;
            s.Judged += e =>
            {
                events++;
                last = e;
            };
            s.Start();
            s.PressLane(0, 1 + error);
            s.PressLane(0, 1);
            s.ReleaseLane(0, 1.09);
            s.PressLane(0, 1.10);
            Assert.That(s.Good, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(last.ErrorSeconds, Is.EqualTo(error).Within(1e-9));
        }

        [Test]
        [Category("Fast")]
        public void NearestCandidateAndStableTieOrder()
        {
            var s = Session(NoteData.Tap(0, 6.5), NoteData.Tap(0, 6.68));
            s.Start();
            s.PressLane(0, 6.59);
            Assert.That(s.GetState(0), Is.EqualTo(NoteState.Completed));
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Pending));
            s.ReleaseLane(0, 6.60);
            s.PressLane(0, 6.68);
            Assert.That(s.Resolved, Is.EqualTo(2));
            s.Start();
            s.PressLane(0, 6.61);
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Completed));
        }

        [TestCase(0, .08, Judgment.Good)]
        [TestCase(.08, 0, Judgment.Good)]
        [TestCase(0, 0, Judgment.Perfect)]
        [TestCase(0, -1, Judgment.Miss)]
        [Category("Fast")]
        public void HoldUsesWorseHeadAndTail(double headError, double tailError, Judgment expected)
        {
            var s = Session(NoteData.Hold(0, 2, 4));
            int heads = 0;
            s.HoldStarted += e =>
            {
                heads++;
                Assert.That(e.Head.NoteIndex, Is.Zero);
            };
            s.Start();
            s.PressLane(0, 2 + headError);
            Assert.That(s.Resolved, Is.EqualTo(1));
            Assert.That(heads, Is.EqualTo(1));
            s.ReleaseLane(0, 4 + tailError);
            var record = s.GetNoteJudgments(0);
            Assert.That(record.Start.Value.Grade, Is.EqualTo(s.Grade(headError)));
            Assert.That(record.End.Value.Grade, Is.EqualTo(s.Grade(tailError)));
            if (expected == Judgment.Miss)
            {
                Assert.That(s.Miss, Is.GreaterThan(0));
            }
            else
            {
                Assert.That(s.Miss, Is.Zero);
            }

            s.Advance(12);
            Assert.That(s.Resolved, Is.EqualTo(s.Snapshot.TotalNotes));
        }

        [Test]
        [Category("Fast")]
        public void AutomaticHoldCompletionKeepsHeadGradeAndIgnoresLateRelease()
        {
            var s = new RhythmSession(new ChartData(new[] { NoteData.Hold(0, 2, 4) }, 12),
                new GameModeRules(4), new TempoMap(new[] { new TempoPoint(0, 120) }),
                new PlayRules { WarmupGood = .11 });
            s.Start();
            s.PressLane(0, 2.08);
            s.Advance(4.12);
            s.ReleaseLane(0, 5);
            Assert.That(s.Good, Is.EqualTo(1));
            Assert.That(s.Resolved, Is.EqualTo(s.Snapshot.TotalNotes));
            Assert.That(s.GetNoteJudgments(0).Start.Value.Grade, Is.EqualTo(Judgment.Good));
            Assert.That(s.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Perfect));
        }

        [Test]
        [Category("Fast")]
        public void ChordsAndOtherLaneDuringHoldWorkAndEmptyPressDoesNotResetCombo()
        {
            var s = Session(NoteData.Hold(0, 2, 4), NoteData.Tap(1, 3), NoteData.Tap(2, 3));
            s.Start();
            s.PressLane(0, 2);
            s.PressLane(1, 3);
            s.PressLane(2, 3);
            s.PressLane(3, 3.5);
            int combo = s.Combo;
            s.PressLane(3, 3.5);
            Assert.That(s.Combo, Is.EqualTo(combo));
            s.ReleaseLane(0, 4);
            Assert.That(s.Combo, Is.EqualTo(s.Snapshot.TotalNotes));
            Assert.That(s.Miss, Is.Zero);
        }

        [Test]
        [Category("Fast")]
        public void TimeoutBoundaryAndResetClearAllState()
        {
            var s = Session(NoteData.Tap(0, 1), NoteData.Hold(1, 2, 4));
            s.Start();
            s.Advance(1.11);
            Assert.That(s.Miss, Is.Zero);
            s.Advance(1.111);
            Assert.That(s.Miss, Is.EqualTo(1));
            s.Advance(12);
            Assert.That(s.State, Is.EqualTo(SessionState.Completed));
            Assert.That(s.Miss, Is.EqualTo(s.Snapshot.TotalNotes));
            s.Start();
            Assert.That(s.Resolved, Is.Zero);
            Assert.That(s.Combo, Is.Zero);
            Assert.That(s.GetState(1), Is.EqualTo(NoteState.Pending));
            Assert.That(s.IsHeld(1), Is.False);
        }

        [Test]
        [Category("Fast")]
        public void SixLanesAndDefensiveCopy()
        {
            var notes = new[] { NoteData.Tap(5, 1) };
            var chart = new ChartData(notes, 3);
            notes[0] = NoteData.Tap(10, 1);

            var s = new RhythmSession(chart, new GameModeRules(6),
                new TempoMap(new[] { new TempoPoint(0, 120) }), new PlayRules());
            s.Start();
            s.PressLane(5, 1);
            Assert.That(s.Perfect, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.PressLane(6, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(double.NaN));
        }

        [Test]
        [Category("Fast")]
        public void MalformedChartAndSettingsFailBeforePlay()
        {
            foreach (
                var notes in new[]
                {
                    new[] { NoteData.Hold(0, 1, 1) },
                    new[] { NoteData.Tap(4, 1) },
                    new[] { NoteData.Tap(0, 2), NoteData.Tap(1, 1) },
                    new[] { NoteData.Hold(0, 1, 2), NoteData.Tap(0, 2) },
                    new[] { NoteData.Tap(0, double.NaN) },
                    Array.Empty<NoteData>(),
                }
            )
            {
                Assert.Throws<InvalidOperationException>(() =>
                    new ChartData(notes, 12).Validate(new GameModeRules(4))
                );
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new JudgmentSettings(.1, .05));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JudgmentSettings(double.NaN, .1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameModeRules(0));
        }
    }
}
