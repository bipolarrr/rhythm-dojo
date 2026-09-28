using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    // Detached authoring snapshots; EditorSession never exposes its owned instances.
    public sealed class SongProject
    {
        public Guid Id = Guid.NewGuid();
        public string Title = "", Artist = "", Audio;
        public double AudioOffsetSeconds;
        public TempoPoint[] TempoPoints = { new TempoPoint(0, 120) };
        public ChartDocument[] Charts = Array.Empty<ChartDocument>();
        public SongProject Copy() => new SongProject { Id = Id, Title = Title, Artist = Artist, Audio = Audio,
            AudioOffsetSeconds = AudioOffsetSeconds, TempoPoints = (TempoPoint[])TempoPoints.Clone(),
            Charts = Charts.Select(c => c.Copy()).ToArray() };
    }
    public sealed class ChartDocument
    {
        public Guid Id = Guid.NewGuid();
        public string Name = "", ModeId = "";
        public int LaneCount;
        public double CompletionTimeSeconds;
        public NoteData[] Notes = Array.Empty<NoteData>();
        public ChartDocument Copy() => new ChartDocument { Id = Id, Name = Name, ModeId = ModeId,
            LaneCount = LaneCount, CompletionTimeSeconds = CompletionTimeSeconds, Notes = (NoteData[])Notes.Clone() };
    }
    public interface ISongProjectStore
    {
        Task SaveAsync(SongProject project, CancellationToken cancellationToken);
        Task<SongProject> LoadAsync(Guid songId, CancellationToken cancellationToken);
    }
    public interface IProjectAudioImporter
    {
        Task<string> ImportAsync(Guid songId, string sourcePath, CancellationToken cancellationToken);
    }
    public sealed class ProjectValidationException : InvalidOperationException
    {
        public string FieldPath { get; }
        public Guid? ChartId { get; }
        public int? NoteIndex { get; }
        public ProjectValidationException(string path, string message, Guid? chartId = null, int? noteIndex = null)
            : base(path + ": " + message) { FieldPath = path; ChartId = chartId; NoteIndex = noteIndex; }
    }
    public static class ProjectValidation
    {
        public static void Require(bool condition, string path, string message)
        { if (!condition) throw new ProjectValidationException(path, message); }
        public static void AudioPath(string path)
        {
            if (path == null) return;
            Require(path.Length > 0 && !path.Contains("\\") && !path.Contains(":") &&
                path.Split('/').All(p => p.Length > 0 && p != "." && p != ".." &&
                    !p.EndsWith(".") && !p.EndsWith(" ") && !ReservedName(p) && !p.Any(c => c < 32 || "<>\"|?*".Contains(c))),
                "song.audio", "Expected a portable relative path within the song folder.");
        }
        private static bool ReservedName(string segment)
        {
            string name = segment.Split('.')[0].ToUpperInvariant();
            return name == "CON" || name == "PRN" || name == "AUX" || name == "NUL" ||
                (name.Length == 4 && (name.StartsWith("COM") || name.StartsWith("LPT")) && "123456789¹²³".Contains(name[3]));
        }
        public static void Structure(SongProject p)
        {
            Require(p != null, "$", "Project required.");
            Require(p.Id != Guid.Empty, "song.id", "Nonempty UUID required.");
            Require(p.Title != null && p.Artist != null, "song", "Title and artist must be strings.");
            AudioPath(p.Audio);
            Require(double.IsFinite(p.AudioOffsetSeconds), "timing.audioOffsetSeconds", "Finite number required.");
            Require(p.TempoPoints != null, "timing.tempoPoints", "Array required.");
            for (int i = 0; i < p.TempoPoints.Length; i++)
                Require(double.IsFinite(p.TempoPoints[i].StartTimeSeconds) && double.IsFinite(p.TempoPoints[i].Bpm),
                    $"timing.tempoPoints[{i}]", "Finite numbers required.");
            Require(p.Charts != null, "charts", "Array required.");
            var ids = new HashSet<Guid> { p.Id };
            for (int i = 0; i < p.Charts.Length; i++)
            {
                var c = p.Charts[i]; string path = $"charts[{i}]";
                Require(c != null, path, "Chart required.");
                Require(c.Id != Guid.Empty && ids.Add(c.Id), path + ".id", "Unique nonempty UUID required.");
                Require(c.Name != null && c.ModeId != null, path, "Name and modeId must be strings.");
                Require(double.IsFinite(c.CompletionTimeSeconds), path + ".completionTimeSeconds", "Finite number required.");
                Require(c.Notes != null, path + ".notes", "Array required.");
                for (int j = 0; j < c.Notes.Length; j++)
                {
                    var n = c.Notes[j];
                    if (!Enum.IsDefined(typeof(NoteKind), n.Kind) || !double.IsFinite(n.StartTime) || !double.IsFinite(n.EndTime) ||
                        (n.Kind == NoteKind.Tap && n.StartTime != n.EndTime))
                        throw new ProjectValidationException(path + $".notes[{j}]", "Invalid kind or non-finite/noncanonical time.", c.Id, j);
                }
            }
        }
        public static SongProject Normalize(SongProject project)
        {
            Structure(project); var copy = project.Copy();
            foreach (var chart in copy.Charts) chart.Notes = chart.Notes.OrderBy(n => n.StartTime).ToArray();
            return copy;
        }
        public static bool ContentEquals(SongProject a, SongProject b) =>
            a.Id == b.Id && a.Title == b.Title && a.Artist == b.Artist && a.Audio == b.Audio &&
            a.AudioOffsetSeconds == b.AudioOffsetSeconds && a.TempoPoints.SequenceEqual(b.TempoPoints) &&
            a.Charts.Length == b.Charts.Length && a.Charts.Zip(b.Charts, (x, y) => x.Id == y.Id && x.Name == y.Name &&
                x.ModeId == y.ModeId && x.LaneCount == y.LaneCount && x.CompletionTimeSeconds == y.CompletionTimeSeconds &&
                x.Notes.SequenceEqual(y.Notes)).All(equal => equal);
    }
}
