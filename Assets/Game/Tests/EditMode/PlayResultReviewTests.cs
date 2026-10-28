using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmDojo.Tests
{
    public sealed class PlayResultReviewTests
    {
        [Test]
        public void SnapshotKeepsEveryNoteAndEveryHoldJudgmentWithoutBorrowingLists()
        {
            var chart = new ChartData(new[] { NoteData.Tap(0, 1), NoteData.Hold(1, 2, 4), NoteData.Tap(0, 8) }, 10);
            var judgments = new List<JudgmentEvent>
            {
                new JudgmentEvent(0, chart[0], Judgment.Good, .08),
                new JudgmentEvent(1, chart[1], Judgment.Perfect, 0),
                new JudgmentEvent(1, chart[1], Judgment.Miss, 0),
                new JudgmentEvent(1, chart[1], Judgment.Perfect, 0),
            };
            var states = new[] { NoteState.Completed, NoteState.Holding, NoteState.Pending };
            var labels = new[] { "D", "F" };
            var result = new PlayResult("Song", "Artist", "Hard", null, default, 1, chart,
                new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(3, 180) }), labels, judgments, states);
            judgments.Clear();
            states[1] = NoteState.Completed;
            labels[0] = "Changed";
            Assert.That(result.Notes.Count, Is.EqualTo(3));
            Assert.That(result.LaneLabels[0], Is.EqualTo("D"));
            Assert.That(result.Notes[1].State, Is.EqualTo(NoteState.Holding));
            Assert.That(result.Notes[0].Judgments.Single().ErrorSeconds, Is.EqualTo(.08));
            Assert.That(result.Notes[1].Judgments.Select(item => item.Grade),
                Is.EqualTo(new[] { Judgment.Perfect, Judgment.Miss, Judgment.Perfect }));
            Assert.That(result.Notes[1].Grade, Is.EqualTo(Judgment.Miss));
            Assert.That(result.Notes[2].Grade, Is.Null);
            Assert.That(result.Notes[2].Judgments, Is.Empty);
            Assert.That(result.Tempo.BeatAtTime(4), Is.EqualTo(9));
        }

        [Test]
        public void ReviewCanInspectEveryLongNoteJudgmentAndNavigateWholeChart()
        {
            var scene = EditorSceneManager.OpenScene(EditorTools.SceneBuilder.ResultsPath, OpenSceneMode.Additive);

            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResultsView>()).Single();
                var chart = new ChartData(new[] { NoteData.Hold(0, 1, 90), NoteData.Tap(5, 100) }, 105);
                var judgments = Enumerable.Range(0, 200)
                    .Select(index => new JudgmentEvent(0, chart[0], Judgment.Perfect, 0)).ToArray();
                var result = new PlayResult("Long", "Artist", "Six lanes", null, default, 200, chart,
                    new TempoMap(new[] { new TempoPoint(0, 120) }),
                    new[] { "A", "S", "D", "F", "J", "K" }, judgments,
                    new[] { NoteState.Completed, NoteState.Pending });
                var review = view.review.GetComponent<ResultsChartReview>();
                review.Show(result);
                Assert.That(view.review.lanes.text, Does.Contain("K"));
                view.review.eventPosition.value = view.review.eventPosition.maxValue;
                Assert.That(view.review.events.text, Does.Contain("200. Perfect"));
                Assert.That(view.review.events.text, Does.Not.Contain("\n1. Perfect"));
                review.SelectNote(1);
                Assert.That(view.review.selected.text, Does.Contain("미판정"));
                Assert.That(view.review.chart.FirstBeat, Is.GreaterThan(0));
                Assert.That(view.review.chart.FirstBeat + view.review.chart.VisibleBeats, Is.GreaterThan(200));
                var bounds = view.review.chart.NoteBounds(1);
                Assert.That(bounds.height, Is.GreaterThan(0));
                Assert.That(view.review.chart.HitTest(bounds.center), Is.EqualTo(1));
                review.SelectNote(0);
                Assert.That(view.review.chart.FirstBeat, Is.LessThanOrEqualTo(2));
                Assert.That(view.review.selected.text, Does.Contain("Perfect 200"));
                review.Close();
                Assert.That(view.review.gameObject.activeSelf, Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
