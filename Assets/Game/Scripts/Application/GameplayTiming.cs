using System;

namespace RhythmDojo.Application
{
    // The clock adapter's input result is already calibrated. Only song time needs conversion.
    public sealed class GameplayTiming
    {
        private readonly ISongClock clock;

        public GameplayTiming(ISongClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public double SongToLogicalTime(double songTime)
        {
            double timingOffsetSeconds = (clock as IInputTimingClock)?.InputTimingOffsetSeconds ?? 0;
            return InputTiming.Calibrate(songTime, timingOffsetSeconds);
        }

        public double InputToLogicalTime(double timestamp) => clock.InputTimeToSongTime(timestamp);
    }
}
