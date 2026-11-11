using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Retains its serialized type for existing scenes; now draws a neutral dust mote.
    public sealed class DojoPetalGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            var half = rect.size * .5f;
            vh.AddVert(center, color, Vector2.zero);
            var edge = new Color(color.r, color.g, color.b, 0);
            for (int i = 0; i <= 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y),
                    edge, Vector2.zero);
                if (i > 0)
                {
                    vh.AddTriangle(0, i, i + 1);
                }
            }
        }
    }
}
