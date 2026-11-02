using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Split the original sprite in the mesh, keeping its texture and importer unchanged.
    public sealed class DojoSlidingImage : Image
    {
        [Min(0f)]
        public float splitDepth;
        private float separation;
        public float Separation => separation;

        public void SetSeparation(float value)
        {
            if (Mathf.Approximately(separation, value))
            {
                return;
            }
            separation = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!sprite)
            {
                return;
            }

            var rect = GetPixelAdjustedRect();
            float aspect = sprite.rect.width / sprite.rect.height;
            if (preserveAspect)
            {
                if (rect.width / rect.height > aspect)
                {
                    float width = rect.height * aspect;
                    rect.x += (rect.width - width) * .5f;
                    rect.width = width;
                }
                else
                {
                    float height = rect.width / aspect;
                    rect.y += (rect.height - height) * .5f;
                    rect.height = height;
                }
            }

            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            float middle = (uv.x + uv.z) * .5f;
            float depth = Mathf.Min(splitDepth, separation * .75f);
            if (depth > .01f)
            {
                AddCutFace(helper, rect, uv, middle, rect.center.x - separation, depth, false);
                AddCutFace(helper, rect, uv, middle, rect.center.x + separation, -depth, true);
            }
            AddHalf(helper, new Rect(rect.x - separation, rect.y, rect.width * .5f, rect.height),
                new Vector4(uv.x, uv.y, middle, uv.w));
            AddHalf(helper, new Rect(rect.center.x + separation, rect.y, rect.width * .5f, rect.height),
                new Vector4(middle, uv.y, uv.z, uv.w));
            if (depth > .01f)
            {
                float bevel = Mathf.Min(2f, depth * .25f);
                AddDepthQuad(helper,
                    new Vector2(rect.center.x - separation - bevel, rect.yMin),
                    new Vector2(rect.center.x - separation - bevel, rect.yMax),
                    new Vector2(rect.center.x - separation, rect.yMax),
                    new Vector2(rect.center.x - separation, rect.yMin),
                    new Vector4(middle - .003f, uv.y, middle, uv.w), new Color(1f, .85f, .60f, .85f));
                AddDepthQuad(helper,
                    new Vector2(rect.center.x + separation, rect.yMin),
                    new Vector2(rect.center.x + separation, rect.yMax),
                    new Vector2(rect.center.x + separation + bevel, rect.yMax),
                    new Vector2(rect.center.x + separation + bevel, rect.yMin),
                    new Vector4(middle, uv.y, middle + .003f, uv.w), new Color(1f, .92f, .72f, .9f));
            }
        }

        private void AddCutFace(VertexHelper helper, Rect rect, Vector4 uv, float middle, float edge, float depth, bool darker)
        {
            var tint = new Color(.72f, .49f, .27f);
            if (darker)
            {
                tint = new Color(.46f, .28f, .15f);
            }
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float start = i / (float)segments;
                float end = (i + 1f) / segments;
                float y0 = Mathf.Lerp(rect.yMin, rect.yMax, start);
                float y1 = Mathf.Lerp(rect.yMin, rect.yMax, end);
                float shade = .91f + .09f * Mathf.Sin(i * 2.4f);
                var grain = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
                AddDepthQuad(helper, new Vector2(edge, y0), new Vector2(edge, y1),
                    new Vector2(edge + depth, y1 - Mathf.Abs(depth) * .35f),
                    new Vector2(edge + depth, y0 - Mathf.Abs(depth) * .35f),
                    new Vector4(middle - .004f, Mathf.Lerp(uv.y, uv.w, start),
                        middle + .004f, Mathf.Lerp(uv.y, uv.w, end)), grain);
            }
        }

        private void AddDepthQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 uv, Color tint)
        {
            int index = helper.currentVertCount;
            tint *= color;
            helper.AddVert(a, tint, new Vector2(uv.x, uv.y));
            helper.AddVert(b, tint, new Vector2(uv.x, uv.w));
            helper.AddVert(c, tint, new Vector2(uv.z, uv.w));
            helper.AddVert(d, tint, new Vector2(uv.z, uv.y));
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }

        private void AddHalf(VertexHelper helper, Rect rect, Vector4 uv)
        {
            int index = helper.currentVertCount;
            helper.AddVert(new Vector2(rect.xMin, rect.yMin), color, new Vector2(uv.x, uv.y));
            helper.AddVert(new Vector2(rect.xMin, rect.yMax), color, new Vector2(uv.x, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMax), color, new Vector2(uv.z, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMin), color, new Vector2(uv.z, uv.y));
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
