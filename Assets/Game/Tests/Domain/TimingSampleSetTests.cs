using System;
using System.Collections.Generic;
using NUnit.Framework;
using RhythmDojo.Application;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class TimingSampleSetTests
    {
        [TestCase(false)]
        [TestCase(true)]
        [Category("Fast")]
        public void OppositeChordErrorsNeverRecommendAGlobalOffset(bool reverse)
        {
            var samples = new TimingSampleSet();
            double error = .04;
            if (reverse)
            {
                error = -error;
            }

            for (int i = 0; i < 12; i++)
            {
                samples.Add(i, error);
                samples.Add(i, -error);
            }

            Assert.That(samples.RecommendOffsetMs(0), Is.Null);
        }

        [Test]
        [Category("Fast")]
        public void OrderAndIdenticalChordMultiplicityDoNotWeightTheRecommendation(
            [Values(false, true)] bool reverse, [Values(1, 2, 4)] int chordSize)
        {
            var samples = new TimingSampleSet();
            for (int i = 0; i < 12; i++)
            {
                int time = i;
                if (reverse)
                {
                    time = 11 - i;
                }

                for (int lane = 0; lane < chordSize; lane++)
                {
                    samples.Add(time, .04);
                }
            }

            Assert.That(samples.RecommendOffsetMs(-20), Is.EqualTo(20));
        }

        [TestCase(11, 0, .04, 0, null)]
        [TestCase(12, 6, .04, 0, null)]
        [TestCase(14, 6, .04, 0, 40)]
        [TestCase(12, 0, .004, 0, null)]
        [TestCase(12, 0, .005, 0, 5)]
        [TestCase(12, 0, .04, 170, null)]
        [TestCase(12, 0, -.04, -160, -200)]
        [TestCase(12, 0, double.NaN, 0, null)]
        [TestCase(12, 0, double.PositiveInfinity, 0, null)]
        [Category("Fast")]
        public void RecommendationHonorsCoverageCountChangeAndFiniteLimits(
            int successes, int misses, double error, int offset, int? expected)
        {
            var samples = new TimingSampleSet();
            for (int i = 0; i < successes; i++)
            {
                samples.Add(i, error);
            }

            for (int i = 0; i < misses; i++)
            {
                samples.Add(successes + i, null);
            }

            Assert.That(samples.RecommendOffsetMs(offset), Is.EqualTo(expected));
        }

        [Test]
        [Category("Fast")]
        public void ChordsUseTheirMedianAndRejectWideInternalSpread()
        {
            var samples = new TimingSampleSet();
            for (int i = 0; i < 12; i++)
            {
                samples.Add(i, .02);
                samples.Add(i, .04);
                samples.Add(i, .06);
            }

            Assert.That(samples.RecommendOffsetMs(0), Is.EqualTo(40));
            samples.Add(0, -.02);
            Assert.That(samples.RecommendOffsetMs(0), Is.Null);
        }

        [Test]
        [Category("Fast")]
        public void SettingsCalculatorRejectsNonFiniteOutliersAndRequiresEightSamples()
        {
            var errors = new List<double>();
            for (int i = 0; i < 7; i++)
            {
                errors.Add(.04);
            }

            Assert.That(InputTiming.RecommendOffsetMs(errors, 0, 8), Is.Null);
            errors.Add(.04);
            Assert.That(InputTiming.RecommendOffsetMs(errors, 0, 8), Is.EqualTo(40));
            errors.Add(double.PositiveInfinity);
            Assert.That(InputTiming.RecommendOffsetMs(errors, 0, 8), Is.Null);
        }
    }
}
