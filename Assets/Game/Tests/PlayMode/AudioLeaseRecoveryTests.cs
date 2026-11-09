using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Content;
using RhythmDojo.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class AudioLeaseRecoveryTests : GameplayTestContext
    {
        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator OnlyLatestResetPublishesAndEveryLeaseReleasesOnce()
        {
            var source = Object.FindFirstObjectByType<AppFlowController>().Settings.catalog[0];
            var first = new TaskCompletionSource<SongAudioLease>();
            var second = new TaskCompletionSource<SongAudioLease>();
            int loads = 0;
            int initialReleases = 0;
            int staleReleases = 0;
            int currentReleases = 0;
            var initial = new SongAudioLease(source.AudioClip, () => initialReleases++, token =>
            {
                loads++;
                if (loads == 1)
                {
                    return first.Task;
                }
                return second.Task;
            });
            using var song = new PlayableSong(SongAssetAdapter.ToDocument(source), source.Mode, initial);
            int changed = 0;
            song.AudioChanged += clip =>
            {
                Assert.That(initialReleases, Is.Zero, "Consumers must attach before old audio is released.");
                changed++;
            };
            song.InvalidateAudio();
            var old = song.RecoverAudioAsync(CancellationToken.None);
            song.InvalidateAudio();
            var latest = song.RecoverAudioAsync(CancellationToken.None);
            second.SetResult(new SongAudioLease(source.AudioClip, () => currentReleases++));
            yield return Await(() => latest.IsCompleted);
            Assert.That(latest.Result, Is.True);
            first.SetResult(new SongAudioLease(source.AudioClip, () => staleReleases++));
            yield return Await(() => old.IsCompleted);
            Assert.That(old.Result, Is.False);
            Assert.That(song.NeedsAudioRecovery, Is.False);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(initialReleases, Is.EqualTo(1));
            Assert.That(staleReleases, Is.EqualTo(1));
            Assert.That(currentReleases, Is.Zero);
            song.Dispose();
            song.Dispose();
            Assert.That(currentReleases, Is.EqualTo(1));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator CancelledRecoveryDisposesLateResultAndCanRetry()
        {
            var source = Object.FindFirstObjectByType<AppFlowController>().Settings.catalog[0];
            var pending = new TaskCompletionSource<SongAudioLease>();
            int releases = 0;
            int discarded = 0;
            int loads = 0;
            using var song = new PlayableSong(SongAssetAdapter.ToDocument(source), source.Mode,
                new SongAudioLease(source.AudioClip, () => releases++, token =>
                {
                    loads++;
                    if (loads == 1)
                    {
                        return pending.Task;
                    }
                    return Task.FromResult(new SongAudioLease(source.AudioClip, () => releases++));
                }));
            song.InvalidateAudio();
            using var cancellation = new CancellationTokenSource();
            var recovery = song.RecoverAudioAsync(cancellation.Token);
            cancellation.Cancel();
            pending.SetResult(new SongAudioLease(source.AudioClip, () => discarded++));
            yield return Await(() => recovery.IsCompleted);
            Assert.That(recovery.IsCanceled, Is.True);
            Assert.That(discarded, Is.EqualTo(1));
            Assert.That(releases, Is.Zero);
            Assert.That(song.NeedsAudioRecovery, Is.True);
            var retry = song.RecoverAudioAsync(CancellationToken.None);
            yield return Await(() => retry.IsCompleted);
            Assert.That(retry.Result, Is.True);
            Assert.That(releases, Is.EqualTo(1));
            song.Dispose();
            Assert.That(releases, Is.EqualTo(2));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DisposalDuringRecoveryRejectsAndReleasesLateAudio()
        {
            var source = Object.FindFirstObjectByType<AppFlowController>().Settings.catalog[0];
            var pending = new TaskCompletionSource<SongAudioLease>();
            int releases = 0;
            using var song = new PlayableSong(SongAssetAdapter.ToDocument(source), source.Mode,
                new SongAudioLease(source.AudioClip, () => releases++, token => pending.Task));
            song.InvalidateAudio();
            var recovery = song.RecoverAudioAsync(CancellationToken.None);
            song.Dispose();
            pending.SetResult(new SongAudioLease(source.AudioClip, () => releases++));
            yield return Await(() => recovery.IsCompleted);
            Assert.That(recovery.IsCanceled, Is.True);
            Assert.That(releases, Is.EqualTo(2));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator BorrowedAudioNeedsNoReloadAndIsNotDestroyed()
        {
            var source = Object.FindFirstObjectByType<AppFlowController>().Settings.catalog[0];
            var clip = source.AudioClip;
            using var song = SongAssetAdapter.Load(source);
            song.InvalidateAudio();
            Assert.That(song.NeedsAudioRecovery, Is.False);
            var recovery = song.RecoverAudioAsync(CancellationToken.None);
            yield return Await(() => recovery.IsCompleted);
            Assert.That(recovery.Result, Is.True);
            Assert.That(song.AudioClip, Is.SameAs(clip));
            song.Dispose();
            yield return null;
            Assert.That(clip, Is.Not.Null);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator LoaderCancellationWithoutNewResetDoesNotRetryForever()
        {
            var source = Object.FindFirstObjectByType<AppFlowController>().Settings.catalog[0];
            int loads = 0;
            using var song = new PlayableSong(SongAssetAdapter.ToDocument(source), source.Mode,
                new SongAudioLease(source.AudioClip, null, token =>
                {
                    loads++;
                    throw new OperationCanceledException();
                }));
            song.InvalidateAudio();
            var recovery = song.RecoverAudioAsync(CancellationToken.None);
            yield return Await(() => recovery.IsCompleted);
            Assert.That(recovery.IsCanceled, Is.True);
            Assert.That(loads, Is.EqualTo(1));
            Assert.That(song.NeedsAudioRecovery, Is.True);
        }
    }
}
