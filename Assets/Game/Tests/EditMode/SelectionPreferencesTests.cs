using System;
using NUnit.Framework;
using UnityEditor;
using SessionState = RhythmDojo.Gameplay.SessionState;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    public sealed class SelectionPreferencesTests
    {
        private string key;
        [SetUp] public void SetUp() => key = "RhythmDojo.Tests." + Guid.NewGuid();
        [TearDown] public void TearDown() { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        [Test]
        public void OptionsFavoritesAndRecordsSurviveReload()
        {
            var preferences = new SelectionPreferences(key);
            preferences.Difficulty = "Hard"; preferences.ScrollMode = ScrollMode.Bpm;
            preferences.Multiplier = 1.5; preferences.TimingOffsetMs = 123; preferences.ToggleFavorite("a");
            preferences.SaveCompleted("a", "Hard", new SessionSnapshot(SessionState.Completed, 9, 1, 0, 10, 10), 10);
            var loaded = new SelectionPreferences(key);
            Assert.That(loaded.Difficulty, Is.EqualTo("Hard"));
            Assert.That(loaded.Multiplier, Is.EqualTo(1.5));
            Assert.That(loaded.ScrollMode, Is.EqualTo(ScrollMode.Bpm));
            Assert.That(loaded.TimingOffsetMs, Is.EqualTo(123));
            Assert.That(loaded.IsFavorite("a"), Is.True);
            var record = loaded.FindRecord("a", "Hard");
            Assert.That(record.score, Is.EqualTo(950000)); Assert.That(record.Rank, Is.EqualTo("S"));
            Assert.That(record.maxCombo, Is.EqualTo(10));
        }
        [Test]
        public void IncompleteAndWorseRunsDoNotReplaceBest()
        {
            var preferences = new SelectionPreferences(key);
            preferences.SaveCompleted("a", "Standard", new SessionSnapshot(SessionState.Playing, 1, 0, 0, 1, 10), 1);
            Assert.That(preferences.FindRecord("a", "Standard"), Is.Null);
            preferences.SaveCompleted("a", "Standard", new SessionSnapshot(SessionState.Completed, 10, 0, 0, 10, 10), 10);
            preferences.SaveCompleted("a", "Standard", new SessionSnapshot(SessionState.Completed, 1, 0, 9, 0, 10), 1);
            Assert.That(preferences.FindRecord("a", "Standard").score, Is.EqualTo(1000000));
            Assert.That(preferences.FindRecord("a", "Easy"), Is.Null);
        }
        [TestCase(95, "S")] [TestCase(90, "A")] [TestCase(80, "B")] [TestCase(70, "C")] [TestCase(69, "D")]
        public void RankBoundaries(double rate, string rank) => Assert.That(new SongRecord { rate = rate }.Rank, Is.EqualTo(rank));
        [Test]
        public void CorruptPreferencesFallBackAndOffsetsAreClamped()
        {
            PlayerPrefs.SetString(key, "not json");
            var preferences = new SelectionPreferences(key);
            Assert.That(preferences.Multiplier, Is.EqualTo(1));
            preferences.TimingOffsetMs = 300; Assert.That(preferences.TimingOffsetMs, Is.EqualTo(200));
            preferences.TimingOffsetMs = -300; Assert.That(preferences.TimingOffsetMs, Is.EqualTo(-200));
        }
        [Test]
        public void DemoPreparationIsIdempotentAndPreservesExistingCharts()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
            int count = settings.catalog.Count;
            string original = System.IO.File.ReadAllText(SceneBuilder.ChartPath);
            DemoContentBuilder.Prepare(); DemoContentBuilder.Prepare();
            Assert.That(settings.catalog.Count, Is.EqualTo(count));
            Assert.That(System.IO.File.ReadAllText(SceneBuilder.ChartPath), Is.EqualTo(original));
            var demo = AssetDatabase.LoadAssetAtPath<SongDefinition>(DemoContentBuilder.SongPath);
            demo.Validate(); Assert.That(demo.AudioClip.length, Is.EqualTo(30).Within(.01));
            Assert.That(demo.Mode.LaneCount, Is.EqualTo(4)); Assert.That(demo.Cover, Is.Not.Null);
        }
        [Test]
        public void InformationPanelReservesHeaderAndFooterAndAlbumFillsFrame()
        {
            var view = SongSelectionLayoutBuilder.Build();
            try
            {
                var left = (RectTransform)view.songTitle.transform.parent;
                Assert.That(left.anchorMin, Is.EqualTo(new Vector2(.025f, .095f)));
                Assert.That(left.anchorMax, Is.EqualTo(new Vector2(.325f, .89f)));
                Assert.That(left.offsetMin, Is.EqualTo(Vector2.zero)); Assert.That(left.offsetMax, Is.EqualTo(Vector2.zero));
                Assert.That(view.album.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectMode,
                    Is.EqualTo(UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent));
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }
    }
}