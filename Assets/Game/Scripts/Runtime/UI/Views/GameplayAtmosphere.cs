using System;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.UI
{
    // Authored scenery only. Neither the highway nor its camera is an animation target.
    public sealed class GameplayAtmosphere : MonoBehaviour
    {
        public CanvasGroup windowLight;
        public CanvasGroup floorInk;
        public RectTransform nearInk;
        public RectTransform farInk;

        private Vector2 nearHome;
        private Vector2 farHome;
        private float age;
        private float impulse;
        private float impulseAge = 1;
        private int lastImpactFrame = -1;
        private bool initialized;

        public void Validate()
        {
            if (!windowLight || !floorInk)
            {
                throw new InvalidOperationException("Gameplay atmosphere layers missing.");
            }
            if (!nearInk || !farInk)
            {
                throw new InvalidOperationException("Gameplay ink motion targets missing.");
            }
        }

        private void Awake()
        {
            Validate();
            nearHome = nearInk.anchoredPosition;
            farHome = farInk.anchoredPosition;
            initialized = true;
            ResetPresentation();
        }

        public void React(Judgment grade)
        {
            if (grade == Judgment.Miss || lastImpactFrame == Time.frameCount)
            {
                return;
            }

            // Chords and streams never accumulate brightness or displacement.
            lastImpactFrame = Time.frameCount;
            impulse = .45f;
            if (grade == Judgment.Perfect)
            {
                impulse = 1;
            }
            impulseAge = 0;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            impulseAge += Time.unscaledDeltaTime;
            float breeze = DojoAmbientMotion.Wind(age * .35f) - .82f;
            float attack = Mathf.Clamp01(impulseAge / .065f);
            float release = 1 - Mathf.SmoothStep(0, 1, impulseAge / .42f);
            float contact = attack * release * impulse;

            windowLight.alpha = .58f + breeze * .18f + contact * .07f;
            floorInk.alpha = .72f + contact * .12f;
            nearInk.anchoredPosition = nearHome + new Vector2(breeze * 7, -contact * 2);
            farInk.anchoredPosition = farHome + new Vector2(-breeze * 3, 0);
        }

        public void ResetPresentation()
        {
            impulse = 0;
            impulseAge = 1;
            lastImpactFrame = -1;
            age = 0;
            if (!initialized)
            {
                return;
            }

            windowLight.alpha = .58f;
            floorInk.alpha = .72f;
            nearInk.anchoredPosition = nearHome;
            farInk.anchoredPosition = farHome;
        }

        private void OnDisable()
        {
            ResetPresentation();
        }
    }
}
