using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [Serializable]
    public struct SerializedTempoPoint
    {
        public double startTimeSeconds;
        public double bpm;
        public SerializedTempoPoint(double time, double bpm) { startTimeSeconds = time; this.bpm = bpm; }
    }
    [Serializable]
    public sealed class SongTiming
    {
        [Tooltip("Positive offset advances chart time relative to audio.")]
        public double chartAudioOffsetSeconds;
        public SerializedTempoPoint[] tempoPoints = { new SerializedTempoPoint(0, 120) };
        public TempoMap ToTempoMap()
        {
            if (!double.IsFinite(chartAudioOffsetSeconds) || tempoPoints == null) throw new InvalidOperationException("Invalid song timing.");
            var points = new TempoPoint[tempoPoints.Length];
            for (int i = 0; i < points.Length; i++) points[i] = new TempoPoint(tempoPoints[i].startTimeSeconds, tempoPoints[i].bpm);
            return new TempoMap(points);
        }
    }
    [CreateAssetMenu(menuName = "Rhythm Dojo/Song")]
    public sealed class SongDefinition : ScriptableObject
    {
        [SerializeField] private string songId;
        [SerializeField] private string title;
        [SerializeField] private string artist;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private RhythmChart chart;
        [SerializeField] private GameModeDefinition mode;
        [SerializeField] private Sprite cover;
        public Sprite Cover => cover;
        public void SetCover(Sprite value) => cover = value;
        [SerializeField] private SongTiming timing = new SongTiming();
        public string SongId => songId;
        public string Title => title;
        public string Artist => artist;
        public AudioClip AudioClip => audioClip;
        public RhythmChart Chart => chart;
        public GameModeDefinition Mode => mode;
        public SongTiming Timing => timing;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(songId) || string.IsNullOrWhiteSpace(title) || !audioClip || !chart || !mode || timing == null)
                throw new InvalidOperationException("Song needs an ID, title, audio, chart, mode and timing.");
            if (audioClip.length <= 0) throw new InvalidOperationException("Song audio is empty.");
            chart.Validate(mode.ToRules()); _ = timing.ToTempoMap();
        }
        public void SetGeneratedDefaults(string id, string name, AudioClip clip, RhythmChart chartAsset,
            GameModeDefinition modeAsset, SongTiming songTiming, string composer = "Rhythm Dojo")
        { songId = id; title = name; artist = composer; audioClip = clip; chart = chartAsset; mode = modeAsset; timing = songTiming; }
    }
}
