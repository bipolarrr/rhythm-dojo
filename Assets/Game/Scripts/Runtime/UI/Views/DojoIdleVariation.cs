using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    // Scene-owned settings and session-owned timing; the view arbitrates which actor may begin.
    [Serializable]
    public sealed class DojoIdleVariation
    {
        public enum Motion
        {
            None, Settle, Hands, Look, Nod, Demonstrate, Focus, Explain, Point, Observe, Approve, Sleeve, Wind,
            Punch, Kick, Jump, Ready
        }

        [Serializable]
        public sealed class Variation
        {
            public Motion motion;
            [Min(.1f)] public float duration = 1.4f;
            [Min(0)] public float weight = 1;
            [Tooltip("Lesson = 1, practice = 2, summit = 4. Combine by adding.")]
            public int modeMask = 7;
            public Sprite sprite;
            public AnimationClip clip;
            public float Duration
            {
                get
                {
                    if (clip)
                    {
                        return clip.length;
                    }
                    return duration;
                }
            }
        }

        public bool enabled = true;
        public Vector2 cooldown = new Vector2(1.5f, 3.5f);
        public Variation[] variations = Array.Empty<Variation>();
        public bool IsPlaying => Current != null;
        public Variation Current { get; private set; }
        public Motion LastPlayed { get; private set; }
        public float Remaining { get; private set; }
        public float Progress { get; private set; }
        public float Strength
        {
            get
            {
                float enter = Mathf.SmoothStep(0, 1, Progress / .25f);
                float leave = 1 - Mathf.SmoothStep(0, 1, (Progress - .65f) / .35f);
                return enter * leave;
            }
        }

        [NonSerialized] private System.Random random = new System.Random();

        public void Validate()
        {
            if (!float.IsFinite(cooldown.x) || !float.IsFinite(cooldown.y))
            {
                throw new InvalidOperationException("Dojo idle cooldown must be finite.");
            }
            if (cooldown.x <= 0 || cooldown.y < cooldown.x)
            {
                throw new InvalidOperationException("Dojo idle cooldown range is invalid.");
            }
            if (variations == null || variations.Length == 0)
            {
                throw new InvalidOperationException("Dojo idle variations missing.");
            }
            foreach (var variation in variations)
            {
                if (variation == null || variation.motion == Motion.None)
                {
                    throw new InvalidOperationException("Dojo idle motion missing.");
                }
                if (!float.IsFinite(variation.duration) || variation.duration <= 0)
                {
                    throw new InvalidOperationException("Dojo idle duration is invalid.");
                }
                if (!float.IsFinite(variation.weight) || variation.weight < 0)
                {
                    throw new InvalidOperationException("Dojo idle weight is invalid.");
                }
            }
        }

        public void Cancel()
        {
            Current = null;
            Progress = 0;
            Remaining = Mathf.Lerp(cooldown.x, cooldown.y, (float)random.NextDouble());
        }

        public void Advance(float seconds, int mode, bool allowStart)
        {
            if (!enabled)
            {
                if (IsPlaying)
                {
                    Cancel();
                }
                return;
            }
            if (IsPlaying)
            {
                Progress = Mathf.Min(1, Progress + seconds / Current.Duration);
                if (Progress >= 1)
                {
                    Cancel();
                }
                return;
            }
            Remaining = Mathf.Max(0, Remaining - seconds);
            if (!allowStart || Remaining > 0)
            {
                return;
            }

            float total = 0;
            foreach (var variation in variations)
            {
                if (CanChoose(variation, mode))
                {
                    total += variation.weight;
                }
            }
            if (total <= 0)
            {
                // A single eligible variation rests instead of repeating indefinitely.
                Cancel();
                return;
            }
            float choice = (float)random.NextDouble() * total;
            foreach (var variation in variations)
            {
                if (!CanChoose(variation, mode))
                {
                    continue;
                }
                choice -= variation.weight;
                if (choice < 0)
                {
                    Begin(variation);
                    return;
                }
            }
        }

        public bool Respond(Motion motion, int mode)
        {
            if (!enabled || IsPlaying)
            {
                return false;
            }
            foreach (var variation in variations)
            {
                if (variation.motion == motion && CanChoose(variation, mode))
                {
                    Begin(variation);
                    return true;
                }
            }
            return false;
        }

        private bool CanChoose(Variation variation, int mode)
        {
            if (variation.weight <= 0 || variation.motion == LastPlayed)
            {
                return false;
            }
            return (variation.modeMask & (1 << mode)) != 0;
        }

        private void Begin(Variation variation)
        {
            Current = variation;
            LastPlayed = variation.motion;
            Progress = 0;
        }
    }
}
