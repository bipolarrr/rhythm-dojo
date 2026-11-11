using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // A continuous cloth mesh avoids gaps between independently rotated sprite strips.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoWindFlag : MaskableGraphic
    {
        public Sprite cloth;
        private float age;
        private float strength = 1f;

        public override Texture mainTexture
        {
            get
            {
                if (cloth)
                {
                    return cloth.texture;
                }
                return s_WhiteTexture;
            }
        }

        public void Animate(float time, float gust)
        {
            age = time;
            strength = gust;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!cloth)
            {
                return;
            }
            var rect = rectTransform.rect;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(cloth);
            const int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float wave = Mathf.Sin(age * 2.2f - t * 3.8f) * t * 2.4f * strength;
                wave -= t * t * 5f;
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                float u = Mathf.Lerp(uv.x, uv.z, t);
                vh.AddVert(new Vector3(x, rect.yMin + wave), color, new Vector2(u, uv.y));
                vh.AddVert(new Vector3(x, rect.yMax + wave), color, new Vector2(u, uv.w));
                if (i == segments)
                {
                    continue;
                }
                int start = i * 2;
                vh.AddTriangle(start, start + 1, start + 3);
                vh.AddTriangle(start, start + 3, start + 2);
            }
        }
    }
}
