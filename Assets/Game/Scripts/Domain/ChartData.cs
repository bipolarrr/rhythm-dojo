using System;

namespace RhythmDojo.Gameplay
{
    public enum NoteKind { Tap, Hold }

    public readonly struct NoteData
    {
        public int Lane { get; }
        public NoteKind Kind { get; }
        public double StartTime { get; }
        public double EndTime { get; }
        public NoteData(int lane, NoteKind kind, double startTime, double endTime)
        { Lane = lane; Kind = kind; StartTime = startTime; EndTime = endTime; }
        public static NoteData Tap(int lane, double time) => new NoteData(lane, NoteKind.Tap, time, time);
        public static NoteData Hold(int lane, double start, double end) => new NoteData(lane, NoteKind.Hold, start, end);
    }

    public readonly struct GameModeRules
    {
        public int LaneCount { get; }
        public GameModeRules(int laneCount)
        {
            if (laneCount <= 0) throw new ArgumentOutOfRangeException(nameof(laneCount));
            LaneCount = laneCount;
        }
    }

    public readonly struct JudgmentSettings
    {
        public double PerfectWindow { get; }
        public double GoodWindow { get; }
        public JudgmentSettings(double perfectWindow, double goodWindow)
        {
            if (!double.IsFinite(perfectWindow) || !double.IsFinite(goodWindow) ||
                perfectWindow <= 0 || goodWindow < perfectWindow)
                throw new ArgumentOutOfRangeException(nameof(goodWindow), "Invalid judgment windows.");
            PerfectWindow = perfectWindow; GoodWindow = goodWindow;
        }
        public void Validate() { _ = new JudgmentSettings(PerfectWindow, GoodWindow); }
    }

    public sealed class ChartData
    {
        private readonly NoteData[] notes;
        public int Count => notes.Length;
        public NoteData this[int index] => notes[index];
        public double CompletionTime { get; }
        public ChartData(NoteData[] notes, double completionTime)
        {
            this.notes = (NoteData[])(notes ?? throw new ArgumentNullException(nameof(notes))).Clone();
            CompletionTime = completionTime;
        }
        public void Validate(GameModeRules mode)
        {
            if (mode.LaneCount <= 0 || notes.Length == 0 || !double.IsFinite(CompletionTime) || CompletionTime <= 0)
                throw new InvalidOperationException("Chart needs notes, a mode and a finite positive completion time.");
            var previousEnd = new double[mode.LaneCount];
            for (int lane = 0; lane < previousEnd.Length; lane++) previousEnd[lane] = double.NegativeInfinity;
            double previousStart = -1;
            for (int i = 0; i < notes.Length; i++)
            {
                var n = notes[i];
                if (n.Lane < 0 || n.Lane >= mode.LaneCount || !Enum.IsDefined(typeof(NoteKind), n.Kind) ||
                    !double.IsFinite(n.StartTime) || !double.IsFinite(n.EndTime) || n.StartTime < 0 ||
                    n.StartTime < previousStart || n.EndTime >= CompletionTime ||
                    (n.Kind == NoteKind.Hold ? n.EndTime <= n.StartTime : n.EndTime != n.StartTime))
                    throw new InvalidOperationException($"Invalid chart note {i}: lane, kind, ordering or interval.");
                if (previousEnd[n.Lane] >= n.StartTime)
                    throw new InvalidOperationException($"Note {i} overlaps another note in lane {n.Lane}.");
                previousEnd[n.Lane] = n.EndTime; previousStart = n.StartTime;
            }
        }
        public ChartData WithCompletionTime(double completionTime) => new ChartData(notes, completionTime);
    }
}
