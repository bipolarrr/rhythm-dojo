using System;
using System.Linq;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Authoring
{
    public static class SongProjectAssetAdapter
    {
        // AudioClip references are not portable files. Supply an imported relative path, or leave a draft.
        public static SongProject Copy(
            SongDefinition source,
            string importedAudioPath = null,
            string chartName = "Normal"
        )
        {
            if (!source)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ChartDocument chart;

            if (source.Chart)
            {
                if (source.Mode)
                {
                    chart = new ChartDocument
                    {
                        Slot = ChartSlot.Standard,
                        Name = chartName,
                        ModeId = source.Mode.name,
                        LaneCount = source.Mode.LaneCount,
                        CompletionTimeSeconds = source.Chart.CompletionTime,
                        Notes = Enumerable
                            .Range(0, source.Chart.Count)
                            .Select(i => source.Chart[i])
                            .ToArray(),
                    };
                }
                else
                {
                    chart = new ChartDocument
                    {
                        Slot = ChartSlot.Standard,
                        Name = chartName,
                        ModeId = "",
                        LaneCount = 0,
                        CompletionTimeSeconds = source.Chart.CompletionTime,
                        Notes = Enumerable
                            .Range(0, source.Chart.Count)
                            .Select(i => source.Chart[i])
                            .ToArray(),
                    };
                }
            }
            else
            {
                chart = null;
            }

            return ProjectValidation.Normalize(
                new SongProject
                {
                    Title = source.Title ?? "",
                    Artist = source.Artist ?? "",
                    Audio = importedAudioPath,
                    AudioOffsetSeconds = source.Timing?.chartAudioOffsetSeconds ?? 0,
                    TempoPoints =
                        source
                            .Timing?.tempoPoints?.Select(t => new TempoPoint(
                                t.startTimeSeconds,
                                t.bpm
                            ))
                            .ToArray()
                        ?? Array.Empty<TempoPoint>(),
                    Charts = (
                        chart switch
                        {
                            null => Array.Empty<ChartDocument>(),
                            _ => new[] { chart },
                        }
                    ),
                }
            );
        }
    }
}
