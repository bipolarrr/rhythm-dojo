using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class ProjectLibraryFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator RestoredAudioLoadsAfterStructuredFailureAndRefresh()
        {
            string root = Path.Combine(UnityEngine.Application.temporaryCachePath, "Recovery-" + Guid.NewGuid());
            var store = new FileSongProjectStore(root);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var mode = flow.Settings.catalog[0].Mode;
            var project = new SongProject
            {
                Title = "Recovered project",
                Charts = new[] { new ChartDocument
                {
                    Slot = ChartSlot.Standard, ModeId = mode.name, LaneCount = 4,
                    CompletionTimeSeconds = 3, Notes = new[] { NoteData.Tap(0, 1) }
                } }
            };
            PlayableSong loaded = null;
            try
            {
                var import = store.ImportAsync(project.Id, Path.GetFullPath("Assets/Game/Audio/TestPulse.wav"), default);
                yield return new WaitUntil(() => import.IsCompleted);
                project.Audio = import.GetAwaiter().GetResult();
                var save = store.SaveAsync(project, default);
                yield return new WaitUntil(() => save.IsCompleted);
                save.GetAwaiter().GetResult();
                var library = new SongLibrary();
                library.Register(new ProjectSongProvider(store, mode));
                string id = project.Id.ToString("D");
                string path = store.ResolveAudioPath(project.Id, project.Audio);
                byte[] bytes = File.ReadAllBytes(path);
                File.Delete(path);
                var missing = library.LoadChartAsync(id, ChartSlot.Standard, default);
                yield return new WaitUntil(() => missing.IsCompleted);
                Assert.That(missing.Exception.InnerException, Is.TypeOf<SongLoadException>());
                Assert.That(((SongLoadException)missing.Exception.InnerException).Failure.Kind,
                    Is.EqualTo(SongLoadFailureKind.MissingAudio));
                File.WriteAllBytes(path, bytes);
                library.Refresh();
                var retry = library.LoadChartAsync(id, ChartSlot.Standard, default);
                yield return new WaitUntil(() => retry.IsCompleted);
                loaded = retry.GetAwaiter().GetResult();
                Assert.That(loaded.AudioClip.samples, Is.GreaterThan(0));
                Assert.That(loaded.ChartId, Is.EqualTo(project.Charts[0].Id.ToString("D")));
            }
            finally
            {
                loaded?.Dispose();
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [UnityTest]
        public IEnumerator SavedThreeChartProjectSelectsLoadsAndRefreshesEachSlot()
        {
            string root = Path.Combine(
                UnityEngine.Application.temporaryCachePath,
                "ThreeCharts-" + Guid.NewGuid()
            );
            var store = new FileSongProjectStore(root);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var project = new SongProject
            {
                Title = "Three chart integration",
                Artist = "Verification",
                Grid = new BeatGridSettings
                {
                    OriginSeconds = .237,
                    Meters = new[] { new MeterPoint(0, 7, 8), new MeterPoint(6, 3, 4) }
                },
                Charts = Enumerable
                    .Range(0, 3)
                    .Select(i => new ChartDocument
                    {
                        Slot = (ChartSlot)i,
                        Name = ((ChartSlot)i).ToString(),
                        ModeId = flow.Settings.catalog[0].Mode.name,
                        LaneCount = 4,
                        CompletionTimeSeconds = 12,
                        Notes = Enumerable
                            .Range(0, i + 1)
                            .Select(n => NoteData.Tap(n, n + 1))
                            .ToArray(),
                    })
                    .ToArray(),
            };
            var provider = new ProjectSongProvider(store, flow.Settings.catalog[0].Mode);

            try
            {
                var imported = store.ImportAsync(
                    project.Id,
                    Path.GetFullPath("Assets/Game/Audio/TestPulse.wav"),
                    default
                );
                yield return new WaitUntil(() => imported.IsCompleted);
                project.Audio = imported.GetAwaiter().GetResult();

                var saved = store.SaveAsync(project, default);
                yield return new WaitUntil(() => saved.IsCompleted);
                saved.GetAwaiter().GetResult();
                string path = Path.Combine(store.GetSongFolder(project.Id), FileSongProjectStore.FileName);
                string original = File.ReadAllText(path);
                Assert.That(original, Does.Contain("\"version\": 3"));
                Assert.That(ProjectValidation.ContentEquals(project, store.Read(project.Id)), Is.True);
                flow.Library.Register(provider);

                for (int i = 0; i < 3; i++)
                {
                    Object
                        .FindFirstObjectByType<SongSelectionScreen>()
                        .SelectSong(project.Id.ToString("D"));

                    var view = Object.FindFirstObjectByType<SongSelectionView>();
                    Assert.That(view.difficultyButtons.All(button => button.interactable), Is.True);
                    yield return Click(view.difficultyButtons[i]);
                    CaptureSelection("three-chart-selection-" + i);
                    yield return Click(view.play);
                    yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
                    yield return null;
                    Assert.That(
                        flow.CurrentRequest.Song.ChartId,
                        Is.EqualTo(project.Charts[i].Id.ToString("D"))
                    );
                    Assert.That(
                        Object.FindFirstObjectByType<RhythmGameController>().Chart.Count,
                        Is.EqualTo(i + 1)
                    );
                    yield return Press(UnityEngine.InputSystem.Key.Escape);
                    yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
                    yield return null;
                }

                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(ProjectValidation.ContentEquals(project, store.Read(project.Id)), Is.True);

                project.Charts[2].Notes = Array.Empty<NoteData>();
                saved = store.SaveAsync(project, default);
                yield return new WaitUntil(() => saved.IsCompleted);
                saved.GetAwaiter().GetResult();
                flow.Library.Refresh();
                Object
                    .FindFirstObjectByType<SongSelectionScreen>()
                    .SelectSong(project.Id.ToString("D"));

                var invalid = Object.FindFirstObjectByType<SongSelectionView>();
                yield return Click(invalid.difficultyButtons[2]);
                Assert.That(invalid.play.interactable, Is.False);
                Assert.That(invalid.details.text, Does.Contain("재생 불가"));
                project.Charts = project.Charts.Take(2).ToArray();
                saved = store.SaveAsync(project, default);
                yield return new WaitUntil(() => saved.IsCompleted);
                saved.GetAwaiter().GetResult();
                flow.Library.Refresh();
                Assert.That(invalid.difficultyButtons[2].interactable, Is.False);
            }
            finally
            {
                flow.Library.Unregister(provider);

                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [UnityTest]
        public IEnumerator NestedModalScopesKeepInputBlockedUntilTheLastOwnerCloses()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var focus = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            var first = new ModalInputScope(screen.gameObject.scene);
            var second = new ModalInputScope(screen.gameObject.scene);
            try
            {
                Assert.That(screen.enabled, Is.False);
                first.Dispose();
                Assert.That(screen.enabled, Is.False, "The second owner must keep background input blocked.");
                yield return Press(UnityEngine.InputSystem.Key.Enter);
                Assert.That(Object.FindFirstObjectByType<AppFlowController>().Transitioning, Is.False);
                second.Dispose();
                Assert.That(screen.enabled, Is.True);
                Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                    Is.EqualTo(focus));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ModalScopeRestoresInputAndFocusAfterOpeningFailureAndDispose()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var focus = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.Throws<InvalidOperationException>(() =>
                ModalInputScope.Open(
                    screen.gameObject.scene,
                    () =>
                    {
                        Assert.That(screen.enabled, Is.False);
                        throw new InvalidOperationException("Simulated popup load failure");
                    }
                )
            );
            Assert.That(screen.enabled, Is.True);
            Assert.That(
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(focus)
            );
            using (var scope = new ModalInputScope(screen.gameObject.scene))
            {
                Assert.Throws<InvalidOperationException>(() => scope.Close(() => null, null));
                Assert.That(screen.enabled, Is.False);
                yield return Press(UnityEngine.InputSystem.Key.Enter);
                Assert.That(
                    Object.FindFirstObjectByType<AppFlowController>().Transitioning,
                    Is.False
                );
            }

            Assert.That(screen.enabled, Is.True);
            Assert.That(
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(focus)
            );
        }
    }
}
