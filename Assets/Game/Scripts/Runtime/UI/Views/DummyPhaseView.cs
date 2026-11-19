using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DummyPhaseView : TrainingJourneyView
    {
        public Transform axis;
        public JourneyScenery[] pieces;
        public Transform[] joints;
        public Transform[] hands;
        public Vector3[] handRest;
        public Vector3[] handContact;
        public Transform[] chips;

        private readonly float[] strikeAge = { 1, 1 };
        private readonly float[] strikeStrength = new float[2];
        private readonly float[] pressure = new float[2];
        private readonly float[] painAge = { 1, 1 };
        private float finishAge = -1;
        private float damage;

        public bool Finishing => finishAge >= 0;
        public bool Destroyed { get; private set; }
        public bool FinishComplete => finishAge >= 1.65f;
        public float Damage => damage;
        public float FinishAge => finishAge;

        public override void Validate()
        {
            base.Validate();
            if (!axis || pieces == null || pieces.Length < 8)
            {
                throw new InvalidOperationException("Articulated dummy pieces are missing.");
            }
            if (joints == null || joints.Length != 2)
            {
                throw new InvalidOperationException("Dummy arm joints are missing.");
            }
            if (hands == null || hands.Length != 2)
            {
                throw new InvalidOperationException("First-person hands are missing.");
            }
            if (handRest == null || handContact == null || chips == null)
            {
                throw new InvalidOperationException("Dummy hand poses or chip pool are missing.");
            }
            if (handRest.Length != 2 || handContact.Length != 2 || chips.Length != 8)
            {
                throw new InvalidOperationException("Dummy poses or chip count are invalid.");
            }
            foreach (var item in pieces)
            {
                if (item == null || !item.item)
                {
                    throw new InvalidOperationException("Dummy fragment reference is missing.");
                }
            }
            foreach (var items in new[] { joints, hands, chips })
            {
                foreach (var item in items)
                {
                    if (!item)
                    {
                        throw new InvalidOperationException("Dummy articulation reference is missing.");
                    }
                }
            }
        }

        public override void ResetPresentation()
        {
            base.ResetPresentation();
            finishAge = -1;
            Destroyed = false;
            damage = 0;
            sceneryCamera.transform.localRotation = Quaternion.identity;
            axis.localRotation = Quaternion.identity;
            for (int i = 0; i < 2; i++)
            {
                strikeAge[i] = painAge[i] = 1;
                strikeStrength[i] = pressure[i] = 0;
                hands[i].localPosition = handRest[i];
                hands[i].localRotation = Quaternion.identity;
                joints[i].localRotation = Quaternion.identity;
            }
            foreach (var item in pieces)
            {
                item.item.localPosition = item.position;
                item.item.localRotation = Quaternion.Euler(item.rotation);
            }
            foreach (var item in chips)
            {
                item.gameObject.SetActive(false);
            }
        }

        public void React(int lane, int part, float strength, bool missed)
        {
            if (Finishing)
            {
                return;
            }
            int side = lane % 2;
            if (missed)
            {
                // Coalesce repeated failed hold checks without hiding the fact in the
                // performance model. The gesture does not block subsequent input.
                if (painAge[side] >= .15f)
                {
                    painAge[side] = 0;
                }
                strikeAge[side] = 1;
                pressure[side] = 0;
                return;
            }
            painAge[side] = 1;
            if (part == 1)
            {
                pressure[side] = Mathf.Min(1, pressure[side] + strength * 2);
                return;
            }
            if (strikeAge[side] >= .075f)
            {
                strikeAge[side] = 0;
            }
            strikeStrength[side] = Mathf.Max(strength, .55f);
        }

        public void BeginFinish(bool flawless)
        {
            if (Finishing)
            {
                return;
            }
            finishAge = 0;
            Destroyed = flawless;
        }

        public void Step(float delta, float progress)
        {
            if (delta <= 0)
            {
                return;
            }
            delta = Mathf.Min(delta, .1f);
            damage = Mathf.Max(damage, Mathf.Clamp01(progress));
            StepEnvironment(delta, damage, 0);
            if (Finishing)
            {
                finishAge += delta;
                ApplyFinish();
                return;
            }
            float impact = 0;
            for (int i = 0; i < 2; i++)
            {
                strikeAge[i] += delta;
                painAge[i] += delta;
                pressure[i] = Mathf.MoveTowards(pressure[i], 0, delta * 1.5f);
                float contact = StrikePose(strikeAge[i]);
                float vibration = Impact(strikeAge[i]) * strikeStrength[i];
                vibration += Mathf.Sin(VisualTime * 55) * pressure[i] * .15f;
                impact += vibration;
                joints[i].localRotation = Quaternion.Euler(vibration * 5, vibration * 9, vibration * 3);
                var position = Vector3.Lerp(handRest[i], handContact[i], contact);
                position.y += Mathf.Sin(VisualTime * 1.9f + i) * .018f;
                position.z += pressure[i] * .13f;
                float pain = Mathf.Clamp01(1 - painAge[i] / .32f);
                position.x += Mathf.Sin(painAge[i] * 65) * pain * .075f;
                position.y -= pain * .14f;
                hands[i].localPosition = position;
                hands[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(painAge[i] * 65) * pain * 13);
            }
            axis.localRotation = Quaternion.Euler(impact * 1.8f, impact * 2.8f, impact * .8f);
            ApplyChips(Mathf.Min(strikeAge[0], strikeAge[1]), false);
        }

        private static float StrikePose(float age)
        {
            if (age < .055f)
            {
                return Mathf.SmoothStep(0, 1, age / .055f);
            }
            return 1 - Mathf.SmoothStep(0, 1, (age - .055f) / .23f);
        }

        private static float Impact(float age)
        {
            if (age < .055f || age > .42f)
            {
                return 0;
            }
            float t = age - .055f;
            return Mathf.Cos(t * 55) * Mathf.Exp(-t * 12);
        }

        private void ApplyFinish()
        {
            axis.localRotation = Quaternion.identity;
            if (!Destroyed)
            {
                // Relax the guard and bow; the damaged dummy remains standing.
                float settle = Mathf.SmoothStep(0, 1, finishAge / .7f);
                for (int i = 0; i < 2; i++)
                {
                    hands[i].localPosition = handRest[i] + Vector3.down * (settle * .7f);
                    hands[i].localRotation = Quaternion.identity;
                    joints[i].localRotation = Quaternion.identity;
                }
                sceneryCamera.transform.localRotation = Quaternion.Euler(settle * 2, 0, 0);
                ApplyChips(1, false);
                return;
            }

            float strike = StrikePose(Mathf.Max(0, finishAge - .07f));
            for (int i = 0; i < 2; i++)
            {
                hands[i].localPosition = Vector3.Lerp(handRest[i], handContact[i], strike);
                hands[i].localPosition += Vector3.down * Mathf.SmoothStep(0, .6f, (finishAge - .5f) / .8f);
                hands[i].localRotation = Quaternion.identity;
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                var item = pieces[i];
                float t = Mathf.Max(0, finishAge - .125f - i % 3 * .018f);
                float direction = -1;
                if (i % 2 == 0)
                {
                    direction = 1;
                }
                // Deterministic gravity, low outward impulse, floor contact and one
                // damped bounce. These are the original authored wood pieces.
                float floor = .05f + i % 3 * .07f;
                float fall = Mathf.Sqrt(Mathf.Max(0, 2 * (item.position.y - floor) / 12));
                float flight = Mathf.Min(t, fall);
                float y = Mathf.Max(floor, item.position.y - 6 * flight * flight);
                if (t > fall)
                {
                    y += Mathf.Abs(Mathf.Sin((t - fall) * 14)) * .09f * Mathf.Exp(-(t - fall) * 9);
                }
                float spread = (1 - Mathf.Exp(-t * 3)) * (1 + i % 4 * .32f);
                item.item.localPosition = new Vector3(item.position.x + direction * spread,
                    y, item.position.z - flight * .5f);
                var rotation = new Vector3(flight * 28, flight * direction * 23,
                    direction * Mathf.Min(t * 110, 76));
                if (i >= pieces.Length - 2)
                {
                    rotation = new Vector3(0, 0, -direction * Mathf.Min(t * 40, 12));
                }
                item.item.localRotation = Quaternion.Euler(item.rotation + rotation);
            }
            ApplyChips(finishAge - .07f, true);
        }

        private void ApplyChips(float age, bool finish)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                bool visible = age >= .055f && age < .45f;
                if (!finish && damage < .45f)
                {
                    visible = false;
                }
                chips[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                float t = age - .055f;
                chips[i].localPosition = new Vector3(side * (2 + t * (1 + i * .12f)),
                    2.4f + t * .4f - t * t * 7, 5 - t);
                chips[i].localRotation = Quaternion.Euler(0, 0, i * 51 + t * 180);
            }
        }
    }
}
