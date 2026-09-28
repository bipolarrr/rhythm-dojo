using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public readonly struct EditorNote
    {
        public long Id { get; }
        public NoteData Data { get; }
        public EditorNote(long id, NoteData data) { Id = id; Data = data; }
    }
    // Invoke on the owning UI thread (including continuations). A batch is one undo command.
    public sealed class EditorSession
    {
        internal sealed class State
        {
            public SongProject Project;
            public Dictionary<Guid, List<EditorNote>> Notes = new Dictionary<Guid, List<EditorNote>>();
            public State Copy() => new State { Project = Project.Copy(),
                Notes = Notes.ToDictionary(p => p.Key, p => new List<EditorNote>(p.Value)) };
            public void Normalize()
            {
                foreach (var chart in Project.Charts)
                {
                    Notes[chart.Id] = Notes[chart.Id].OrderBy(n => n.Data.StartTime).ToList();
                    chart.Notes = Notes[chart.Id].Select(n => n.Data).ToArray();
                }
                ProjectValidation.Structure(Project);
            }
        }
        private State state;
        private SongProject saved;
        private long nextId;
        private readonly List<State> undo = new List<State>(), redo = new List<State>();
        private readonly SemaphoreSlim saves = new SemaphoreSlim(1, 1);
        private bool editing;
        public Guid? ActiveChartId { get; private set; }
        public event Action Changed;
        public bool IsDirty => saved == null || !ProjectValidation.ContentEquals(saved, state.Project);
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public SongProject Snapshot => state.Project.Copy();
        public EditorSession(SongProject project, bool isSaved = false)
        { SetProject(project); if (isSaved) saved = state.Project.Copy(); }
        private void SetProject(SongProject project)
        {
            var p = ProjectValidation.Normalize(project);
            var replacement = new State { Project = p };
            foreach (var c in p.Charts) replacement.Notes.Add(c.Id, c.Notes.Select(n => new EditorNote(++nextId, n)).ToList());
            state = replacement; ActiveChartId = p.Charts.Length == 0 ? (Guid?)null : p.Charts[0].Id;
        }
        public EditorNote[] GetNotes(Guid chartId) => state.Notes[chartId].ToArray();
        public long? ResolveNoteId(ProjectValidationException error) => error.ChartId.HasValue && error.NoteIndex.HasValue &&
            state.Notes.TryGetValue(error.ChartId.Value, out var notes) && error.NoteIndex.Value >= 0 && error.NoteIndex.Value < notes.Count
                ? notes[error.NoteIndex.Value].Id : (long?)null;
        public void SelectChart(Guid chartId)
        { if (!state.Notes.ContainsKey(chartId)) throw new KeyNotFoundException(); ActiveChartId = chartId; Changed?.Invoke(); }
        public void Edit(Action<EditorEdit> batch)
        {
            if (editing) throw new InvalidOperationException("Nested edits are not supported.");
            editing = true;
            try
            {
                var candidate = state.Copy(); var edit = new EditorEdit(candidate, () => checked(++nextId));
                try { batch(edit); candidate.Normalize(); } finally { edit.Close(); }
                if (ProjectValidation.ContentEquals(state.Project, candidate.Project) &&
                    state.Notes.All(p => candidate.Notes.ContainsKey(p.Key) && p.Value.SequenceEqual(candidate.Notes[p.Key]))) return;
                undo.Add(state); if (undo.Count > 100) undo.RemoveAt(0);
                state = candidate; redo.Clear(); RepairSelection();
            }
            finally { editing = false; }
            Changed?.Invoke();
        }
        private void RepairSelection()
        { if (!ActiveChartId.HasValue || !state.Notes.ContainsKey(ActiveChartId.Value)) ActiveChartId = state.Project.Charts.FirstOrDefault()?.Id; }
        private void Restore(List<State> from, List<State> to)
        {
            if (editing) throw new InvalidOperationException("Cannot restore during a batch.");
            if (from.Count == 0) return;
            to.Add(state); state = from[from.Count - 1]; from.RemoveAt(from.Count - 1); RepairSelection(); Changed?.Invoke();
        }
        public void Undo() => Restore(undo, redo);
        public void Redo() => Restore(redo, undo);
        public async Task SaveAsync(ISongProjectStore store, CancellationToken token = default)
        {
            var snapshot = Snapshot;
            await saves.WaitAsync(token);
            try { await store.SaveAsync(snapshot, token); saved = snapshot; }
            finally { saves.Release(); }
            Changed?.Invoke();
        }
        // Loading returns a new session, so a failed load can never destroy the current one.
        public static async Task<EditorSession> LoadAsync(ISongProjectStore store, Guid id, CancellationToken token = default)
            => new EditorSession(await store.LoadAsync(id, token), true);
    }
    public sealed class EditorEdit
    {
        private EditorSession.State state;
        private readonly Func<long> allocate;
        internal EditorEdit(EditorSession.State state, Func<long> allocate) { this.state = state; this.allocate = allocate; }
        internal void Close() { state = null; }
        private EditorSession.State State => state ?? throw new InvalidOperationException("Batch already closed.");
        private ChartDocument Chart(Guid id) => State.Project.Charts.First(c => c.Id == id);
        public void SetMetadata(string title, string artist) { State.Project.Title = title; State.Project.Artist = artist; }
        public void SetAudio(string relativePath) { State.Project.Audio = relativePath; }
        public void SetTiming(double offset, params TempoPoint[] points)
        { State.Project.AudioOffsetSeconds = offset; State.Project.TempoPoints = (TempoPoint[])points.Clone(); }
        public Guid AddChart(string name, string modeId, int laneCount, double completionTime = 0)
        {
            var chart = new ChartDocument { Name = name, ModeId = modeId, LaneCount = laneCount, CompletionTimeSeconds = completionTime };
            State.Project.Charts = State.Project.Charts.Concat(new[] { chart }).ToArray();
            State.Notes.Add(chart.Id, new List<EditorNote>()); return chart.Id;
        }
        public void RenameChart(Guid id, string name) { Chart(id).Name = name; }
        public void SetChartSettings(Guid id, string modeId, int laneCount, double completionTime)
        { var c = Chart(id); c.ModeId = modeId; c.LaneCount = laneCount; c.CompletionTimeSeconds = completionTime; }
        public void DeleteChart(Guid id)
        { _ = Chart(id); State.Project.Charts = State.Project.Charts.Where(c => c.Id != id).ToArray(); State.Notes.Remove(id); }
        public long AddNote(Guid chartId, NoteData note)
        { var notes = State.Notes[chartId]; long id = allocate(); notes.Add(new EditorNote(id, note)); return id; }
        public void UpdateNote(Guid chartId, long id, NoteData note)
        { var notes = State.Notes[chartId]; int i = Index(notes, id); notes[i] = new EditorNote(id, note); }
        public void DeleteNote(Guid chartId, long id)
        { var notes = State.Notes[chartId]; notes.RemoveAt(Index(notes, id)); }
        private static int Index(List<EditorNote> notes, long id)
        { int index = notes.FindIndex(n => n.Id == id); return index < 0 ? throw new KeyNotFoundException("Unknown note ID " + id) : index; }
    }
}
