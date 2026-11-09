using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Core
{
    // Owns the values from one play, independently of the disposed playback request.
    public sealed class PlayResult
    {
        public string Title { get; }
        public string Artist { get; }
        public string Difficulty { get; }
        public Sprite Cover { get; }
        public SessionSnapshot Session { get; }
        public int MaxCombo { get; }
        public int Score => ScoreRules.Score(Session.AccuracyRate);
        public int? PreviousBestScore { get; internal set; }
        public bool CanCompareScore { get; internal set; }
        public int? TimingOffsetRecommendationMs { get; private set; }
        internal int? AppliedTimingOffsetMs { get; set; }
        public ChartData Chart { get; }
        public TempoMap Tempo { get; }
        public IReadOnlyList<string> LaneLabels { get; }
        public IReadOnlyList<PlayNoteResult> Notes { get; }

        internal void RecommendTimingOffset(int timingOffsetMs)
        {
            TimingOffsetRecommendationMs = null;
            if (Session.State != SessionState.Completed || Session.Failure.Failed)
            {
                return;
            }

            var samples = new TimingSampleSet();
            foreach (var note in Notes)
            {
                double? error = null;
                if (note.Start is { } start && start.Grade != Judgment.Miss)
                {
                    error = start.ErrorSeconds;
                }

                samples.Add(note.Note.StartTime, error);
            }

            TimingOffsetRecommendationMs = samples.RecommendOffsetMs(timingOffsetMs);
        }

        public PlayResult(
            string title, string artist, string difficulty, Sprite cover,
            SessionSnapshot session, int maxCombo, ChartData chart, TempoMap tempo,
            IList<string> laneLabels, RhythmSession judgments)
        {
            Title = title;
            Artist = artist;
            Difficulty = difficulty;
            Cover = cover;
            Session = session;
            MaxCombo = maxCombo;
            Chart = chart;
            Tempo = tempo;
            var labels = new string[laneLabels.Count];
            laneLabels.CopyTo(labels, 0);
            LaneLabels = Array.AsReadOnly(labels);
            var notes = new PlayNoteResult[chart.Count];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = new PlayNoteResult(i, chart[i], judgments.GetState(i), judgments.GetNoteJudgments(i));
            }

            Notes = Array.AsReadOnly(notes);
        }

        public PlayResult(
            string title,
            string artist,
            string difficulty,
            Sprite cover,
            SessionSnapshot session,
            int maxCombo
        )
            : this(title, artist, difficulty, cover, session, maxCombo, null, null,
                Array.Empty<string>(), Array.Empty<JudgmentEvent>(), Array.Empty<NoteState>())
        {
        }

        public PlayResult(
            string title,
            string artist,
            string difficulty,
            Sprite cover,
            SessionSnapshot session,
            int maxCombo,
            ChartData chart,
            TempoMap tempo,
            IList<string> laneLabels,
            IList<JudgmentEvent> judgments,
            IList<NoteState> noteStates
        )
        {
            Title = title;
            Artist = artist;
            Difficulty = difficulty;
            Cover = cover;
            Session = session;
            MaxCombo = maxCombo;
            Chart = chart;
            Tempo = tempo;
            var labels = new string[laneLabels.Count];
            laneLabels.CopyTo(labels, 0);
            LaneLabels = Array.AsReadOnly(labels);
            var notes = new List<PlayNoteResult>();

            if (chart != null)
            {
                var grouped = new List<JudgmentEvent>[chart.Count];

                for (int i = 0; i < grouped.Length; i++)
                {
                    grouped[i] = new List<JudgmentEvent>();
                }

                foreach (var judgment in judgments)
                {
                    grouped[judgment.NoteIndex].Add(judgment);
                }

                for (int i = 0; i < chart.Count; i++)
                {
                    notes.Add(new PlayNoteResult(i, chart[i], noteStates[i], grouped[i]));
                }
            }

            Notes = notes.AsReadOnly();
        }
    }
}
