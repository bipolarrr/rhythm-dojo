using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Mesh icons keep the buttons independent of platform font glyphs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuIcon : MaskableGraphic
    {
        public enum IconKind { Settings, Power }
        public IconKind kind;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            var center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .31f;
            if (kind == IconKind.Power)
            {
                for (int i = 0; i < 48; i++)
                {
                    float a = (120f + 300f * i / 48f) * Mathf.Deg2Rad;
                    float b = (120f + 300f * (i + 1) / 48f) * Mathf.Deg2Rad;
                    Line(helper, center + Direction(a) * radius, center + Direction(b) * radius, radius * .14f);
                }
                Line(helper, center + Vector2.up * radius * 1.25f, center + Vector2.up * radius * .2f, radius * .16f);
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    float a = i * Mathf.PI * 2 / 64;
                    float b = (i + 1) * Mathf.PI * 2 / 64;
                    float outer = radius * ((i % 8 < 4) ? 1f : .8f);
                    Quad(helper, center + Direction(a) * outer, center + Direction(b) * outer,
                        center + Direction(b) * radius * .5f, center + Direction(a) * radius * .5f);
                }
            }
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        private void Line(VertexHelper helper, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * .5f;
            Quad(helper, a - normal, a + normal, b + normal, b - normal);
        }

        private void Quad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = helper.currentVertCount;
            helper.AddVert(a, color, Vector2.zero); helper.AddVert(b, color, Vector2.zero);
            helper.AddVert(c, color, Vector2.zero); helper.AddVert(d, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}