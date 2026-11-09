using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Content
{
    public sealed class SongAudioLease : IDisposable
    {
        private Action release;

        public AudioClip Clip { get; }
        public Func<CancellationToken, Task<SongAudioLease>> Reload { get; }

        public SongAudioLease(AudioClip clip, Action release = null)
            : this(clip, release, null)
        {
        }

        public SongAudioLease(AudioClip clip, Action release,
            Func<CancellationToken, Task<SongAudioLease>> reload)
        {
            Clip = clip;
            this.release = release;
            Reload = reload;
        }

        public void Dispose()
        {
            var callback = release;
            release = null;
            callback?.Invoke();
        }
    }

    // Inject the authoring audio importer and mode registry; this adapter has no file format policy.
    // Call on Unity's main thread. Audio loaders must return to that synchronization context.
    public sealed class DocumentSongLoader : ISongDocumentLoader
    {
        private readonly Func<string, GameModeDefinition> resolveMode;
        private readonly Func<string, CancellationToken, Task<SongAudioLease>> loadAudio;

        public DocumentSongLoader(
            Func<string, GameModeDefinition> resolveMode,
            Func<string, CancellationToken, Task<SongAudioLease>> loadAudio
        )
        {
            this.resolveMode = resolveMode ?? throw new ArgumentNullException(nameof(resolveMode));
            this.loadAudio = loadAudio ?? throw new ArgumentNullException(nameof(loadAudio));
        }

        public async Task<PlayableSong> LoadAsync(
            SongDocument document,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = (document ?? throw new ArgumentNullException(nameof(document))).Copy();
            GameModeDefinition mode;
            try
            {
                mode = resolveMode(snapshot.ModeId);
                if (!mode)
                {
                    throw new ArgumentException("ModeId: unknown mode " + snapshot.ModeId);
                }

                snapshot.Validate(mode.ToRules());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new SongLoadException(SongLoadFailure.FromChartValidation(e));
            }

            SongAudioLease lease;
            try
            {
                lease = await loadAudio(snapshot.AudioId, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new SongLoadException(SongLoadFailure.FromAudio(e));
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (lease == null || !lease.Clip)
                {
                    throw new SongLoadException(new SongLoadFailure(
                        SongLoadFailureKind.AudioLoadFailed, "AudioId: loader returned no audio."));
                }

                if (lease.Clip.length <= 0)
                {
                    throw new SongLoadException(new SongLoadFailure(
                        SongLoadFailureKind.AudioLoadFailed, "AudioId: audio is empty."));
                }

                return new PlayableSong(snapshot, mode, lease);
            }
            catch
            {
                lease?.Dispose();
                throw;
            }
        }
    }
}
