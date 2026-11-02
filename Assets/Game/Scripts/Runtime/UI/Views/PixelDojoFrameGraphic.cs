using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelDojoFrameGraphic : MaskableGraphic
    {
        public Color borderColor = new Color(.29f, .13f, .09f);
        public Color trimColor = new Color(.72f, .46f, .24f);
        public float borderWidth = 10;
        public Sprite artwork;

        public override Texture mainTexture
        {
            get
            {
                if (artwork)
                {
                    return artwork.texture;
                }

                return base.mainTexture;
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (artwork)
            {
                BuildArtwork(helper);
                return;
            }

            var rect = rectTransform.rect;
            Quad(helper, rect, new Color(.10f, .055f, .045f));
            Quad(helper, Inset(rect, 3), borderColor);
            Quad(helper, Inset(rect, borderWidth - 3), trimColor);
            Quad(helper, Inset(rect, borderWidth), color);
            var edge = new Color(.98f, .83f, .58f, .6f);
            Quad(helper, new Rect(rect.xMin + borderWidth, rect.yMax - borderWidth - 2, rect.width - borderWidth * 2, 2), edge);
            foreach (float x in new[] { rect.xMin + 8, rect.xMax - 20 })
            {
                foreach (float y in new[] { rect.yMin + 8, rect.yMax - 20 })
                {
                    Quad(helper, new Rect(x, y, 12, 12), borderColor);
                    Quad(helper, new Rect(x + 3, y + 3, 6, 6), trimColor);
                }
            }
        }

        private void BuildArtwork(VertexHelper helper)
        {
            var rect = GetPixelAdjustedRect();
            float edge = Mathf.Min(borderWidth, Mathf.Min(rect.width, rect.height) * .5f);
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork);
            var border = artwork.border;
            var x = new[] { rect.xMin, rect.xMin + edge, rect.xMax - edge, rect.xMax };
            var y = new[] { rect.yMin, rect.yMin + edge, rect.yMax - edge, rect.yMax };
            var u = new[] { uv.x, uv.x + border.x / artwork.texture.width,
                uv.z - border.z / artwork.texture.width, uv.z };
            var v = new[] { uv.y, uv.y + border.y / artwork.texture.height,
                uv.w - border.w / artwork.texture.height, uv.w };
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    var tint = Color.white;
                    if (row == 1 && column == 1)
                    {
                        tint = color;
                    }

                    int index = helper.currentVertCount;
                    helper.AddVert(new Vector2(x[column], y[row]), tint, new Vector2(u[column], v[row]));
                    helper.AddVert(new Vector2(x[column], y[row + 1]), tint, new Vector2(u[column], v[row + 1]));
                    helper.AddVert(new Vector2(x[column + 1], y[row + 1]), tint, new Vector2(u[column + 1], v[row + 1]));
                    helper.AddVert(new Vector2(x[column + 1], y[row]), tint, new Vector2(u[column + 1], v[row]));
                    helper.AddTriangle(index, index + 1, index + 2);
                    helper.AddTriangle(index, index + 2, index + 3);
                }
            }
        }

        private static Rect Inset(Rect rect, float amount)
        {
            return new Rect(rect.x + amount, rect.y + amount, Mathf.Max(0, rect.width - amount * 2), Mathf.Max(0, rect.height - amount * 2));
        }

        private static void Quad(VertexHelper helper, Rect rect, Color tint)
        {
            int index = helper.currentVertCount;
            helper.AddVert(new Vector2(rect.xMin, rect.yMin), tint, Vector2.zero);
            helper.AddVert(new Vector2(rect.xMin, rect.yMax), tint, Vector2.zero);
            helper.AddVert(new Vector2(rect.xMax, rect.yMax), tint, Vector2.zero);
            helper.AddVert(new Vector2(rect.xMax, rect.yMin), tint, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
