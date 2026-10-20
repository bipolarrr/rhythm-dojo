using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public static class BeatGridEditing
    {
        public static void ApplyOrigin(EditorSession session, TempoPoint[] points,
            BeatGridSettings settings, double origin, bool preserveBeats)
        {
            double delta = origin - settings.OriginSeconds;
            var shifted = (TempoPoint[])points.Clone();
            for (int i = 1; i < shifted.Length; i++)
            {
                shifted[i] = new TempoPoint(shifted[i].StartTimeSeconds + delta, shifted[i].Bpm);
            }

            var grid = settings.Copy();
            grid.OriginSeconds = origin;
            for (int i = 1; i < grid.Meters.Length; i++)
            {
                var meter = grid.Meters[i];
                grid.Meters[i] = new MeterPoint(meter.TimeSeconds + delta, meter.Numerator, meter.Denominator);
            }

            Apply(session, shifted, grid, preserveBeats);
        }

        public static void Apply(EditorSession session, TempoPoint[] points,
            BeatGridSettings settings, bool preserveBeats)
        {
            var snapshot = session.Snapshot;
            var before = new BeatGridTiming(snapshot.TempoPoints, snapshot.Grid);
            var after = new BeatGridTiming(points, settings);

            session.Edit(edit =>
            {
                edit.SetTiming(snapshot.AudioOffsetSeconds, points);
                edit.SetGrid(settings);
                if (!preserveBeats)
                {
                    return;
                }

                foreach (var chart in snapshot.Charts)
                {
                    foreach (var note in session.GetNotes(chart.Id))
                    {
                        double start = after.TimeAtBeat(before.BeatAtTime(note.Data.StartTime));
                        NoteData replacement;
                        if (note.Data.Kind == NoteKind.Hold)
                        {
                            double end = after.TimeAtBeat(before.BeatAtTime(note.Data.EndTime));
                            replacement = NoteData.Hold(note.Data.Lane, start, end);
                        }
                        else
                        {
                            replacement = NoteData.Tap(note.Data.Lane, start);
                        }

                        edit.UpdateNote(chart.Id, note.Id, replacement);
                    }

                    edit.SetChartSettings(chart.Id, chart.ModeId, chart.LaneCount,
                        after.TimeAtBeat(before.BeatAtTime(chart.CompletionTimeSeconds)));
                }
            });
        }
    }
}
