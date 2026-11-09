using System;
using System.Collections.Generic;

namespace RhythmDojo.Application
{
    // A chord contributes one time point, irrespective of lane or storage order.
    public sealed class TimingSampleSet
    {
        private readonly Dictionary<double, List<double>> samples = new Dictionary<double, List<double>>();
        private bool invalid;

        public void Add(double startTime, double? successfulError)
        {
            if (!double.IsFinite(startTime))
            {
                invalid = true;
                return;
            }

            if (!samples.TryGetValue(startTime, out var errors))
            {
                errors = new List<double>();
                samples.Add(startTime, errors);
            }

            if (!successfulError.HasValue)
            {
                return;
            }

            if (!double.IsFinite(successfulError.Value))
            {
                invalid = true;
                return;
            }

            errors.Add(successfulError.Value);
        }

        public int? RecommendOffsetMs(int currentOffsetMs)
        {
            if (invalid)
            {
                return null;
            }

            var errors = new List<double>();
            foreach (var values in samples.Values)
            {
                if (values.Count == 0)
                {
                    continue;
                }

                values.Sort();
                if (values[values.Count - 1] - values[0] > .050)
                {
                    return null;
                }

                int middle = values.Count / 2;
                double median = values[middle];
                if (values.Count % 2 == 0)
                {
                    median = (values[middle - 1] + median) / 2;
                }

                errors.Add(median);
            }

            if (errors.Count < samples.Count * .70)
            {
                return null;
            }

            int? recommendation = InputTiming.RecommendOffsetMs(errors, currentOffsetMs, 12);
            if (!recommendation.HasValue || Math.Abs(recommendation.Value - currentOffsetMs) < 5)
            {
                return null;
            }

            return recommendation;
        }
    }
}
