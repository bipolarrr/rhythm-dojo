using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Reveal the existing glyph ink in narrow bristle tracks. No replacement font/material.
    public sealed class ResultsBrushReveal : BaseMeshEffect
    {
        private float progress = 1;
        private readonly UIVertex[] quad = new UIVertex[4];
        private readonly System.Collections.Generic.List<UIVertex> vertices =
            new System.Collections.Generic.List<UIVertex>();

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value))
            {
                return;
            }
            progress = value;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || progress >= 1)
            {
                return;
            }
            if (progress <= 0)
            {
                helper.Clear();
                return;
            }

            vertices.Clear();
            float left = float.MaxValue;
            float right = float.MinValue;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref quad[0], i);
                vertices.Add(quad[0]);
                left = Mathf.Min(left, quad[0].position.x);
                right = Mathf.Max(right, quad[0].position.x);
            }
            helper.Clear();
            float edge = Mathf.Lerp(left - 18, right + 18, progress);
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                // Text glyph quad order: top-left, top-right, bottom-right, bottom-left.
                float width = vertices[i + 1].position.x - vertices[i].position.x;
                if (width <= 0)
                {
                    continue;
                }
                for (int strip = 0; strip < 10; strip++)
                {
                    float top = strip / 10f;
                    float bottom = (strip + 1) / 10f;
                    var a = Lerp(vertices[i], vertices[i + 3], top);
                    var b = Lerp(vertices[i + 1], vertices[i + 2], top);
                    var c = Lerp(vertices[i + 1], vertices[i + 2], bottom);
                    var d = Lerp(vertices[i], vertices[i + 3], bottom);
                    float bristle = Mathf.Sin(strip * 2.4f) * 5 + top * 8;
                    float amount = Mathf.Clamp01((edge + bristle - a.position.x) / width);
                    if (amount <= 0)
                    {
                        continue;
                    }
                    quad[0] = a;
                    quad[1] = Lerp(a, b, amount);
                    quad[2] = Lerp(d, c, amount);
                    quad[3] = d;
                    helper.AddUIVertexQuad(quad);
                }
            }
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            var vertex = a;
            vertex.position = Vector3.LerpUnclamped(a.position, b.position, t);
            vertex.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
            vertex.color = Color32.Lerp(a.color, b.color, t);
            return vertex;
        }
    }
}
