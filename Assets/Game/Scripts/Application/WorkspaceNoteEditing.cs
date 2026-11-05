using System;
using System.Collections.Generic;
using System.Linq;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public sealed class WorkspaceNoteEditing
    {
        public HashSet<long> Selected { get; } = new HashSet<long>();
        private NoteData[] clipboard = Array.Empty<NoteData>();

        public void Select(EditorSession session, Guid chartId, NoteKind? kind = null)
        {
            Selected.Clear();
            foreach (var note in session.GetNotes(chartId))
            {
                if (!kind.HasValue || note.Data.Kind == kind.Value)
                {
                    Selected.Add(note.Id);
                }
            }
        }

        public void Copy(EditorSession session, Guid chartId)
        {
            clipboard = session.GetNotes(chartId).Where(n => Selected.Contains(n.Id))
                .Select(n => n.Data).ToArray();
        }

        public void Paste(EditorSession session, Guid chartId, double time)
        {
            if (clipboard.Length == 0)
            {
                return;
            }

            double shift = Math.Max(0, time) - clipboard.Min(n => n.StartTime);
            var ids = new List<long>();
            session.Edit(edit =>
            {
                foreach (var note in clipboard)
                {
                    ids.Add(edit.AddNote(chartId, Move(note, note.Lane, shift)));
                }

                var chart = Array.Find(session.Snapshot.Charts, c => c.Id == chartId);
                double end = clipboard.Max(n => n.EndTime) + shift + 1;
                if (end > chart.CompletionTimeSeconds)
                {
                    edit.SetChartSettings(chartId, chart.ModeId, chart.LaneCount, end);
                }
            });
            Selected.Clear();
            Selected.UnionWith(ids);
        }

        public void Delete(EditorSession session, Guid chartId)
        {
            var notes = session.GetNotes(chartId).Where(n => Selected.Contains(n.Id)).ToArray();
            if (notes.Length == 0)
            {
                return;
            }

            session.Edit(edit =>
            {
                foreach (var note in notes)
                {
                    edit.DeleteNote(chartId, note.Id);
                }
            });
            Selected.Clear();
        }

        public void Remap(EditorSession session, Guid chartId, int[] lanes)
        {
            var chart = Array.Find(session.Snapshot.Charts, c => c.Id == chartId);
            if (lanes.Length != chart.LaneCount || lanes.Distinct().Count() != chart.LaneCount)
            {
                throw new ArgumentException("Lane mapping must be a permutation.");
            }

            if (lanes.Any(lane => lane < 0 || lane >= chart.LaneCount))
            {
                throw new ArgumentException("Lane mapping is out of range.");
            }

            var notes = session.GetNotes(chartId).Where(n => Selected.Contains(n.Id)).ToArray();
            session.Edit(edit =>
            {
                foreach (var note in notes)
                {
                    edit.UpdateNote(chartId, note.Id, Move(note.Data, lanes[note.Data.Lane], 0));
                }
            });
        }

        public bool MoveNote(EditorSession session, Guid chartId, long id, int lane, double time)
        {
            var chart = Array.Find(session.Snapshot.Charts, item => item.Id == chartId);
            if (lane < 0 || lane >= chart.LaneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            if (!double.IsFinite(time) || time < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(time));
            }

            var notes = session.GetNotes(chartId);
            var note = Array.Find(notes, item => item.Id == id);
            if (note.Id == 0)
            {
                return false;
            }

            var moved = Move(note.Data, lane, time - note.Data.StartTime);
            foreach (var other in notes)
            {
                if (other.Id == id || other.Data.Lane != lane)
                {
                    continue;
                }

                if (other.Data.StartTime <= moved.EndTime && other.Data.EndTime >= moved.StartTime)
                {
                    return false;
                }
            }

            if (note.Data.Lane == lane && note.Data.StartTime == time)
            {
                return true;
            }

            session.Edit(edit =>
            {
                edit.UpdateNote(chartId, id, moved);
                if (moved.EndTime >= chart.CompletionTimeSeconds)
                {
                    edit.SetChartSettings(chartId, chart.ModeId, chart.LaneCount, moved.EndTime + 1);
                }
            });
            return true;
        }

        public bool MoveNotes(EditorSession session, Guid chartId, long[] ids, int laneShift, double timeShift)
        {
            if (!double.IsFinite(timeShift))
            {
                throw new ArgumentOutOfRangeException(nameof(timeShift));
            }

            var movingIds = new HashSet<long>(ids);
            var notes = session.GetNotes(chartId);
            var moving = notes.Where(note => movingIds.Contains(note.Id)).ToArray();
            if (moving.Length == 0 || moving.Length != movingIds.Count)
            {
                return false;
            }

            var chart = Array.Find(session.Snapshot.Charts, item => item.Id == chartId);
            var moved = new NoteData[moving.Length];
            for (int i = 0; i < moving.Length; i++)
            {
                int lane = moving[i].Data.Lane + laneShift;
                if (lane < 0 || lane >= chart.LaneCount)
                {
                    return false;
                }
                if (moving[i].Data.StartTime + timeShift < 0)
                {
                    return false;
                }

                moved[i] = Move(moving[i].Data, lane, timeShift);
                foreach (var other in notes)
                {
                    if (movingIds.Contains(other.Id) || other.Data.Lane != lane)
                    {
                        continue;
                    }
                    if (other.Data.StartTime <= moved[i].EndTime && other.Data.EndTime >= moved[i].StartTime)
                    {
                        return false;
                    }
                }
            }

            if (laneShift == 0 && timeShift == 0)
            {
                return true;
            }

            session.Edit(edit =>
            {
                for (int i = 0; i < moving.Length; i++)
                {
                    edit.UpdateNote(chartId, moving[i].Id, moved[i]);
                }
                double end = moved.Max(note => note.EndTime);
                if (end >= chart.CompletionTimeSeconds)
                {
                    edit.SetChartSettings(chartId, chart.ModeId, chart.LaneCount, end + 1);
                }
            });
            return true;
        }

        public int QuantizeRecordedNotes(EditorSession session, Guid chartId, IEnumerable<long> ids, double step)
        {
            var snapshot = session.Snapshot;
            var chart = Array.Find(snapshot.Charts, item => item.Id == chartId);
            if (chart == null)
            {
                return 0;
            }

            var recorded = new HashSet<long>(ids);
            var timing = new BeatGridTiming(snapshot.TempoPoints, snapshot.Grid);
            var notes = session.GetNotes(chartId).OrderBy(note => note.Data.Lane)
                .ThenBy(note => note.Data.StartTime).ToArray();
            var adjusted = new List<EditorNote>();
            int skipped = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                var note = notes[i];
                if (!recorded.Contains(note.Id))
                {
                    continue;
                }

                var snapped = QuantizeNote(note.Data, timing, step);
                if (snapped.Equals(note.Data))
                {
                    continue;
                }

                if (i > 0 && notes[i - 1].Data.Lane == snapped.Lane && notes[i - 1].Data.EndTime >= snapped.StartTime)
                {
                    skipped++;
                    continue;
                }

                if (i + 1 < notes.Length && notes[i + 1].Data.Lane == snapped.Lane && notes[i + 1].Data.StartTime <= snapped.EndTime)
                {
                    skipped++;
                    continue;
                }

                notes[i] = new EditorNote(note.Id, snapped);
                adjusted.Add(notes[i]);
            }

            if (adjusted.Count == 0)
            {
                return skipped;
            }

            session.Edit(edit =>
            {
                foreach (var note in adjusted)
                {
                    edit.UpdateNote(chartId, note.Id, note.Data);
                }

                double end = adjusted.Max(note => note.Data.EndTime);
                if (end >= chart.CompletionTimeSeconds)
                {
                    edit.SetChartSettings(chartId, chart.ModeId, chart.LaneCount, end + 1);
                }
            });
            return skipped;
        }

        private static NoteData QuantizeNote(NoteData note, BeatGridTiming timing, double step)
        {
            timing.GetSnapTimeBounds(note.StartTime, step, out double? earlierStart, out double laterStart);
            double start = NearestTime(note.StartTime, earlierStart, laterStart);
            if (note.Kind != NoteKind.Hold)
            {
                return NoteData.Tap(note.Lane, start);
            }

            timing.GetSnapTimeBounds(note.EndTime, step, out double? earlierEnd, out double laterEnd);
            double end = NearestTime(note.EndTime, earlierEnd, laterEnd);
            if (end <= start)
            {
                if (laterEnd <= start)
                {
                    timing.GetSnapTimeBounds(start, step, out _, out laterEnd);
                }

                double endShift = Math.Abs(laterEnd - note.EndTime) - Math.Abs(end - note.EndTime);
                if (earlierStart.HasValue && earlierStart.Value < end)
                {
                    double startShift = Math.Abs(earlierStart.Value - note.StartTime) - Math.Abs(start - note.StartTime);
                    if (startShift < endShift)
                    {
                        return NoteData.Hold(note.Lane, earlierStart.Value, end);
                    }
                }

                end = laterEnd;
            }

            return NoteData.Hold(note.Lane, start, end);
        }

        private static double NearestTime(double time, double? earlier, double later)
        {
            if (earlier.HasValue && Math.Abs(time - earlier.Value) <= Math.Abs(later - time))
            {
                return earlier.Value;
            }

            return later;
        }

        private static NoteData Move(NoteData note, int lane, double shift)
        {
            if (note.Kind == NoteKind.Hold)
            {
                return NoteData.Hold(lane, note.StartTime + shift, note.EndTime + shift);
            }

            return NoteData.Tap(lane, note.StartTime + shift);
        }
    }
}
