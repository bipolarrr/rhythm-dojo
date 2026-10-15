using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public enum ChartSlot
    {
        Easy,
        Standard,
        Hard,
    }

    // Detached authoring snapshots; EditorSession never exposes its owned instances.
    public sealed class SongProject
    {
        public Guid Id = Guid.NewGuid();
        public string Title = "",
            Artist = "",
            Audio;
        public double AudioOffsetSeconds;
        public TempoPoint[] TempoPoints = { new TempoPoint(0, 120) };
        public BeatGridSettings Grid = new BeatGridSettings();
        public ChartDocument[] Charts = Array.Empty<ChartDocument>();

        public SongProject Copy() =>
            new SongProject
            {
                Id = Id,
                Title = Title,
                Artist = Artist,
                Audio = Audio,
                AudioOffsetSeconds = AudioOffsetSeconds,
                TempoPoints = (TempoPoint[])TempoPoints.Clone(),
                Grid = Grid.Copy(),
                Charts = Charts.Select(c => c.Copy()).ToArray(),
            };
    }

    public sealed class ChartDocument
    {
        public ChartSlot? Slot;
        public Guid Id = Guid.NewGuid();
        public string Name = "",
            ModeId = "";
        public int LaneCount;
        public double CompletionTimeSeconds;
        public NoteData[] Notes = Array.Empty<NoteData>();

        public ChartDocument Copy() =>
            new ChartDocument
            {
                Id = Id,
                Name = Name,
                ModeId = ModeId,
                LaneCount = LaneCount,
                CompletionTimeSeconds = CompletionTimeSeconds,
                Notes = (NoteData[])Notes.Clone(),
                Slot = Slot,
            };
    }

    public interface ISongProjectStore
    {
        Task SaveAsync(SongProject project, CancellationToken cancellationToken);
        Task<SongProject> LoadAsync(Guid songId, CancellationToken cancellationToken);
    }

    public interface IProjectAudioImporter
    {
        Task<string> ImportAsync(
            Guid songId,
            string sourcePath,
            CancellationToken cancellationToken
        );
    }

    public sealed class ProjectValidationException : InvalidOperationException
    {
        public string FieldPath { get; }
        public Guid? ChartId { get; }
        public int? NoteIndex { get; }

        public ProjectValidationException(
            string path,
            string message,
            Guid? chartId = null,
            int? noteIndex = null
        )
            : base(path + ": " + message)
        {
            FieldPath = path;
            ChartId = chartId;
            NoteIndex = noteIndex;
        }
    }

    public static class ProjectValidation
    {
        public static void Require(bool condition, string path, string message)
        {
            if (!condition)
            {
                throw new ProjectValidationException(path, message);
            }
        }

        public static void AudioPath(string path)
        {
            if (path == null)
            {
                return;
            }

            Require(
                path.Length > 0 && !path.Contains("\\") && !path.Contains(":"),
                "song.audio",
                "Expected a portable relative path within the song folder."
            );
            Require(
                path.Split('/')
                    .All(p =>
                    {
                        if (p.Length == 0 || p == "." || p == "..")
                        {
                            return false;
                        }

                        if (p.EndsWith(".") || p.EndsWith(" ") || ReservedName(p))
                        {
                            return false;
                        }

                        return !p.Any(c => c < 32 || "<>\"|?*".Contains(c));
                    }),
                "song.audio",
                "Expected a portable relative path within the song folder."
            );
        }

        private static bool ReservedName(string segment)
        {
            string name = segment.Split('.')[0].ToUpperInvariant();

            if ((name == "CON" || name == "PRN") || name == "AUX")
            {
                return true;
            }

            if (name == "NUL")
            {
                return true;
            }

            return name.Length == 4
                && (name.StartsWith("COM") || name.StartsWith("LPT"))
                && "123456789¹²³".Contains(name[3]);
        }

        public static void Structure(SongProject p)
        {
            Require(p != null, "$", "Project required.");
            Require(p.Id != Guid.Empty, "song.id", "Nonempty UUID required.");
            Require(
                p.Title != null && p.Artist != null,
                "song",
                "Title and artist must be strings."
            );
            AudioPath(p.Audio);
            Require(
                double.IsFinite(p.AudioOffsetSeconds),
                "timing.audioOffsetSeconds",
                "Finite number required."
            );
            Require(p.TempoPoints != null, "timing.tempoPoints", "Array required.");
            Require(p.Grid != null, "timing.grid", "Grid required.");
            p.Grid.Validate();

            for (int i = 0; i < p.TempoPoints.Length; i++)
            {
                Require(
                    double.IsFinite(p.TempoPoints[i].StartTimeSeconds)
                        && double.IsFinite(p.TempoPoints[i].Bpm),
                    $"timing.tempoPoints[{i}]",
                    "Finite numbers required."
                );
            }

            Require(p.Charts != null, "charts", "Array required.");

            var ids = new HashSet<Guid> { p.Id };
            var slots = new HashSet<ChartSlot>();

            for (int i = 0; i < p.Charts.Length; i++)
            {
                var c = p.Charts[i];
                string path = $"charts[{i}]";
                Require(c != null, path, "Chart required.");
                Require(
                    !c.Slot.HasValue
                        || Enum.IsDefined(typeof(ChartSlot), c.Slot.Value)
                            && slots.Add(c.Slot.Value),
                    path + ".slot",
                    "Unique Easy, Standard or Hard slot required."
                );
                Require(
                    c.Id != Guid.Empty && ids.Add(c.Id),
                    path + ".id",
                    "Unique nonempty UUID required."
                );
                Require(
                    c.Name != null && c.ModeId != null,
                    path,
                    "Name and modeId must be strings."
                );
                Require(
                    double.IsFinite(c.CompletionTimeSeconds),
                    path + ".completionTimeSeconds",
                    "Finite number required."
                );
                Require(c.Notes != null, path + ".notes", "Array required.");

                for (int j = 0; j < c.Notes.Length; j++)
                {
                    var n = c.Notes[j];

                    if (!Enum.IsDefined(typeof(NoteKind), n.Kind) || !double.IsFinite(n.StartTime))
                    {
                        throw new ProjectValidationException(
                            path + $".notes[{j}]",
                            "Invalid kind or non-finite/noncanonical time.",
                            c.Id,
                            j
                        );
                    }

                    if (
                        !double.IsFinite(n.EndTime)
                        || n.Kind == NoteKind.Tap && n.StartTime != n.EndTime
                    )
                    {
                        throw new ProjectValidationException(
                            path + $".notes[{j}]",
                            "Invalid kind or non-finite/noncanonical time.",
                            c.Id,
                            j
                        );
                    }
                }
            }
        }

        public static SongProject Normalize(SongProject project)
        {
            Structure(project);

            var copy = project.Copy();

            foreach (var chart in copy.Charts)
            {
                chart.Notes = chart.Notes.OrderBy(n => n.StartTime).ToArray();
            }

            return copy;
        }

        public static bool ContentEquals(SongProject a, SongProject b)
        {
            if (a.Grid.OriginSeconds != b.Grid.OriginSeconds || !a.Grid.Meters.SequenceEqual(b.Grid.Meters))
            {
                return false;
            }

            if ((!(a.Id == b.Id) || !(a.Title == b.Title)) || !(a.Artist == b.Artist))
            {
                return false;
            }

            if (!(a.Audio == b.Audio) || !(a.AudioOffsetSeconds == b.AudioOffsetSeconds))
            {
                return false;
            }

            if (
                !a.TempoPoints.SequenceEqual(b.TempoPoints) || !(a.Charts.Length == b.Charts.Length)
            )
            {
                return false;
            }

            return a
                .Charts.Zip(
                    b.Charts,
                    (x, y) =>
                    {
                        if ((!(x.Id == y.Id) || !(x.Name == y.Name)) || !(x.ModeId == y.ModeId))
                        {
                            return false;
                        }

                        if (!(x.Slot == y.Slot) || !(x.LaneCount == y.LaneCount))
                        {
                            return false;
                        }

                        if (!(x.CompletionTimeSeconds == y.CompletionTimeSeconds))
                        {
                            return false;
                        }

                        return x.Notes.SequenceEqual(y.Notes);
                    }
                )
                .All(equal => equal);
        }
    }
}
