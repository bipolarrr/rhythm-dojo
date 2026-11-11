using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    [Category("Fast")]
    public sealed class DojoAnimationAssetsTests
    {
        private const string Root = "Assets/Game/Art/AlphaDojo/Training/";

        [TestCase("Trainee")]
        [TestCase("Senior")]
        public void BreathingChangesUpperBodyPixelsWhileFeetStayExactlyRegistered(string actor)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + actor + "Idle.anim");
            Assert.That(clip.length, Is.InRange(1.5f, 2.5f));
            Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty,
                "Idle must not animate transforms or scale.");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Assert.That(bindings.Length, Is.EqualTo(1));
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            var first = Pixels((Sprite)frames[0].value);
            int changed = 0;
            foreach (var frame in frames)
            {
                var current = Pixels((Sprite)frame.value);
                for (int i = 0; i < current.Length; i++)
                {
                    if (i < 128 * 46)
                    {
                        Assert.That(current[i], Is.EqualTo(first[i]), "Breathing cannot move feet or shins.");
                    }
                    else if (current[i] != first[i])
                    {
                        changed++;
                    }
                }
            }
            Assert.That(changed, Is.GreaterThan(100), "A static image is not a breathing animation.");
            CollectionAssert.AreEqual(first, Pixels((Sprite)frames[frames.Length - 1].value));
        }

        [Test]
        public void JumpHasAirborneFeetAndKickChangesTheLegSilhouette()
        {
            var idle = Load("TraineeFrame0");
            var crouch = Load("TraineeFrame19");
            var air = Load("TraineeFrame21");
            var kick = Load("TraineeFrame15");
            Assert.That(Bottom(air), Is.GreaterThan(Bottom(idle) + 12));
            Assert.That(Bottom(crouch), Is.EqualTo(Bottom(idle)));
            Assert.That(Bottom(kick), Is.EqualTo(Bottom(idle)));
            var first = Pixels(idle);
            var extended = Pixels(kick);
            int changed = 0;
            for (int y = 24; y < 70; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    if (Mathf.Abs(first[y * 128 + x].a - extended[y * 128 + x].a) > .5f)
                    {
                        changed++;
                    }
                }
            }
            Assert.That(changed, Is.GreaterThan(200), "The kick must redraw limbs, not reuse a shifted idle.");
        }

        private static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(Root + name + ".asset");
        }

        private static Color[] Pixels(Sprite sprite)
        {
            var pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, 128, 128);
            for (int i = 0; i < pixels.Length; i++)
            {
                // Unity dilates invisible RGB around alpha edges during import.
                if (pixels[i].a == 0)
                {
                    pixels[i] = Color.clear;
                }
            }
            return pixels;
        }

        private static int Bottom(Sprite sprite)
        {
            var pixels = Pixels(sprite);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > .5f)
                {
                    return i / 128;
                }
            }
            Assert.Fail("Empty animation frame.");
            return -1;
        }
    }
}
