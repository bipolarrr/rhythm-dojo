using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    // All coordinates are authored in pixels on the 480 x 270 stage, measured from the floor up.
    public abstract class DojoTrainingStage : MonoBehaviour
    {
        // Sprite-sheet order is shared by the editor's sliced assets and all four routines.
        protected enum Drawing
        {
            LongStride, Gather, KneeDrive, SplitStride, Plant, Guard, Windup, Jab, Cross, Heel,
            Duck, Tuck, FlyingHeel, Land, Stagger, Lotus, Inhale, OpenHands, SplitWater, SeatedSalute,
            Salute, ChamberKick, AdvancingPalm, Block
        }

        protected enum EffectRow { Spray, Stream, Ripple, Flash, Chips, Dust, MapleLeaf }

        public SpriteRenderer trainee;
        public SpriteRenderer shadow;
        public Transform floor;

        public virtual void Validate()
        {
            if (!trainee || !shadow || !floor)
            {
                throw new InvalidOperationException("The action stage needs an actor, shadow and floor mark.");
            }
        }

        // Keep the actors proportional within the space beside the health gauge at narrower aspects.
        public void Frame(float width, float offset)
        {
            float stretch = width / 480;
            transform.localPosition = new Vector3(-width * .5f + offset, 0, 0);
            transform.localScale = new Vector3(stretch, 1, 1);
            float size = Mathf.Min(stretch, 1);
            floor.localScale = new Vector3(size / stretch, size, 1);
        }

        public abstract void Step(DojoRoomPhaseView room, float age, float beat, float hit, float miss);

        protected static void Place(Transform item, float x, float y)
        {
            item.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(y), item.localPosition.z);
        }

        protected static int Cel(float age, float rate, int count)
        {
            return Mathf.FloorToInt(Mathf.Repeat(age * rate, count));
        }

        protected static void CheckSprites(Sprite[] sprites, int count)
        {
            if (sprites == null || sprites.Length != count)
            {
                throw new InvalidOperationException("Action stage drawing count must be " + count);
            }
            foreach (var sprite in sprites)
            {
                if (!sprite)
                {
                    throw new InvalidOperationException("An action stage drawing is missing.");
                }
            }
        }

        protected static void CheckRenderers(SpriteRenderer[] items, int count)
        {
            if (items == null || items.Length != count)
            {
                throw new InvalidOperationException("Action stage layer count must be " + count);
            }
            foreach (var item in items)
            {
                if (!item || !item.sprite)
                {
                    throw new InvalidOperationException("An authored action stage layer is missing.");
                }
            }
        }
    }
}
