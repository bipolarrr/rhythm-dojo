using System;
using System.Collections.Generic;

namespace RhythmDojo.Gameplay
{
    public readonly struct MeterPoint
    {
        public double TimeSeconds { get; }
        public int Numerator { get; }
        public int Denominator { get; }

        public MeterPoint(double timeSeconds, int numerator, int denominator)
        {
            TimeSeconds = timeSeconds;
            Numerator = numerator;
            Denominator = denominator;
        }
    }

    public sealed class BeatGridSettings
    {
        // Chart time, independent of the playback/audio offset.
        public double OriginSeconds;
        public MeterPoint[] Meters = { new MeterPoint(0, 4, 4) };

        public BeatGridSettings Copy()
        {
            return new BeatGridSettings
            {
                OriginSeconds = OriginSeconds,
                Meters = (MeterPoint[])Meters.Clone()
            };
        }

        public void Validate()
        {
            if (!double.IsFinite(OriginSeconds) || Meters == null)
            {
                throw new ArgumentException("Grid origin must be finite and meters are required.");
            }

            if (Meters.Length == 0 || Meters[0].TimeSeconds != 0)
            {
                throw new ArgumentException("Meter map must start at zero.");
            }

            double previous = -1;
            foreach (var point in Meters)
            {
                if (!double.IsFinite(point.TimeSeconds) || point.TimeSeconds <= previous)
                {
                    throw new ArgumentException("Meter points must have distinct increasing times.");
                }

                if (point.Numerator < 1 || point.Numerator > 32)
                {
                    throw new ArgumentException("Meter numerator must be between 1 and 32.");
                }

                int denominator = point.Denominator;
                if (denominator < 1 || denominator > 32 || (denominator & (denominator - 1)) != 0)
                {
                    throw new ArgumentException("Meter denominator must be a power of two up to 32.");
                }

                previous = point.TimeSeconds;
            }
        }
    }

    // All editor views use quarter-note beats, including compound meters.
    public sealed class BeatGridTiming
    {
        private readonly TempoMap tempo;
        private readonly BeatGridSettings settings;
        private readonly double originBeat;

        public BeatGridTiming(TempoPoint[] points, BeatGridSettings settings)
            : this(new TempoMap(points), settings) { }

        public BeatGridTiming(TempoMap tempo, BeatGridSettings settings)
        {
            settings.Validate();
            this.tempo = tempo;
            this.settings = settings.Copy();
            originBeat = tempo.BeatAtTime(settings.OriginSeconds);
        }

        public double TimeAtBeat(double beat) => tempo.TimeAtBeat(beat + originBeat);

        public double BeatAtTime(double time) => tempo.BeatAtTime(time) - originBeat;

        public double BpmAt(double time) => tempo.BpmAt(time);

        public int MeterIndexAt(double time)
        {
            int index = 0;
            while (index + 1 < settings.Meters.Length && settings.Meters[index + 1].TimeSeconds <= time)
            {
                index++;
            }

            return index;
        }

        public void GetMusicalPosition(double beat, out int measure, out double beatInMeasure)
        {
            double time = TimeAtBeat(beat);
            int index = MeterIndexAt(time);
            measure = 1;
            double start = 0;
            for (int i = 0; i < index; i++)
            {
                var meter = settings.Meters[i];
                double end = BeatAtTime(settings.Meters[i + 1].TimeSeconds);
                double length = meter.Numerator * 4.0 / meter.Denominator;
                measure += (int)Math.Ceiling((end - start) / length - 1e-8);
                start = end;
            }

            var current = settings.Meters[index];
            double unit = 4.0 / current.Denominator;
            double bars = (beat - start) / (current.Numerator * unit);
            double whole = Math.Floor(bars + 1e-8);
            measure += (int)whole;
            beatInMeasure = (beat - start - whole * current.Numerator * unit) / unit;
        }

        public bool IsMeasure(double beat)
        {
            GetMusicalPosition(beat, out _, out double position);
            return Math.Abs(position) < 1e-6;
        }

        public double SnapBeat(double beat, double step, double bottom, double top)
        {
            int index = MeterIndexAt(TimeAtBeat(beat));
            double anchor = 0;
            if (index > 0)
            {
                anchor = BeatAtTime(settings.Meters[index].TimeSeconds);
                bottom = Math.Max(bottom, anchor);
            }

            if (index + 1 < settings.Meters.Length)
            {
                top = Math.Min(top, BeatAtTime(settings.Meters[index + 1].TimeSeconds));
            }

            double rounded = anchor + Math.Round((beat - anchor) / step,
                MidpointRounding.AwayFromZero) * step;
            double first = anchor + Math.Ceiling((bottom - anchor) / step) * step;
            double last = anchor + Math.Floor((top - anchor) / step) * step;
            return Math.Max(first, Math.Min(last, rounded));
        }

        public IEnumerable<double> Lines(double firstBeat, double lastBeat, double subdivision, int maxLines = 200000)
        {
            double range = lastBeat - firstBeat;
            double budget = Math.Max(1, maxLines / 2.0);
            for (int i = 0; i < settings.Meters.Length; i++)
            {
                var meter = settings.Meters[i];
                double anchor = 0;
                double low = firstBeat;
                if (i > 0)
                {
                    anchor = BeatAtTime(meter.TimeSeconds);
                    low = Math.Max(low, anchor);
                }

                double high = lastBeat;
                if (i + 1 < settings.Meters.Length)
                {
                    high = Math.Min(high, BeatAtTime(settings.Meters[i + 1].TimeSeconds) - 1e-8);
                }

                double step = subdivision;
                // At overview scales emit every N meter beats instead of producing an unbounded mesh.
                if (range / step > budget)
                {
                    step *= Math.Ceiling(range / step / budget);
                }

                double index = Math.Ceiling((low - anchor) / step - 1e-8);
                double unit = 4.0 / meter.Denominator;
                if (range / unit > budget)
                {
                    double bar = meter.Numerator * unit;
                    unit = bar * Math.Ceiling(range / bar / budget);
                }

                double unitIndex = Math.Ceiling((low - anchor) / unit - 1e-8);
                for (int count = 0; count < maxLines; count++)
                {
                    double subdivisionBeat = anchor + index * step;
                    double unitBeat = anchor + unitIndex * unit;
                    double beat = Math.Min(subdivisionBeat, unitBeat);
                    if (beat > high + 1e-8)
                    {
                        break;
                    }

                    yield return beat;
                    if (subdivisionBeat <= beat + 1e-8)
                    {
                        index++;
                    }

                    if (unitBeat <= beat + 1e-8)
                    {
                        unitIndex++;
                    }
                }
            }
        }
    }
}
