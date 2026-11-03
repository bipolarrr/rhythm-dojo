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
