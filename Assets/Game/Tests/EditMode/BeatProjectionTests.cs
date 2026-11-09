using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    [Category("Authoring")]
    public sealed class BeatProjectionTests
    {
        [Test]
        [Category("Fast")]
        public void TimeProjectionKeepsScrollSpeedAndChangesSpacingAt256And64Bpm()
        {
            var timing = new BeatGridTiming(new[]
            {
                new TempoPoint(0, 128),
                new TempoPoint(7.5, 256),
                new TempoPoint(15, 64)
            }, new BeatGridSettings());

            foreach (double time in new[] { -1.0, 0, 7.4, 7.5, 14.9, 15, 20 })
            {
                var projection = new BeatProjection(timing.BeatAtTime(time), -320, 20, timing);
                double distance = projection.Project(timing.BeatAtTime(time + 0.25))
                    - projection.Project(timing.BeatAtTime(time));
                Assert.That(distance, Is.EqualTo(0.25 * 128 / 60 * 20).Within(1e-9));
                Assert.That(projection.Project(timing.BeatAtTime(time)), Is.EqualTo(-320).Within(1e-9));
            }

            var grid = new BeatProjection(0, 0, 20, timing);
            Assert.That(grid.Project(2) - grid.Project(1), Is.EqualTo(20).Within(1e-9));
            double fast = timing.BeatAtTime(8);
            double slow = timing.BeatAtTime(16);
            Assert.That(grid.Project(fast + 1) - grid.Project(fast), Is.EqualTo(10).Within(1e-9));
            Assert.That(grid.Project(slow + 1) - grid.Project(slow), Is.EqualTo(40).Within(1e-9));
        }

        [Test]
        [Category("Fast")]
        public void TimeProjectionRoundTripsWithGridOriginAndFractionalTempoBoundaries()
        {
            var timing = new BeatGridTiming(new[]
            {
                new TempoPoint(0, 128),
                new TempoPoint(7.51, 256),
                new TempoPoint(15.13, 64)
            }, new BeatGridSettings { OriginSeconds = 2.08193 });

            foreach (double bottomTime in new[] { -20.0, 0, 7.5, 15, 100 })
            {
                foreach (double scale in new[] { 0.1, 20, 100 })
                {
                    var projection = new BeatProjection(timing.BeatAtTime(bottomTime), -320, scale, timing);
                    foreach (double time in new[] { -10.0, 0, 7.51 - 1e-9, 7.51, 7.51 + 1e-9, 15.13, 200 })
                    {
                        double beat = timing.BeatAtTime(time);
                        Assert.That(projection.Unproject(projection.Project(beat)), Is.EqualTo(beat).Within(1e-9));
                    }
                }
            }
        }

        [Test]
        [Category("Fast")]
        public void TempoRoundTripsAcrossFractionalBoundariesWithoutQuantizing()
        {
            var tempo = new TempoMap(
                new[]
                {
                    new TempoPoint(0, 123),
                    new TempoPoint(2.37, 181),
                    new TempoPoint(8.19, 67),
                }
            );

            foreach (
                double time in new[]
                {
                    -1.0,
                    0,
                    2.37 - 1e-9,
                    2.37,
                    2.37 + 1e-9,
                    8.19,
                    12345.678901234,
                }
            )
            {
                Assert.That(
                    tempo.TimeAtBeat(tempo.BeatAtTime(time)),
                    Is.EqualTo(time).Within(1e-10)
                );
            }

            foreach (double beat in new[] { -0.5, 0, 1.25, 4.8585, 22.4155, 9999.123456789 })
            {
                Assert.That(
                    tempo.BeatAtTime(tempo.TimeAtBeat(beat)),
                    Is.EqualTo(beat).Within(1e-10)
                );
            }

            Assert.That(tempo.BeatAtTime(2.37), Is.EqualTo(2.37 * 123 / 60).Within(1e-12));
        }

        [Test]
        [Category("Fast")]
        public void ProjectionRoundTripsAndZoomKeepsBottomFixedAtEverySizeAndScroll()
        {
            foreach (double height in new[] { 96.0, 217.5, 479, 720 })
            {
                foreach (double bottomBeat in new[] { 0.0, 0.5, 1048576.5 })
                {
                    foreach (int beats in new[] { 4, 12, 16, 32, 68, 132, 7200 })
                    {
                        var projection = new BeatProjection(bottomBeat, -height, height / beats);
                        Assert.That(projection.Project(bottomBeat), Is.EqualTo(-height));

                        foreach (double offset in new[] { 0, 0.123456789, 1, 4, 32 })
                        {
                            double beat = bottomBeat + offset;
                            Assert.That(
                                projection.Unproject(projection.Project(beat)),
                                Is.EqualTo(beat).Within(1e-9)
                            );
                        }
                    }
                }
            }
        }
    }
}
