using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Content
{
    public sealed class SongAudioLease : IDisposable
    {
        private Action release;
        public AudioClip Clip { get; }
        public SongAudioLease(AudioClip clip, Action release = null) { Clip = clip; this.release = release; }
        public void Dispose() { var callback = release; release = null; callback?.Invoke(); }
    }
    // Inject the authoring audio importer and mode registry; this adapter has no file format policy.
    // Call on Unity's main thread. Audio loaders must return to that synchronization context.
    public sealed class DocumentSongLoader : ISongDocumentLoader
    {
        private readonly Func<string, GameModeDefinition> resolveMode;
        private readonly Func<string, CancellationToken, Task<SongAudioLease>> loadAudio;
        public DocumentSongLoader(Func<string, GameModeDefinition> resolveMode,
            Func<string, CancellationToken, Task<SongAudioLease>> loadAudio)
        {
            this.resolveMode = resolveMode ?? throw new ArgumentNullException(nameof(resolveMode));
            this.loadAudio = loadAudio ?? throw new ArgumentNullException(nameof(loadAudio));
        }
        public async Task<PlayableSong> LoadAsync(SongDocument document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = (document ?? throw new ArgumentNullException(nameof(document))).Copy();
            var mode = resolveMode(snapshot.ModeId);
            if (!mode) throw new InvalidOperationException("ModeId: unknown mode " + snapshot.ModeId);
            snapshot.Validate(mode.ToRules());
            var lease = await loadAudio(snapshot.AudioId, cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (lease == null) throw new InvalidOperationException("AudioId: loader returned no audio.");
                return new PlayableSong(snapshot, lease.Clip, mode, lease.Dispose);
            }
            catch { lease?.Dispose(); throw; }
        }
    }
}
