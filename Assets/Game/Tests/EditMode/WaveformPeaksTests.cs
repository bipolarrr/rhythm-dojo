using System;
using NUnit.Framework;
using RhythmDojo.Authoring;
using UnityEngine;

namespace RhythmDojo.Tests
{
    public sealed class WaveformPeaksTests
    {
        [Test]
        public void RangeExcludesAdjacentTransientsAtCoarseAndSampleResolution()
        {
            const int frequency = 48000;
            var clip = AudioClip.Create("Precise peak boundaries", 17001, 2, frequency, false);
            try
            {
                var samples = new float[34002];
                samples[64 * 2] = 0.9f;
                samples[1000 * 2 + 1] = -0.8f;
                samples[17000 * 2] = 0.7f;
                clip.SetData(samples, 0);
                var peaks = new WaveformPeaks();
                var build = peaks.Build(clip);
                while (build.MoveNext())
                {
                    // Build the overview pyramid before testing exact interval edges.
                }

                Assert.That(peaks.Range(128.0 / frequency, 1024.0 / frequency).y, Is.Zero);
                Assert.That(peaks.Range(999.1 / frequency, 999.9 / frequency), Is.EqualTo(Vector2.zero));
                Assert.That(peaks.Range(999.9 / frequency, 1000.1 / frequency).x,
                    Is.EqualTo(-0.8f).Within(0.0001));
                Assert.That(peaks.Range(16999.1 / frequency, 16999.9 / frequency), Is.EqualTo(Vector2.zero));
                Assert.That(peaks.Range(16999.9 / frequency, 17000.1 / frequency).y,
                    Is.EqualTo(0.7f).Within(0.0001));
                Assert.That(peaks.Range(0.01, 0.01), Is.EqualTo(Vector2.zero));
                Assert.That(peaks.Range(0.02, 0.01), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [TestCase(44100)]
        [TestCase(48000)]
        public void EveryZoomRangeMatchesPcmSamplesOnTheSameHalfOpenTimeAxis(int frequency)
        {
            var clip = AudioClip.Create("Sample axis", 17001, 2, frequency, false);
            try
            {
                var samples = new float[34002];
                var random = new System.Random(1234);
                for (int i = 0; i < samples.Length; i += 2)
                {
                    samples[i] = (float)(random.NextDouble() * 2 - 1);
                    samples[i + 1] = -samples[i];
                }

                clip.SetData(samples, 0);
                var peaks = new WaveformPeaks();
                var build = peaks.Build(clip);
                while (build.MoveNext())
                {
                }

                foreach (int start in new[] { 0, 1, 63, 64, 127, 16383, 16999 })
                {
                    foreach (int width in new[] { 1, 2, 63, 64, 127, 128, 129, 513, 17001 })
                    {
                        int end = Math.Min(17001, start + width);
                        Vector2 expected = Vector2.zero;
                        for (int i = start * 2; i < end * 2; i++)
                        {
                            expected.x = Math.Min(expected.x, samples[i]);
                            expected.y = Math.Max(expected.y, samples[i]);
                        }

                        Assert.That(peaks.Range((double)start / frequency, (double)end / frequency),
                            Is.EqualTo(expected), $"Frames [{start}, {end}) at {frequency} Hz");
                    }

                    Assert.That(peaks.Sample(start, 0), Is.EqualTo(samples[start * 2]));
                    Assert.That(peaks.Sample(start, 1), Is.EqualTo(samples[start * 2 + 1]));
                    Assert.That(peaks.FrameAtOrAfter((double)start / frequency), Is.EqualTo(start));
                    Assert.That(peaks.Range((start + 0.1) / frequency, (start + 0.9) / frequency),
                        Is.EqualTo(Vector2.zero), "An interval between sample timestamps contains no sample.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [TestCase(100, 0)]
        [TestCase(1000, 1)]
        [TestCase(10000, 2)]
        public void FrequencyBandsUseRedGreenAndBlueWithoutCancellingStereo(int tone, int band)
        {
            const int frequency = 48000;
            var clip = AudioClip.Create("RGB verification", frequency, 2, frequency, false);
            try
            {
                var samples = new float[frequency * 2];
                for (int i = 0; i < frequency; i++)
                {
                    float value = (float)Math.Sin(i * 2 * Math.PI * tone / frequency) * 0.8f;
                    samples[i * 2] = value;
                    samples[i * 2 + 1] = -value;
                }

                Assert.That(clip.SetData(samples, 0), Is.True);
                var peaks = new WaveformPeaks();
                var build = peaks.Build(clip);
                while (build.MoveNext())
                {
                    // Filter state must survive each incremental read.
                }

                foreach (double duration in new[] { 0.02, 0.2, 0.8 })
                {
                    Color color = peaks.ColorRange(0.1, 0.1 + duration);
                    Assert.That(color[band], Is.EqualTo(1).Within(0.001));
                    Assert.That(color[band], Is.GreaterThan(color[(band + 1) % 3] * 1.3f));
                    Assert.That(color[band], Is.GreaterThan(color[(band + 2) % 3] * 1.3f));
                    Assert.That(color.a, Is.EqualTo(1));
                }

                Assert.That(peaks.Range(0.1, 0.9).y, Is.GreaterThan(0.79f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ColorCacheFollowsLocalFrequencyChangesAndHandlesSilence()
        {
            const int frequency = 44100;
            var clip = AudioClip.Create("Changing RGB verification", frequency * 3, 1, frequency, false);
            try
            {
                var samples = new float[frequency * 3];
                int[] tones = { 100, 1000, 10000 };
                for (int i = 0; i < samples.Length; i++)
                {
                    int tone = tones[i / frequency];
                    samples[i] = (float)Math.Sin(i * 2 * Math.PI * tone / frequency) * 0.6f;
                }

                Assert.That(clip.SetData(samples, 0), Is.True);
                var peaks = new WaveformPeaks();
                Color silence = peaks.ColorRange(0, 1);
                var build = peaks.Build(clip);
                while (build.MoveNext())
                {
                    // Exercise reads that cross a frequency transition.
                }

                for (int i = 0; i < tones.Length; i++)
                {
                    Color color = peaks.ColorRange(i + 0.2, i + 0.8);
                    Assert.That(color[i], Is.EqualTo(1).Within(0.001));
                    Assert.That(color[i], Is.GreaterThan(color[(i + 1) % 3] * 1.3f));
                    Assert.That(color[i], Is.GreaterThan(color[(i + 2) % 3] * 1.3f));
                }

                Assert.That(peaks.ColorRange(-2, -1), Is.EqualTo(silence));
                Assert.That(peaks.ColorRange(4, 5), Is.EqualTo(silence));
                Assert.That(peaks.ColorRange(1, 1), Is.EqualTo(silence));
                Assert.That(peaks.ColorRange(2, 1), Is.EqualTo(silence));
                Assert.That(clip.SetData(new float[samples.Length], 0), Is.True);
                build = peaks.Build(clip);
                Assert.That(build.MoveNext(), Is.True);
                Assert.That(peaks.Ready, Is.False);
                while (build.MoveNext())
                {
                    // Rebuild must discard the previous frequency cache.
                }

                Assert.That(peaks.ColorRange(0, clip.length), Is.EqualTo(silence));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PeakPyramidPreservesStereoTransientsAndPartialFinalBlock()
        {
            var clip = AudioClip.Create("Transient verification", 17001, 2, 48000, false);
            try
            {
                var samples = new float[34002];
                samples[1025] = -0.85f;
                samples[34000] = 0.97f;
                Assert.That(clip.SetData(samples, 0), Is.True);
                var peaks = new WaveformPeaks();
                var build = peaks.Build(clip);
                while (build.MoveNext())
                {
                    // Complete the incremental generator without depending on frame timing.
                }

                Assert.That(peaks.Ready, Is.True);
                double duration = (double)clip.samples / clip.frequency;
                Vector2 full = peaks.Range(0, duration);
                Assert.That(full.x, Is.EqualTo(-0.85f).Within(0.0001));
                Assert.That(full.y, Is.EqualTo(0.97f).Within(0.0001));
                Assert.That(peaks.Range(16960.0 / 48000, duration).y, Is.EqualTo(0.97f).Within(0.0001));
                Assert.That(peaks.Range(-2, -1), Is.EqualTo(Vector2.zero));
                Assert.That(peaks.Range(1, 2), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
