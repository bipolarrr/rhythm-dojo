using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // The builder owns every image, trail and speed line. Only their poses change at runtime.
    public sealed class PracticeStrikeAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class StrikeVisual
        {
            public Image image;
            public CanvasGroup speedLines;
            [NonSerialized] public float age, contactTime, duration, releaseAge;
            [NonSerialized] public int direction, motion;
            [NonSerialized] public bool active, contacted, changesSong, rapid, releasing, smeared;
            [NonSerialized] public Vector2 releasePosition;
            [NonSerialized] public Vector2 contactPosition;
        }

        [Serializable]
        public sealed class Afterimage
        {
            public Image image;
            [NonSerialized] public float age;
        }

        public StrikeVisual[] visuals;
        public Afterimage[] afterimages;
        public Sprite[] frames;
        public WoodenDummyRig rig;
        [Range(1, 3)] public int maximumStrikes = 3;
        [Range(0, 4)] public int maximumAfterimages = 4;

        public event Action<int, bool, bool> Contact;
        public int LastMotion { get; private set; } = -1;
        public int LastContactMotion { get; private set; }
        public Vector2 LastContactPosition { get; private set; }
        public int ActiveCount { get; private set; }
        public int AfterimageCount { get; private set; }
        public bool IsAnimating => ActiveCount > 0 || AfterimageCount > 0;

        private int lastLeft = -1, lastRight = -1, trailCursor;

        public void Validate()
        {
            if (!rig)
            {
                throw new InvalidOperationException("Practice strikes require the authored dummy contact landmarks.");
            }
            if (visuals == null || visuals.Length != 3)
            {
                throw new InvalidOperationException("Practice requires three authored strike slots.");
            }
            if (afterimages == null || afterimages.Length != 4)
            {
                throw new InvalidOperationException("Practice requires four authored afterimages.");
            }
            if (frames == null || frames.Length != 12)
            {
                throw new InvalidOperationException("Practice requires four poses each for punch, chop and kick.");
            }
            foreach (var visual in visuals)
            {
                if (visual == null || !visual.image || !visual.speedLines)
                {
                    throw new InvalidOperationException("Practice strike composition missing.");
                }
            }
            foreach (var trail in afterimages)
            {
                if (trail == null || !trail.image)
                {
                    throw new InvalidOperationException("Practice afterimage missing.");
                }
            }
            foreach (var frame in frames)
            {
                if (!frame)
                {
                    throw new InvalidOperationException("Practice strike pose missing.");
                }
            }
        }

        public bool Play(int direction, bool changesSong, bool rapid)
        {
            int count = Mathf.Clamp(maximumStrikes, 1, visuals.Length);
            for (int i = 0; i < count; i++)
            {
                var visual = visuals[i];
                if (visual.active)
                {
                    continue;
                }

                int previous = lastRight;
                if (direction < 0)
                {
                    previous = lastLeft;
                }
                int motion = UnityEngine.Random.Range(0, 3);
                if (previous >= 0)
                {
                    motion = 2;
                    if (previous == 2)
                    {
                        motion = UnityEngine.Random.Range(0, 2);
                    }
                }
                if (direction < 0)
                {
                    lastLeft = motion;
                }
                else
                {
                    lastRight = motion;
                }

                LastMotion = motion;
                visual.motion = motion;
                visual.direction = direction;
                visual.changesSong = changesSong;
                visual.rapid = rapid;
                visual.age = visual.releaseAge = 0;
                visual.contacted = visual.releasing = visual.smeared = false;
                visual.active = true;
                visual.contactTime = .032f;
                visual.duration = .115f;
                if (rapid)
                {
                    visual.contactTime = .025f;
                    visual.duration = .095f;
                }
                visual.image.enabled = true;
                visual.image.color = Color.white;
                Pose(visual);
                ActiveCount++;
                return true;
            }
            return false;
        }

        public void StopBarrage()
        {
            foreach (var visual in visuals)
            {
                if (!visual.active || !visual.rapid || visual.releasing)
                {
                    continue;
                }
                visual.releasing = true;
                visual.image.sprite = frames[visual.motion * 4 + 3];
                visual.image.rectTransform.pivot = visual.image.sprite.pivot / visual.image.sprite.rect.size;
                visual.releasePosition = visual.image.rectTransform.anchoredPosition;
                visual.speedLines.alpha = 0;
            }
        }

        public void Tick(float deltaTime)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Practice.StrikeAnimation");
            TickAfterimages(deltaTime);
            ActiveCount = 0;
            foreach (var visual in visuals)
            {
                if (!visual.active)
                {
                    continue;
                }

                if (visual.releasing)
                {
                    visual.releaseAge += deltaTime;
                    float t = Mathf.Clamp01(visual.releaseAge / .04f);
                    visual.image.rectTransform.anchoredPosition = Vector2.Lerp(visual.releasePosition,
                        new Vector2(visual.direction * 900, visual.releasePosition.y - 70), t);
                    if (t >= 1)
                    {
                        Hide(visual);
                        continue;
                    }
                }
                else
                {
                    visual.age += deltaTime;
                    Pose(visual);
                    if (visual.rapid && !visual.smeared)
                    {
                        if (visual.age >= visual.contactTime * .42f)
                        {
                            visual.smeared = true;
                            Stamp(visual, 0);
                        }
                    }
                    if (!visual.contacted && visual.age >= visual.contactTime)
                    {
                        visual.contacted = true;
                        // A hitch still presents the contact pose for one rendered frame.
                        visual.age = visual.contactTime;
                        Pose(visual);
                        Stamp(visual, .025f);
                        LastContactMotion = visual.motion;
                        LastContactPosition = visual.contactPosition;
                        Contact?.Invoke(visual.direction, visual.changesSong, visual.rapid);
                    }
                    if (visual.age >= visual.duration)
                    {
                        Hide(visual);
                        continue;
                    }
                }
                ActiveCount++;
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private void Pose(StrikeVisual visual)
        {
            float time = visual.age;
            float hold = .0175f;
            if (visual.rapid)
            {
                hold = .012f;
            }
            float enter = Mathf.Clamp01(time / visual.contactTime);
            float retreat = Mathf.Clamp01((time - visual.contactTime - hold) /
                (visual.duration - visual.contactTime - hold));
            int frame = 0;
            if (enter >= .42f)
            {
                frame = 1;
            }
            if (enter >= 1)
            {
                frame = 2;
            }
            if (retreat > 0)
            {
                frame = 3;
            }

            var rect = visual.image.rectTransform;
            var sprite = frames[visual.motion * 4 + frame];
            visual.image.sprite = sprite;
            rect.pivot = sprite.pivot / sprite.rect.size;
            float height = 185;
            float arc = 20;
            float twist = -8;
            float contactAngle = 0;
            switch (visual.motion)
            {
                case 1:
                    arc = 55;
                    twist = 12;
                    break;
                case 2:
                    height = 210;
                    contactAngle = 12;
                    arc = -40;
                    twist = -14;
                    break;
            }
            if (!visual.contacted)
            {
                visual.contactPosition = rig.ContactPoint(visual.direction, (RectTransform)transform,
                    visual.motion == 2);
            }
            var contact = visual.contactPosition;
            var start = new Vector2(visual.direction * 900, contact.y - 70);
            var control = new Vector2(visual.direction * 430, contact.y + arc);
            float t = enter * enter;
            var position = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * contact;
            float angle = Mathf.Lerp(twist, 0, t);
            float scale = Mathf.Lerp(.9f, 1, t);
            if (retreat > 0)
            {
                position = Vector2.Lerp(contact, start, 1 - (1 - retreat) * (1 - retreat));
                position.y += Mathf.Sin(retreat * Mathf.PI) * arc * .55f;
                angle = Mathf.Lerp(0, -twist, retreat);
                scale = Mathf.Lerp(1, .9f, retreat);
            }
            // Foreshortening and wrist/ankle articulation come from authored silhouettes;
            // the curve, rotation and brief compression supply momentum between frames.
            float compression = Mathf.Sin(Mathf.Clamp01((time - visual.contactTime) / hold) * Mathf.PI);
            rect.sizeDelta = new Vector2(height * sprite.rect.width / sprite.rect.height, height);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0, 0, -visual.direction * (angle + contactAngle));
            rect.localScale = new Vector3(-visual.direction * scale * (1 + compression * .045f),
                scale * (1 - compression * .035f), 1);

            var lines = (RectTransform)visual.speedLines.transform;
            lines.anchoredPosition = contact + new Vector2(visual.direction * 115, -20 + arc * .2f);
            lines.localRotation = Quaternion.Euler(0, 0, -visual.direction * (8 + angle));
            lines.localScale = new Vector3(-visual.direction, 1, 1);
            visual.speedLines.alpha = Mathf.Sin(enter * Mathf.PI * .8f) * (1 - retreat);
        }

        private void Stamp(StrikeVisual visual, float initialAge)
        {
            int count = Mathf.Clamp(maximumAfterimages, 0, afterimages.Length);
            if (count == 0)
            {
                return;
            }
            var trail = afterimages[trailCursor % count];
            trailCursor = (trailCursor + 1) % count;
            trail.age = initialAge;
            var source = visual.image.rectTransform;
            var rect = trail.image.rectTransform;
            trail.image.sprite = visual.image.sprite;
            trail.image.color = new Color(1f, .77f, .38f, .32f);
            trail.image.enabled = true;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition + new Vector2(visual.direction * 42, -24);
            rect.localRotation = source.localRotation * Quaternion.Euler(0, 0, visual.direction * 5);
            rect.localScale = source.localScale;
            AfterimageCount = Mathf.Min(count, AfterimageCount + 1);
        }

        private void TickAfterimages(float deltaTime)
        {
            AfterimageCount = 0;
            foreach (var trail in afterimages)
            {
                if (!trail.image.enabled)
                {
                    continue;
                }
                trail.age += deltaTime;
                float fade = Mathf.Clamp01(trail.age / .065f);
                trail.image.color = new Color(1f, .77f, .38f, .32f * (1 - fade));
                if (fade >= 1)
                {
                    trail.image.enabled = false;
                    continue;
                }
                AfterimageCount++;
            }
        }

        private static void Hide(StrikeVisual visual)
        {
            visual.active = false;
            visual.image.enabled = false;
            visual.image.rectTransform.anchoredPosition = new Vector2(visual.direction * 1250, -620);
            visual.speedLines.alpha = 0;
        }

        public void ResetPresentation()
        {
            foreach (var visual in visuals)
            {
                Hide(visual);
            }
            foreach (var trail in afterimages)
            {
                trail.image.enabled = false;
            }
            ActiveCount = AfterimageCount = trailCursor = 0;
        }
    }
}
