using System;

namespace RhythmDojo.Gameplay
{
    public readonly struct TempoPoint
    {
        public double StartTimeSeconds { get; }
        public double Bpm { get; }
        public TempoPoint(double startTimeSeconds, double bpm)
        { StartTimeSeconds = startTimeSeconds; Bpm = bpm; }
    }

    public sealed class TempoMap
    {
        private readonly TempoPoint[] points;
        private readonly double[] beats;
        public int Count => points.Length;
        public TempoPoint this[int index] => points[index];
        public double MinBpm { get; }
        public double MaxBpm { get; }
        public TempoMap(TempoPoint[] points)
        {
            this.points = (TempoPoint[])(points ?? throw new ArgumentNullException(nameof(points))).Clone();
            if (points.Length == 0 || points[0].StartTimeSeconds != 0)
                throw new ArgumentException("Tempo map must start at chart time zero.");
            double min = double.MaxValue, max = 0, previous = -1;
            foreach (var point in this.points)
            {
                if (!double.IsFinite(point.StartTimeSeconds) || point.StartTimeSeconds < 0 ||
                    point.StartTimeSeconds <= previous || !double.IsFinite(point.Bpm) || point.Bpm <= 0)
                    throw new ArgumentException("Tempo points need ordered distinct times and positive finite BPM.");
                previous = point.StartTimeSeconds; min = Math.Min(min, point.Bpm); max = Math.Max(max, point.Bpm);
            }
            MinBpm = min; MaxBpm = max;
            beats = new double[points.Length];
            for (int i = 1; i < points.Length; i++)
                beats[i] = beats[i - 1] + (points[i].StartTimeSeconds - points[i - 1].StartTimeSeconds) * points[i - 1].Bpm / 60;
        }
        public int FindSegment(double time)
        {
            if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
            int low = 0, high = points.Length - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (points[middle].StartTimeSeconds <= time) low = middle; else high = middle - 1;
            }
            return low;
        }
        public double BpmAt(double time) => points[FindSegment(time)].Bpm;

        public double BeatAtTime(double time)
        {
            int segment = FindSegment(time);
            return beats[segment] + (time - points[segment].StartTimeSeconds) * points[segment].Bpm / 60;
        }

        public double TimeAtBeat(double beat)
        {
            if (!double.IsFinite(beat)) throw new ArgumentOutOfRangeException(nameof(beat));
            int low = 0, high = points.Length - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (beats[middle] <= beat) low = middle; else high = middle - 1;
            }
            return points[low].StartTimeSeconds + (beat - beats[low]) * 60 / points[low].Bpm;
        }
    }
}
