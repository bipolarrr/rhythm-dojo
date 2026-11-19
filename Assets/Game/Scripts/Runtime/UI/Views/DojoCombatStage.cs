using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DojoCombatStage : DojoTrainingStage
    {
        public bool sparring;
        public SpriteRenderer partner;
        public SpriteRenderer partnerShadow;
        public Sprite[] partnerPoses;
        public SpriteRenderer wood;
        public SpriteRenderer echo;
        public SpriteRenderer contact;
        public SpriteRenderer debris;
        public SpriteRenderer pennant;
        public Sprite[] flags;
        public SpriteRenderer[] leaves;

        // Contact times within a phrase. The long final rest lets both fighters recover their guard.
        private static readonly float[] Bout = { .60f, 1.04f, 1.78f, 2.62f, 3.25f };
        private static readonly float[] Flurry = { .52f, .77f, 1.08f, 1.63f, 1.86f, 2.31f, 2.70f };
        private static readonly Drawing[] Strikes =
        {
            Drawing.Jab, Drawing.Cross, Drawing.Heel, Drawing.Cross, Drawing.Jab, Drawing.Cross, Drawing.Heel
        };

        public override void Validate()
        {
            base.Validate();
            if (!echo || !contact || !debris)
            {
                throw new InvalidOperationException("The combat effect drawings are missing.");
            }
            if (!pennant)
            {
                throw new InvalidOperationException("Training court pennant missing.");
            }
            CheckSprites(flags, 6);
            CheckRenderers(leaves, 7);
            if (sparring)
            {
                if (!partner || !partnerShadow)
                {
                    throw new InvalidOperationException("The sparring partner is missing.");
                }
                CheckSprites(partnerPoses, 10);
            }
            else if (!wood)
            {
                throw new InvalidOperationException("The striking post is missing.");
            }
        }

        public override void Step(DojoRoomPhaseView room, float age, float beat, float hit, float miss)
        {
            Wind(room, age);
            var times = Flurry;
            float cycle = 3.85f;
            if (sparring)
            {
                times = Bout;
                cycle = 4.65f;
            }

            float phrase = Mathf.Repeat(age * .86f + beat * .075f, cycle);
            int index = -1;
            for (int i = 0; i < times.Length; i++)
            {
                if (times[i] > phrase)
                {
                    break;
                }
                index = i;
            }
            float elapsed = 10;
            if (index >= 0)
            {
                elapsed = phrase - times[index];
            }
            int next = (index + 1) % times.Length;
            float until = Mathf.Repeat(times[next] - phrase, cycle);
            bool counter = sparring && index == 3;
            bool live = hit < .275f;
            if (live)
            {
                index = room.Strikes % Strikes.Length;
                elapsed = hit;
                counter = false;
            }
            Drawing strike = Strikes[Mathf.Max(0, index)];
            var pose = AttackPose(elapsed, until, strike, Strikes[next], phrase > times[times.Length - 1] + .45f, out float advance);

            float contactX = 9;
            float contactY = 45;
            if (strike == Drawing.Heel)
            {
                contactY = 58;
            }
            bool stumble = miss < .17f && !live;
            float recoil = 0;
            if (elapsed < .36f)
            {
                float sinceImpact = Mathf.Max(0, elapsed - .038f);
                recoil = Mathf.Exp(-sinceImpact * 10) * Mathf.Cos(sinceImpact * 26);
            }
            // Hold both fighters on the registered contact drawing for the first two or three frames.
            float footworkTime = age - Mathf.Min(elapsed, .038f);
            float step = Mathf.Sin(footworkTime * 2.1f) * 4;
            float heroX = -32 + step + advance;
            if (sparring)
            {
                float gap = 6 * Mathf.Sin(footworkTime * 1.23f);
                float partnerX = 34 + gap + 8 * recoil;
                int response = 0;
                if (counter && elapsed < .25f)
                {
                    response = 3;
                    partnerX -= 11;
                    pose = Drawing.Block;
                    heroX -= 9 * recoil;
                    contactX = -13;
                }
                else if (elapsed < .18f && !stumble)
                {
                    response = 5;
                }
                else if (until < .20f)
                {
                    response = 9;
                    if (next == 3)
                    {
                        response = 8;
                    }
                }
                else if (phrase > 3.86f)
                {
                    response = 6;
                }
                partner.sprite = partnerPoses[response];
                Place(partner.transform, partnerX, 4 + Mathf.Round(Mathf.Sin(age * 2)));
                Place(partnerShadow.transform, partnerX, 3);
                heroX += gap * .6f;
                contactX += gap * .7f;
            }
            else
            {
                wood.transform.localRotation = Quaternion.Euler(0, 0, -13 * recoil);
            }
            if (stumble)
            {
                pose = Drawing.Stagger;
                heroX -= 6;
            }
            if (room.Finishing && !sparring)
            {
                pose = Drawing.Salute;
                heroX = -35;
                if (room.Destroyed)
                {
                    float fall = Mathf.Clamp01(room.FinishAge / .65f);
                    wood.transform.localRotation = Quaternion.Euler(0, 0, -83 * fall * fall);
                    pose = Drawing.Heel;
                    if (fall > .7f)
                    {
                        pose = Drawing.Salute;
                    }
                }
                else
                {
                    wood.transform.localRotation = Quaternion.identity;
                }
            }
            trainee.sprite = room.poses[(int)pose];
            Place(trainee.transform, heroX, 0);
            Place(shadow.transform, heroX + 1, 0);
            echo.enabled = pose == strike && elapsed < .082f;
            echo.sprite = room.poses[(int)Drawing.Windup];
            Place(echo.transform, heroX - 11, 0);

            Contact(room, contactX, contactY, elapsed, !stumble && !room.Finishing, live);
        }

        private void Contact(DojoRoomPhaseView room, float contactX, float contactY, float elapsed, bool landed, bool live)
        {
            contact.enabled = landed && elapsed < .105f;
            debris.enabled = landed && elapsed < .23f;
            float emphasis = 1;
            if (live)
            {
                emphasis += room.Strength * .26f;
            }
            Place(contact.transform, contactX, contactY);
            contact.transform.localScale = Vector3.one * emphasis;
            room.Effect(contact, (int)EffectRow.Flash, (int)(Mathf.Min(elapsed, 1) * 50));
            var effect = EffectRow.Chips;
            if (sparring)
            {
                effect = EffectRow.Dust;
                contactY = 2;
            }
            Place(debris.transform, contactX + 2, contactY);
            room.Effect(debris, (int)effect, (int)(Mathf.Min(elapsed, 1) * 26));
        }

        private static Drawing AttackPose(float elapsed, float until, Drawing strike, Drawing next, bool rest,
            out float advance)
        {
            var pose = Drawing.Guard;
            advance = 0;
            if (elapsed < .085f)
            {
                pose = strike;
                advance = 10;
            }
            else if (elapsed < .22f)
            {
                pose = Drawing.Windup;
                advance = 7 * (1 - Mathf.InverseLerp(.085f, .22f, elapsed));
            }
            else if (until < .18f)
            {
                pose = Drawing.Windup;
                advance = -4;
                if (next == Drawing.Heel)
                {
                    pose = Drawing.ChamberKick;
                }
            }
            else if (rest)
            {
                pose = Drawing.Salute;
            }
            else if (until < .34f)
            {
                pose = Drawing.AdvancingPalm;
                advance = -2;
            }

            return pose;
        }

        private void Wind(DojoRoomPhaseView room, float age)
        {
            pennant.sprite = flags[Cel(age, 8, 6)];
            for (int i = 0; i < leaves.Length; i++)
            {
                float fall = Mathf.Repeat(age * (.13f + i * .009f) + i * .359f, 1);
                float x = Mathf.Repeat(i * 92 + fall * 69 + Mathf.Sin(age * 2 + i) * 9, 480);
                Place(leaves[i].transform, x, 238 - fall * 215);
                room.Effect(leaves[i], (int)EffectRow.MapleLeaf, Cel(age + i, 7 + i % 3, 6));
            }
        }
    }
}
