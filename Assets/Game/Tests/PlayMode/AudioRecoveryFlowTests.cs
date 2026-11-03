using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class AudioRecoveryFlowTests : GameplayTestContext
    {
        private sealed class InvalidatingLoader : ISongDocumentLoader
        {
            private readonly ISongDocumentLoader loader;

            public InvalidatingLoader(ISongDocumentLoader loader)
            {
                this.loader = loader;
            }

            public async Task<PlayableSong> LoadAsync(SongDocument document, CancellationToken token)
            {
                var song = await loader.LoadAsync(document, token);
                song.InvalidateAudio();
                return song;
            }
        }

        [UnityTest]
        public IEnumerator ExternalWaveReloadsAfterResetAndReturnsEditorSession()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            WriteWave(path);
            try
            {
                var flow = Object.FindFirstObjectByType<AppFlowController>();
                var source = flow.Settings.catalog[0];
                var document = SongAssetAdapter.ToDocument(source);
                document.AudioId = path;
                document.Notes = new[] { NoteData.Tap(0, 25) };
                document.CompletionTime = 28;
                string returned = null;
                var loader = new DocumentSongLoader(_ => source.Mode, ChartEditorCompositionRoot.LoadAudio);
                var task = flow.PlayAsync(document, loader, "recovery-session",
                    AppFlowController.SelectionPath, id => returned = id, CancellationToken.None);
                yield return Await(() => task.IsCompleted);
                Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
                yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
                yield return null;
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
                var originalClip = flow.CurrentRequest.Song.AudioClip;
                game.StartSession();
                yield return new WaitForSecondsRealtime(.4f);
                menu.Open();
                double time = game.Clock.SongTime;
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                var resetSamples = new float[1024];
                bool resetReadable = originalClip.GetData(resetSamples, 0);
                Directory.CreateDirectory("Logs/pr-quality-remediation");
                File.WriteAllText("Logs/pr-quality-remediation/task2-reset-probe.txt",
                    $"Original after reset: readable={resetReadable}, peak={Peak(resetSamples)}\n");
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                menu.BeginResume();
                yield return Await(() => !game.IsPaused);
                Assert.That(flow.CurrentRequest.Song.AudioClip, Is.Not.SameAs(originalClip),
                    "An external clip must be decoded again after audio reset.");
                Assert.That(game.Clock.SongTime, Is.GreaterThanOrEqualTo(time));
                var samples = new float[1024];
                Assert.That(flow.CurrentRequest.Song.AudioClip.GetData(samples, 0), Is.True);
                Assert.That(Peak(samples), Is.GreaterThan(.1f));
                var audioSource = ((Component)game.Clock).GetComponent<AudioSource>();
                Assert.That(audioSource.clip, Is.SameAs(flow.CurrentRequest.Song.AudioClip));
                double outputPeak = 0;
                for (int i = 0; i < 15; i++)
                {
                    AudioListener.GetOutputData(samples, 0);
                    outputPeak = Math.Max(outputPeak, Peak(samples));
                    yield return new WaitForSecondsRealtime(.02f);
                }
                File.WriteAllText("Logs/pr-quality-remediation/task2-recovered-output.txt",
                    $"Recovered mixer peak={outputPeak:R}; samples={audioSource.clip.samples}; frequency={audioSource.clip.frequency}\n");
                Assert.That(outputPeak, Is.GreaterThan(.001), "Recovered sound must reach the mixer output.");
                var recovered = flow.CurrentRequest.Song.AudioClip;
                Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
                yield return Await(() => returned != null);
                yield return null;
                Assert.That(returned, Is.EqualTo("recovery-session"));
                Assert.That(originalClip == null, Is.True);
                Assert.That(recovered == null, Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator MissingExternalWaveKeepsPauseAndRetryRestoresPlayback()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            WriteWave(path);
            try
            {
                yield return PlayWave(path);
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
                game.StartSession();
                yield return new WaitForSecondsRealtime(.4f);
                menu.Open();
                double time = game.Clock.SongTime;
                int resolved = game.Session.Resolved;
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                File.Delete(path);
                yield return Click(menu.resumeButton);
                yield return Await(() => !menu.RecoveringAudio);
                Assert.That(menu.RecoveryError, Is.Not.Empty);
                Assert.That(menu.recoveryStatus.gameObject.activeSelf, Is.True);
                Assert.That(menu.panel.activeSelf, Is.True);
                Assert.That(menu.countdown.activeSelf, Is.False);
                game.ResumeSession();
                game.RestartSession();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(game.IsPaused, Is.True);
                Assert.That(game.Clock.Running, Is.False);
                Assert.That(game.Clock.SongTime, Is.EqualTo(time));
                Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
                WriteWave(path);
                yield return Click(menu.resumeButton);
                yield return Await(() => !game.IsPaused);
                Assert.That(menu.RecoveryError, Is.Null);
                Assert.That(game.Clock.Running, Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator ExitRemainsClickableDuringRecoveryAndLateClipIsReleased()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            WriteWave(path);
            var pending = new TaskCompletionSource<SongAudioLease>();
            Task<SongAudioLease> late = null;
            int releases = 0;
            try
            {
                yield return PlayWave(path, async (audio, token) =>
                {
                    var lease = await ChartEditorCompositionRoot.LoadAudio(audio, token);
                    return new SongAudioLease(lease.Clip, () =>
                    {
                        releases++;
                        lease.Dispose();
                    }, cancellation => pending.Task);
                });
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
                game.StartSession();
                yield return new WaitForSecondsRealtime(.4f);
                menu.Open();
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                yield return Click(menu.resumeButton);
                Assert.That(menu.RecoveringAudio, Is.True);
                Assert.That(menu.exitButton.interactable, Is.True);
                Assert.That(game.Clock.Running, Is.False);
                yield return Click(menu.exitButton);
                yield return Await(() => Object.FindFirstObjectByType<SongSelectionScreen>());
                Assert.That(releases, Is.EqualTo(1));
                late = ChartEditorCompositionRoot.LoadAudio(path, CancellationToken.None);
                yield return Await(() => late.IsCompleted);
                Assert.That(late.IsFaulted, Is.False, late.Exception?.ToString());
                var clip = late.Result.Clip;
                pending.SetResult(new SongAudioLease(clip, () =>
                {
                    releases++;
                    late.Result.Dispose();
                }));
                yield return Await(() => releases == 2);
                yield return null;
                Assert.That(clip == null, Is.True);
                Assert.That(Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
            }
            finally
            {
                pending.TrySetCanceled();
                if (late != null && late.Status == TaskStatus.RanToCompletion)
                {
                    late.Result.Dispose();
                }
                File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator BuiltInAudioSurvivesResetWithoutReplacingBorrowedClip()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var task = flow.PlaySelectedAsync(CancellationToken.None);
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            var clip = flow.CurrentRequest.Song.AudioClip;
            game.StartSession();
            yield return new WaitForSecondsRealtime(.4f);
            menu.Open();
            Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
            yield return null;
            yield return Click(menu.resumeButton);
            yield return Await(() => !game.IsPaused);
            Assert.That(flow.CurrentRequest.Song.AudioClip, Is.SameAs(clip));
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            Assert.That(Peak(samples), Is.GreaterThan(.001));
            var output = new float[1024];
            double outputPeak = 0;
            for (int i = 0; i < 100; i++)
            {
                AudioListener.GetOutputData(output, 0);
                outputPeak = Math.Max(outputPeak, Peak(output));
                yield return new WaitForSecondsRealtime(.02f);
            }
            File.WriteAllText("Logs/pr-quality-remediation/task2-builtin-output.txt",
                $"Borrowed clip mixer peak={outputPeak:R}; samples={clip.samples}; frequency={clip.frequency}\n");
            Assert.That(outputPeak, Is.GreaterThan(.001), "Borrowed sound must still reach the mixer after reset.");
        }

        [UnityTest]
        public IEnumerator ResetBeforeStartBlocksQueuedSpaceUntilAudioIsRecovered()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            WriteWave(path);
            try
            {
                yield return PlayWave(path);
                var flow = Object.FindFirstObjectByType<AppFlowController>();
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
                var clip = flow.CurrentRequest.Song.AudioClip;
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                yield return Press(Key.Space);
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready));
                Assert.That(game.Clock.Running, Is.False);
                Assert.That(menu.panel.activeSelf, Is.True);
                yield return Click(menu.restartButton);
                yield return Await(() => game.Clock.Running);
                Assert.That(flow.CurrentRequest.Song.AudioClip, Is.Not.SameAs(clip));
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            }
            finally
            {
                File.Delete(path);
            }
        }

        private IEnumerator PlayWave(string path,
            Func<string, CancellationToken, Task<SongAudioLease>> loadAudio = null,
            bool invalidateBeforeGameplay = false)
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = SongAssetAdapter.ToDocument(source);
            document.AudioId = path;
            document.Notes = new[] { NoteData.Tap(0, 25) };
            document.CompletionTime = 28;
            ISongDocumentLoader loader = new DocumentSongLoader(_ => source.Mode,
                loadAudio ?? ChartEditorCompositionRoot.LoadAudio);
            if (invalidateBeforeGameplay)
            {
                loader = new InvalidatingLoader(loader);
            }
            var task = flow.PlayAsync(document, loader, "recovery-session",
                AppFlowController.SelectionPath, _ => { }, CancellationToken.None);
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidatedBeforeGameplayInitializationExposesRecoveryWithoutAnotherReset()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            WriteWave(path);
            try
            {
                yield return PlayWave(path, null, true);
                var flow = Object.FindFirstObjectByType<AppFlowController>();
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
                Assert.That(flow.CurrentRequest.Song.NeedsAudioRecovery, Is.True);
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready));
                Assert.That(menu.panel.activeSelf, Is.True);
                yield return Click(menu.restartButton);
                yield return Await(() => game.Clock.Running);
                Assert.That(flow.CurrentRequest.Song.NeedsAudioRecovery, Is.False);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static double Peak(float[] samples)
        {
            double peak = 0;
            foreach (float sample in samples)
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }
            return peak;
        }

        private static void WriteWave(string path)
        {
            const int frequency = 48000;
            const int frames = frequency * 30;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(frequency);
            writer.Write(frequency * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);
            for (int i = 0; i < frames; i++)
            {
                writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / frequency) * 12000));
            }
        }
    }
}
