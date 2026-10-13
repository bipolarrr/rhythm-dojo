using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Content
{
    public sealed class ProjectSongProvider : IChartSongProvider
    {
        private readonly FileSongProjectStore store;
        private readonly GameModeDefinition mode;
        public ProjectSongProvider(FileSongProjectStore store, GameModeDefinition mode) { this.store = store; this.mode = mode; }
        public IReadOnlyList<SongEntry> GetEntries()
        {
            var entries = new List<SongEntry>();
            foreach (Guid id in store.ListSongIds())
            {
                try
                {
                    var project = store.Read(id);
                    var charts = new List<ChartEntry>();
                    foreach (var chart in project.Charts.Where(c => c.Slot.HasValue))
                    {
                        string error = null, revision = null;
                        try { revision = RecordIdentity.Chart(Document(project, chart)); }
                        catch (Exception e) { error = e.Message; }
                        charts.Add(new ChartEntry(chart.Id.ToString("D"), chart.Slot.Value, error, revision, chart.Notes.Length, chart.CompletionTimeSeconds));
                    }
                    double min = project.TempoPoints.Length == 0 ? 0 : project.TempoPoints.Min(p => p.Bpm);
                    double max = project.TempoPoints.Length == 0 ? 0 : project.TempoPoints.Max(p => p.Bpm);
                    entries.Add(new SongEntry(id.ToString("D"), project.Title, project.Artist,
                        project.Charts.Length == 0 ? 0 : project.Charts.Max(c => c.CompletionTimeSeconds),
                        project.Charts.Sum(c => c.Notes.Length), mode.LaneCount, min, max,
                        charts.Count == 0 ? "Easy / Standard / Hard 슬롯을 지정하세요." : null, charts: charts));
                }
                catch (Exception e) { entries.Add(new SongEntry(id.ToString("D"), id.ToString("D"), "", 0, 0, 0, 0, 0, e.Message)); }
            }
            return entries;
        }
        private SongDocument Document(SongProject project, ChartDocument chart) => ChartPlaybackAdapter.ToDocument(project, chart.Id,
            mode.name, mode.ToRules(), audio => store.AudioExists(project.Id, audio));
        public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken) => LoadChartAsync(songId, ChartSlot.Standard, cancellationToken);
        public async Task<PlayableSong> LoadChartAsync(string songId, ChartSlot slot, CancellationToken cancellationToken)
        {
            var project = await store.LoadAsync(Guid.Parse(songId), cancellationToken);
            var chart = project.Charts.FirstOrDefault(c => c.Slot == slot) ?? throw new InvalidOperationException("Chart slot missing.");
            var loader = new DocumentSongLoader(id => id == mode.name ? mode : null,
                (audio, token) => ChartEditorCompositionRoot.LoadAudio(store.ResolveAudioPath(project.Id, audio), token));
            return await loader.LoadAsync(Document(project, chart), cancellationToken);
        }
    }
}
