using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Fixed, authored paper silhouette: uneven cut edges, warm fold and thin ink rule.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DialoguePaperGraphic : MaskableGraphic
    {
        public Texture paperTexture;

        public override Texture mainTexture
        {
            get
            {
                if (paperTexture)
                {
                    return paperTexture;
                }
                return base.mainTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            const int sections = 32;
            for (int i = 0; i <= sections; i++)
            {
                float t = i / (float)sections;
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                float grain = Mathf.Sin(i * 9.7f) * 2 + Mathf.Sin(i * 3.1f) * 1.5f;
                float taper = Mathf.Abs(t * 2 - 1);
                float inset = taper * taper * 16;
                var tint = color * Mathf.Lerp(.90f, 1, Mathf.Sin(t * Mathf.PI));
                tint.a = color.a;
                // Sample only the existing panel's paper interior, excluding its wooden frame.
                float u = Mathf.Lerp(.15f, .85f, t);
                vh.AddVert(new Vector2(x, rect.yMin + inset + grain), tint, new Vector2(u, .23f));
                vh.AddVert(new Vector2(x, rect.yMax - inset + grain), tint, new Vector2(u, .77f));
                if (i == 0)
                {
                    continue;
                }
                int index = i * 2;
                vh.AddTriangle(index - 2, index - 1, index);
                vh.AddTriangle(index - 1, index + 1, index);
            }
        }
    }
}
