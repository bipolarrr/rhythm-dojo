using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Content
{
    // Owns a loader lease; built-in AudioClips are borrowed, imported clips can be released.
    public sealed class PlayableSong : IDisposable
    {
        private Action release;
        private bool disposed;
        public string SongId { get; }
        public string Title { get; }
        public string Artist { get; }
        public ChartData Chart { get; }
        public TempoMap Tempo { get; }
        public double AudioOffset { get; }
        public AudioClip AudioClip { get; }
        public GameModeDefinition Mode { get; }

        public PlayableSong(SongDocument document, AudioClip audio, GameModeDefinition mode, Action releaseAudio = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!audio || audio.length <= 0) throw new InvalidOperationException("AudioId: audio missing or empty.");
            if (!mode) throw new InvalidOperationException("ModeId: mode missing.");
            var snapshot = document.Copy(); snapshot.Validate(mode.ToRules());
            SongId = snapshot.SongId; Title = snapshot.Title; Artist = snapshot.Artist;
            Chart = new ChartData(snapshot.Notes, snapshot.CompletionTime);
            Tempo = new TempoMap(snapshot.TempoPoints); AudioOffset = snapshot.AudioOffset;
            AudioClip = audio; Mode = UnityEngine.Object.Instantiate(mode); release = releaseAudio;
        }
        public void Validate()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableSong));
            if (!AudioClip || !Mode) throw new InvalidOperationException("Playback resources unavailable.");
            Chart.Validate(Mode.ToRules());
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Mode)
            {
                if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(Mode);
                else UnityEngine.Object.DestroyImmediate(Mode);
            }
            var callback = release; release = null; callback?.Invoke();
        }
    }

    public static class SongAssetAdapter
    {
        public static SongDocument ToDocument(SongDefinition song)
        {
            song.Validate();
            var chart = song.Chart.ToChartData(); var tempo = song.Timing.ToTempoMap();
            var notes = new NoteData[chart.Count]; var points = new TempoPoint[tempo.Count];
            for (int i = 0; i < notes.Length; i++) notes[i] = chart[i];
            for (int i = 0; i < points.Length; i++) points[i] = tempo[i];
            return new SongDocument { SongId = song.SongId, Title = song.Title, Artist = song.Artist,
                ModeId = song.Mode.name, AudioId = song.SongId, AudioOffset = song.Timing.chartAudioOffsetSeconds,
                CompletionTime = chart.CompletionTime, Notes = notes, TempoPoints = points };
        }
        public static PlayableSong Load(SongDefinition song) => new PlayableSong(ToDocument(song), song.AudioClip, song.Mode);
    }
}
