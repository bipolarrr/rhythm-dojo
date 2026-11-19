using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class WaterfallStage : DojoTrainingStage
    {
        public SpriteRenderer curtain;
        public Sprite[] cascade;
        public SpriteRenderer[] streams;
        public SpriteRenderer[] spray;
        public SpriteRenderer[] ripples;

        public override void Validate()
        {
            base.Validate();
            if (!curtain)
            {
                throw new InvalidOperationException("The falling water is missing.");
            }
            CheckSprites(cascade, 12);
            CheckRenderers(streams, 6);
            CheckRenderers(spray, 5);
            CheckRenderers(ripples, 4);
        }

        public override void Step(DojoRoomPhaseView room, float age, float beat, float hit, float miss)
        {
            float cycle = Mathf.Repeat(age + beat * .045f, 5.4f);
            var pose = Drawing.Lotus;
            if (cycle > 1.15f && cycle < 2.8f)
            {
                pose = Drawing.Inhale;
            }
            else if (cycle >= 2.8f && cycle < 4.35f)
            {
                pose = Drawing.OpenHands;
            }
            else if (cycle >= 4.35f)
            {
                pose = Drawing.SeatedSalute;
            }
            bool partWater = cycle > 3.64f && cycle < 4.04f;
            if (hit < .19f || partWater)
            {
                pose = Drawing.SplitWater;
            }
            if (miss < .14f)
            {
                pose = Drawing.Inhale;
            }
            trainee.sprite = room.poses[(int)pose];
            float breath = Mathf.Round(Mathf.Sin(cycle * 1.16f) * 1.1f);
            Place(trainee.transform, 0, breath);
            curtain.sprite = cascade[Cel(age, 17, 12)];
            for (int i = 0; i < streams.Length; i++)
            {
                var stream = streams[i];
                float fall = Mathf.Repeat(age * (1.4f + .17f * i) + i * .371f, 1);
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                float split = 0;
                if (pose == Drawing.SplitWater)
                {
                    split = 7 * side * fall;
                }
                Place(stream.transform, (i - 2.5f) * 8 + split, 206 - fall * 157);
                room.Effect(stream, (int)EffectRow.Stream, Cel(age + i * .31f, 16 + i, 6));
            }
            for (int i = 0; i < spray.Length; i++)
            {
                float scale = .8f + (i % 3) * .19f;
                if (pose == Drawing.SplitWater)
                {
                    scale *= 1.32f;
                }
                spray[i].transform.localScale = Vector3.one * scale;
                room.Effect(spray[i], (int)EffectRow.Spray, Cel(age + i * .21f, 12 + i, 6));
            }
            for (int i = 0; i < ripples.Length; i++)
            {
                room.Effect(ripples[i], (int)EffectRow.Ripple, Cel(age + i * .19f, 5 + i, 6));
            }
        }
    }
}
