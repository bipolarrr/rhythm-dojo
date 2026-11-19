using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    [Category("Fast")]
    public sealed class BackgroundProgressTests
    {
        private static readonly TempoMap Tempo = new TempoMap(new[]
        {
            new TempoPoint(0, 120), new TempoPoint(2, 240),
        });

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FlawlessFinishRequiresWholeSongGoodOrPerfectAndEveryObservedUnit(int mistake)
        {
            var chart = new ChartData(new[]
            {
                NoteData.Tap(0, .5), NoteData.Hold(1, 1, 1.8), NoteData.Tap(0, 3),
            }, 4);
            var session = new RhythmSession(chart, new GameModeRules(2), Tempo, new PlayRules());
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 4, 0);
            session.Start();
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            session.JudgmentResolved += result =>
            {
                if (mistake == 3 && result.Unit.Note == 0)
                {
                    return; // An incomplete observation stream can never authorize destruction.
                }
                performance.Consume(result);
                performance.Consume(result); // Duplicate delivery cannot fill the missing unit.
            };
            session.InputResolved += performance.Consume;
            if (mistake == 1)
            {
                session.PressLane(1, .2);
                session.ReleaseLane(1, .21);
            }
            session.PressLane(0, .575);
            session.ReleaseLane(0, .58);
            session.PressLane(1, 1.075);
            if (mistake == 2)
            {
                session.ReleaseLane(1, 1.14);
                session.PressLane(1, 1.15); // Recovery before the next check still records an interruption.
            }
            session.ReleaseLane(1, 1.8);
            session.PressLane(0, 3.075);
            Assert.That(performance.CanFinishFlawlessly(session.Snapshot), Is.False,
                "Finishing the last note is insufficient before Completed.");
            session.Advance(4);
            Assert.That(session.Good, Is.GreaterThan(0));
            Assert.That(session.Miss, Is.Zero);
            Assert.That(performance.Progress(3), Is.EqualTo(1));
            Assert.That(performance.CanFinishFlawlessly(session.Snapshot), Is.EqualTo(mistake == 0));
            performance.Reset(Guid.NewGuid());
            Assert.That(performance.CanFinishFlawlessly(session.Snapshot), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestartInsideExistingJudgedCallbackCannotRelabelOldFactAsNewSession(bool legacy)
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 1) }, 8);
            var rules = new PlayRules();
            RhythmSession session;
            if (legacy)
            {
                session = new RhythmSession(chart, rules.Timing, new GameModeRules(2));
            }
            else
            {
                session = new RhythmSession(chart, new GameModeRules(2), Tempo, rules);
            }
            session.Start();
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 8, 0);
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            session.Judged += result =>
            {
                session.Start();
                performance.Reset(session.SessionId);
            };
            session.JudgmentResolved += result => performance.Consume(result);
            session.PressLane(0, 1);
            Assert.That(performance.Progress(0), Is.Zero);
            Assert.That(session.Resolved, Is.Zero);
        }

        [TestCase(HoldEndMode.Automatic)]
        [TestCase(HoldEndMode.Release)]
        public void HoldAcrossBoundaryUsesActualUniqueCheckUnitsWithOneTotalContribution(HoldEndMode end)
        {
            var chart = new ChartData(new[] { NoteData.Hold(0, 1, 3) }, 8);
            var rules = new PlayRules { HoldEnd = end };
            var session = new RhythmSession(chart, new GameModeRules(2), Tempo, rules);
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 8, 0);
            session.Start();
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            var events = new List<JudgmentResolvedEvent>();
            session.JudgmentResolved += result =>
            {
                events.Add(result);
                Assert.That(performance.Consume(result), Is.True);
                Assert.That(performance.Consume(result), Is.False);
            };
            session.PressLane(0, 1);
            session.Advance(2);
            session.ReleaseLane(0, 3);
            session.Advance(8);

            Assert.That(events.Select(e => e.Unit.Id).Distinct().Count(), Is.EqualTo(events.Count));
            Assert.That(events.Sum(e => e.Unit.Weight), Is.EqualTo(1).Within(1e-10));
            Assert.That(events.Count, Is.EqualTo(session.Snapshot.TotalNotes));
            Assert.That(performance.Progress(0), Is.EqualTo(1).Within(1e-10));
            Assert.That(performance.Progress(1), Is.EqualTo(1).Within(1e-10));
            Assert.That(performance.Progress(2), Is.Zero);
            Assert.That(plan.PhaseOf(events.Single(e => e.Unit.ScheduledTime == 2).Unit.Id), Is.EqualTo(1));
        }

        [Test]
        public void EarlyAndLateInputsCreditScheduledPhaseAndGoodEqualsPerfect()
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 1.98), NoteData.Tap(1, 2.02) }, 8);
            var session = new RhythmSession(chart, new GameModeRules(2), Tempo, new PlayRules());
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 8, 0);
            session.Start();
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            session.JudgmentResolved += result => performance.Consume(result);
            session.PressLane(1, 1.94);
            Assert.That(performance.Progress(0), Is.Zero);
            Assert.That(performance.Progress(1), Is.EqualTo(1));
            session.PressLane(0, 2.06);
            Assert.That(performance.Progress(0), Is.EqualTo(1));
            Assert.That(session.Good, Is.EqualTo(2));
        }

        [Test]
        public void MissEmptyInputRepeatedDownAndStaleSessionCannotCreateProgress()
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 1), NoteData.Tap(0, 1.8) }, 8);
            var session = new RhythmSession(chart, new GameModeRules(2), Tempo, new PlayRules());
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 8, 0);
            session.Start();
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            JudgmentResolvedEvent previous = default;
            session.JudgmentResolved += result =>
            {
                previous = result;
                performance.Consume(result);
            };
            session.InputResolved += performance.Consume;
            session.PressLane(1, .5);
            session.PressLane(1, .6);
            Assert.That(performance.Mistakes, Is.EqualTo(1));
            session.Advance(1.2);
            Assert.That(performance.Progress(0), Is.Zero);
            session.PressLane(0, 1.8);
            session.PressLane(0, 1.8);
            Assert.That(performance.Progress(0), Is.EqualTo(.5));
            session.Start();
            performance.Reset(session.SessionId);
            Assert.That(performance.Consume(previous), Is.False);
            Assert.That(performance.Progress(0), Is.Zero);
        }

        [Test]
        public void AutomaticHoldInterruptionBetweenChecksIsObservedWithoutInventingJudgments()
        {
            var chart = new ChartData(new[] { NoteData.Hold(0, 1, 2) }, 8);
            var session = new RhythmSession(chart, new GameModeRules(2), Tempo, new PlayRules());
            var inputs = new List<InputResolutionEvent>();
            session.InputResolved += inputs.Add;
            session.Start();
            session.PressLane(0, 1);
            int count = session.Resolved;
            session.ReleaseLane(0, 1.04);
            session.PressLane(0, 1.06);
            Assert.That(session.Resolved, Is.EqualTo(count));
            Assert.That(session.Miss, Is.Zero);
            Assert.That(inputs.Select(e => e.Resolution), Is.EqualTo(new[]
            {
                InputResolution.AcceptedHead, InputResolution.HoldInterrupted, InputResolution.HoldRecovery,
            }));
        }

        [Test]
        [Category("Compatibility")]
        public void LegacyHoldHasOnlyHeadAndResolveAndMissDoesNotFillUnemittedTail()
        {
            var chart = new ChartData(new[] { NoteData.Hold(0, 1, 1.8) }, 8);
            var session = new RhythmSession(chart, new JudgmentSettings(.05, .1), new GameModeRules(2));
            var plan = new BackgroundPhasePlan(chart, session.Schedule, 8, 0);
            session.Start();
            var performance = new BackgroundPerformanceState(plan, session.SessionId);
            var events = new List<JudgmentResolvedEvent>();
            session.JudgmentResolved += result =>
            {
                events.Add(result);
                performance.Consume(result);
            };
            session.PressLane(0, 1);
            Assert.That(performance.Progress(0), Is.EqualTo(.5));
            session.ReleaseLane(0, 1.8);
            Assert.That(performance.Progress(0), Is.EqualTo(1));
            Assert.That(events.Count, Is.EqualTo(2));
            session.Start();
            performance.Reset(session.SessionId);
            events.Clear();
            session.Advance(3);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(performance.Progress(0), Is.Zero);
        }

        [Test]
        public void OffsetEmptyPhaseAndVeryShortSongUseMusicTimeRegardlessOfPerformance()
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 2) }, 9);
            var schedule = new JudgmentSchedule(chart, Tempo, new PlayRules());
            var plan = new BackgroundPhasePlan(chart, schedule, 10, 1);
            Assert.That(plan.Duration, Is.EqualTo(8));
            Assert.That(plan.PhaseAt(-1), Is.Zero);
            Assert.That(plan.PhaseAt(3), Is.EqualTo(1));
            Assert.That(plan.PhaseAt(5), Is.EqualTo(2));
            Assert.That(plan.PhaseAt(7), Is.EqualTo(3));
            Assert.That(plan.PhaseAt(20), Is.EqualTo(3));
            var performance = new BackgroundPerformanceState(plan, Guid.NewGuid());
            Assert.That(performance.Progress(1), Is.Zero);
            var shortPlan = new BackgroundPhasePlan(chart, schedule, .008, 0);
            Assert.That(shortPlan.TransitionDuration, Is.EqualTo(.0002).Within(1e-10));
            Assert.That(shortPlan.PhaseAt(.006), Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => new BackgroundPhasePlan(chart, schedule, 1, 9));
        }
    }
}
