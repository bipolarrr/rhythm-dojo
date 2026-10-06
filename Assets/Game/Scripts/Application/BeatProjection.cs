using System;

namespace RhythmDojo.Application
{
    // Coordinates may be pixels or canvas units. No rounding or tempo-dependent scaling.
    public readonly struct BeatProjection
    {
        public double BottomBeat { get; }
        public double Bottom { get; }
        public double UnitsPerBeat { get; }

        public BeatProjection(double bottomBeat, double bottom, double unitsPerBeat)
        {
            if (!double.IsFinite(bottomBeat) || !double.IsFinite(bottom) ||
                !double.IsFinite(unitsPerBeat) || unitsPerBeat <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitsPerBeat));
            BottomBeat = bottomBeat;
            Bottom = bottom;
            UnitsPerBeat = unitsPerBeat;
        }

        public double Project(double beat) => Bottom + (beat - BottomBeat) * UnitsPerBeat;
        public double Unproject(double y) => BottomBeat + (y - Bottom) / UnitsPerBeat;
    }
}
