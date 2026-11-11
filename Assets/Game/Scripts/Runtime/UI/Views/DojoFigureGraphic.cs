using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Deform the existing artwork continuously: no detached sprite seams, cloned materials,
    // or replacement objects. The lower quarter remains planted in every standing pose.
    public sealed class DojoFigureGraphic : Image
    {
        public enum Figure
        {
            Trainee,
            Master,
            Sage,
            Scroll,
            Senior
        }

        public Figure figure;
        public bool spriteAnimation;
        private float breath;
        private float weight;
        private float attention;
        private float wind;
        private float gesture;
        private DojoIdleVariation.Motion motion;
        private float variation;
        private float progress;
        private int context;

        public void SetVariation(DojoIdleVariation.Motion motion, float strength, float progress, int context)
        {
            this.motion = motion;
            variation = strength;
            this.progress = progress;
            this.context = context;
            SetVerticesDirty();
        }

        public void SetPose(float breathing, float weightShift, float focus, float breeze, float instruction)
        {
            breath = breathing;
            weight = weightShift;
            attention = focus;
            wind = breeze;
            gesture = instruction;
            SetVerticesDirty();
        }

        public void Rest()
        {
            SetVariation(DojoIdleVariation.Motion.None, 0, 0, 0);
            SetPose(0, 0, 0, 0, 0);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (!sprite)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            const int columns = 16;
            const int rows = 24;
            for (int y = 0; y <= rows; y++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    var point = new Vector2(x / (float)columns, y / (float)rows);
                    var offset = Offset(point);
                    var position = new Vector3(rect.xMin + (point.x + offset.x) * rect.width,
                        rect.yMin + (point.y + offset.y) * rect.height);
                    vh.AddVert(position, color,
                        new Vector2(Mathf.Lerp(uv.x, uv.z, point.x), Mathf.Lerp(uv.y, uv.w, point.y)));
                    if (x == columns || y == rows)
                    {
                        continue;
                    }

                    int start = y * (columns + 1) + x;
                    vh.AddTriangle(start, start + columns + 1, start + columns + 2);
                    vh.AddTriangle(start, start + columns + 2, start + 1);
                }
            }
        }

        private Vector2 Offset(Vector2 point)
        {
            if (spriteAnimation)
            {
                return Vector2.zero;
            }

            float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, .64f, point.y));
            float chest = Region(point, new Vector2(.53f, .61f), new Vector2(.34f, .23f));
            float head = Region(point, new Vector2(.54f, .83f), new Vector2(.27f, .20f));
            var offset = Vector2.zero;
            switch (figure)
            {
                case Figure.Trainee:
                    offset.x = weight * upper * .005f + attention * head * .003f;
                    offset.x += (point.x - .53f) * chest * breath * .018f;
                    offset.y = chest * breath * .003f;
                    // Guard hand loosens on exhalation; headband trails the same breath/wind.
                    offset.y += Region(point, new Vector2(.72f, .63f), new Vector2(.16f, .16f)) * breath * .002f;
                    offset.x += Region(point, new Vector2(.31f, .80f), new Vector2(.23f, .12f)) * wind * .009f;
                    offset.x += Region(point, new Vector2(.45f, .41f), new Vector2(.12f, .19f)) * wind * .005f;
                    break;
                case Figure.Master:
                case Figure.Senior:
                    // The pointing sleeve and rod move together; the torso and feet stay steady.
                    float arm = Region(point, new Vector2(.22f, .60f), new Vector2(.38f, .22f));
                    offset.y = arm * gesture * .028f + chest * breath * .0015f;
                    offset.x = head * attention * .002f;
                    offset.x += Region(point, new Vector2(.31f, .76f), new Vector2(.12f, .16f)) * wind * .003f;
                    break;
                case Figure.Sage:
                    offset.x = head * attention * .003f;
                    offset.y = head * gesture * -.004f + chest * breath * .001f;
                    offset.x += Region(point, new Vector2(.43f, .70f), new Vector2(.15f, .18f)) * wind * .008f;
                    offset.x += Region(point, new Vector2(.56f, .29f), new Vector2(.38f, .22f)) * wind * .012f;
                    offset.x += Region(point, new Vector2(.70f, .77f), new Vector2(.12f, .20f)) * wind * .008f;
                    break;
                case Figure.Scroll:
                    // Only the paper between the wooden rollers catches a little air.
                    float paper = Region(point, new Vector2(.5f, .60f), new Vector2(.29f, .23f));
                    offset.x = paper * wind * .003f;
                    offset.y = paper * wind * .001f;
                    break;
            }

            offset += VariationOffset(point, upper, chest, head) * variation;

            // Keep soles, scroll supports and the lowest hem vertices at their authored positions.
            offset *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.22f, .34f, point.y));
            return offset;
        }

        private Vector2 VariationOffset(Vector2 point, float upper, float chest, float head)
        {
            float hands = Region(point, new Vector2(.64f, .62f), new Vector2(.27f, .18f));
            float sleeve = Region(point, new Vector2(.36f, .57f), new Vector2(.26f, .22f));
            float hem = Region(point, new Vector2(.55f, .32f), new Vector2(.40f, .22f));
            float beat = Mathf.Sin(progress * Mathf.PI * 4);
            switch (motion)
            {
                case DojoIdleVariation.Motion.Settle:
                    return new Vector2(-upper * .016f, -chest * .012f + hands * .006f);
                case DojoIdleVariation.Motion.Hands:
                    return new Vector2(hands * beat * .010f, hands * -.018f + chest * -.003f);
                case DojoIdleVariation.Motion.Look:
                    float direction = -1;
                    if (context == 2)
                    {
                        direction = 1;
                    }
                    return new Vector2(head * .018f * direction, head * .006f);
                case DojoIdleVariation.Motion.Nod:
                case DojoIdleVariation.Motion.Approve:
                    return new Vector2(head * .005f, -head * .018f);
                case DojoIdleVariation.Motion.Demonstrate:
                    if (figure == Figure.Senior)
                    {
                        return new Vector2(-upper * .012f - sleeve * .012f, sleeve * .006f);
                    }
                    return new Vector2(upper * .022f + hands * .028f, hands * .010f);
                case DojoIdleVariation.Motion.Focus:
                    return new Vector2(-upper * .012f, -chest * .014f + hands * .012f);
                case DojoIdleVariation.Motion.Explain:
                case DojoIdleVariation.Motion.Point:
                    return new Vector2(-sleeve * .010f, sleeve * .015f);
                case DojoIdleVariation.Motion.Observe:
                    return new Vector2(upper * .006f, chest * .003f);
                case DojoIdleVariation.Motion.Sleeve:
                    return new Vector2(sleeve * .014f, sleeve * .018f + head * -.003f);
                case DojoIdleVariation.Motion.Wind:
                    return new Vector2(hem * .022f + sleeve * .009f, hem * .004f);
                default:
                    return Vector2.zero;
            }
        }

        private static float Region(Vector2 point, Vector2 center, Vector2 radius)
        {
            float x = Mathf.Clamp01(1 - Mathf.Abs(point.x - center.x) / radius.x);
            float y = Mathf.Clamp01(1 - Mathf.Abs(point.y - center.y) / radius.y);
            return Mathf.SmoothStep(0, 1, x) * Mathf.SmoothStep(0, 1, y);
        }
    }
}
