using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class MountainPhaseView : TrainingJourneyView
    {
        public Transform horizon;
        public Vector3 horizonPosition;
        private float travel;
        private float travelVelocity;
        private float summit;
        private float impulse;
        private float phaseDuration = 20;

        public float Altitude => sceneryCamera.transform.localPosition.y;
        public bool SummitReached => summit > .95f;
        public float Travel => travel;

        public override void Validate()
        {
            base.Validate();
            if (!horizon)
            {
                throw new InvalidOperationException("Mountain horizon is missing.");
            }
        }

        public void ConfigureDuration(double duration)
        {
            phaseDuration = Mathf.Max(.01f, (float)duration);
        }

        public static float TrailHeight(float distance)
        {
            return Mathf.Min(distance, 53) * .24f;
        }

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            travel = travelVelocity = summit = impulse = 0;
            ApplyPose();
        }

        public void Step(float delta, float progress, float reaction, bool missed)
        {
            if (delta <= 0)
            {
                return;
            }
            delta = Mathf.Min(delta, .1f);
            impulse = Mathf.Max(impulse, reaction);
            if (missed)
            {
                impulse = 0;
            }
            impulse = Mathf.MoveTowards(impulse, 0, delta * 2);
            float target = Mathf.Clamp01(progress / .85f) * 61;
            float smoothing = Mathf.Min(.65f, phaseDuration * .08f);
            float speed = Mathf.Max(12, 61 / (phaseDuration * .65f));
            travel = Mathf.SmoothDamp(travel, target, ref travelVelocity, smoothing, speed, delta);
            float summitTarget = 0;
            if (progress >= .85f && travel > 56)
            {
                summitTarget = 1;
            }
            summit = Mathf.MoveTowards(summit, summitTarget, delta * Mathf.Max(.65f, 7 / phaseDuration));
            StepEnvironment(delta, progress, impulse);
            ApplyPose();
        }

        private void ApplyPose()
        {
            // The whole journey follows an authored rising trail. Near rock faces pass
            // at different depths; at the summit the gaze opens down into the valley.
            float x = Mathf.Sin(travel * .075f) * 1.1f;
            sceneryCamera.transform.localPosition = new Vector3(x, 1.8f + TrailHeight(travel), travel);
            sceneryCamera.transform.localRotation = Quaternion.Euler(summit * 4, -summit * 7, 0);
            // Treat the remote skyline as effectively infinite, while the trail,
            // cliffs, trees and mist retain their authored world depths. Forward
            // travel must not magnify away the valley below the summit.
            horizon.localPosition = horizonPosition + Vector3.forward * travel;
        }
    }
}
