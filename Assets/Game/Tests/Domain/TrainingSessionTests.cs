using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    [Category("Training")]
    public sealed class TrainingSessionTests
    {
        private static RhythmSession Play(bool hit, double error = 0)
        {
            var session = new RhythmSession(new ChartData(new[] { NoteData.Tap(0, 1) }, 2),
                new GameModeRules(4), new TempoMap(new[] { new TempoPoint(0, 100) }), new PlayRules());
            session.Start();
            if (hit)
            {
                session.PressLane(0, 1 + error);
            }
            session.Advance(3);
            return session;
        }

        [TestCase(.05, TrainingOutcome.Succeeded)]
        [TestCase(.051, TrainingOutcome.Failed)]
        [Category("Fast")]
        public void ChordSpreadRejectsSeparatedPerfectInputsAndResetClearsHistory(double spread, TrainingOutcome outcome)
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 1), NoteData.Tap(2, 1) }, 2);
            var playback = new TrainingPlayback(chart, false, .05);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var game = new RhythmSession(chart, new GameModeRules(4),
                    new TempoMap(new[] { new TempoPoint(0, 100) }), new PlayRules());
                var session = new TrainingSession("chord", 1, TrainingStage.Assessment,
                    new TrainingCriteria(0, 90), false);
                game.Start();
                playback.Press(game, 0, 1 - spread / 2);
                game.ReleaseLane(0, 1);
                playback.Press(game, 2, 1 + spread / 2);
                game.ReleaseLane(2, 1.1);
                game.Advance(2);
                Assert.That(game.Snapshot.Perfect, Is.EqualTo(2));
                session.BeginAttempt();
                session.MarkPlaying();
                session.Finish(session.Attempt, game.Snapshot, playback.Inputs);
                Assert.That(session.Outcome, Is.EqualTo(outcome));
                Assert.That(session.ExtraInputs, Is.Zero);
                playback.Reset();
                Assert.That(playback.Inputs, Is.Empty);
            }
        }

        [TestCase(.5, TrainingOutcome.Failed)]
        [TestCase(1, TrainingOutcome.Succeeded)]
        [TestCase(1.2, TrainingOutcome.Failed)]
        [Category("Fast")]
        public void HoldTailRequiresReleaseAndHoldTicksCannotHideAnExtraPress(double release, TrainingOutcome outcome)
        {
            var chart = new ChartData(new[] { NoteData.Hold(0, .3, 1), NoteData.Tap(2, .6) }, 2);
            var game = new RhythmSession(chart, new GameModeRules(4),
                new TempoMap(new[] { new TempoPoint(0, 100) }), new PlayRules { HoldEnd = HoldEndMode.Release });
            var playback = new TrainingPlayback(chart, false);
            game.Start();
            playback.Press(game, 0, .3);
            if (release < .6)
            {
                game.ReleaseLane(0, release);
            }
            playback.Press(game, 2, .6);
            game.ReleaseLane(2, .61);
            if (release >= .6)
            {
                game.ReleaseLane(0, release);
            }
            game.Advance(2);
            var session = new TrainingSession("hold", 1, TrainingStage.Assessment,
                new TrainingCriteria(0, 90), false);
            session.BeginAttempt();
            session.MarkPlaying();
            session.Finish(session.Attempt, game.Snapshot, playback.Inputs);
            Assert.That(session.Outcome, Is.EqualTo(outcome));
            Assert.That(session.ExtraInputs, Is.Zero, "Valid hold heads and taps must be matched.");

            game = new RhythmSession(chart, new GameModeRules(4),
                new TempoMap(new[] { new TempoPoint(0, 100) }), new PlayRules());
            playback.Reset();
            game.Start();
            playback.Press(game, 0, .3);
            playback.Press(game, 3, .5);
            Assert.That(game.Snapshot.Perfect, Is.GreaterThan(1), "The unrelated press sweeps a held tick.");
            Assert.That(playback.ExtraInputs, Is.EqualTo(1), "Only a head judgment can match the new press.");
        }

        [TestCase(true, 0, 100, TrainingOutcome.Succeeded)]
        [TestCase(true, .08, 50, TrainingOutcome.Succeeded)]
        [TestCase(true, .08, 51, TrainingOutcome.Failed)]
        [TestCase(false, 0, 0, TrainingOutcome.Failed)]
        [Category("Fast")]
        public void ExistingJudgmentsDetermineTrainingOutcome(bool hit, double error,
            double accuracy, TrainingOutcome outcome)
        {
            var session = new TrainingSession("lesson", 1, TrainingStage.Practice,
                new TrainingCriteria(0, accuracy), false);
            session.BeginAttempt();
            session.MarkPlaying();
            Assert.That(session.Finish(session.Attempt, Play(hit, error).Snapshot), Is.True);
            Assert.That(session.Outcome, Is.EqualTo(outcome));
        }

        [Test]
        [Category("Fast")]
        public void RestartRejectsOldAndDuplicateResultsAndFailurePreservesCompletion()
        {
            var session = new TrainingSession("lesson", 1, TrainingStage.Practice,
                new TrainingCriteria(0, 50), false);
            session.BeginAttempt();
            session.MarkPlaying();
            int first = session.Attempt;
            Assert.Throws<System.InvalidOperationException>(() => session.BeginAttempt());
            session.ResetPlayback();
            session.MarkPlaying();
            Assert.That(session.Finish(first, Play(true).Snapshot), Is.False);
            Assert.That(session.Finish(session.Attempt, Play(true).Snapshot), Is.True);
            Assert.That(session.Finish(session.Attempt, Play(false).Snapshot), Is.False);
            Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));

            session.BeginAttempt();
            Assert.That(session.Result, Is.Null);
            session.MarkPlaying();
            session.Finish(session.Attempt, Play(false).Snapshot);
            Assert.That(session.Completed, Is.True);
            Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Failed));
            session.BeginAttempt();
            session.Abort();
            Assert.That(session.Completed, Is.True);
            Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Aborted));
            Assert.That(session.Finish(session.Attempt, Play(true).Snapshot), Is.False);
        }

        [Test]
        [Category("Fast")]
        public void EmptyUnfinishedAndEngineFailureCannotCompleteTraining()
        {
            var criteria = new TrainingCriteria(100, 0);
            Assert.That(criteria.IsSatisfied(new SessionSnapshot(SessionState.Completed, 0, 0, 0, 0, 0)), Is.False);
            Assert.That(criteria.IsSatisfied(new SessionSnapshot(SessionState.Playing, 1, 0, 0, 1, 1)), Is.False);
            Assert.That(criteria.IsSatisfied(new SessionSnapshot(SessionState.Completed, 1, 0, 0, 1, 2)), Is.False);
            var rules = new PlayRules { StartingHealth = 1, GaugeFailure = GaugeFailureMode.Continue };
            var game = new RhythmSession(new ChartData(new[] { NoteData.Tap(0, 1) }, 2),
                new GameModeRules(4), new TempoMap(new[] { new TempoPoint(0, 100) }), rules);
            game.Start();
            game.Advance(3);
            Assert.That(game.Snapshot.Failure.Failed, Is.True);
            Assert.That(criteria.IsSatisfied(game.Snapshot), Is.False);
        }

        [Test]
        [Category("Fast")]
        public void GuidedStagesRequireAssessmentAndExtraPressesCannotPass()
        {
            var session = new TrainingSession("trill", 1, TrainingStage.Assessment,
                new TrainingCriteria(0, 90), false, true);
            foreach (var step in new[] { TrainingStep.Demonstration, TrainingStep.Imitation,
                TrainingStep.Application, TrainingStep.Assessment })
            {
                session.BeginAttempt();
                session.MarkPlaying();
                Assert.That(session.Step, Is.EqualTo(step));
                session.Finish(session.Attempt, Play(true).Snapshot,
                    new[] { new TrainingInputRecord(2, .5, false) });
                Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Failed));
                Assert.That(session.Completed, Is.False);
                Assert.That(session.NextStep, Is.EqualTo(step));
                session.BeginAttempt();
                session.MarkPlaying();
                session.Finish(session.Attempt, Play(true).Snapshot);
                Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));
                Assert.That(session.Completed, Is.EqualTo(step == TrainingStep.Assessment));
            }
            session.ReturnToSelection();
            Assert.That(session.Step, Is.EqualTo(TrainingStep.Demonstration));
            Assert.That(session.Completed, Is.True);
            Assert.That(session.Result, Is.Null);
            Assert.That(session.Inputs, Is.Empty);
        }
    }
}
