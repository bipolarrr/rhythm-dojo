using System;
using NUnit.Framework;
using RhythmDojo.Application;

namespace RhythmDojo.Tests
{
    public sealed class AudioResumeTimingTests
    {
        [Test]
        public void ConversionRoundsToWithinOneSample(
            [Values(44100, 48000, 96000)] int frequency,
            [Values(-.25, 0, .25)] double chartOffset,
            [Values(0, .00001, .123456789, 12.3456789)] double audioTime)
        {
            int sample = AudioResumeTiming.SamplePosition(audioTime + chartOffset, chartOffset,
                frequency, frequency * 30);
            Assert.That(Math.Abs(sample / (double)frequency - audioTime),
                Is.LessThanOrEqualTo(1.0 / frequency));
        }

        [Test]
        public void BeforeAudioClampsToZeroAndEndUsesEndSentinel()
        {
            Assert.That(AudioResumeTiming.SamplePosition(-1, .25, 48000, 48000), Is.Zero);
            Assert.That(AudioResumeTiming.SamplePosition(1.25, .25, 48000, 48000), Is.EqualTo(48000));
            Assert.That(AudioResumeTiming.SamplePosition(100, -.25, 48000, 48000), Is.EqualTo(48000));
            Assert.That(AudioResumeTiming.SamplePosition(1.25 - .25 / 48000, .25, 48000, 48000),
                Is.EqualTo(48000), "Rounding beyond the final sample must not replay that sample.");
        }
    }
}
