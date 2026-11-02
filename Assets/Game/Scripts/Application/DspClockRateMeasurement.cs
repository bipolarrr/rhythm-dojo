using System;

namespace RhythmDojo.Application
{
    public enum DspClockRateState
    {
        Undetermined,
        Healthy,
        Unhealthy,
    }

    public readonly struct DspClockRateMeasurement
    {
        public const double AllowedDeviation = .01;
        public const int RequiredConsecutiveMeasurements = 2;

        public double ElapsedSeconds { get; }
        public double DspElapsedSeconds { get; }
        public int BufferSize { get; }
        public int SampleRate { get; }
        public double Ratio { get; }
        public double LowerBound { get; }
        public double UpperBound { get; }
        public DspClockRateState State { get; }

        public DspClockRateMeasurement(double elapsedSeconds, double dspElapsedSeconds,
            int bufferSize, int sampleRate)
        {
            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }
            if (!double.IsFinite(dspElapsedSeconds) || dspElapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dspElapsedSeconds));
            }
            if (bufferSize <= 0 || sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            ElapsedSeconds = elapsedSeconds;
            DspElapsedSeconds = dspElapsedSeconds;
            BufferSize = bufferSize;
            SampleRate = sampleRate;
            Ratio = dspElapsedSeconds / elapsedSeconds;
            // Each endpoint may be observed anywhere inside one DSP block.
            double endpointError = 2.0 * bufferSize / sampleRate;
            LowerBound = Math.Max(0, (dspElapsedSeconds - endpointError) / elapsedSeconds);
            UpperBound = (dspElapsedSeconds + endpointError) / elapsedSeconds;
            State = DspClockRateState.Undetermined;
            if (LowerBound >= 1 - AllowedDeviation && UpperBound <= 1 + AllowedDeviation)
            {
                State = DspClockRateState.Healthy;
            }
            else if (UpperBound < 1 - AllowedDeviation || LowerBound > 1 + AllowedDeviation)
            {
                State = DspClockRateState.Unhealthy;
            }
        }
    }
}
