using System;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("Training")]
    public sealed class TrainingProgressTests
    {
        private string key;

        [SetUp]
        public void SetUp()
        {
            key = "RhythmDojo.Tests.Training." + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        [Category("Fast")]
        public void OnlySuccessPersistsAndRetryCannotEraseVersionedCompletion()
        {
            string ordinary = PlayerPrefs.GetString(SelectionPreferences.StorageKey, "");
            var store = new TrainingProgressStore(key);
            var session = new TrainingSession("lesson", 1, TrainingStage.Practice,
                new TrainingCriteria(0, 50), false);
            session.BeginAttempt();
            session.Abort();
            store.SaveCompleted(session);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            session.BeginAttempt();
            session.MarkPlaying();
            session.Finish(session.Attempt, new SessionSnapshot(SessionState.Completed, 0, 0, 1, 0, 1));
            store.SaveCompleted(session);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);

            session.BeginAttempt();
            session.MarkPlaying();
            session.Finish(session.Attempt, new SessionSnapshot(SessionState.Completed, 1, 0, 0, 1, 1));
            store.SaveCompleted(session);
            string saved = PlayerPrefs.GetString(key);
            store.SaveCompleted(session);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(saved));
            session.BeginAttempt();
            session.Abort();
            store.SaveCompleted(session);
            var restored = new TrainingProgressStore(key);
            Assert.That(restored.IsCompleted("lesson", 1), Is.True);
            Assert.That(restored.IsCompleted("lesson", 2), Is.False);
            Assert.That(restored.IsCompleted("different", 1), Is.False);
            Assert.That(PlayerPrefs.GetString(SelectionPreferences.StorageKey, ""), Is.EqualTo(ordinary));
        }

        [TestCase("not-json")]
        [TestCase("{\"version\":99,\"completed\":[{\"lessonId\":\"lesson\",\"definitionVersion\":1}]}")]
        [TestCase("{\"version\":1,\"completed\":[null,{\"lessonId\":\"lesson\",\"definitionVersion\":0}]}")]
        [Category("Fast")]
        public void CorruptOrUnsupportedStorageDoesNotGrantCompletion(string json)
        {
            PlayerPrefs.SetString(key, json);
            Assert.That(new TrainingProgressStore(key).IsCompleted("lesson", 1), Is.False);
        }

        [TestCase("Trill")]
        [TestCase("StairsDenim")]
        [TestCase("Jack")]
        [TestCase("Burst")]
        [TestCase("HoldTap")]
        [TestCase("ShieldRelease")]
        [TestCase("ChordTrill")]
        [Category("Fast")]
        public void TrillFeedbackUsesRecordedEvidenceAndOnlyAssessmentSaves(string name)
        {
            var lesson = UnityEditor.AssetDatabase.LoadAssetAtPath<TrainingLesson>(
                "Assets/Game/Settings/Training/" + name + ".asset");
            lesson.Validate();
            var store = new TrainingProgressStore(key);
            var session = new TrainingSession(lesson.LessonId, lesson.DefinitionVersion, lesson.Stage,
                lesson.Criteria, false, lesson.Guided);
            for (int i = 0; i < 4; i++)
            {
                session.BeginAttempt();
                session.MarkPlaying();
                var song = lesson.SongForStep(session.Step);
                var chart = song.Chart.ToChartData();
                var game = new RhythmSession(chart, song.Mode.ToRules(), song.Timing.ToTempoMap(), lesson.CreateRules());
                game.Start();
                var playback = new TrainingPlayback(chart, true, lesson.MaximumChordSpreadSeconds);
                playback.Advance(game, chart.CompletionTime);
                game.Advance(chart.CompletionTime + .1);
                session.Finish(session.Attempt, game.Snapshot);
                Assert.That(session.Outcome, Is.EqualTo(TrainingOutcome.Succeeded));
                Assert.That(game.Snapshot.AccuracyRate, Is.EqualTo(100));
                store.SaveCompleted(session);
                Assert.That(new TrainingProgressStore(key).IsCompleted(lesson.LessonId, lesson.DefinitionVersion),
                    Is.EqualTo(i == 3));
            }
        }
    }
}
