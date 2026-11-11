using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    // All targets and particles are authored in the scene. Animation never replaces them.
    public sealed class DojoAmbientMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Sway
        {
            public RectTransform target;
            public Vector2 movement;
            public float angle = 2;
            public float speed = 1;
            public float phase;
            [NonSerialized] public Vector2 home;
            [NonSerialized] public Quaternion rotation;
        }

        public Sway[] sways = Array.Empty<Sway>();
        public RectTransform[] petals = Array.Empty<RectTransform>();
        public Vector2 drift = new Vector2(22, -16);
        private Vector2[] petalHomes;
        private Quaternion[] petalRotations;
        private float age;

        // One slow, consistently rightward breeze drives the summit cloth.
        public static float Wind(float time)
        {
            return .82f + Mathf.Sin(time * .63f) * .23f + Mathf.Sin(time * 1.07f + .8f) * .09f;
        }

        private void Awake()
        {
            foreach (var sway in sways)
            {
                if (!sway.target)
                {
                    throw new InvalidOperationException("Dojo ambient target missing.");
                }
                sway.home = sway.target.anchoredPosition;
                sway.rotation = sway.target.localRotation;
            }

            petalHomes = new Vector2[petals.Length];
            petalRotations = new Quaternion[petals.Length];
            for (int i = 0; i < petals.Length; i++)
            {
                if (!petals[i])
                {
                    throw new InvalidOperationException("Dojo ambient particle missing.");
                }
                petalHomes[i] = petals[i].anchoredPosition;
                petalRotations[i] = petals[i].localRotation;
            }
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            foreach (var sway in sways)
            {
                float wave = Wind(age * sway.speed + sway.phase) - .82f;
                sway.target.anchoredPosition = sway.home + sway.movement * wave;
                sway.target.localRotation = sway.rotation * Quaternion.Euler(0, 0, sway.angle * wave);
            }

            var bounds = ((RectTransform)transform).rect;
            for (int i = 0; i < petals.Length; i++)
            {
                float phase = i * 2.4f;
                float x = petalHomes[i].x + drift.x * age + Mathf.Sin(age * .63f + phase) * 1.2f;
                float y = petalHomes[i].y + drift.y * age;
                x = Mathf.Repeat(x - bounds.xMin + 24, bounds.width + 48) + bounds.xMin - 24;
                y = Mathf.Repeat(y - bounds.yMin + 24, bounds.height + 48) + bounds.yMin - 24;
                petals[i].anchoredPosition = new Vector2(x, y);
                petals[i].localRotation = petalRotations[i];
            }
        }

        private void OnDisable()
        {
            if (petalHomes == null)
            {
                return;
            }
            age = 0;
            foreach (var sway in sways)
            {
                sway.target.anchoredPosition = sway.home;
                sway.target.localRotation = sway.rotation;
            }
            for (int i = 0; i < petals.Length; i++)
            {
                petals[i].anchoredPosition = petalHomes[i];
                petals[i].localRotation = petalRotations[i];
            }
        }
    }
}
