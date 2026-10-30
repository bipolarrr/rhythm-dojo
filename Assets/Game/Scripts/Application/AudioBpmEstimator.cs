using System;

namespace RhythmDojo.Application
{
    public static class AudioBpmEstimator
    {
        // Positive energy changes, sampled at 200 Hz, then normalized autocorrelation.
        public static double Estimate(float[] samples, int sampleRate, int channels, double referenceBpm = 120)
        {
            if (samples == null || sampleRate < 200 || channels < 1)
            {
                throw new ArgumentException("Valid PCM audio is required.");
            }

            int hop = Math.Max(1, sampleRate / 200);
            int count = samples.Length / channels / hop;
            if (count < 800)
            {
                throw new ArgumentException("BPM 측정에는 4초 이상의 음원이 필요합니다.");
            }

            var onset = new double[count];
            double previous = 0;
            double total = 0;
            for (int i = 0; i < count; i++)
            {
                double energy = 0;
                int start = i * hop * channels;
                for (int j = 0; j < hop * channels; j++)
                {
                    energy += Math.Abs(samples[start + j]);
                }

                onset[i] = Math.Max(0, energy - previous);
                previous = energy;
                total += onset[i];
            }

            if (total < 0.001)
            {
                throw new InvalidOperationException("박자 변화가 충분하지 않아 BPM을 측정할 수 없습니다.");
            }

            // Broaden transients so periods between 200 Hz envelope samples are not
            // unfairly outranked by exact integer multiples of the beat period.
            var smoothed = new double[count];
            for (int i = 2; i < count - 2; i++)
            {
                smoothed[i] = onset[i] + .5 * (onset[i - 1] + onset[i + 1])
                    + .25 * (onset[i - 2] + onset[i + 2]);
            }
            onset = smoothed;

            double rate = (double)sampleRate / hop;
            double best = double.NegativeInfinity;
            double result = 0;
            for (int lag = (int)(rate * 60 / 240); lag <= (int)(rate * 60 / 60); lag++)
            {
                double product = 0;
                double left = 0;
                double right = 0;
                for (int i = lag; i < count; i++)
                {
                    product += onset[i] * onset[i - lag];
                    left += onset[i] * onset[i];
                    right += onset[i - lag] * onset[i - lag];
                }

                double bpm = rate * 60 / lag;
                double score = product / Math.Sqrt(Math.Max(1e-20, left * right));
                score -= Math.Abs(Math.Log(bpm / Math.Max(60, referenceBpm))) * 0.12;
                if (score > best)
                {
                    best = score;
                    result = bpm;
                }
            }

            if (best < 0.08)
            {
                throw new InvalidOperationException("일정한 박자를 찾지 못했습니다. BPM을 직접 입력하세요.");
            }

            return Math.Round(result, 2);
        }
    }
}
