using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("Results")]
    public sealed class ResultsFlowTests : GameplayTestContext
    {
        private IEnumerator LoadGameplay()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
        }

        private static void Complete(RhythmGameController game, bool perfect)
        {
            game.StartSession();

            if (perfect)
            {
                var inputs = new List<(double time, int lane, bool press)>();

                for (int i = 0; i < game.Chart.Count; i++)
                {
                    var note = game.Chart[i];
                    inputs.Add((note.StartTime, note.Lane, true));
                    double end = note.StartTime + .01;

                    if (note.Kind == NoteKind.Hold)
                    {
                        end = note.EndTime;
                    }

                    inputs.Add((end, note.Lane, false));
                }

                foreach (var input in inputs.OrderBy(item => item.time).ThenBy(item => item.press))
                {
                    if (input.press)
                    {
                        game.Session.PressLane(input.lane, input.time);
                    }
                    else
                    {
                        game.Session.ReleaseLane(input.lane, input.time);
                    }
                }
            }

            game.Session.Advance(game.Chart.CompletionTime + 1);
        }

        [UnityTest]
        public IEnumerator CompletionDisplaysThisPlayAndNextPlayReplacesIt()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var title = flow.CurrentRequest.Song.Title;
            var artist = flow.CurrentRequest.Song.Artist;
            var difficulty = flow.CurrentRequest.Difficulty.DisplayName;
            Complete(game, true);
            var expected = game.Session.Snapshot;
            yield return AssertOutcomeRemainsVisible("GAME CLEAR");
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            Assert.That(view.title.text, Is.EqualTo(title));
            Assert.That(view.artist.text, Is.EqualTo(artist));
            Assert.That(view.difficulty.text, Is.EqualTo(difficulty));
            Assert.That(expected.Perfect, Is.EqualTo(expected.TotalNotes));
            Assert.That(view.perfect.text, Is.EqualTo(expected.Perfect.ToString()));
            Assert.That(view.good.text, Is.EqualTo("0"));
            Assert.That(view.miss.text, Is.EqualTo("0"));
            Assert.That(view.outcome.text, Is.EqualTo("CLEAR"));
            Assert.That(flow.LastResult.MaxCombo, Is.EqualTo(expected.TotalNotes));
            Assert.That(view.accuracy.text, Is.EqualTo("정확도  100.00%"));
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
            Assert.That(view.feedback.GetComponentsInChildren<UnityEngine.UI.Text>(), Is.Empty);
            Assert.That(view.moreButton.onClick.GetPersistentEventCount(), Is.Zero);
            yield return Click(view.moreButton);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            Assert.That(view.review.gameObject.activeSelf, Is.True);
            yield return Click(view.review.closeButton);
            yield return Click(view.backButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return LoadGameplay();
            Assert.That(flow.LastResult, Is.Null);
            game = Object.FindFirstObjectByType<RhythmGameController>();
            Complete(game, false);
            expected = game.Session.Snapshot;
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(expected.Miss, Is.EqualTo(expected.TotalNotes));
            Assert.That(view.perfect.text, Is.EqualTo("0"));
            Assert.That(view.miss.text, Is.EqualTo(expected.TotalNotes.ToString()));
            Assert.That(flow.LastResult.MaxCombo, Is.Zero);
            Assert.That(view.outcome.text, Is.EqualTo("FAILED"));
            Assert.That(view.accuracy.text, Is.EqualTo("정확도  0.00%"));
        }

        [UnityTest]
        public IEnumerator GameOverDisplaysFailureAndEscapeDuringPlaySkipsResults()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var rules = flow.Rules.Snapshot;
            rules.GaugeFailure = GaugeFailureMode.Stop;
            flow.Rules.Update(rules);
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Complete(game, false);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.GameOver));
            yield return AssertOutcomeRemainsVisible("GAME OVER");
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(view.outcome.text, Is.EqualTo("FAILED"));
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.GameOver));
            yield return Click(view.backButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return LoadGameplay();
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(flow.LastResult, Is.Null);
        }

        [UnityTest]
        public IEnumerator RestartDuringOutcomeDelayCancelsPendingResults()
        {
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Complete(game, true);
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            game.StartSession();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Object.FindFirstObjectByType<ResultsScreen>(), Is.Null);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(Object.FindFirstObjectByType<GameplayHudView>().gameOverPanel.activeSelf, Is.False);
            Complete(game, true);
            yield return AssertOutcomeRemainsVisible("GAME CLEAR");
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
        }

        private IEnumerator AssertOutcomeRemainsVisible(string outcome)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            // The next frame lets both the HUD and the composition root observe the outcome.
            yield return null;
            yield return null;
            var view = Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo(outcome));
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(Object.FindFirstObjectByType<ResultsScreen>(), Is.Null);
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator DirectResultsSceneHasEmptyStateAndBackWorks()
        {
            yield return SceneManager.LoadSceneAsync(AppFlowController.ResultsPath);
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            view.Validate();
            Assert.That(view.title.text, Is.EqualTo("플레이 결과가 없습니다"));
            Assert.That(view.perfect.text, Is.EqualTo("—"));
            Assert.That(view.moreButton.interactable, Is.False);
            yield return Click(view.backButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
        }

        [UnityTest]
        public IEnumerator RealClockCompletionTransitionsWithoutUserInput()
        {
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            int total = game.Session.Snapshot.TotalNotes;
            yield return Press(Key.Space);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(flow.LastResult.Session.Miss, Is.EqualTo(total));
            Assert.That(flow.LastResult.Session.Perfect, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<ResultsView>().miss.text, Is.EqualTo(total.ToString()));
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator RestartResetsStatisticsAndGoodJudgmentsReachResults()
        {
            yield return LoadGameplay();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            game.StartSession();
            var note = game.Chart[0];
            game.Session.PressLane(note.Lane, note.StartTime);
            game.Session.ReleaseLane(note.Lane, note.StartTime + .01);
            Assert.That(game.Session.Perfect, Is.GreaterThan(0));
            game.StartSession();
            var timing = flow.CurrentRequest.Rules.Timing;
            game.Session.PressLane(note.Lane, note.StartTime + (timing.PerfectWindow + timing.GoodWindow) / 2);
            game.Session.ReleaseLane(note.Lane, note.StartTime + timing.GoodWindow + .01);
            game.Session.Advance(game.Chart.CompletionTime + 1);
            var expected = game.Session.Snapshot;
            Assert.That(expected.Good, Is.GreaterThan(0));
            Assert.That(expected.Perfect, Is.Zero);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(view.good.text, Is.EqualTo(expected.Good.ToString()));
            Assert.That(view.perfect.text, Is.EqualTo("0"));
            Assert.That(view.miss.text, Is.EqualTo(expected.Miss.ToString()));
            Assert.That(flow.LastResult.MaxCombo, Is.EqualTo(expected.Good));
            Assert.That(flow.LastResult.Notes.Sum(note => note.Judgments.Count), Is.EqualTo(expected.Resolved));
            Assert.That(flow.LastResult.Notes.SelectMany(note => note.Judgments)
                .Any(item => item.Grade == Judgment.Perfect), Is.False);
        }

        [UnityTest]
        public IEnumerator MoreShowsPlayedChartAndPerNoteJudgmentsThenReturnsToSameResult()
        {
            yield return LoadGameplay();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var chart = game.Chart;
            var first = chart[0];
            var timing = flow.CurrentRequest.Rules.Timing;
            game.StartSession();
            game.Session.PressLane(first.Lane, first.StartTime + (timing.PerfectWindow + timing.GoodWindow) / 2);
            game.Session.ReleaseLane(first.Lane, first.StartTime + timing.GoodWindow + .01);
            game.Session.Advance(chart.CompletionTime + 1);
            var expected = game.Session.Snapshot;
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            var result = flow.LastResult;
            Assert.That(result.Notes.Count, Is.EqualTo(chart.Count));
            Assert.That(result.Notes.Select(note => note.Note),
                Is.EqualTo(Enumerable.Range(0, chart.Count).Select(index => chart[index])));
            var judgments = result.Notes.SelectMany(note => note.Judgments).ToArray();
            Assert.That(judgments.Count(item => item.Grade == Judgment.Perfect), Is.EqualTo(expected.Perfect));
            Assert.That(judgments.Count(item => item.Grade == Judgment.Good), Is.EqualTo(expected.Good));
            Assert.That(judgments.Count(item => item.Grade == Judgment.Miss), Is.EqualTo(expected.Miss));
            yield return Click(view.moreButton);
            Assert.That(view.review.gameObject.activeSelf, Is.True);
            Assert.That(view.backButton.interactable, Is.False);
            Assert.That(view.review.selected.text, Does.Contain("Good 1"));
            Assert.That(view.review.events.text, Does.Contain("1. Good"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = view.review.chart.rectTransform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
                rect.TransformPoint(view.review.chart.NoteBounds(1).center));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = point }
                    .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
            yield return null;
            yield return null;
            Assert.That(view.review.GetComponent<ResultsChartReview>().SelectedIndex, Is.EqualTo(1));
            Assert.That(view.review.selected.text, Does.Contain("노트 #2"));
            yield return Press(Key.Escape);
            Assert.That(view.review.gameObject.activeSelf, Is.False);
            Assert.That(flow.LastResult, Is.SameAs(result));
            Assert.That(view.backButton.interactable, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            yield return Click(view.moreButton);
            Assert.That(view.review.GetComponent<ResultsChartReview>().SelectedIndex, Is.Zero);
            yield return Click(view.review.closeButton);
            yield return Click(view.backButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
        }

        [UnityTest]
        public IEnumerator EarlyFailureKeepsFutureNotesUnjudgedInWholeChartReview()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var rules = flow.Rules.Snapshot;
            rules.GaugeFailure = GaugeFailureMode.Stop;
            flow.Rules.Update(rules);
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            int count = game.Chart.Count;
            Complete(game, false);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var result = flow.LastResult;
            Assert.That(result.Notes.Count, Is.EqualTo(count));
            Assert.That(result.Notes.Sum(note => note.Judgments.Count), Is.EqualTo(result.Session.Resolved));
            var pending = result.Notes.First(note => note.Judgments.Count == 0);
            Assert.That(pending.Grade, Is.Null);
            var view = Object.FindFirstObjectByType<ResultsView>();
            yield return Click(view.moreButton);
            view.review.GetComponent<ResultsChartReview>().SelectNote(pending.Index);
            Assert.That(view.review.selected.text, Does.Contain("미판정"));
            Assert.That(view.review.selected.text, Does.Contain("Miss 0"));
            Assert.That(view.review.events.text, Does.Contain("판정 기록이 없습니다"));
            yield return Click(view.review.closeButton);
        }
    }
}
