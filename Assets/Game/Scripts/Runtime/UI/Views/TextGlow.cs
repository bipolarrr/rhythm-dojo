using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class TextGlow : BaseMeshEffect
    {
        [SerializeField] private Color glowColor = new Color(1f, .22f, .35f, .65f);
        [SerializeField, Range(0f, 16f)] private float radius = 8f;
        private readonly List<UIVertex> original = new List<UIVertex>();
        private readonly List<UIVertex> expanded = new List<UIVertex>();

        public void Configure(Color color, float glowRadius)
        {
            glowColor = color;
            radius = Mathf.Clamp(glowRadius, 0f, 16f);
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || radius <= 0f || glowColor.a <= 0f || helper.currentVertCount == 0) return;
            original.Clear();
            helper.GetUIVertexStream(original);
            expanded.Clear();

            const int directions = 16;
            // Stay below the UI mesh vertex limit if this effect is reused on long text.
            int rings = Mathf.Min(4, (64000 / original.Count - 1) / directions);
            if (rings <= 0) return;
            for (int ring = rings; ring >= 1; ring--)
            {
                float distance = ring / (float)rings;
                float opacity = Mathf.Exp(-distance * distance / .405f) * .12f;
                for (int sample = 0; sample < directions; sample++)
                {
                    float angle = (sample + ring * .5f) * Mathf.PI * 2f / directions;
                    var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * distance;
                    foreach (var source in original)
                    {
                        var vertex = source;
                        vertex.position += offset;
                        var color = glowColor;
                        color.a *= source.color.a / 255f * opacity;
                        vertex.color = color;
                        expanded.Add(vertex);
                    }
                }
            }

            // Draw the unchanged text last so the letters remain crisp over the halo.
            expanded.AddRange(original);
            helper.Clear();
            helper.AddUIVertexTriangleStream(expanded);
        }
    }
}
