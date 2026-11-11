using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Authored vector accents: tapered dry strokes or a feathered pool of local contrast.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoInkGraphic : MaskableGraphic
    {
        public bool wash;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            if (wash)
            {
                const int segments = 48;
                vh.AddVert(rect.center, color, Vector2.zero);
                for (int ring = 1; ring <= 5; ring++)
                {
                    float radius = ring / 5f;
                    var tint = color;
                    tint.a *= 1f - Mathf.SmoothStep(0, 1, radius);
                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        var point = new Vector2(Mathf.Cos(angle) * rect.width, Mathf.Sin(angle) * rect.height);
                        vh.AddVert(rect.center + point * (.5f * radius), tint, Vector2.zero);
                        if (i == segments)
                        {
                            continue;
                        }
                        int current = 1 + (ring - 1) * (segments + 1) + i;
                        if (ring == 1)
                        {
                            vh.AddTriangle(0, current, current + 1);
                            continue;
                        }
                        int inner = current - segments - 1;
                        vh.AddTriangle(inner, current, current + 1);
                        vh.AddTriangle(inner, current + 1, inner + 1);
                    }
                }
                return;
            }

            const int steps = 24;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), .35f);
                float grain = .8f + .2f * Mathf.Sin(i * 7.1f);
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                float center = rect.center.y + rect.height * (t - .5f) * .25f;
                float width = rect.height * .44f * taper * grain;
                vh.AddVert(new Vector2(x, center - width), color, Vector2.zero);
                vh.AddVert(new Vector2(x, center + width), color, Vector2.zero);
                if (i > 0)
                {
                    int current = i * 2;
                    vh.AddTriangle(current - 2, current - 1, current);
                    vh.AddTriangle(current - 1, current + 1, current);
                }
            }
        }
    }
}
