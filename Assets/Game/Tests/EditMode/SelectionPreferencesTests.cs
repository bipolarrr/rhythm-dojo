using System;
using System.Linq;
using RhythmDojo.Content;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.EditorTools;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using SessionState = RhythmDojo.Gameplay.SessionState;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SelectionPreferencesTests
    {
        private string key;

        [SetUp]
        public void SetUp() => key = "RhythmDojo.Tests." + Guid.NewGuid();

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        [Category("Fast")]
        public void OptionsFavoritesAndRecordsSurviveReload()
        {
            var preferences = new SelectionPreferences(key);
            preferences.Difficulty = "Hard";
            preferences.ScrollMode = ScrollMode.Bpm;
            preferences.Multiplier = 1.5;
            preferences.TimingOffsetMs = 123;
            preferences.ToggleFavorite("a");
            preferences.SaveCompleted(
                "a",
                "Hard",
                new SessionSnapshot(SessionState.Completed, 9, 1, 0, 10, 10),
                10
            );

            var loaded = new SelectionPreferences(key);
            Assert.That(loaded.Difficulty, Is.EqualTo("Hard"));
            Assert.That(loaded.Multiplier, Is.EqualTo(1.5));
            Assert.That(loaded.ScrollMode, Is.EqualTo(ScrollMode.Bpm));
            Assert.That(loaded.TimingOffsetMs, Is.EqualTo(123));
            Assert.That(loaded.IsFavorite("a"), Is.True);

            var record = loaded.FindRecord("a", "Hard");
            Assert.That(record.score, Is.EqualTo(950000));
            Assert.That(record.Rank, Is.EqualTo("S"));
            Assert.That(record.maxCombo, Is.EqualTo(10));
        }

        [Test]
        [Category("Fast")]
        public void IncompleteAndWorseRunsDoNotReplaceBest()
        {
            var preferences = new SelectionPreferences(key);
            preferences.SaveCompleted(
                "a",
                "Standard",
                new SessionSnapshot(SessionState.Playing, 1, 0, 0, 1, 10),
                1
            );
            Assert.That(preferences.FindRecord("a", "Standard"), Is.Null);
            preferences.SaveCompleted(
                "a",
                "Standard",
                new SessionSnapshot(SessionState.Completed, 10, 0, 0, 10, 10),
                10
            );
            preferences.SaveCompleted(
                "a",
                "Standard",
                new SessionSnapshot(SessionState.Completed, 1, 0, 9, 0, 10),
                1
            );
            Assert.That(preferences.FindRecord("a", "Standard").score, Is.EqualTo(1000000));
            Assert.That(preferences.FindRecord("a", "Easy"), Is.Null);
        }

        [TestCase(95, "S")]
        [TestCase(90, "A")]
        [TestCase(80, "B")]
        [TestCase(70, "C")]
        [TestCase(69, "D")]
        [Category("Fast")]
        public void RankBoundaries(double rate, string rank) =>
            Assert.That(new SongRecord { rate = rate }.Rank, Is.EqualTo(rank));

        [Test]
        [Category("Fast")]
        public void CorruptPreferencesFallBackAndOffsetsAreClamped()
        {
            PlayerPrefs.SetString(key, "not json");

            var preferences = new SelectionPreferences(key);
            Assert.That(preferences.Multiplier, Is.EqualTo(1));
            preferences.TimingOffsetMs = 300;
            Assert.That(preferences.TimingOffsetMs, Is.EqualTo(200));
            preferences.TimingOffsetMs = -300;
            Assert.That(preferences.TimingOffsetMs, Is.EqualTo(-200));
        }

        [Test]
        [Category("Fast")]
        public void DemoPreparationIsIdempotentAndPreservesExistingCharts()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(
                TestContentBuilder.SettingsPath
            );
            int count = settings.catalog.Count;
            string original = System.IO.File.ReadAllText(SceneBuilder.ChartPath);
            DemoContentBuilder.Prepare();
            DemoContentBuilder.Prepare();
            Assert.That(settings.catalog.Count, Is.EqualTo(count));
            Assert.That(System.IO.File.ReadAllText(SceneBuilder.ChartPath), Is.EqualTo(original));

            var demo = AssetDatabase.LoadAssetAtPath<SongDefinition>(DemoContentBuilder.SongPath);
            demo.Validate();
            Assert.That(demo.AudioClip.length, Is.EqualTo(30).Within(.01));
            Assert.That(demo.Mode.LaneCount, Is.EqualTo(4));
            Assert.That(demo.Cover, Is.Not.Null);
        }

        [Test]
        [Category("Fast")]
        public void ListRetainsIdsAcrossSortAndClearsSelectionForEmptyFavorites()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
            var library = new SongLibrary();
            library.Register(new BuiltInSongProvider(settings.catalog));
            var preferences = new SelectionPreferences(key);
            var selection = new SongSelectionService(settings, library, preferences);
            var list = selection.List;
            string selected = list.Refresh(library, preferences, "tempo-pulse");
            Assert.That(selected, Is.EqualTo("tempo-pulse"));
            list.Sort = 2;
            Assert.That(list.Refresh(library, preferences, selected), Is.EqualTo(selected));
            Assert.That(list.Entries.Select(song => song.MinBpm), Is.Ordered);
            Assert.That(list.Move(list.Entries.Last().Id, 1), Is.EqualTo(list.Entries[0].Id));
            preferences.ToggleFavorite("test-pulse");
            list.FavoritesOnly = true;
            selected = list.Refresh(library, preferences, selected);
            Assert.That(selected, Is.EqualTo("test-pulse"));
            preferences.ToggleFavorite(selected);
            selected = list.Refresh(library, preferences, selected);
            selection.UpdateSelection(selected, selection.SelectedDifficulty, ScrollMode.Bpm, 1.5);
            Assert.That(selection.SelectedSongId, Is.Null);
            Assert.That(list.Move(selected, 1), Is.Null);
            var presentation = SongSelectionPresentation.Create(selection, list, settings.difficulties,
                preferences, null, false, null);
            Assert.That(presentation.CanPlay, Is.False);
            Assert.That(presentation.Message, Does.Contain("즐겨찾기"));
            Assert.That(selection.SelectedScrollMode, Is.EqualTo(ScrollMode.Bpm));
            Assert.That(selection.SelectedMultiplier, Is.EqualTo(1.5));
            list.FavoritesOnly = false;
            Assert.That(list.Refresh(library, preferences, selected), Is.EqualTo(list.Entries[0].Id));
        }

        [Test]
        [Category("Fast")]
        public void DisplayUsesSelectedChartAndRulesWithoutViewControls()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
            var library = new SongLibrary();
            library.Register(new BuiltInSongProvider(settings.catalog));
            var preferences = new SelectionPreferences(key);
            var selection = new SongSelectionService(settings, library, preferences);
            selection.List.Refresh(library, preferences, "test-pulse");
            selection.UpdateSelection("test-pulse", settings.difficulties[1], ScrollMode.Constant, 1);
            var presentation = SongSelectionPresentation.Create(selection, selection.List,
                settings.difficulties, preferences, null, false, null);
            Assert.That(presentation.CanPlay, Is.True);
            Assert.That(presentation.Difficulties[1], Is.True);
            Assert.That(presentation.Title, Is.EqualTo(library.Find("test-pulse").Title));
            selection.UpdateSelection("test-pulse", settings.difficulties[0], ScrollMode.Constant, 1);
            presentation = SongSelectionPresentation.Create(selection, selection.List,
                settings.difficulties, preferences, null, true, null);
            Assert.That(presentation.CanPlay, Is.False);
            Assert.That(presentation.Message, Does.Contain("채보가 없습니다"));
            Assert.That(presentation.Heading, Is.EqualTo("실력 검정"));
        }

        [Test]
        [Category("Fast")]
        public void InformationPanelReservesHeaderAndFooterAndAlbumFillsFrame()
        {
            var view = SongSelectionLayoutBuilder.Build();

            try
            {
                view.Validate();
                Assert.That(view.practice.gameObject.activeSelf, Is.True);
                Assert.That(view.libraryPanel.activeSelf, Is.False);
                Assert.That(view.play, Is.SameAs(view.practice.play));
                Assert.That(view.difficultyButtons, Is.SameAs(view.practice.difficulties));
                Assert.That(view.libraryPanel.transform.GetSiblingIndex(),
                    Is.GreaterThan(view.practice.transform.GetSiblingIndex()));
                Assert.That(view.modePopup.transform.GetSiblingIndex(),
                    Is.GreaterThan(view.libraryPanel.transform.GetSiblingIndex()));
                Assert.That(
                    view.album.GetComponent<UnityEngine.UI.AspectRatioFitter>().aspectMode,
                    Is.EqualTo(UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
