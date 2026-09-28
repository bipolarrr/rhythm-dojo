using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Content
{
    public sealed class SongEntry
    {
        public string Id { get; }
        public string Title { get; }
        public string Artist { get; }
        public double Duration { get; }
        public int NoteCount { get; }
        public int LaneCount { get; }
        public double MinBpm { get; }
        public double MaxBpm { get; }
        public string Error { get; }
        public SongEntry(string id, string title, string artist, double duration, int notes, int lanes,
            double minBpm, double maxBpm, string error = null)
        { Id = id; Title = title; Artist = artist; Duration = duration; NoteCount = notes;
            LaneCount = lanes; MinBpm = minBpm; MaxBpm = maxBpm; Error = error; }
        public SongEntry WithError(string error) => new SongEntry(Id, Title, Artist, Duration, NoteCount, LaneCount, MinBpm, MaxBpm, error);
    }
    public interface ISongProvider
    {
        IReadOnlyList<SongEntry> GetEntries();
        Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken);
    }
    public interface ISongLibrary
    {
        IReadOnlyList<SongEntry> Entries { get; }
        event Action Changed;
        void Refresh();
        SongEntry Find(string songId);
    }
    public interface ISongLoader
    {
        Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken);
    }
    public interface ISongDocumentLoader
    {
        Task<PlayableSong> LoadAsync(SongDocument document, CancellationToken cancellationToken);
    }
    public sealed class SongLibrary : ISongLibrary, ISongLoader
    {
        private readonly List<ISongProvider> providers = new List<ISongProvider>();
        private readonly Dictionary<string, ISongProvider> sources = new Dictionary<string, ISongProvider>(StringComparer.Ordinal);
        public IReadOnlyList<SongEntry> Entries { get; private set; } = Array.Empty<SongEntry>();
        public event Action Changed;
        public void Register(ISongProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (providers.Contains(provider)) return;
            providers.Add(provider); Refresh();
        }
        public void Unregister(ISongProvider provider) { if (providers.Remove(provider)) Refresh(); }
        public void Refresh()
        {
            var items = providers.SelectMany(p => p.GetEntries().Select(e => (entry: e, provider: p))).ToArray();
            var duplicates = new HashSet<string>(items.GroupBy(x => x.entry.Id).Where(g => g.Count() > 1).Select(g => g.Key));
            sources.Clear(); var entries = new List<SongEntry>();
            foreach (var item in items)
            {
                var entry = item.entry;
                if (string.IsNullOrWhiteSpace(entry.Id)) entry = entry.WithError("SongId: required.");
                else if (duplicates.Contains(entry.Id)) entry = entry.WithError("Duplicate song ID: " + entry.Id);
                else if (entry.Error == null) sources.Add(entry.Id, item.provider);
                entries.Add(entry);
            }
            Entries = entries.AsReadOnly(); Changed?.Invoke();
        }
        public SongEntry Find(string songId) => Entries.FirstOrDefault(e => e.Id == songId);
        public async Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (songId == null || !sources.TryGetValue(songId, out var provider))
                throw new InvalidOperationException(Find(songId)?.Error ?? "Song not available: " + songId);
            var song = await provider.LoadAsync(songId, cancellationToken);
            if (cancellationToken.IsCancellationRequested) { song?.Dispose(); cancellationToken.ThrowIfCancellationRequested(); }
            if (song == null) throw new InvalidOperationException("Song loader returned no song.");
            return song;
        }
    }
    public sealed class BuiltInSongProvider : ISongProvider
    {
        private readonly SongCatalog catalog;
        public BuiltInSongProvider(SongCatalog catalog) { this.catalog = catalog; }
        public IReadOnlyList<SongEntry> GetEntries()
        {
            var entries = new List<SongEntry>();
            for (int i = 0; i < catalog.Count; i++)
            {
                var song = catalog[i]; string error = catalog.GetEntryError(i);
                if (error != null) { entries.Add(new SongEntry(song ? song.SongId : null, song ? song.Title : "Missing song", "", 0, 0, 0, 0, 0, error)); continue; }
                var tempo = song.Timing.ToTempoMap();
                entries.Add(new SongEntry(song.SongId, song.Title, song.Artist, song.AudioClip.length, song.Chart.Count,
                    song.Mode.LaneCount, tempo.MinBpm, tempo.MaxBpm));
            }
            return entries;
        }
        public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int i = 0; i < catalog.Count; i++)
                if (catalog[i] && catalog[i].SongId == songId) return Task.FromResult(SongAssetAdapter.Load(catalog[i]));
            throw new InvalidOperationException("Song not found: " + songId);
        }
    }
}
