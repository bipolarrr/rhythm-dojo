using System;

namespace RhythmDojo.Application
{
    public static class AudioResumeTiming
    {
        // sampleCount is the end sentinel: never clamp a finished song to its final sample.
        public static int SamplePosition(double songTime, double chartOffset, int frequency, int sampleCount)
        {
            if (!double.IsFinite(songTime) || !double.IsFinite(chartOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(songTime));
            }
            if (frequency <= 0 || sampleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency));
            }

            double samples = (songTime - chartOffset) * frequency;
            if (samples <= 0)
            {
                return 0;
            }
            if (samples >= sampleCount)
            {
                return sampleCount;
            }
            return (int)Math.Round(samples, MidpointRounding.AwayFromZero);
        }
    }
}
