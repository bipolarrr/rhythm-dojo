using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class RunPhaseView : TrainingJourneyView
    {
        public JourneyScenery[] roadside;
        public JourneyScenery[] road;
        public Transform gateway;

        private float distance;
        private float momentum;
        private float speed;
        private float stumble;
        private float opening;

        public float Speed => speed;
        public float Momentum => momentum;
        public bool PeakReached => opening > .95f;

        public override void Validate()
        {
            base.Validate();
            if (!gateway || roadside == null || road == null)
            {
                throw new InvalidOperationException("Running trail composition is missing.");
            }
            foreach (var items in new[] { roadside, road })
            {
                if (items.Length == 0)
                {
                    throw new InvalidOperationException("Running trail pool is empty.");
                }
                foreach (var item in items)
                {
                    if (item == null || !item.item)
                    {
                        throw new InvalidOperationException("Running trail pool reference is missing.");
                    }
                }
            }
        }

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            distance = momentum = stumble = opening = 0;
            speed = 3;
            ApplyPose(0);
        }

        public void Step(float delta, float progress, float reaction, bool missed)
        {
            if (delta <= 0)
            {
                return;
            }
            delta = Mathf.Min(delta, .1f);
            // Weighted judgment impulses keep dense hold checks from creating more
            // propulsion than taps. Mistakes affect pace, never earned distance.
            momentum = Mathf.Clamp01(momentum + reaction * .24f);
            if (missed)
            {
                momentum *= .45f;
                stumble = .42f;
            }
            momentum = Mathf.MoveTowards(momentum, 0, delta * .065f);
            stumble = Mathf.Max(0, stumble - delta);
            float target = 3 + momentum * 20;
            if (stumble > 0)
            {
                target *= .65f;
            }
            speed = Mathf.MoveTowards(speed, target, delta * 38);
            distance += speed * delta;
            float targetOpening = 0;
            if (progress >= .85f)
            {
                targetOpening = 1;
            }
            opening = Mathf.MoveTowards(opening, targetOpening, delta);
            StepEnvironment(delta, progress, momentum);
            ApplyPose(progress);
        }

        private void ApplyPose(float progress)
        {
            foreach (var item in roadside)
            {
                float z = Mathf.Repeat(item.position.z - distance, 96) - 5;
                float x = item.position.x * (1 + opening * .42f);
                item.item.localPosition = new Vector3(x, item.position.y, z);
            }
            foreach (var item in road)
            {
                float z = Mathf.Repeat(item.position.z - distance, 96) - 8;
                item.item.localPosition = new Vector3(0, 0, z);
            }
            gateway.localPosition = new Vector3(0, 5, Mathf.Lerp(110, 55, progress));
            // A small lateral misstep belongs only to the scenery camera. No roll,
            // global shake, FOV pumping or note-camera writes.
            float slip = Mathf.Sin(stumble * 26) * stumble * .024f;
            sceneryCamera.transform.localPosition = new Vector3(slip, 1.8f, 0);
            sceneryCamera.transform.localRotation = Quaternion.identity;
        }
    }
}
