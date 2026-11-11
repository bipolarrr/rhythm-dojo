using System.Collections.Generic;
using NUnit.Framework;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    [Category("Fast")]
    public sealed class DojoIdleVariationTests
    {
        [Test]
        public void WeightedChoicesRespectContextCooldownAndNeverRepeat()
        {
            var idle = new DojoIdleVariation
            {
                cooldown = new Vector2(2, 5),
                variations = new[]
                {
                    Variation(DojoIdleVariation.Motion.Nod, 3, 1),
                    Variation(DojoIdleVariation.Motion.Settle, 1, 1),
                    Variation(DojoIdleVariation.Motion.Hands, 0, 1),
                    Variation(DojoIdleVariation.Motion.Wind, 100, 4)
                }
            };
            idle.Validate();
            idle.Cancel();
            var last = DojoIdleVariation.Motion.None;
            var delays = new HashSet<float>();
            for (int i = 0; i < 40; i++)
            {
                Assert.That(idle.Remaining, Is.InRange(2f, 5f));
                delays.Add(idle.Remaining);
                idle.Advance(idle.Remaining - .01f, 0, true);
                Assert.That(idle.IsPlaying, Is.False, "Cooldown cannot start a gesture early.");
                idle.Advance(.02f, 0, true);
                Assert.That(idle.IsPlaying, Is.True);
                Assert.That(idle.Current.motion, Is.Not.EqualTo(last));
                Assert.That(idle.Current.motion,
                    Is.EqualTo(DojoIdleVariation.Motion.Nod).Or.EqualTo(DojoIdleVariation.Motion.Settle));
                last = idle.Current.motion;
                Assert.That(idle.Strength, Is.Zero);
                idle.Advance(.5f, 0, false);
                Assert.That(idle.Strength, Is.GreaterThan(.9f));
                idle.Advance(.5f, 0, false);
                Assert.That(idle.IsPlaying, Is.False);
                Assert.That(idle.Strength, Is.Zero, "Every gesture must return exactly to idle.");
            }
            Assert.That(delays.Count, Is.GreaterThan(1), "Cooldowns must vary, not use a fixed period.");
        }

        [Test]
        public void ArbitrationCancellationAndDisabledSettingsCannotLeakAQueuedGesture()
        {
            var idle = new DojoIdleVariation
            {
                variations = new[]
                {
                    Variation(DojoIdleVariation.Motion.Nod, 1, 1),
                    Variation(DojoIdleVariation.Motion.Settle, 1, 1)
                }
            };
            idle.Cancel();
            idle.Advance(20, 0, false);
            Assert.That(idle.IsPlaying, Is.False);
            idle.Advance(0, 0, true);
            Assert.That(idle.IsPlaying, Is.True);
            var last = idle.Current.motion;
            idle.Cancel();
            Assert.That(idle.IsPlaying, Is.False);
            Assert.That(idle.Respond(last, 0), Is.False, "A response also respects repeat suppression.");
            idle.enabled = false;
            idle.Advance(20, 0, true);
            Assert.That(idle.Respond(DojoIdleVariation.Motion.Nod, 0), Is.False);
            Assert.That(idle.IsPlaying, Is.False);
            idle.enabled = true;
            idle.Advance(20, 2, true);
            Assert.That(idle.IsPlaying, Is.False, "No eligible summit variation means idle.");
            idle.Advance(20, 0, true);
            Assert.That(idle.Current.motion, Is.Not.EqualTo(last));
        }

        private static DojoIdleVariation.Variation Variation(DojoIdleVariation.Motion motion, float weight, int modeMask)
        {
            return new DojoIdleVariation.Variation
            {
                motion = motion,
                weight = weight,
                modeMask = modeMask,
                duration = 1
            };
        }
    }
}
