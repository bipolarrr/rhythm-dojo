using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using RhythmDojo.Core;
using RhythmDojo.UI;
using RhythmDojo.Services;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SelectionBrowserTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator DemoCoverAndRowsRenderAtBothResolutions()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            screen.SelectSong("demo-melody"); yield return null;
            Assert.That(view.songTitle.text, Does.Contain("Demo Melody"));
            Assert.That(view.album.sprite, Is.Not.Null);
            Assert.That(view.artistBpm.text, Does.Contain("120"));
            CaptureSelection("demo-selection-1920", 1920, 1080);
            CaptureSelection("demo-selection-1280");
            yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            Assert.That(Object.FindFirstObjectByType<RhythmGameController>().Chart.Count, Is.EqualTo(54));
        }
        [UnityTest]
        public IEnumerator FavoritesEmptyStateAndSortingUseSongIds()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows.Count, Is.Zero); Assert.That(view.play.interactable, Is.False);
            yield return Click(view.allTab);
            var row = view.Rows.First(r => r.Entry.Id == "test-pulse");
            yield return Click(row.favorite);
            Assert.That(new SelectionPreferences().IsFavorite("test-pulse"), Is.True);
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows.Count, Is.EqualTo(1)); Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("test-pulse"));
            yield return Click(view.Rows[0].favorite);
            Assert.That(view.Rows.Count, Is.Zero); Assert.That(view.play.interactable, Is.False);
            yield return Click(view.allTab);
            Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong("tempo-pulse");
            view.sortList.value = 2; yield return null;
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
            Assert.That(view.Rows.Select(r => r.Entry.Id), Is.EqualTo(new[] { "test-pulse", "demo-melody", "tempo-pulse" }));
            Assert.That(view.Rows.Select(r => r.Entry.MinBpm).Distinct().Count(), Is.EqualTo(3));
            Assert.That(view.Rows.All(r => r.bpm && !string.IsNullOrEmpty(r.bpm.text)), Is.True);
            view.sortList.value = 1; yield return null;
            Assert.That(view.Rows.Select(r => r.Entry.Id), Is.EqualTo(new[] { "tempo-pulse", "test-pulse", "demo-melody" }));
            Assert.That(view.Rows.Select(r => r.Entry.Artist).Distinct().Count(), Is.EqualTo(3));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
            view.sortList.value = 0; yield return null;
            Assert.That(view.Rows.Select(r => r.Entry.Id), Is.EqualTo(new[] { "test-pulse", "tempo-pulse", "demo-melody" }));
        }
        [UnityTest]
        public IEnumerator TimingAndOptionsSurviveSettingsAndSceneReturn()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            view.timingOffset.value = 125; view.multiplierList.value = 4;
            yield return Click(view.settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsView>()); yield return null;
            yield return Click(Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionView>()); yield return null;
            view = Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(view.timingOffset.value, Is.EqualTo(125));
            Assert.That(Object.FindFirstObjectByType<AppFlowController>().SelectedMultiplier, Is.EqualTo(1.5));
            yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            Assert.That(Object.FindFirstObjectByType<AppFlowController>().CurrentRequest.TimingOffsetMs, Is.EqualTo(125));
            Assert.That(Object.FindFirstObjectByType<RhythmDojo.Audio.SongClock>().InputTimingOffsetSeconds, Is.EqualTo(.125));
        }
        [UnityTest]
        public IEnumerator CompletedRunSavesBestAndAbortedRunDoesNotCreateRecord()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            screen.SelectSong("demo-melody"); yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>(), "Record test: enter gameplay"); yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var root = Object.FindFirstObjectByType<GameplayCompositionRoot>();
            root.ReturnToSelection();
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionView>(), "Record test: return to selection"); yield return null;
            Assert.That(flow.Preferences.FindRecord("demo-melody", flow.SelectedDifficulty.name), Is.Null);
            view = Object.FindFirstObjectByType<SongSelectionView>();
            string replayDetails = view.details.text;
            int playClicks = 0; view.play.onClick.AddListener(() => playClicks++);
            yield return Click(view.play);
            Assert.That(playClicks, Is.EqualTo(1), "Second play click delivered; details: " + replayDetails);
            Assert.That(flow.Transitioning || Object.FindFirstObjectByType<RhythmGameController>(), Is.True, "Second play request; details: " + replayDetails);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>(), "Record test: enter gameplay"); yield return null;
            game = Object.FindFirstObjectByType<RhythmGameController>();
            var inputs = new List<(double time, int lane, bool press)>();
            for (int i = 0; i < game.Chart.Count; i++)
            {
                var note = game.Chart[i];
                inputs.Add((note.StartTime, note.Lane, true));
                inputs.Add((note.Kind == NoteKind.Hold ? note.EndTime : note.StartTime + .01, note.Lane, false));
            }
            game.Session.Start();
            foreach (var input in inputs.OrderBy(e => e.time).ThenBy(e => e.press))
                if (input.press) game.Session.PressLane(input.lane, input.time);
                else game.Session.ReleaseLane(input.lane, input.time);
            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return null; yield return null;
            var best = new SelectionPreferences().FindRecord("demo-melody", flow.SelectedDifficulty.name);
            Assert.That(best, Is.Not.Null); Assert.That(best.score, Is.EqualTo(1000000));
            Assert.That(best.maxCombo, Is.EqualTo(54));
            Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionView>(), "Record test: return to selection"); yield return null;
            Assert.That(Object.FindFirstObjectByType<SongSelectionView>().record.text, Does.Contain("1,000,000"));
        }
        [UnityTest]
        public IEnumerator EscapePopupBlocksPlaybackAndReturnsToTitle()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Press(Key.Escape);
            Assert.That(view.backPopup.activeSelf, Is.True); Assert.That(view.play.interactable, Is.False);
            yield return Press(Key.Escape);
            Assert.That(view.backPopup.activeSelf, Is.False);
            yield return Click(view.back); yield return Click(view.confirmBack);
            yield return Await(() => Object.FindFirstObjectByType<MainMenuScreen>());
        }
    }
}