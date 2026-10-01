using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Tests
{
    [Category("Authoring")]
    public sealed class AuthoringFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator UnsavedDocumentPlaytestReturnsSessionAndReleasesAudio()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; var document = SongAssetAdapter.ToDocument(source);
            document.SongId = "unsaved"; document.Title = "Unsaved song";
            int releases = 0; string returned = null;
            var audio = UnityEngine.Object.Instantiate(source.AudioClip);
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => Task.FromResult(
                new SongAudioLease(audio, () => { releases++; UnityEngine.Object.Destroy(audio); })));
            var task = flow.PlayAsync(document, loader, "editor-session-42", AppFlowController.SelectionPath,
                id => returned = id, CancellationToken.None);
            yield return Await(() => task.IsCompleted); Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            document.Title = "Edited after request"; document.Notes[0] = NoteData.Tap(99, 0);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.ReadModel.Snapshot.SongTitle, Is.EqualTo("Unsaved song"));
            Assert.That(flow.CurrentRequest.Song.Chart[0].Lane, Is.Zero);
            Assert.That(releases, Is.Zero);
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
            yield return Await(() => returned != null); yield return null;
            Assert.That(returned, Is.EqualTo("editor-session-42"));
            Assert.That(releases, Is.EqualTo(1)); Assert.That(audio == null, Is.True);
            Assert.That(source.AudioClip, Is.Not.Null); Assert.That(flow.CurrentRequest, Is.Null);
        }

        [UnityTest]
        public IEnumerator CanceledEditorLoadStaysOnScreenAndAllowsRetry()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; int releases = 0;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => completion.Task);
            using var cancellation = new CancellationTokenSource();
            var task = flow.PlayAsync(SongAssetAdapter.ToDocument(source), loader, "cancelled", AppFlowController.SelectionPath,
                _ => Assert.Fail("Canceled load must not return a playtest"), cancellation.Token);
            Assert.That(flow.Transitioning, Is.True); cancellation.Cancel();
            completion.SetResult(new SongAudioLease(source.AudioClip, () => releases++));
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsCanceled, Is.True); Assert.That(releases, Is.EqualTo(1));
            Assert.That(flow.Transitioning, Is.False); Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>(), Is.Not.Null);
            var retry = flow.PlaySelectedAsync(CancellationToken.None);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(retry.IsFaulted, Is.False);
        }

        [UnityTest]
        public IEnumerator DelayedPlaytestKeepsOptionsCapturedAtRequest()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0]; var selectedDifficulty = flow.SelectedDifficulty;
            var completion = new TaskCompletionSource<SongAudioLease>();
            var loader = new DocumentSongLoader(_ => source.Mode, (_, token) => completion.Task);
            var task = flow.PlayAsync(SongAssetAdapter.ToDocument(source), loader, "delayed", AppFlowController.SelectionPath,
                _ => { }, CancellationToken.None);
            flow.Selection.UpdateSelection(source.SongId, flow.Settings.difficulties[0], ScrollMode.Bpm, 1.5);
            completion.SetResult(new SongAudioLease(source.AudioClip));
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.Difficulty, Is.EqualTo(selectedDifficulty));
            Assert.That(flow.CurrentRequest.ScrollMode, Is.EqualTo(ScrollMode.Constant));
            Assert.That(flow.CurrentRequest.Multiplier, Is.EqualTo(1));
        }
    }
}
