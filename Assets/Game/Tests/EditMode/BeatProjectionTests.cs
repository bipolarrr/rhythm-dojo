using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class BeatProjectionTests
    {
        [Test]
        public void TempoRoundTripsAcrossFractionalBoundariesWithoutQuantizing()
        {
            var tempo = new TempoMap(new[] { new TempoPoint(0, 123), new TempoPoint(2.37, 181), new TempoPoint(8.19, 67) });
            foreach (double time in new[] { -1.0, 0, 2.37 - 1e-9, 2.37, 2.37 + 1e-9, 8.19, 12345.678901234 })
                Assert.That(tempo.TimeAtBeat(tempo.BeatAtTime(time)), Is.EqualTo(time).Within(1e-10));
            foreach (double beat in new[] { -0.5, 0, 1.25, 4.8585, 22.4155, 9999.123456789 })
                Assert.That(tempo.BeatAtTime(tempo.TimeAtBeat(beat)), Is.EqualTo(beat).Within(1e-10));
            Assert.That(tempo.BeatAtTime(2.37), Is.EqualTo(2.37 * 123 / 60).Within(1e-12));
        }

        [Test]
        public void ProjectionRoundTripsAndZoomKeepsBottomFixedAtEverySizeAndScroll()
        {
            foreach (double height in new[] { 96.0, 217.5, 479, 720 })
            foreach (double bottomBeat in new[] { 0.0, 0.5, 1048576.5 })
            foreach (int beats in new[] { 4, 12, 16, 32, 68, 132, 7200 })
            {
                var projection = new BeatProjection(bottomBeat, -height, height / beats);
                Assert.That(projection.Project(bottomBeat), Is.EqualTo(-height));
                foreach (double offset in new[] { 0, 0.123456789, 1, 4, 32 })
                {
                    double beat = bottomBeat + offset;
                    Assert.That(projection.Unproject(projection.Project(beat)), Is.EqualTo(beat).Within(1e-9));
                }
            }
        }
    }
}
