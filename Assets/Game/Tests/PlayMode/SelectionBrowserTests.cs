using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SelectionBrowserTests : GameplayTestContext
    {
        private sealed class LongSongListProvider : ISongProvider
        {
            public IReadOnlyList<SongEntry> GetEntries() =>
                Enumerable
                    .Range(0, 24)
                    .Select(i => new SongEntry(
                        $"scroll-test-{i}",
                        $"Scroll Test {i:00}",
                        "Test",
                        30,
                        1,
                        4,
                        120,
                        120
                    ))
                    .ToArray();

            public System.Threading.Tasks.Task<PlayableSong> LoadAsync(
                string songId,
                System.Threading.CancellationToken cancellationToken
            ) => throw new System.NotSupportedException();
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator KeyboardSelectionKeepsRowsVisibleWithDifferentHeightsAndSpacing()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.library);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var provider = new LongSongListProvider();
            flow.Library.Register(provider);

            try
            {
                var layout = view.rowContent.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 13;

                for (int i = 0; i < view.Rows.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        view.Rows[i].GetComponent<LayoutElement>().preferredHeight = 83;
                    }
                    else
                    {
                        view.Rows[i].GetComponent<LayoutElement>().preferredHeight = 127;
                    }
                }

                Canvas.ForceUpdateCanvases();
                EventSystem.current.SetSelectedGameObject(view.Rows[0].gameObject);

                for (int i = 1; i < view.Rows.Count; i++)
                {
                    yield return Press(Key.DownArrow);
                    AssertRowVisible(view, i);
                }

                for (int i = view.Rows.Count - 2; i >= 0; i--)
                {
                    yield return Press(Key.UpArrow);
                    AssertRowVisible(view, i);
                }
            }
            finally
            {
                flow.Library.Unregister(provider);
            }
        }

        private static void AssertRowVisible(SongSelectionView view, int index)
        {
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(view.Rows[index].gameObject)
            );

            var corners = new Vector3[4];
            ((RectTransform)view.Rows[index].transform).GetWorldCorners(corners);

            foreach (var corner in corners)
            {
                float y = view.scroll.viewport.InverseTransformPoint(corner).y;
                Assert.That(
                    y,
                    Is.InRange(
                        view.scroll.viewport.rect.yMin - 1,
                        view.scroll.viewport.rect.yMax + 1
                    ),
                    $"Selected row {index} must remain inside the viewport."
                );
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DemoCoverAndRowsRenderAtBothResolutions()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            screen.SelectSong("demo-melody");
            yield return null;
            Assert.That(view.songTitle.text, Does.Contain("Demo Melody"));
            Assert.That(view.album.sprite, Is.Not.Null);
            Assert.That(view.artistBpm.text, Does.Contain("120"));
            CaptureSelection("demo-selection-1920", 1920, 1080);
            CaptureSelection("demo-selection-1280");
            CaptureSelection("demo-selection-4x3", 1024, 768);
            CaptureSelection("demo-selection-ultrawide", 2560, 1080);
            yield return Click(view.practice.library);
            CaptureSelection("selection-library-1920", 1920, 1080);
            CaptureSelection("selection-library-1280");
            CaptureSelection("selection-library-4x3", 1024, 768);
            CaptureSelection("selection-library-ultrawide", 2560, 1080);
            yield return Click(view.modeButton);
            CaptureSelection("selection-mode-popup-1920", 1920, 1080);
            yield return Click(view.cancelMode);
            yield return Click(view.closeLibrary);
            yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            Assert.That(
                Object.FindFirstObjectByType<RhythmGameController>().Chart.Count,
                Is.EqualTo(54)
            );
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator FavoritesEmptyStateAndSortingUseSongIds()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.library);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows.Count, Is.Zero);
            Assert.That(view.play.interactable, Is.False);
            yield return Click(view.allTab);

            var row = view.Rows.First(r => r.Entry.Id == "test-pulse");
            yield return Click(row.favorite);
            Assert.That(new SelectionPreferences().IsFavorite("test-pulse"), Is.True);
            yield return Click(view.favoritesTab);
            Assert.That(view.Rows.Count, Is.EqualTo(1));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("test-pulse"));
            yield return Click(view.Rows[0].favorite);
            Assert.That(view.Rows.Count, Is.Zero);
            Assert.That(view.play.interactable, Is.False);
            yield return Click(view.allTab);
            Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong("tempo-pulse");
            yield return Choose(view.sortList, 2);
            yield return null;
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
            Assert.That(
                view.Rows.Select(r => r.Entry.Id),
                Is.EqualTo(
                    flow.Library.Entries.OrderBy(s => s.MinBpm)
                        .ThenBy(s => s.Title)
                        .Select(s => s.Id)
                )
            );
            Assert.That(
                view.Rows.Select(r => r.Entry.MinBpm).Distinct().Count(),
                Is.EqualTo(flow.Library.Entries.Select(s => s.MinBpm).Distinct().Count())
            );
            Assert.That(view.Rows.All(r => r.bpm && !string.IsNullOrEmpty(r.bpm.text)), Is.True);
            yield return Choose(view.sortList, 1);
            yield return null;
            Assert.That(
                view.Rows.Select(r => r.Entry.Id),
                Is.EqualTo(
                    flow.Library.Entries.OrderBy(
                            s => s.Artist,
                            System.StringComparer.OrdinalIgnoreCase
                        )
                        .ThenBy(s => s.Title)
                        .Select(s => s.Id)
                )
            );
            Assert.That(
                view.Rows.Select(r => r.Entry.Artist).Distinct().Count(),
                Is.EqualTo(flow.Library.Entries.Select(s => s.Artist).Distinct().Count())
            );
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
            yield return Choose(view.sortList, 0);
            yield return null;
            Assert.That(
                view.Rows.Select(r => r.Entry.Id),
                Is.EqualTo(
                    flow.Library.Entries.OrderBy(
                            s => s.Title,
                            System.StringComparer.OrdinalIgnoreCase
                        )
                        .Select(s => s.Id)
                )
            );
            yield return Choose(view.sortList, 2);
            string selected = flow.Selection.SelectedSongId;
            yield return Click(view.closeLibrary);
            yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            flow.ReturnFromGameplay();
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionView>());
            view = Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(view.libraryPanel.activeSelf, Is.False);
            yield return Click(view.practice.library);
            Assert.That(view.sortList.value, Is.EqualTo(2));
            Assert.That(flow.Selection.SelectedSongId, Is.EqualTo(selected));
            Assert.That(view.Rows.Select(row => row.Entry.Id), Is.EqualTo(flow.Selection.List.Entries.Select(song => song.Id)));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator TimingAndOptionsSurviveSettingsAndSceneReturn()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.settingsButton);
            yield return Await(() => Object.FindFirstObjectByType<SettingsView>());
            yield return null;

            var settingsView = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(settingsView.gameplayTab);
            settingsView.timingOffset.value = 125;
            settingsView.speedSlider.value = 1.5f;
            yield return Click(settingsView.back);
            yield return Await(() => !Object.FindFirstObjectByType<SettingsView>());
            yield return Await(() => Object.FindFirstObjectByType<SongSelectionView>());
            yield return null;
            view = Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(view.timingOffset.value, Is.EqualTo(125));
            Assert.That(
                Object.FindFirstObjectByType<AppFlowController>().SelectedMultiplier,
                Is.EqualTo(1.5)
            );
            yield return Click(view.play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            Assert.That(
                Object.FindFirstObjectByType<AppFlowController>().CurrentRequest.TimingOffsetMs,
                Is.EqualTo(125)
            );
            Assert.That(
                Object.FindFirstObjectByType<RhythmDojo.Audio.SongClock>().InputTimingOffsetSeconds,
                Is.EqualTo(.125)
            );
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator CompletedRunSavesBestAndAbortedRunDoesNotCreateRecord()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            screen.SelectSong("demo-melody");
            yield return Click(view.play);
            yield return Await(
                () => Object.FindFirstObjectByType<RhythmGameController>(),
                "Record test: enter gameplay"
            );
            yield return null;

            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var root = Object.FindFirstObjectByType<GameplayCompositionRoot>();
            root.ReturnToSelection();
            yield return Await(
                () => Object.FindFirstObjectByType<SongSelectionView>(),
                "Record test: return to selection"
            );
            yield return null;
            Assert.That(
                flow.Preferences.FindRecord("demo-melody", flow.SelectedDifficulty.name),
                Is.Null
            );
            view = Object.FindFirstObjectByType<SongSelectionView>();

            string replayDetails = view.details.text;
            int playClicks = 0;
            view.play.onClick.AddListener(() => playClicks++);
            yield return Click(view.play);
            Assert.That(
                playClicks,
                Is.EqualTo(1),
                "Second play click delivered; details: " + replayDetails
            );
            Assert.That(
                flow.Transitioning || Object.FindFirstObjectByType<RhythmGameController>(),
                Is.True,
                "Second play request; details: " + replayDetails
            );
            yield return Await(
                () => Object.FindFirstObjectByType<RhythmGameController>(),
                "Record test: enter gameplay"
            );
            yield return null;
            game = Object.FindFirstObjectByType<RhythmGameController>();

            var inputs = new List<(double time, int lane, bool press)>();

            for (int i = 0; i < game.Chart.Count; i++)
            {
                var note = game.Chart[i];
                inputs.Add((note.StartTime, note.Lane, true));
                inputs.Add(
                    (
                        (
                            note.Kind switch
                            {
                                NoteKind.Hold => note.EndTime,
                                _ => note.StartTime + .01,
                            }
                        ),
                        note.Lane,
                        false
                    )
                );
            }

            game.Session.Start();

            foreach (var input in inputs.OrderBy(e => e.time).ThenBy(e => e.press))
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

            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return null;
            yield return null;

            var best = new SelectionPreferences().FindRecord(
                "demo-melody",
                RecordIdentity.Rules(
                    flow.CurrentRequest.Song.ChartId,
                    flow.CurrentRequest.Song.Revision,
                    flow.CurrentRequest.Rules
                )
            );
            Assert.That(best, Is.Not.Null);
            Assert.That(best.score, Is.EqualTo(1000000));
            Assert.That(best.maxCombo, Is.EqualTo(game.Session.Snapshot.TotalNotes));
            Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(
                () => Object.FindFirstObjectByType<SongSelectionView>(),
                "Record test: return to selection"
            );
            yield return null;
            Assert.That(
                Object.FindFirstObjectByType<SongSelectionView>().record.text,
                Does.Contain("1,000,000")
            );
            var recordView = Object.FindFirstObjectByType<SongSelectionView>();
            Assert.That(recordView.record.text, Does.Not.Contain("최대 콤보"));
            Assert.That(recordView.comboRecord.text, Does.Contain($"최대 콤보  {best.maxCombo:N0}"));
            Assert.That(recordView.comboRecord.text, Does.Contain("Gauge"));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EscapePopupBlocksPlaybackAndReturnsToTitle()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.library);
            yield return Click(view.modeButton);
            yield return Press(Key.Escape);
            Assert.That(view.modePopup.activeSelf, Is.False);
            Assert.That(view.libraryPanel.activeSelf, Is.True);
            Assert.That(view.play.interactable, Is.False);
            yield return Press(Key.Escape);
            Assert.That(view.libraryPanel.activeSelf, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(view.practice.library.gameObject));
            yield return Press(Key.Escape);
            yield return Await(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                == MainMenuActions.ModeSelectionScenePath);
            yield return Press(Key.Escape);
            yield return Await(() => Object.FindFirstObjectByType<MainMenuEntranceScreen>());
        }
    }
}
