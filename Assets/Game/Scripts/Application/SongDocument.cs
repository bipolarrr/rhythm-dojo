using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    // Mutable authoring data. Playback always takes a defensive snapshot.
    [Serializable]
    public sealed class SongDocument
    {
        public string SongId;
        public string Title;
        public string Artist;
        public string ModeId;
        public string AudioId;
        public double AudioOffset;
        public double CompletionTime;
        public TempoPoint[] TempoPoints = Array.Empty<TempoPoint>();
        public NoteData[] Notes = Array.Empty<NoteData>();

        public SongDocument Copy() => new SongDocument
        {
            SongId = SongId, Title = Title, Artist = Artist, ModeId = ModeId, AudioId = AudioId,
            AudioOffset = AudioOffset, CompletionTime = CompletionTime,
            TempoPoints = TempoPoints == null ? null : (TempoPoint[])TempoPoints.Clone(),
            Notes = Notes == null ? null : (NoteData[])Notes.Clone()
        };

        public void Validate(GameModeRules mode)
        {
            if (string.IsNullOrWhiteSpace(SongId)) throw new InvalidOperationException("SongId: required.");
            if (string.IsNullOrWhiteSpace(Title)) throw new InvalidOperationException("Title: required.");
            if (string.IsNullOrWhiteSpace(ModeId)) throw new InvalidOperationException("ModeId: required.");
            if (string.IsNullOrWhiteSpace(AudioId)) throw new InvalidOperationException("AudioId: required.");
            if (!double.IsFinite(AudioOffset)) throw new InvalidOperationException("AudioOffset: must be finite.");
            try { _ = new TempoMap(TempoPoints); }
            catch (Exception e) { throw new InvalidOperationException("TempoPoints: " + e.Message, e); }
            try { new ChartData(Notes, CompletionTime).Validate(mode); }
            catch (Exception e) { throw new InvalidOperationException("Notes/CompletionTime: " + e.Message, e); }
        }
    }

    public interface ISongDocumentStore
    {
        Task SaveAsync(SongDocument document, CancellationToken cancellationToken);
        Task<SongDocument> LoadAsync(string songId, CancellationToken cancellationToken);
    }
}
