using System;
using System.Linq;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Authoring
{
    public static class SongProjectAssetAdapter
    {
        // AudioClip references are not portable files. Supply an imported relative path, or leave a draft.
        public static SongProject Copy(SongDefinition source, string importedAudioPath = null, string chartName = "Normal")
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            var chart = source.Chart ? new ChartDocument { Name = chartName, ModeId = source.Mode ? source.Mode.name : "",
                LaneCount = source.Mode ? source.Mode.LaneCount : 0, CompletionTimeSeconds = source.Chart.CompletionTime,
                Notes = Enumerable.Range(0, source.Chart.Count).Select(i => source.Chart[i]).ToArray() } : null;
            return ProjectValidation.Normalize(new SongProject { Title = source.Title ?? "", Artist = source.Artist ?? "",
                Audio = importedAudioPath, AudioOffsetSeconds = source.Timing?.chartAudioOffsetSeconds ?? 0,
                TempoPoints = source.Timing?.tempoPoints?.Select(t => new TempoPoint(t.startTimeSeconds, t.bpm)).ToArray() ?? Array.Empty<TempoPoint>(),
                Charts = chart == null ? Array.Empty<ChartDocument>() : new[] { chart } });
        }
    }
}
