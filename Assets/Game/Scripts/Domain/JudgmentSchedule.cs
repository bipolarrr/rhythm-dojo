using System;
using System.Collections.Generic;

namespace RhythmDojo.Gameplay
{
    // One immutable schedule is shared by judgment and presentation. Time is the
    // sweep deadline; ScheduledTime is the musical opportunity (never the input time).
    public sealed class JudgmentSchedule
    {
        public readonly struct Check
        {
            public readonly int Id, Note, Part, Ordinal;
            public readonly double Time, ScheduledTime, Weight;

            public Check(int id, int note, int part, int ordinal, double time,
                double scheduledTime, double weight)
            {
                Id = id;
                Note = note;
                Part = part;
                Ordinal = ordinal;
                Time = time;
                ScheduledTime = scheduledTime;
                Weight = weight;
            }
        }

        private readonly Check[] heads;
        private readonly Check[] tails;
        public IReadOnlyList<Check> Checks { get; }

        public Check Head(int note) => heads[note];
        public Check Tail(int note) => tails[note];

        public JudgmentSchedule(ChartData chart, TempoMap tempo, PlayRules rules)
        {
            heads = new Check[chart.Count];
            tails = new Check[chart.Count];
            var checks = new List<Check>();
            for (int i = 0; i < chart.Count; i++)
            {
                var note = chart[i];
                var ticks = new List<double>();
                if (note.Kind == NoteKind.Hold && rules != null)
                {
                    double startBeat = tempo.BeatAtTime(note.StartTime);
                    for (int tick = 1; ; tick++)
                    {
                        double time = tempo.TimeAtBeat(startBeat + tick * rules.HoldCheckBeats);
                        if (time >= note.EndTime - RhythmSession.TimingTolerance)
                        {
                            break;
                        }
                        ticks.Add(time);
                    }
                }

                double weight = 1;
                if (note.Kind == NoteKind.Hold)
                {
                    weight = .5;
                    if (ticks.Count > 0)
                    {
                        weight = .25;
                    }
                }
                double window = rules?.Timing.GoodWindow ?? 0;
                heads[i] = new Check(checks.Count, i, 0, 0, note.StartTime + window,
                    note.StartTime, weight);
                checks.Add(heads[i]);
                if (note.Kind != NoteKind.Hold)
                {
                    continue;
                }

                for (int tick = 0; tick < ticks.Count; tick++)
                {
                    checks.Add(new Check(checks.Count, i, 1, tick + 1, ticks[tick],
                        ticks[tick], .5 / ticks.Count));
                }
                double end = note.EndTime;
                if (rules?.HoldEnd == HoldEndMode.Release)
                {
                    end += window;
                }
                tails[i] = new Check(checks.Count, i, 2, 0, end, note.EndTime, weight);
                checks.Add(tails[i]);
            }

            checks.Sort((a, b) =>
            {
                int order = a.Time.CompareTo(b.Time);
                if (order != 0)
                {
                    return order;
                }
                bool aTimeout = a.Part == 0 || a.Part == 2 && rules?.HoldEnd == HoldEndMode.Release;
                bool bTimeout = b.Part == 0 || b.Part == 2 && rules?.HoldEnd == HoldEndMode.Release;
                order = aTimeout.CompareTo(bTimeout);
                if (order != 0)
                {
                    return order;
                }
                if (a.Note != b.Note)
                {
                    return a.Note.CompareTo(b.Note);
                }
                return a.Part.CompareTo(b.Part);
            });
            Checks = checks.AsReadOnly();
        }
    }

    public readonly struct JudgmentResolvedEvent
    {
        public Guid SessionId { get; }
        public JudgmentSchedule.Check Unit { get; }
        public Judgment Grade { get; }
        public double ActualTime { get; }

        public JudgmentResolvedEvent(Guid sessionId, JudgmentSchedule.Check unit,
            Judgment grade, double actualTime)
        {
            SessionId = sessionId;
            Unit = unit;
            Grade = grade;
            ActualTime = actualTime;
        }
    }

    public enum InputResolution
    {
        AcceptedHead,
        HoldRecovery,
        EmptyPress,
        HoldInterrupted,
    }

    public readonly struct InputResolutionEvent
    {
        public Guid SessionId { get; }
        public int Lane { get; }
        public double Time { get; }
        public InputResolution Resolution { get; }

        public InputResolutionEvent(Guid sessionId, int lane, double time, InputResolution resolution)
        {
            SessionId = sessionId;
            Lane = lane;
            Time = time;
            Resolution = resolution;
        }
    }
}
