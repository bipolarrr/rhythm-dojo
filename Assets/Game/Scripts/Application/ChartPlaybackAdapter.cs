using System;
using System.Linq;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public static class ChartPlaybackAdapter
    {
        public static SongDocument ToDocument(SongProject project, Guid chartId, string actualModeId,
            GameModeRules mode, Func<string, bool> audioExists)
        {
            var p = ProjectValidation.Normalize(project);
            var c = p.Charts.FirstOrDefault(chart => chart.Id == chartId)
                ?? throw new ProjectValidationException("charts", "Unknown chart " + chartId);
            string path = "charts[" + c.Id + "]";
            ProjectValidation.Require(!string.IsNullOrWhiteSpace(p.Title), "song.title", "Required for playback.");
            ProjectValidation.Require(!string.IsNullOrWhiteSpace(c.ModeId) && c.ModeId == actualModeId && c.LaneCount == mode.LaneCount,
                path + ".modeId/laneCount", "Chart must match the actual mode.");
            ProjectValidation.Require(p.Audio != null && audioExists != null && audioExists(p.Audio), "song.audio", "Audio file missing.");
            try { _ = new TempoMap(p.TempoPoints); }
            catch (ArgumentException e) { throw new ProjectValidationException("timing.tempoPoints", e.Message); }
            ProjectValidation.Require(c.Notes.Length > 0 && c.CompletionTimeSeconds > 0, path, "Playback needs notes and positive completion time.");
            // Attach structured locations to the same interval and overlap rules used by ChartData.
            var ends = Enumerable.Repeat(double.NegativeInfinity, mode.LaneCount).ToArray();
            for (int i = 0; i < c.Notes.Length; i++)
            {
                var n = c.Notes[i];
                if (n.Lane < 0 || n.Lane >= mode.LaneCount || n.StartTime < 0 || n.EndTime >= c.CompletionTimeSeconds ||
                    (n.Kind == NoteKind.Hold && n.EndTime <= n.StartTime))
                    throw new ProjectValidationException(path + $".notes[{i}]", "Invalid lane or interval.", c.Id, i);
                if (ends[n.Lane] >= n.StartTime)
                    throw new ProjectValidationException(path + $".notes[{i}]", "Overlapping notes.", c.Id, i);
                ends[n.Lane] = n.EndTime;
            }
            var document = new SongDocument { SongId = p.Id.ToString("D"), Title = p.Title, Artist = p.Artist,
                AudioId = p.Audio, ModeId = c.ModeId, AudioOffset = p.AudioOffsetSeconds,
                CompletionTime = c.CompletionTimeSeconds, Notes = c.Notes, TempoPoints = p.TempoPoints };
            document.Validate(mode);
            return document;
        }
    }
}
