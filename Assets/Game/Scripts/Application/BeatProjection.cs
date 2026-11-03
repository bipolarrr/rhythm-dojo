using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    // Coordinates may be pixels or canvas units. A timing map keeps scrolling linear in time.
    public readonly struct BeatProjection
    {
        public double BottomBeat { get; }
        public double Bottom { get; }
        public double UnitsPerBeat { get; }

        private readonly BeatGridTiming timing;
        private readonly double bottomTime;
        private readonly double referenceBpm;

        public BeatProjection(double bottomBeat, double bottom, double unitsPerBeat)
        {
            if (
                (!double.IsFinite(bottomBeat) || !double.IsFinite(bottom))
                || !double.IsFinite(unitsPerBeat)
            )
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerBeat));
            }

            if (unitsPerBeat <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerBeat));
            }

            BottomBeat = bottomBeat;
            Bottom = bottom;
            UnitsPerBeat = unitsPerBeat;
            timing = null;
            bottomTime = 0;
            referenceBpm = 0;
        }

        public BeatProjection(double bottomBeat, double bottom, double unitsPerBeat, BeatGridTiming timing)
            : this(bottomBeat, bottom, unitsPerBeat)
        {
            this.timing = timing ?? throw new ArgumentNullException(nameof(timing));
            bottomTime = timing.TimeAtBeat(bottomBeat);
            referenceBpm = timing.BpmAt(0);
        }

        public double Project(double beat)
        {
            if (timing != null)
            {
                return Bottom + (timing.TimeAtBeat(beat) - bottomTime) * referenceBpm / 60 * UnitsPerBeat;
            }

            return Bottom + (beat - BottomBeat) * UnitsPerBeat;
        }

        public double Unproject(double y)
        {
            if (timing != null)
            {
                return timing.BeatAtTime(bottomTime + (y - Bottom) / UnitsPerBeat * 60 / referenceBpm);
            }

            return BottomBeat + (y - Bottom) / UnitsPerBeat;
        }
    }
}
