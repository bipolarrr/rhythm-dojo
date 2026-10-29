using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class PlayRulesTests
    {
        private static RhythmSession Create(PlayRules rules, params NoteData[] notes) =>
            new RhythmSession(
                new ChartData(notes, notes.Max(n => n.EndTime) + 1),
                new GameModeRules(4),
                new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(2, 240) }),
                rules
            );

        [TestCase(HoldEndMode.Automatic)]
        [TestCase(HoldEndMode.Release)]
        public void RegrabPreservesEndpointsAndMatchesFrameSweeps(HoldEndMode end)
        {
            var a = Create(new PlayRules { HoldEnd = end }, NoteData.Hold(0, 1, 3), NoteData.Tap(1, 2));
            var b = Create(new PlayRules { HoldEnd = end }, NoteData.Hold(0, 1, 3), NoteData.Tap(1, 2));
            foreach (var session in new[] { a, b })
            {
                session.Start();
                session.PressLane(0, 1);
                session.ReleaseLane(0, 1.5);
                if (end == HoldEndMode.Release)
                {
                    Assert.That(session.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Miss));
                    Assert.That(session.Miss, Is.EqualTo(1));
                }

                session.PressLane(0, 1.6);
                if (session == a)
                {
                    for (double time = 1.61; time < 2; time += .01)
                    {
                        session.Advance(time);
                    }
                }

                session.PressLane(1, 2);
                session.ReleaseLane(0, 3);
                session.Advance(5);
                Assert.That(session.GetNoteJudgments(1).Start.Value.Grade, Is.EqualTo(Judgment.Perfect));
                Assert.That(session.Resolved, Is.EqualTo(session.Snapshot.TotalNotes));
            }

            Assert.That(a.Perfect, Is.EqualTo(b.Perfect));
            Assert.That(a.Miss, Is.EqualTo(b.Miss));
            Assert.That(a.Health, Is.EqualTo(b.Health));
            Assert.That(a.Combo, Is.EqualTo(b.Combo));
            var record = a.GetNoteJudgments(0);
            Assert.That(record.Start.Value.Grade, Is.EqualTo(Judgment.Perfect));
            if (end == HoldEndMode.Release)
            {
                Assert.That(record.End.Value.ErrorSeconds, Is.EqualTo(-1.5));
                Assert.That(a.Miss, Is.EqualTo(20));
            }
            else
            {
                Assert.That(record.End.Value.Grade, Is.EqualTo(Judgment.Perfect));
            }

            a.Start();
            Assert.That(a.GetNoteJudgments(0).Start, Is.Null);
            Assert.That(a.GetNoteJudgments(0).End, Is.Null);
        }

        [TestCase(HoldEndMode.Automatic)]
        [TestCase(HoldEndMode.Release)]
        public void MissedHeadRecoveryDependsOnEndMode(HoldEndMode end)
        {
            var session = Create(new PlayRules { HoldEnd = end }, NoteData.Hold(0, 1, 3));
            session.Start();
            session.PressLane(0, 1.3);
            session.ReleaseLane(0, 3);
            session.Advance(5);
            Assert.That(session.GetNoteJudgments(0).Start.Value.Grade, Is.EqualTo(Judgment.Miss));
            if (end == HoldEndMode.Release)
            {
                Assert.That(session.Perfect, Is.Zero);
                Assert.That(session.Miss, Is.EqualTo(session.Snapshot.TotalNotes));
            }
            else
            {
                Assert.That(session.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Perfect));
            }
        }

        [TestCase(-.1, Judgment.Good)]
        [TestCase(0, Judgment.Perfect)]
        [TestCase(.1, Judgment.Good)]
        [TestCase(.101, Judgment.Miss)]
        public void ReleaseWindowKeepsInclusiveBoundariesAndDoesNotOverwrite(double error, Judgment grade)
        {
            var session = Create(new PlayRules { HoldEnd = HoldEndMode.Release }, NoteData.Hold(0, 1, 2));
            session.Start();
            session.PressLane(0, 1);
            session.ReleaseLane(0, 2 + error);
            var record = session.GetNoteJudgments(0);
            session.PressLane(0, 2.2);
            session.ReleaseLane(0, 2.3);
            session.Advance(5);
            Assert.That(record.End.Value.Grade, Is.EqualTo(grade));
            Assert.That(session.GetNoteJudgments(0).End, Is.EqualTo(record.End));
            Assert.That(session.Resolved, Is.EqualTo(session.Snapshot.TotalNotes));
        }

        [Test]
        public void EarlyReleaseFailureStopsBeforeFutureRecordsAndTicks()
        {
            var session = Create(new PlayRules
            {
                HoldEnd = HoldEndMode.Release,
                StartingHealth = 10,
                GaugeFailure = GaugeFailureMode.Stop,
            }, NoteData.Hold(0, 1, 3), NoteData.Tap(1, 2));
            session.Start();
            session.PressLane(0, 1);
            session.ReleaseLane(0, 1.2);
            int resolved = session.Resolved;
            session.Advance(10);
            Assert.That(session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(session.Resolved, Is.EqualTo(resolved));
            Assert.That(session.GetNoteJudgments(0).End.Value.Grade, Is.EqualTo(Judgment.Miss));
            Assert.That(session.GetNoteJudgments(1).Start, Is.Null);
        }

        [Test]
        public void FixedRecordSlotUpdatesAllocateNoManagedMemory()
        {
            var records = new NoteJudgmentRecord[1];
            var judgment = new JudgmentEvent(0, NoteData.Hold(0, 1, 2), Judgment.Perfect, 0);
            records[0] = new NoteJudgmentRecord(judgment, judgment);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; i++)
            {
                records[0] = new NoteJudgmentRecord(judgment, records[0].End);
                records[0] = new NoteJudgmentRecord(records[0].Start, judgment);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Only fixed result-record slot updates are measured.");
            Assert.That(records[0].End, Is.Not.Null);
        }

        [TestCase(JudgmentProfile.Warmup, .05, .1)]
        [TestCase(JudgmentProfile.Sparring, .016, .05)]
        public void TimingBoundariesAreInclusive(
            JudgmentProfile profile,
            double perfect,
            double good
        )
        {
            var rules = new PlayRules { Profile = profile };

            foreach (double sign in new[] { -1.0, 1.0 })
            {
                Assert.That(
                    JudgmentEvaluator.Grade(sign * perfect, rules.Timing),
                    Is.EqualTo(Judgment.Perfect)
                );
                Assert.That(
                    JudgmentEvaluator.Grade(sign * (perfect + .00001), rules.Timing),
                    Is.EqualTo(Judgment.Good)
                );
                Assert.That(
                    JudgmentEvaluator.Grade(sign * good, rules.Timing),
                    Is.EqualTo(Judgment.Good)
                );
                Assert.That(
                    JudgmentEvaluator.Grade(sign * (good + .00001), rules.Timing),
                    Is.EqualTo(Judgment.Miss)
                );
            }
        }

        [Test]
        public void RecoveryStartsAtTenGoodKeepsComboWithoutRecoveryAndMissResets()
        {
            var rules = new PlayRules();
            Assert.That(GaugeRules.Apply(80, 9, Judgment.Perfect, rules, false), Is.EqualTo(80));
            Assert.That(GaugeRules.Apply(80, 10, Judgment.Perfect, rules, false), Is.EqualTo(82));
            Assert.That(GaugeRules.Apply(80, 11, Judgment.Good, rules, false), Is.EqualTo(80));
            Assert.That(ComboRules.Apply(10, Judgment.Good), Is.EqualTo(11));
            Assert.That(ComboRules.Apply(10, Judgment.Miss), Is.Zero);
            Assert.That(GaugeRules.Apply(99, 10, Judgment.Perfect, rules, false), Is.EqualTo(100));
        }

        [TestCase(GaugeFailureMode.Continue)]
        [TestCase(GaugeFailureMode.Stop)]
        public void FailureIsLatchedOnceAndRestartClearsIt(GaugeFailureMode mode)
        {
            var rules = new PlayRules { StartingHealth = 10, GaugeFailure = mode };
            var session = Create(
                rules,
                Enumerable.Range(1, 15).Select(i => NoteData.Tap(0, i)).ToArray()
            );
            int failures = 0;
            session.Failed += _ => failures++;
            session.Start();
            session.Advance(1.2);
            Assert.That(session.Failure.Failed, Is.True);
            Assert.That(session.Failure.Time, Is.EqualTo(1.1).Within(1e-9));

            if (mode == GaugeFailureMode.Continue)
            {
                for (int i = 2; i <= 15; i++)
                {
                    session.PressLane(0, i);
                    session.ReleaseLane(0, i + .01);
                }

                session.Advance(17);
                Assert.That(session.State, Is.EqualTo(SessionState.Completed));
                Assert.That(session.Perfect, Is.EqualTo(14));
            }
            else
            {
                Assert.That(session.State, Is.EqualTo(SessionState.GameOver));
            }

            Assert.That(session.Health, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
            rules.StartingHealth = 100;
            session.Start();
            Assert.That(session.Failure.Failed, Is.False);
            Assert.That(session.Health, Is.EqualTo(10), "The session owns its initial rule copy.");
        }

        [TestCase(1, 1)]
        [TestCase(2, 0)]
        [TestCase(3, 0)]
        public void ChallengeAllowsLimitThenStops(int preset, int allowed)
        {
            var rules = new PlayRules { Mode = PlayMode.Challenge };
            rules.SelectChallenge(preset);

            var session = Create(rules, NoteData.Tap(0, 1), NoteData.Tap(0, 2), NoteData.Tap(0, 3));
            session.Start();
            session.Advance(10);
            Assert.That(session.Miss, Is.EqualTo(allowed + 1));
            Assert.That(session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(session.Health, Is.EqualTo(100), "Challenge does not apply Gauge.");
            Assert.That(ChallengeRules.Exceeded(1000000, 1000000, 1, new PlayRules()), Is.False);
        }

        [Test]
        public void HoldScheduleCrossesTempoChangeAndHasFixedDenominator()
        {
            var session = Create(new PlayRules(), NoteData.Hold(0, 1, 3));

            // 2 beats before the tempo change, 4 after it: 23 interior ticks + head + tail.
            Assert.That(session.Snapshot.TotalNotes, Is.EqualTo(25));
            session.Start();
            session.PressLane(0, 1.075);
            session.Advance(3);
            session.ReleaseLane(0, 3);
            session.Advance(4);
            Assert.That(session.Good, Is.EqualTo(1));
            Assert.That(session.Perfect, Is.EqualTo(24));
            Assert.That(session.Resolved, Is.EqualTo(25));
        }

        [Test]
        public void ReleaseAndRegrabResultsDoNotDependOnFrameSweeps()
        {
            var a = Create(new PlayRules(), NoteData.Hold(0, 1, 3));
            var b = Create(new PlayRules(), NoteData.Hold(0, 1, 3));
            void Run(RhythmSession session, bool frames)
            {
                session.Start();
                session.PressLane(0, 1);

                if (frames)
                {
                    for (double t = 1.01; t < 1.5; t += .01)
                    {
                        session.Advance(t);
                    }
                }

                session.ReleaseLane(0, 1.5);
                Assert.That(session.Combo, Is.Zero);

                if (frames)
                {
                    for (double t = 1.51; t < 2; t += .01)
                    {
                        session.Advance(t);
                    }
                }

                session.PressLane(0, 2);
                session.ReleaseLane(0, 3);
                session.Advance(4);
            }

            Run(a, true);
            Run(b, false);
            Assert.That(a.Perfect, Is.EqualTo(b.Perfect));
            Assert.That(a.Miss, Is.EqualTo(b.Miss));
            Assert.That(a.Miss, Is.GreaterThan(0));
            Assert.That(a.Resolved, Is.EqualTo(a.Snapshot.TotalNotes));
        }

        [TestCase(HoldEndMode.Automatic, 0)]
        [TestCase(HoldEndMode.Release, 1)]
        public void EndIsCountedOnce(HoldEndMode end, int good)
        {
            var session = Create(new PlayRules { HoldEnd = end }, NoteData.Hold(0, 1, 2));
            session.Start();
            session.PressLane(0, 1);
            session.ReleaseLane(0, 2.075);
            session.Advance(4);
            Assert.That(session.Good, Is.EqualTo(good));
            Assert.That(session.Resolved, Is.EqualTo(session.Snapshot.TotalNotes));
        }

        [Test]
        public void ShortAutomaticTailAtHeadDeadlineResolvesBeforeRelease()
        {
            var session = Create(new PlayRules(), NoteData.Hold(0, 1, 1.1));
            session.Start();
            session.PressLane(0, 1);
            session.ReleaseLane(0, 1.1);
            session.Advance(3);
            Assert.That(session.Perfect, Is.EqualTo(2));
            Assert.That(session.Miss, Is.Zero);
        }
    }
}
