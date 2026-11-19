using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    // All painted cards, cloth meshes and the fixed dust pool are authored by the builder.
    // Only pose and per-instance shader state change during a session.
    public sealed class DojoRoomPhaseView : MonoBehaviour
    {
        public Camera roomCamera;
        public Transform leftDoor;
        public Transform rightDoor;
        public Transform[] dust;
        public Renderer[] surfaces;
        public CanvasGroup picture;
        public Vector3 leftDoorClosed;
        public Vector3 rightDoorClosed;
        public float doorDistance;
        public Transform[] hanging;

        private MaterialPropertyBlock properties;
        private float leftOpening;
        private float rightOpening;
        private float leftVelocity;
        private float rightVelocity;
        private float travel;
        private float travelVelocity;
        private float elapsed;
        private float impulseAge = 1;
        private float impulse;
        private float wind = 1;
        private float climaxAge = -1;
        private float climaxStart;
        private float clearanceAge;
        private float passageAge;
        private float exteriorAge;
        private bool timedExit;

        public float Opening => leftOpening;
        public float Travel => travel;
        public float VisualTime => elapsed;
        public bool ReadyForHandoff => exteriorAge >= .55f;

        public void Validate()
        {
            if (!roomCamera || !leftDoor || !rightDoor)
            {
                throw new InvalidOperationException("Dojo room camera or sliding doors missing.");
            }
            if (hanging == null || hanging.Length != 4)
            {
                throw new InvalidOperationException("Dojo painted hanging layers missing.");
            }
            if (doorDistance <= 0)
            {
                throw new InvalidOperationException("Dojo painted door travel is not authored.");
            }
            foreach (var item in hanging)
            {
                if (!item)
                {
                    throw new InvalidOperationException("Dojo hanging layer has a missing element.");
                }
            }
            if (!picture || dust == null || surfaces == null)
            {
                throw new InvalidOperationException("Dojo room presentation references missing.");
            }
            if (dust.Length != 12 || surfaces.Length == 0)
            {
                throw new InvalidOperationException("Dojo room authored geometry or dust pool missing.");
            }
            foreach (var item in dust)
            {
                if (!item)
                {
                    throw new InvalidOperationException("Dojo dust pool has a missing element.");
                }
            }
            foreach (var item in surfaces)
            {
                if (!item || !item.sharedMaterial)
                {
                    throw new InvalidOperationException("Dojo surface has a missing renderer or material.");
                }
            }
        }

        public void ResetPresentation()
        {
            Validate();
            properties ??= new MaterialPropertyBlock();
            leftOpening = rightOpening = leftVelocity = rightVelocity = 0;
            travel = travelVelocity = elapsed = 0;
            impulseAge = 1;
            impulse = 0;
            wind = 1;
            climaxAge = -1;
            climaxStart = clearanceAge = passageAge = exteriorAge = 0;
            timedExit = false;
            ApplyPose();
        }

        // This is a scheduled departure, not earned performance. The director keeps
        // the music phase and judgment totals unchanged while the passage finishes.
        public void BeginTimedExit()
        {
            timedExit = true;
        }

        public void Step(float delta, float progress, float reaction, bool missed)
        {
            if (delta <= 0)
            {
                return;
            }
            if (reaction > 0)
            {
                impulse = Mathf.Clamp01(reaction);
                impulseAge = 0;
            }
            if (missed)
            {
                wind = .45f;
            }

            // A clock catch-up must not become a camera teleport. Small integration
            // steps also keep acceleration and clearance timing consistent at 30/60/120 Hz.
            float remaining = Mathf.Min(delta, .1f);
            while (remaining > .00001f)
            {
                float step = Mathf.Min(remaining, 1f / 120);
                AdvanceMotion(step, progress);
                remaining -= step;
            }
            ApplyPose();
        }

        private void AdvanceMotion(float delta, float progress)
        {
            elapsed += delta;
            impulseAge += delta;
            wind = Mathf.MoveTowards(wind, 1, delta * 2.2f);
            float target = Mathf.Clamp01(progress / .85f);
            if (timedExit)
            {
                target = 1;
            }
            leftOpening = Mathf.SmoothDamp(leftOpening, target, ref leftVelocity, .62f, .8f, delta);
            rightOpening = Mathf.SmoothDamp(rightOpening, target, ref rightVelocity, .70f, .76f, delta);

            bool departing = progress >= .85f || timedExit;
            if (departing && climaxAge < 0)
            {
                climaxAge = 0;
                climaxStart = travel;
            }
            if (climaxAge >= 0)
            {
                climaxAge += delta;
                if (Mathf.Min(leftOpening, rightOpening) >= .94f)
                {
                    clearanceAge += delta;
                }
                if (clearanceAge >= .30f)
                {
                    passageAge += delta;
                }
            }

            // Approach, acceleration, cruise and braking share the same position AND
            // velocity. Never replace the approach with a new position-only tween.
            float approach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, .85f, progress));
            if (timedExit)
            {
                approach = Mathf.Max(approach, Mathf.Min(leftOpening, rightOpening));
            }
            float destination = Mathf.Max(climaxStart, approach * 3.1f);
            float speed = .65f;
            if (passageAge > 0)
            {
                float acceleration = Mathf.SmoothStep(0, 1, passageAge / .85f);
                destination = Mathf.Lerp(destination, 8.9f, acceleration);
                speed = Mathf.Lerp(speed, 1.8f, acceleration);
            }
            travel = Mathf.SmoothDamp(travel, destination, ref travelVelocity, .95f, speed, delta);

            if (travel >= 8.85f && travelVelocity < .12f)
            {
                exteriorAge += delta;
            }
        }

        private void ApplyPose()
        {
            leftDoor.localPosition = leftDoorClosed + Vector3.left * (leftOpening * doorDistance);
            rightDoor.localPosition = rightDoorClosed + Vector3.right * (rightOpening * doorDistance);
            float drift = Mathf.Sin(elapsed * .47f) * .006f + Mathf.Sin(elapsed * .83f) * .003f;
            roomCamera.transform.localPosition = new Vector3(drift, 1.28f, travel);
            roomCamera.transform.localRotation = Quaternion.identity;
            // Cover wide displays without stretching pixels or exposing the card edges.
            float fit = Mathf.Min(1, (16f / 9) / roomCamera.aspect);
            roomCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(29 * Mathf.Deg2Rad) * fit) * Mathf.Rad2Deg;

            for (int i = 0; i < hanging.Length; i++)
            {
                float angle = Mathf.Sin(elapsed * (.71f + i * .13f) + i * 1.4f);
                angle -= Mathf.Sin(i * 1.4f);
                hanging[i].localRotation = Quaternion.Euler(0, 0, angle * .8f * wind);
            }

            float pulse = 0;
            if (impulseAge < .59f)
            {
                float rise = Mathf.SmoothStep(0, 1, (impulseAge - .06f) / .18f);
                float settle = 1 - Mathf.SmoothStep(0, 1, (impulseAge - .24f) / .35f);
                pulse = impulse * rise * settle;
            }
            properties ??= new MaterialPropertyBlock();
            properties.SetFloat("_Opening", Mathf.Min(leftOpening, rightOpening));
            properties.SetFloat("_VisualTime", elapsed);
            properties.SetFloat("_Pulse", pulse);
            properties.SetFloat("_Wind", wind);
            foreach (var surface in surfaces)
            {
                surface.SetPropertyBlock(properties);
            }

            for (int i = 0; i < dust.Length; i++)
            {
                float phase = Mathf.Repeat(elapsed * (.037f + i * .0017f) + i * .267f, 1);
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                dust[i].localPosition = new Vector3(side * (1.35f + i * .08f) +
                    Mathf.Sin(elapsed * .71f + i * 1.7f) * .13f, .35f + phase * 2.6f,
                    3.8f + (i % 4) * .85f);
                float size = (.009f + i % 3 * .004f) * Mathf.Sin(phase * Mathf.PI);
                dust[i].localScale = Vector3.one * size;
            }
        }
    }
}
