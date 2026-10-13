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
        public IEnumerator SavedThreeChartProjectSelectsLoadsAndRefreshesEachSlot()
        {
            string root = Path.Combine(UnityEngine.Application.temporaryCachePath, "ThreeCharts-" + Guid.NewGuid());
            var store = new FileSongProjectStore(root);
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var project = new SongProject { Title = "Three chart integration", Artist = "Verification",
                Charts = Enumerable.Range(0, 3).Select(i => new ChartDocument { Slot = (ChartSlot)i, Name = ((ChartSlot)i).ToString(),
                    ModeId = flow.Settings.catalog[0].Mode.name, LaneCount = 4, CompletionTimeSeconds = 12,
                    Notes = Enumerable.Range(0, i + 1).Select(n => NoteData.Tap(n, n + 1)).ToArray() }).ToArray() };
            var provider = new ProjectSongProvider(store, flow.Settings.catalog[0].Mode);
            try
            {
                var imported = store.ImportAsync(project.Id, Path.GetFullPath("Assets/Game/Audio/TestPulse.wav"), default);
                yield return new WaitUntil(() => imported.IsCompleted); project.Audio = imported.GetAwaiter().GetResult();
                var saved = store.SaveAsync(project, default); yield return new WaitUntil(() => saved.IsCompleted); saved.GetAwaiter().GetResult();
                flow.Library.Register(provider);
                for (int i = 0; i < 3; i++)
                {
                    Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong(project.Id.ToString("D"));
                    var view = Object.FindFirstObjectByType<SongSelectionView>();
                    Assert.That(view.difficultyButtons.All(button => button.interactable), Is.True);
                    yield return Click(view.difficultyButtons[i]);
                    CaptureSelection("three-chart-selection-" + i);
                    yield return Click(view.play);
                    yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
                    Assert.That(flow.CurrentRequest.Song.ChartId, Is.EqualTo(project.Charts[i].Id.ToString("D")));
                    Assert.That(Object.FindFirstObjectByType<RhythmGameController>().Chart.Count, Is.EqualTo(i + 1));
                    yield return Press(UnityEngine.InputSystem.Key.Escape);
                    yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
                }
                project.Charts[2].Notes = Array.Empty<NoteData>();
                saved = store.SaveAsync(project, default); yield return new WaitUntil(() => saved.IsCompleted); saved.GetAwaiter().GetResult();
                flow.Library.Refresh(); Object.FindFirstObjectByType<SongSelectionScreen>().SelectSong(project.Id.ToString("D"));
                var invalid = Object.FindFirstObjectByType<SongSelectionView>();
                yield return Click(invalid.difficultyButtons[2]);
                Assert.That(invalid.play.interactable, Is.False); Assert.That(invalid.details.text, Does.Contain("재생 불가"));
                project.Charts = project.Charts.Take(2).ToArray();
                saved = store.SaveAsync(project, default); yield return new WaitUntil(() => saved.IsCompleted); saved.GetAwaiter().GetResult();
                flow.Library.Refresh(); Assert.That(invalid.difficultyButtons[2].interactable, Is.False);
            }
            finally
            {
                flow.Library.Unregister(provider);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
        [UnityTest]
        public IEnumerator ModalScopeRestoresInputAndFocusAfterOpeningFailureAndDispose()
        {
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            var focus = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.Throws<InvalidOperationException>(() => ModalInputScope.Open(screen.gameObject.scene, () =>
            { Assert.That(screen.enabled, Is.False); throw new InvalidOperationException("Simulated popup load failure"); }));
            Assert.That(screen.enabled, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.EqualTo(focus));
            using (var scope = new ModalInputScope(screen.gameObject.scene))
            {
                Assert.Throws<InvalidOperationException>(() => scope.Close(() => null, null));
                Assert.That(screen.enabled, Is.False);
                yield return Press(UnityEngine.InputSystem.Key.Enter);
                Assert.That(Object.FindFirstObjectByType<AppFlowController>().Transitioning, Is.False);
            }
            Assert.That(screen.enabled, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.EqualTo(focus));
        }
    }
}
