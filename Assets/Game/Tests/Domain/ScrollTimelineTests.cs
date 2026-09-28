using System;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class ScrollTimelineTests
    {
        private static TempoMap Map() => new TempoMap(new[] { new TempoPoint(0,120), new TempoPoint(6,180) });
        [TestCase(.5)] [TestCase(1)] [TestCase(2)]
        public void IntegratesEverySegmentAndAppliesIndependentMultiplier(double multiplier)
        {
            var scroll = new BpmScrollTimeline(Map(), 8, 120, multiplier);
            Assert.That(scroll.DistanceBetween(5,7), Is.EqualTo(20 * multiplier).Within(1e-9));
            Assert.That(scroll.DistanceBetween(7,5), Is.EqualTo(-20 * multiplier).Within(1e-9));
            Assert.That(new ConstantScrollTimeline(8,multiplier).DistanceBetween(5,7), Is.EqualTo(16 * multiplier));
        }
        [Test]
        public void BoundaryIsContinuousAndVelocityChanges()
        {
            var scroll = new BpmScrollTimeline(Map(),8,120,1); const double step = .0001;
            Assert.That(scroll.DistanceBetween(6-step,10) - scroll.DistanceBetween(6,10), Is.EqualTo(8*step).Within(1e-9));
            Assert.That(scroll.DistanceBetween(6,10) - scroll.DistanceBetween(6+step,10), Is.EqualTo(12*step).Within(1e-9));
            Assert.That(scroll.DistanceBetween(6,6), Is.Zero);
        }
        [Test]
        public void CountdownAndLastSegmentExtendAndInputsAreCopied()
        {
            var points = new[] { new TempoPoint(0,120), new TempoPoint(6,180) }; var map = new TempoMap(points);
            points[0] = new TempoPoint(0,1); var scroll = new BpmScrollTimeline(map,8,120,1);
            Assert.That(scroll.DistanceBetween(-2,1), Is.EqualTo(24));
            Assert.That(scroll.DistanceBetween(10,20), Is.EqualTo(120));
            Assert.That(map.BpmAt(-1), Is.EqualTo(120)); Assert.That(map.BpmAt(6), Is.EqualTo(180));
        }
        [Test]
        public void HoldAcrossBoundaryHasConsistentLength()
        {
            var scroll = new BpmScrollTimeline(Map(),8,120,1);
            Assert.That(scroll.DistanceBetween(5.2,6.8), Is.EqualTo(16).Within(1e-9));
            Assert.That(scroll.DistanceBetween(5,6.8)-scroll.DistanceBetween(5,5.2), Is.EqualTo(16).Within(1e-9));
            Assert.That(scroll.DistanceBetween(6.2,6.8), Is.EqualTo(7.2).Within(1e-9));
        }
        [Test]
        public void MalformedTempoAndSpeedAreRejected()
        {
            foreach (var points in new[] {
                Array.Empty<TempoPoint>(), new[] { new TempoPoint(1,120) },
                new[] { new TempoPoint(0,0) }, new[] { new TempoPoint(0,double.NaN) },
                new[] { new TempoPoint(0,120), new TempoPoint(0,180) },
                new[] { new TempoPoint(0,120), new TempoPoint(2,180), new TempoPoint(1,90) } })
                Assert.Throws<ArgumentException>(() => new TempoMap(points));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConstantScrollTimeline(0,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BpmScrollTimeline(Map(),8,0,1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BpmScrollTimeline(Map(),8,120,1).DistanceBetween(double.NaN,1));
        }
    }
}
