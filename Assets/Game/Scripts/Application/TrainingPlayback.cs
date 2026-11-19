using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public readonly struct TrainingInputRecord
    {
        public int Lane { get; }
        public double Time { get; }
        public bool Matched { get; }
        public bool Synchronized { get; }

        public TrainingInputRecord(int lane, double time, bool matched, bool synchronized = true)
        {
            Lane = lane;
            Time = time;
            Matched = matched;
            Synchronized = synchronized;
        }
    }

    // Lessons observe head judgments and replay authored input through the existing engine.
    public sealed class TrainingPlayback
    {
        private readonly ChartData chart;
        private readonly List<TrainingInputRecord> inputs = new List<TrainingInputRecord>();
        private int nextNote;
        private readonly List<(double time, int note, bool release)> demonstrationInputs
            = new List<(double, int, bool)>();
        private readonly bool[] recordedHeads;
        private readonly double[] headTimes;
        private readonly double maximumChordSpreadSeconds;
        private readonly Action onDemonstrationPress;

        public bool IsDemonstration { get; }
        public IReadOnlyList<TrainingInputRecord> Inputs { get; }
        public int ExtraInputs { get; private set; }

        public TrainingPlayback(ChartData chart, bool isDemonstration, double maximumChordSpreadSeconds = 0,
            Action onDemonstrationPress = null)
        {
            this.chart = chart ?? throw new ArgumentNullException(nameof(chart));
            if (!double.IsFinite(maximumChordSpreadSeconds) || maximumChordSpreadSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumChordSpreadSeconds));
            }
            this.maximumChordSpreadSeconds = maximumChordSpreadSeconds;
            this.onDemonstrationPress = onDemonstrationPress;
            recordedHeads = new bool[chart.Count];
            headTimes = new double[chart.Count];
            for (int i = 0; i < chart.Count; i++)
            {
                demonstrationInputs.Add((chart[i].StartTime, i, false));
                if (chart[i].Kind == NoteKind.Hold)
                {
                    demonstrationInputs.Add((chart[i].EndTime, i, true));
                }
            }
            demonstrationInputs.Sort((a, b) =>
            {
                int order = a.time.CompareTo(b.time);
                if (order != 0)
                {
                    return order;
                }
                return b.release.CompareTo(a.release);
            });
            IsDemonstration = isDemonstration;
            Inputs = inputs.AsReadOnly();
            Reset();
        }

        public void Reset()
        {
            nextNote = 0;
            inputs.Clear();
            ExtraInputs = 0;
            Array.Clear(recordedHeads, 0, recordedHeads.Length);
            Array.Fill(headTimes, double.NaN);
        }

        public void Press(RhythmSession session, int lane, double time)
        {
            if (IsDemonstration || session.State != SessionState.Playing)
            {
                return;
            }
            session.PressLaneFromClock(lane, time);
            // Ignore preroll and input after the chart, including a queued late event.
            if (time < 0 || time > chart.CompletionTime)
            {
                return;
            }
            bool matched = false;
            bool synchronized = true;
            for (int i = 0; i < chart.Count; i++)
            {
                if (chart[i].Lane != lane || recordedHeads[i])
                {
                    continue;
                }
                var head = session.GetNoteJudgments(i).Start;
                if (!head.HasValue)
                {
                    continue;
                }
                recordedHeads[i] = true;
                if (head.Value.Grade == Judgment.Miss)
                {
                    continue;
                }
                matched = true;
                headTimes[i] = time;
                synchronized = IsSynchronized(i, time);
            }
            inputs.Add(new TrainingInputRecord(lane, time, matched, synchronized));
            if (!matched)
            {
                ExtraInputs++;
            }
        }

        private bool IsSynchronized(int note, double time)
        {
            if (maximumChordSpreadSeconds == 0)
            {
                return true;
            }
            for (int i = 0; i < chart.Count; i++)
            {
                if (chart[i].StartTime != chart[note].StartTime || double.IsNaN(headTimes[i]))
                {
                    continue;
                }
                if (Math.Abs(time - headTimes[i]) > maximumChordSpreadSeconds + RhythmSession.TimingTolerance)
                {
                    return false;
                }
            }
            return true;
        }

        public void Advance(RhythmSession session, double songTime)
        {
            if (!IsDemonstration || session.State != SessionState.Playing)
            {
                return;
            }
            // Run before timeout sweep. Catch up every due note at its authored time.
            // Pause and resume use the same song clock as the visible notes.
            while (nextNote < demonstrationInputs.Count && demonstrationInputs[nextNote].time <= songTime)
            {
                var input = demonstrationInputs[nextNote++];
                var note = chart[input.note];
                if (input.release)
                {
                    session.ReleaseLaneFromClock(note.Lane, input.time);
                    continue;
                }
                session.PressLaneFromClock(note.Lane, note.StartTime);
                onDemonstrationPress?.Invoke();
                if (note.Kind == NoteKind.Tap)
                {
                    session.ReleaseLaneFromClock(note.Lane, note.StartTime);
                }
            }
        }
    }
}
