using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        private static void Complete(RhythmGameController game, bool perfect, double inputErrorSeconds = 0)
        {
            game.StartSession();

            if (perfect)
            {
                var inputs = new List<(double time, int lane, bool press)>();

                for (int i = 0; i < game.Chart.Count; i++)
                {
                    var note = game.Chart[i];
                    inputs.Add((note.StartTime + inputErrorSeconds, note.Lane, true));
                    double end = note.StartTime + .01;

                    if (note.Kind == NoteKind.Hold)
                    {
                        end = note.EndTime;
                    }

                    inputs.Add((end + inputErrorSeconds, note.Lane, false));
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
        public IEnumerator ConsistentTimingRecommendationIsSavedOnlyOnApplyAndUsedForRetry(
            [Values(false, true)] bool olderScene
        )
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            flow.Preferences.TimingOffsetMs = 10;
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Complete(game, true, .020);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;

            var view = Object.FindFirstObjectByType<ResultsView>();
            if (olderScene)
            {
                Object.Destroy(view.recommendationButton.gameObject);
                view.recommendationButton = null;
                yield return null;

                var screen = Object.FindFirstObjectByType<ResultsScreen>();
                screen.Initialize(flow.LastResult, flow.ReturnFromGameplay, flow.RetryResultAsync, flow.Preferences);
            }

            var recommendation = view.recommendationButton;
            Assert.That(recommendation.gameObject.activeInHierarchy, Is.True);
            var recommendationRect = (RectTransform)recommendation.transform;
            var retryRect = (RectTransform)view.retryButton.transform;
            Assert.That(recommendationRect.anchorMax.y, Is.LessThan(retryRect.anchorMin.y));
            Assert.That(recommendationRect.anchorMin.x, Is.EqualTo(retryRect.anchorMin.x));
            Assert.That(recommendationRect.anchorMax.x, Is.EqualTo(retryRect.anchorMax.x));
            Assert.That(recommendation.GetComponentInChildren<PixelDojoFrameGraphic>().artwork,
                Is.SameAs(view.retryButton.GetComponentInChildren<PixelDojoFrameGraphic>().artwork));
            Assert.That(recommendation.GetComponentInChildren<Text>().text, Does.Contain("+30 ms"));
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(10));
            yield return Click(recommendation);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.EqualTo(30));
            Assert.That(new RhythmDojo.Services.SelectionPreferences().TimingOffsetMs, Is.EqualTo(30));
            yield return Click(view.retryButton);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.EqualTo(30));
        }

        [UnityTest]
        public IEnumerator ResultsWithoutRecommendationCanOpenTimingTestAndRetryWithAppliedOffset()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            flow.Preferences.TimingOffsetMs = 0;
            yield return LoadGameplay();
            Complete(Object.FindFirstObjectByType<RhythmGameController>(), true);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());

            var result = flow.LastResult;
            Assert.That(result.TimingOffsetRecommendationMs, Is.Null);
            var view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(view.recommendationButton.GetComponentInChildren<Text>().text, Is.EqualTo("박자 보정 테스트"));
            yield return Click(view.recommendationButton);

            var settings = Object.FindFirstObjectByType<SettingsView>();
            var panel = settings.transform.Find("Timing Calibration Overlay/Timing Calibration Panel");
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            var start = panel.Find("Start Calibration").GetComponent<Button>();
            var apply = panel.Find("Apply Calibration").GetComponent<Button>();
            start.onClick.Invoke();
            double firstBeat = Time.realtimeSinceStartupAsDouble + .25;
            for (int beat = 4; beat < 16; beat++)
            {
                double timestamp = firstBeat + beat * .5 + .035;
                while (Time.realtimeSinceStartupAsDouble < timestamp)
                {
                    yield return null;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), timestamp);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), timestamp + .001);
            }

            yield return Await(() => apply.interactable);
            Assert.That(flow.Preferences.TimingOffsetMs, Is.Zero);
            yield return Click(apply);
            int offset = flow.Preferences.TimingOffsetMs;
            Assert.That(offset, Is.InRange(32, 40));

            yield return Press(Key.Escape);
            Assert.That(settings.gameObject.activeSelf, Is.False);
            Assert.That(flow.LastResult, Is.SameAs(result));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            yield return Click(view.retryButton);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.EqualTo(offset));
        }

        [UnityTest]
        public IEnumerator CompletionDisplaysThisPlayAndNextPlayReplacesIt()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var expectedCover = Object.FindFirstObjectByType<SongSelectionView>().album.sprite;
            yield return LoadGameplay();
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var title = flow.CurrentRequest.Song.Title;
            var artist = flow.CurrentRequest.Song.Artist;
            var difficulty = flow.CurrentRequest.Difficulty.DisplayName;
            var request = flow.CurrentRequest;
            string recordKey = RecordIdentity.Rules(request.Song.ChartId, request.Song.Revision, request.Rules);
            var previous = new SessionSnapshot(SessionState.Completed, 1, 0, 1, 1, 2);
            flow.Preferences.SaveCompleted(request.Song.SongId, recordKey, previous, 1);
            Complete(game, true);
            var expected = game.Session.Snapshot;
            yield return AssertOutcomeRemainsVisible("Full Combo", 2);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = Object.FindFirstObjectByType<ResultsView>();
            Assert.That(view.cover.sprite, Is.SameAs(expectedCover));
            Assert.That(view.cover.enabled, Is.True);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            Assert.That(view.title.text, Is.EqualTo(title));
            Assert.That(view.artist.text, Is.EqualTo(artist));
            Assert.That(view.difficulty.text, Is.EqualTo(difficulty));
            Assert.That(expected.Perfect, Is.EqualTo(expected.TotalNotes));
            Assert.That(view.perfect.text, Is.EqualTo(expected.Perfect.ToString()));
            Assert.That(view.good.text, Is.EqualTo("0"));
            Assert.That(view.miss.text, Is.EqualTo("0"));
            Assert.That(view.outcome.text, Is.EqualTo("S"));
            Assert.That(flow.LastResult.MaxCombo, Is.EqualTo(expected.TotalNotes));
            Assert.That(view.accuracy.text, Is.EqualTo("정확도  100.00%"));
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
            Assert.That(flow.LastResult.PreviousBestScore, Is.EqualTo(ScoreRules.Score(previous.AccuracyRate)));
            Assert.That(view.newRecordBadge.activeSelf, Is.True);
            Assert.That(view.scoreValue.text, Is.EqualTo(flow.LastResult.Score.ToString("N0")));
            Assert.That(view.retryButton.interactable, Is.True);
            Assert.That(view.moreButton.onClick.GetPersistentEventCount(), Is.Zero);
            yield return Click(view.moreButton);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppFlowController.ResultsPath));
            Assert.That(view.review.gameObject.activeSelf, Is.True);
            yield return Click(view.review.closeButton);
            yield return Click(view.retryButton);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.Song.ChartId, Is.EqualTo(request.Song.ChartId));
            Assert.That(flow.CurrentRequest.Song.Revision, Is.EqualTo(request.Song.Revision));
            Assert.That(flow.CurrentRequest.Difficulty, Is.EqualTo(request.Difficulty));
            Assert.That(flow.CurrentRequest.ScrollMode, Is.EqualTo(request.ScrollMode));
            Assert.That(flow.CurrentRequest.Multiplier, Is.EqualTo(request.Multiplier));
            Assert.That(flow.CurrentRequest.TimingOffsetMs, Is.EqualTo(request.TimingOffsetMs));
            Assert.That(RecordIdentity.Rules(flow.CurrentRequest.Song.ChartId, flow.CurrentRequest.Song.Revision,
                flow.CurrentRequest.Rules), Is.EqualTo(recordKey));
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
            StringAssert.StartsWith("수련 중단", view.progressNote.text);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.retryButton.gameObject));
            yield return Click(view.backButton);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return LoadGameplay();
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            game = Object.FindFirstObjectByType<RhythmGameController>();
            var pause = Object.FindFirstObjectByType<GameplayPauseMenu>();
            Assert.That(game.IsPaused, Is.True);
            Assert.That(pause.panel.activeSelf, Is.True);
            Assert.That(Object.FindFirstObjectByType<ResultsScreen>(), Is.Null);
            yield return Click(pause.exitButton);
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
            yield return AssertOutcomeRemainsVisible("Full Combo", 2);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
        }

        private IEnumerator AssertOutcomeRemainsVisible(string outcome, double minimumDelay = 1)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            // The next frame lets both the HUD and the composition root observe the outcome.
            yield return null;
            yield return null;
            var view = Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo(outcome));
            Assert.That(view.gameOverPanel.GetComponent<UnityEngine.UI.Image>().enabled, Is.False);
            Assert.That(view.gameOverFrame.activeSelf, Is.False);
            Assert.That(view.gameOverHint.gameObject.activeSelf, Is.False);
            Assert.That(view.sessionState.text, Is.EqualTo("결과 화면으로 이동"));
            yield return new WaitForSecondsRealtime((float)(minimumDelay - .25));
            Assert.That(Object.FindFirstObjectByType<ResultsScreen>(), Is.Null);
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.GreaterThanOrEqualTo(minimumDelay));
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
            Assert.That(flow.LastResult.Notes.Sum(note => note.Judgments.Count), Is.EqualTo(flow.LastResult.Notes.Count + flow.LastResult.Notes.Count(note => note.Note.Kind == NoteKind.Hold)));
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
            Assert.That(judgments.Count(item => item.Grade == Judgment.Miss), Is.LessThanOrEqualTo(expected.Miss));
            Assert.That(judgments.Length, Is.EqualTo(result.Notes.Count + result.Notes.Count(note => note.Note.Kind == NoteKind.Hold)));
            Assert.That(view.miss.text, Is.EqualTo(expected.Miss.ToString()));
            yield return Click(view.moreButton);
            Assert.That(view.review.gameObject.activeSelf, Is.True);
            Assert.That(view.backButton.interactable, Is.False);
            Assert.That(view.review.events.text, Does.Contain("시작: Good"));
            view.review.zoom.Show();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var dropdownList = view.review.zoom.transform.Find("Dropdown List");
            Assert.That(dropdownList, Is.Not.Null);
            var options = dropdownList.GetComponentsInChildren<UnityEngine.UI.Toggle>()
                .OrderByDescending(option => option.transform.position.y).ToArray();
            Assert.That(options.Length, Is.EqualTo(4));
            for (int i = 0; i < options.Length; i++)
            {
                var optionRect = (RectTransform)options[i].transform;
                var optionLabel = options[i].GetComponentInChildren<UnityEngine.UI.Text>();
                Assert.That(optionRect.rect.height, Is.GreaterThanOrEqualTo(60));
                Assert.That(optionLabel.preferredHeight, Is.LessThanOrEqualTo(optionLabel.rectTransform.rect.height));
                if (i > 0)
                {
                    Assert.That(Mathf.Abs(optionRect.anchoredPosition.y -
                        ((RectTransform)options[i - 1].transform).anchoredPosition.y),
                        Is.GreaterThanOrEqualTo(optionRect.rect.height));
                }
            }

            var review = view.review.GetComponent<ResultsChartReview>();
            review.SelectNote(result.Notes.First(note => note.Note.Kind == NoteKind.Hold).Index);
            Assert.That(view.review.events.text, Does.Contain("시작: Miss"));
            Assert.That(view.review.events.text, Does.Contain("끝: Miss"));
            review.SelectNote(0);
            Assert.That(view.review.events.text, Does.Contain("시작: Good"));
            options[3].isOn = true;
            Assert.That(view.review.zoom.value, Is.EqualTo(3));
            yield return new WaitForSecondsRealtime(.2f);
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
            view.review.zoom.Show();
            Assert.That(view.review.zoom.GetComponentInChildren<Canvas>(), Is.Not.Null);
            yield return Press(Key.Escape);
            Assert.That(view.review.gameObject.activeSelf, Is.True);
            yield return Await(() => !view.review.zoom.GetComponentInChildren<Canvas>());
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
            Assert.That(result.Notes.Sum(note => note.Judgments.Count), Is.LessThanOrEqualTo(result.Session.Resolved));
            var pending = result.Notes.First(note => note.Judgments.Count == 0);
            Assert.That(pending.Grade, Is.Null);
            var view = Object.FindFirstObjectByType<ResultsView>();
            yield return Click(view.moreButton);
            view.review.GetComponent<ResultsChartReview>().SelectNote(pending.Index);
            Assert.That(view.review.selected.text, Does.Contain("미판정"));
            Assert.That(view.review.events.text, Does.Contain("시작: 미판정"));
            yield return Click(view.review.closeButton);
        }
    }
}
