using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class RidgeRunStage : DojoTrainingStage
    {
        public SpriteRenderer[] horizon;
        public SpriteRenderer[] forest;
        public SpriteRenderer[] trail;
        public SpriteRenderer[] verge;
        public SpriteRenderer log;
        public SpriteRenderer branch;
        public SpriteRenderer echo;
        public SpriteRenderer debris;

        public override void Validate()
        {
            base.Validate();
            CheckRenderers(horizon, 3);
            CheckRenderers(forest, 7);
            CheckRenderers(trail, 3);
            CheckRenderers(verge, 6);
            if (!log || !branch)
            {
                throw new InvalidOperationException("The trail obstacles are missing.");
            }
            if (!echo || !debris)
            {
                throw new InvalidOperationException("The runner effects are missing.");
            }
        }

        public override void Step(DojoRoomPhaseView room, float age, float beat, float hit, float miss)
        {
            float distance = age * 145 + 16 * Mathf.Sin(age * 1.8f);
            float burst = 0;
            if (hit < .36f)
            {
                burst = (1 - hit / .36f) * room.Strength;
            }
            Scroll(horizon, distance * .078f, 480, 0);
            Scroll(forest, distance * .36f, 122, 29);
            Scroll(trail, distance, 384, -17);
            for (int i = 0; i < verge.Length; i++)
            {
                float x = Mathf.Repeat(89 + i * 120 - distance * 1.68f, 720) - 110;
                Place(verge[i].transform, x, verge[i].transform.localPosition.y);
            }

            float logX = Mathf.Repeat(214 - distance + 280, 818) - 280;
            float branchX = Mathf.Repeat(439 - distance + 280, 818) - 280;
            Place(log.transform, logX, -1);
            Place(branch.transform, branchX, 3);
            float heroX = -8 + 6 * Mathf.Sin(age * 1.8f) + burst * 6;
            float approach = logX - heroX;
            float jump = Mathf.Clamp01((59 - approach) / 119);
            float lift = 4 * jump * (1 - jump) * 48;
            var pose = (Drawing)Cel(distance, .093f, 5);
            if (jump > 0 && jump < 1)
            {
                pose = Drawing.Tuck;
                if (jump > .48f)
                {
                    pose = Drawing.FlyingHeel;
                }
            }
            else if (approach < 73 && approach >= 59)
            {
                pose = Drawing.Duck;
            }
            else if (approach <= -60 && approach > -76)
            {
                pose = Drawing.Land;
            }
            else if (Mathf.Abs(branchX - heroX) < 40)
            {
                pose = Drawing.Duck;
            }
            else if (miss < .15f)
            {
                pose = Drawing.Stagger;
                heroX -= 5;
            }

            trainee.sprite = room.poses[(int)pose];
            Place(trainee.transform, heroX, lift);
            Place(shadow.transform, heroX, 0);
            shadow.transform.localScale = Vector3.one * Mathf.Lerp(.94f, .53f, lift / 48);
            echo.enabled = burst > .42f || lift > 36;
            echo.sprite = trainee.sprite;
            Place(echo.transform, heroX - 10, lift - 1);
            debris.enabled = lift < 4;
            Place(debris.transform, heroX - 21, 1);
            room.Effect(debris, (int)EffectRow.Dust, Cel(distance, .075f, 6));
        }

        private static void Scroll(SpriteRenderer[] layers, float distance, float spacing, float y)
        {
            float period = layers.Length * spacing;
            for (int i = 0; i < layers.Length; i++)
            {
                float x = Mathf.Repeat(i * spacing - distance + spacing, period) - spacing;
                Place(layers[i].transform, x, y);
            }
        }
    }
}
