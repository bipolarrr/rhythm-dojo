using System;
using System.Collections.Generic;

namespace RhythmDojo.Application
{
    public static class InputTiming
    {
        public static double Calibrate(double songTime, double timingOffsetSeconds)
        {
            return songTime - timingOffsetSeconds;
        }

        public static int? RecommendOffsetMs(List<double> errors, int currentOffsetMs, int minimumSamples)
        {
            if (errors.Count < minimumSamples)
            {
                return null;
            }

            var sorted = errors.ToArray();
            Array.Sort(sorted);

            int quarter = sorted.Length / 4;
            if (sorted[sorted.Length - 1 - quarter] - sorted[quarter] > .050)
            {
                return null;
            }

            int middle = sorted.Length / 2;
            double median = sorted[middle];
            if (sorted.Length % 2 == 0)
            {
                median = (sorted[middle - 1] + median) / 2;
            }

            // Gameplay errors are measured after subtracting the offset used for that play.
            double offset = Math.Round(currentOffsetMs + median * 1000, MidpointRounding.AwayFromZero);
            if (!double.IsFinite(offset) || Math.Abs(offset) > 200)
            {
                return null;
            }

            return (int)offset;
        }
    }
}
