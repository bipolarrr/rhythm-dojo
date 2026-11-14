using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GameplayWindowLight : MaskableGraphic
    {
        public bool dust;
        private float elapsed;
        private float nextFrame;

        private void Update()
        {
            if (!dust)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed < nextFrame)
            {
                return;
            }

            // Twelve pixel motes share one mesh; no particle objects or per-frame buffers.
            nextFrame = elapsed + .05f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            if (dust)
            {
                for (int i = 0; i < 12; i++)
                {
                    float phase = Mathf.Repeat(elapsed * .025f + i * .271f, 1);
                    float x = .055f + (i % 3) * .030f + Mathf.Sin(phase * 5 + i) * .008f;
                    if (i >= 6)
                    {
                        x = 1 - x;
                    }
                    float y = .24f + phase * .38f;
                    var tint = color;
                    tint.a *= Mathf.Sin(phase * Mathf.PI) * .45f;
                    var center = new Vector2(Mathf.Round(rect.xMin + x * rect.width),
                        Mathf.Round(rect.yMin + y * rect.height));
                    Quad(helper, center, center + Vector2.up * 2,
                        center + Vector2.one * 2, center + Vector2.right * 2, tint, tint);
                }
                return;
            }

            // Oblique shafts terminate outside the note corridor, following the open side doors.
            for (int side = 0; side < 2; side++)
            {
                for (int ray = 0; ray < 3; ray++)
                {
                    float start = .018f + ray * .025f;
                    float end = .095f + ray * .035f;
                    float width = .012f + ray * .003f;
                    var a = Point(rect, start, .70f, side);
                    var b = Point(rect, start + width, .70f, side);
                    var c = Point(rect, end + width * 2, .20f, side);
                    var d = Point(rect, end, .20f, side);
                    Beam(helper, a, b, c, d, ray);
                }
            }
        }

        private void Beam(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int seed)
        {
            const int length = 20;
            const int width = 6;
            for (int step = 0; step < length; step++)
            {
                float t0 = step / (float)length;
                float t1 = (step + 1f) / length;
                for (int strip = 0; strip < width; strip++)
                {
                    float u0 = strip / (float)width;
                    float u1 = (strip + 1f) / width;
                    int index = helper.currentVertCount;
                    helper.AddVert(Vector2.Lerp(Vector2.Lerp(a, d, t0), Vector2.Lerp(b, c, t0), u0),
                        BeamTint(t0, u0, seed), Vector2.zero);
                    helper.AddVert(Vector2.Lerp(Vector2.Lerp(a, d, t0), Vector2.Lerp(b, c, t0), u1),
                        BeamTint(t0, u1, seed), Vector2.zero);
                    helper.AddVert(Vector2.Lerp(Vector2.Lerp(a, d, t1), Vector2.Lerp(b, c, t1), u1),
                        BeamTint(t1, u1, seed), Vector2.zero);
                    helper.AddVert(Vector2.Lerp(Vector2.Lerp(a, d, t1), Vector2.Lerp(b, c, t1), u0),
                        BeamTint(t1, u0, seed), Vector2.zero);
                    helper.AddTriangle(index, index + 1, index + 2);
                    helper.AddTriangle(index, index + 2, index + 3);
                }
            }
        }

        private Color BeamTint(float along, float across, int seed)
        {
            var tint = color;
            float grain = .65f + .35f * Mathf.PerlinNoise(across * 7 + seed, along * 15);
            tint.a *= Mathf.Sin(along * Mathf.PI) * Mathf.Sin(across * Mathf.PI) * grain * .10f;
            return tint;
        }

        private static Vector2 Point(Rect rect, float x, float y, int side)
        {
            if (side == 1)
            {
                x = 1 - x;
            }
            return new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
        }

        private static void Quad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color top, Color bottom)
        {
            int index = helper.currentVertCount;
            helper.AddVert(a, top, Vector2.zero);
            helper.AddVert(b, top, Vector2.zero);
            helper.AddVert(c, bottom, Vector2.zero);
            helper.AddVert(d, bottom, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
