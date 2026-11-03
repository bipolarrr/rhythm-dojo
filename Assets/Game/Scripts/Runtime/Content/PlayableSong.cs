using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Content
{
    // Owns a loader lease; built-in AudioClips are borrowed, imported clips can be released.
    public sealed class PlayableSong : IDisposable
    {
        private Action release;
        private bool disposed;
        private SongAudioLease audioLease;
        private readonly Func<CancellationToken, Task<SongAudioLease>> reload;
        private CancellationTokenSource recoveryCancellation;
        private int audioGeneration;
        private int loadedGeneration;
        private int recoveryRequest;

        public bool NeedsAudioRecovery => !disposed && reload != null && loadedGeneration != audioGeneration;
        public event Action<AudioClip> AudioChanged;

        public string SongId { get; }
        public string ChartId { get; }
        public string Revision { get; }
        public string Title { get; }
        public string Artist { get; }
        public ChartData Chart { get; }
        public TempoMap Tempo { get; }
        public double AudioOffset { get; }
        public AudioClip AudioClip { get; private set; }
        public GameModeDefinition Mode { get; }
        public string ModeId { get; }

        public PlayableSong(
            SongDocument document,
            AudioClip audio,
            GameModeDefinition mode,
            Action releaseAudio = null
        )
            : this(document, mode, new SongAudioLease(audio, releaseAudio))
        {
        }

        public PlayableSong(SongDocument document, GameModeDefinition mode, SongAudioLease lease)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (lease == null || !lease.Clip)
            {
                throw new InvalidOperationException("AudioId: audio missing or empty.");
            }

            if (lease.Clip.length <= 0)
            {
                throw new InvalidOperationException("AudioId: audio missing or empty.");
            }

            if (!mode)
            {
                throw new InvalidOperationException("ModeId: mode missing.");
            }

            var snapshot = document.Copy();
            snapshot.Validate(mode.ToRules());
            SongId = snapshot.SongId;
            Title = snapshot.Title;
            Artist = snapshot.Artist;
            ChartId = snapshot.ChartId ?? snapshot.SongId + ":standard";
            Revision = RecordIdentity.Chart(snapshot);
            Chart = new ChartData(snapshot.Notes, snapshot.CompletionTime);
            Tempo = new TempoMap(snapshot.TempoPoints);
            AudioOffset = snapshot.AudioOffset;
            AudioClip = lease.Clip;
            ModeId = snapshot.ModeId;
            Mode = UnityEngine.Object.Instantiate(mode);
            audioLease = lease;
            release = lease.Dispose;
            reload = lease.Reload;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            InvalidateAudio();
        }

        public void InvalidateAudio()
        {
            if (disposed || reload == null)
            {
                return;
            }

            audioGeneration++;
            recoveryCancellation?.Cancel();
        }

        public async Task<bool> RecoverAudioAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PlayableSong));
            }

            if (!NeedsAudioRecovery)
            {
                return true;
            }

            recoveryCancellation?.Cancel();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            recoveryCancellation = cancellation;
            int generation = audioGeneration;
            int request = ++recoveryRequest;
            SongAudioLease replacement = null;
            try
            {
                replacement = await reload(cancellation.Token);
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed)
                {
                    throw new OperationCanceledException(cancellation.Token);
                }

                if (generation != audioGeneration || request != recoveryRequest)
                {
                    return false;
                }

                cancellation.Token.ThrowIfCancellationRequested();
                if (replacement == null || !replacement.Clip)
                {
                    throw new InvalidOperationException("복구한 음원 데이터가 비어 있습니다.");
                }

                if (replacement.Clip.length <= 0)
                {
                    throw new InvalidOperationException("복구한 음원 데이터가 비어 있습니다.");
                }

                var previous = audioLease;
                audioLease = replacement;
                replacement = null;
                AudioClip = audioLease.Clip;
                release = audioLease.Dispose;
                loadedGeneration = generation;
                try
                {
                    // Consumers replace their source before the previous clip is released.
                    AudioChanged?.Invoke(AudioClip);
                }
                catch
                {
                    InvalidateAudio();
                    throw;
                }
                finally
                {
                    previous.Dispose();
                }

                return !NeedsAudioRecovery;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed)
                {
                    throw;
                }
                if (generation == audioGeneration && request == recoveryRequest)
                {
                    throw;
                }

                return false;
            }
            finally
            {
                replacement?.Dispose();
                if (ReferenceEquals(recoveryCancellation, cancellation))
                {
                    recoveryCancellation = null;
                }
                cancellation.Dispose();
            }
        }

        public void Validate()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PlayableSong));
            }

            if (!AudioClip || !Mode)
            {
                throw new InvalidOperationException("Playback resources unavailable.");
            }

            Chart.Validate(Mode.ToRules());
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            recoveryCancellation?.Cancel();
            AudioChanged = null;

            if (Mode)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Mode);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Mode);
                }
            }

            var callback = release;
            release = null;
            callback?.Invoke();
        }
    }

    public static class SongAssetAdapter
    {
        public static SongDocument ToDocument(SongDefinition song)
        {
            song.Validate();

            var chart = song.Chart.ToChartData();
            var tempo = song.Timing.ToTempoMap();
            var notes = new NoteData[chart.Count];
            var points = new TempoPoint[tempo.Count];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = chart[i];
            }

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = tempo[i];
            }

            return new SongDocument
            {
                SongId = song.SongId,
                Title = song.Title,
                Artist = song.Artist,
                ModeId = song.Mode.name,
                AudioId = song.SongId,
                AudioOffset = song.Timing.chartAudioOffsetSeconds,
                CompletionTime = chart.CompletionTime,
                Notes = notes,
                TempoPoints = points,
            };
        }

        public static PlayableSong Load(SongDefinition song) =>
            new PlayableSong(ToDocument(song), song.AudioClip, song.Mode);
    }
}
