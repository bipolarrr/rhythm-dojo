using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Both halves sample the original artwork, so lettering and grain never change.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoBoardFragments : MaskableGraphic
    {
        public Sprite sprite;
        public float progress;

        public override Texture mainTexture
        {
            get
            {
                if (sprite)
                {
                    return sprite.texture;
                }

                return Texture2D.whiteTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!sprite)
            {
                return;
            }

            var rect = rectTransform.rect;
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            float split = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, 1f, progress));
            float gap = Mathf.Min(progress * 14f, 2f) + split * rect.width * .22f;
            var tint = color;
            tint.a *= 1f - split * .7f;

            for (int row = 0; row < 8; row++)
            {
                float bottom = row / 8f;
                float top = (row + 1) / 8f;
                float seamBottom = .5f + Mathf.Sin(row * 2.4f) * .025f;
                float seamTop = .5f + Mathf.Sin((row + 1) * 2.4f) * .025f;
                AddStrip(helper, rect, uv, 0f, seamBottom, 0f, seamTop,
                    bottom, top, -gap, split, tint);
                AddStrip(helper, rect, uv, seamBottom, 1f, seamTop, 1f,
                    bottom, top, gap, split, tint);
            }
        }

        private static void AddStrip(
            VertexHelper helper, Rect rect, Vector4 uv,
            float leftBottom, float rightBottom, float leftTop, float rightTop,
            float bottom, float top, float offset, float split, Color tint)
        {
            int index = helper.currentVertCount;
            AddVertex(helper, rect, uv, leftBottom, bottom, offset, split, tint);
            AddVertex(helper, rect, uv, leftTop, top, offset, split, tint);
            AddVertex(helper, rect, uv, rightTop, top, offset, split, tint);
            AddVertex(helper, rect, uv, rightBottom, bottom, offset, split, tint);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index + 2, index + 3, index);
        }

        private static void AddVertex(
            VertexHelper helper, Rect rect, Vector4 uv,
            float x, float y, float offset, float split, Color tint)
        {
            var position = new Vector2(
                rect.xMin + rect.width * x + offset,
                rect.yMin + rect.height * y - split * rect.height * .08f);
            var texture = new Vector2(Mathf.Lerp(uv.x, uv.z, x), Mathf.Lerp(uv.y, uv.w, y));
            helper.AddVert(position, tint, texture);
        }
    }
}
