using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
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
    // Real scenes, DSP clock and timestamped keyboard input; no assigned health or result states.
    [Category("Gameplay"), Category("HealthPlaytest")]
    [Category("Results")]
    public sealed class GameplayHealthPlaytestTests : GameplayTestContext
    {
        private RhythmGameController game;
        private RhythmSession session;
        private GameplayHudView view;
        private readonly List<(double time, Key key, bool press)> events =
            new List<(double, Key, bool)>();
        private readonly HashSet<Key> held = new HashSet<Key>();
        private readonly List<(Judgment grade, int health, double ratio)> judgments =
            new List<(Judgment, int, double)>();
        private readonly List<string> evidence = new List<string>();
        private int nextEvent;
        private string evidencePath;

        private IEnumerator LoadGameplay(
            string name,
            bool hard = false,
            bool fractional = false,
            bool continues = false
        )
        {
            evidencePath = "Logs/health-playtest-" + name + ".csv";
            evidence.Clear();
            evidence.Add("judgment,health,healthRatio,state,songTime");
            judgments.Clear();
            events.Clear();
            held.Clear();
            nextEvent = 0;

            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.HardMode.Enabled = hard;

            var rules = new PlayRules();

            if (hard)
            {
                rules.Mode = RhythmDojo.Gameplay.PlayMode.Challenge;
            }
            else
            {
                rules.Mode = RhythmDojo.Gameplay.PlayMode.Gauge;
            }

            if (continues)
            {
                rules.GaugeFailure = GaugeFailureMode.Continue;
            }
            else
            {
                rules.GaugeFailure = GaugeFailureMode.Stop;
            }

            rules.SelectChallenge(3);

            if (fractional)
            {
                rules.MissLimit = 2;
            }

            flow.Rules.Update(rules);

            if (fractional)
            {
                flow.HardMode.SetRule(Judgment.Miss, HealthRuleAction.Damage, 3);
            }

            flow.Selection.UpdateSelection(
                "test-pulse",
                flow.Settings.defaultDifficulty,
                ScrollMode.Constant,
                1
            );
            flow.PlaySelected();
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
            yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            session = game.Session;
            view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(session.State, Is.EqualTo(SessionState.Ready));
            game.ReadModel.Judged += RecordJudgment;
            InputSystem.onBeforeUpdate += FeedInput;
        }

        private void RecordJudgment(JudgmentEvent result)
        {
            var snapshot = session.Snapshot;
            judgments.Add((result.Grade, snapshot.Health, snapshot.HealthRatio));
            evidence.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2:R},{3},{4:F4}",
                    result.Grade,
                    snapshot.Health,
                    snapshot.HealthRatio,
                    snapshot.State,
                    game.Clock.SongTime
                )
            );
        }

        private void PrepareInput(int missedNotes, int goodNote = -1)
        {
            var keys = new[] { Key.D, Key.F, Key.J, Key.K };
            var settings = UnityEngine
                .Object.FindFirstObjectByType<AppFlowController>()
                .CurrentRequest.Rules.Timing;
            double goodOffset = (settings.PerfectWindow + settings.GoodWindow) / 2;

            for (int i = missedNotes; i < game.Chart.Count; i++)
            {
                var note = game.Chart[i];
                double time;

                if (i == goodNote)
                {
                    time = note.StartTime + goodOffset;
                }
                else
                {
                    time = note.StartTime + 0;
                }

                events.Add((time, keys[note.Lane], true));
                events.Add(
                    (
                        (
                            note.Kind switch
                            {
                                NoteKind.Hold => note.EndTime + .025,
                                _ => time + .025,
                            }
                        ),
                        keys[note.Lane],
                        false
                    )
                );
            }

            events.Sort((a, b) => a.time.CompareTo(b.time));
        }

        private void FeedInput()
        {
            if (
                (InputState.currentUpdateType != InputUpdateType.Dynamic || !game)
                || session == null
            )
            {
                return;
            }

            if (session.State != SessionState.Playing)
            {
                return;
            }

            while (nextEvent < events.Count && game.Clock.SongTime >= events[nextEvent].time)
            {
                var input = events[nextEvent++];

                if (input.press)
                {
                    held.Add(input.key);
                }
                else
                {
                    held.Remove(input.key);
                }

                double timestamp =
                    Time.realtimeSinceStartupAsDouble - (game.Clock.SongTime - input.time);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()), timestamp);
            }
        }

        private IEnumerator CheckHealth(int health, string capture)
        {
            yield return null;
            Assert.That(session.Health, Is.EqualTo(health));
            Assert.That(
                view.healthFill.rectTransform.anchorMax.x,
                Is.EqualTo((float)session.Snapshot.HealthRatio).Within(.001)
            );
            Assert.That(
                view.healthLabel.text,
                Does.Contain(
                    (
                        session.Snapshot.Mode switch
                        {
                            RhythmDojo.Gameplay.PlayMode.Gauge => health + " / 100",
                            _ => "Challenge",
                        }
                    )
                )
            );
            Assert.That(view.gameOverPanel.activeSelf, Is.False);
        }

        private IEnumerator CheckResult(SessionState state, string capture)
        {
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<ResultsScreen>(), "Expected result: " + state);
            yield return null;
            var result = UnityEngine.Object.FindFirstObjectByType<AppFlowController>().LastResult;
            Assert.That(result.Session.State, Is.EqualTo(state));
            Assert.That(result.Session.Resolved, Is.EqualTo(session.Resolved));
            var resultView = UnityEngine.Object.FindFirstObjectByType<ResultsView>();

            if (state == SessionState.Completed && !session.Failure.Failed)
            {
                Assert.That(resultView.outcome.text, Is.EqualTo("CLEAR"));
            }
            else
            {
                Assert.That(resultView.outcome.text, Is.EqualTo("FAILED"));
            }

            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>(), Is.Null);
            int resolved = session.Resolved;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(result.Session.Resolved, Is.EqualTo(resolved));
        }

        private IEnumerator RestartFromResults()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<ResultsView>().backButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            UnityEngine.Object.FindFirstObjectByType<AppFlowController>().PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            session = game.Session;
            view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            yield return Press(Key.Space);
        }

        [UnityTest]
        public IEnumerator NormalMissesDamagePerfectAndGoodRecoverThenClear()
        {
            yield return LoadGameplay("normal-clear");
            PrepareInput(2, 3);
            yield return Press(Key.Space);
            yield return Await(() => session.Miss == 2);
            yield return CheckHealth(80, "normal-damage");
            yield return Await(() => session.Perfect == 1);
            yield return CheckHealth(80, "before-recovery-combo");
            yield return Await(() => session.Good == 1);
            yield return CheckHealth(80, "good-no-recovery");
            yield return CheckResult(SessionState.Completed, "normal-clear");
            Assert.That(session.Perfect, Is.EqualTo(session.Snapshot.TotalNotes - 3));
            Assert.That(session.Good, Is.EqualTo(1));
            Assert.That(session.Miss, Is.EqualTo(2));
            Assert.That(
                session.Health,
                Is.EqualTo(100),
                "Recovery must cap at maximum health."
            );
            Assert.That(
                judgments.Take(4).Select(item => item.health),
                Is.EqualTo(new[] { 90, 80, 80, 80 })
            );
            yield return Press(Key.Escape);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
        }

        [UnityTest]
        public IEnumerator NormalTenMissesGameOverThenSpaceRestoresHealth()
        {
            yield return LoadGameplay("normal-game-over");
            yield return Press(Key.Space);
            yield return CheckResult(SessionState.GameOver, "normal-game-over");
            Assert.That(session.Miss, Is.EqualTo(10));
            Assert.That(session.Health, Is.Zero);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().LastResult.Session.Health, Is.Zero);
            Assert.That(
                judgments.Select(item => item.health),
                Is.EqualTo(Enumerable.Range(0, 10).Select(i => 90 - i * 10))
            );
            yield return RestartFromResults();
            yield return CheckHealth(100, "restart");
            Assert.That(session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(session.Resolved, Is.Zero);
            Assert.That(game.Clock.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator HardDefaultMissImmediatelyShowsGameOver()
        {
            yield return LoadGameplay("hard-miss", true);
            yield return Press(Key.Space);
            yield return CheckResult(SessionState.GameOver, "hard-miss");
            Assert.That(session.Miss, Is.EqualTo(1));
            Assert.That(session.Health, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator HardPerfectPreservesHealthAndGoodImmediatelyShowsGameOver()
        {
            yield return LoadGameplay("hard-good", true);
            PrepareInput(0, 1);
            yield return Press(Key.Space);
            yield return Await(() => session.Perfect == 1);
            yield return CheckHealth(100, "hard-perfect");
            yield return CheckResult(SessionState.GameOver, "hard-good");
            Assert.That(session.Perfect, Is.EqualTo(1));
            Assert.That(session.Good, Is.EqualTo(1));
            Assert.That(session.Miss, Is.Zero);
            Assert.That(session.Health, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator HardConfiguredThreeMissesDrainFractionalHealthThenGameOver()
        {
            yield return LoadGameplay("hard-three-misses", true, true);
            yield return Press(Key.Space);
            yield return Await(() => session.Miss == 1);
            yield return null;
            Assert.That(view.healthFill.rectTransform.anchorMax.x, Is.EqualTo(1f).Within(.001));
            yield return CheckResult(SessionState.GameOver, "hard-three-misses");
            Assert.That(judgments.Select(item => item.health), Is.EqualTo(new[] { 100, 100, 100 }));
            Assert.That(judgments[1].ratio, Is.EqualTo(1));
            Assert.That(session.Miss, Is.EqualTo(3));
        }

        [UnityTearDown]
        public IEnumerator StopAutomaticInput()
        {
            InputSystem.onBeforeUpdate -= FeedInput;

            if (game && game.ReadModel != null)
            {
                game.ReadModel.Judged -= RecordJudgment;
            }

            events.Clear();
            held.Clear();

            if (keyboard != null && keyboard.added)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            }

            if (evidencePath != null)
            {
                File.WriteAllLines(evidencePath, evidence);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GaugeFailureContinuesAndLatchesOnceUntilRestart()
        {
            yield return LoadGameplay("failed-completion", continues: true);

            int failures = 0;
            session.Failed += _ => failures++;
            PrepareInput(10);
            yield return Press(Key.Space);
            yield return Await(() => session.Failure.Failed);
            Assert.That(session.State, Is.EqualTo(SessionState.Playing));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(view.healthLabel.text, Does.Contain("FAILED"));
            yield return CheckResult(SessionState.Completed, "failed-completion");
            Assert.That(session.Health, Is.Zero);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(session.Perfect, Is.GreaterThan(0));
            yield return RestartFromResults();
            Assert.That(session.Failure.Failed, Is.False);
        }

        [UnityTest]
        public IEnumerator HoldReleaseAndRegrabUsesOnlyMissedChecksAsPenalties()
        {
            yield return LoadGameplay("hold-regrab", continues: true);
            PrepareInput(0);

            int index = Enumerable
                .Range(0, game.Chart.Count)
                .First(i => game.Chart[i].Kind == NoteKind.Hold);
            var note = game.Chart[index];
            var key = new[] { Key.D, Key.F, Key.J, Key.K }[note.Lane];
            events.Add((note.StartTime + .3, key, false));
            events.Add((note.EndTime - .3, key, true));
            events.Sort((a, b) => a.time.CompareTo(b.time));
            yield return Press(Key.Space);
            yield return Await(() => session.Miss > 0);
            yield return CheckResult(SessionState.Completed, "hold-regrab");
            Assert.That(session.Miss, Is.GreaterThan(0));
            Assert.That(session.Resolved, Is.EqualTo(session.Snapshot.TotalNotes));
            Assert.That(session.GetState(index), Is.EqualTo(NoteState.Completed));
        }
    }
}
