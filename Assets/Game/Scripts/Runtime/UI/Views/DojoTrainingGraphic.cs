using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoTrainingGraphic : MaskableGraphic
    {
        public bool interior;
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

            if (interior)
            {
                BuildInterior(helper);
                return;
            }

            BuildDummy(helper);
        }

        private void BuildArtwork(VertexHelper helper)
        {
            var rect = GetPixelAdjustedRect();
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork);
            helper.AddVert(new Vector2(rect.xMin, rect.yMin), color, new Vector2(uv.x, uv.y));
            helper.AddVert(new Vector2(rect.xMin, rect.yMax), color, new Vector2(uv.x, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMax), color, new Vector2(uv.z, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMin), color, new Vector2(uv.z, uv.y));
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(0, 2, 3);
        }

        private void BuildInterior(VertexHelper helper)
        {
            Box(helper, 0, 0, 1280, 720, new Color(.12f, .085f, .075f));
            Box(helper, 110, 220, 1060, 370, new Color(.24f, .19f, .13f));
            // A raised gallery and warm paper windows echo the existing timber doorway.
            for (int i = 0; i < 6; i++)
            {
                float x = 165 + i * 164;
                Box(helper, x - 7, 278, 121, 208, new Color(.10f, .065f, .055f));
                Box(helper, x, 286, 107, 194, new Color(.92f, .77f, .48f));
                Box(helper, x + 6, 294, 95, 176, new Color(1f, .89f, .65f));
                for (int line = 1; line < 5; line++)
                {
                    Box(helper, x, 286 + line * 36, 107, 4, new Color(.55f, .36f, .20f));
                }

                Box(helper, x + 33, 286, 4, 194, new Color(.55f, .36f, .20f));
                Box(helper, x + 70, 286, 4, 194, new Color(.55f, .36f, .20f));
                Box(helper, x - 5, 580, 117, 72, new Color(.39f, .23f, .13f));
                Box(helper, x + 4, 588, 99, 56, new Color(.69f, .48f, .24f));
            }

            Box(helper, 100, 520, 1080, 32, new Color(.10f, .055f, .045f));
            Box(helper, 100, 546, 1080, 8, new Color(.61f, .35f, .17f));
            for (int i = 0; i < 24; i++)
            {
                Box(helper, 120 + i * 45, 552, 8, 48, new Color(.44f, .20f, .11f));
            }

            Box(helper, 100, 596, 1080, 10, new Color(.61f, .35f, .17f));
            for (int i = 0; i < 4; i++)
            {
                float x = 125 + i * 334;
                Box(helper, x - 15, 226, 48, 442, new Color(.14f, .07f, .055f));
                Box(helper, x - 8, 230, 34, 432, new Color(.42f, .17f, .09f));
                Box(helper, x - 4, 235, 7, 422, new Color(.58f, .28f, .13f));
                Box(helper, x - 23, 218, 64, 18, new Color(.27f, .13f, .085f));
                Box(helper, x - 18, 236, 54, 7, new Color(.70f, .46f, .23f));
            }

            for (int i = 0; i < 8; i++)
            {
                float y = i * 30;
                float light = i * .014f;
                Box(helper, 0, y, 1280, 29, new Color(.37f + light, .22f + light, .13f + light));
                for (int seam = 0; seam < 7; seam++)
                {
                    Box(helper, seam * 205 + (i % 2) * 97, y, 2, 29, new Color(.23f, .13f, .09f));
                    Box(helper, seam * 205 + 19, y + 7, 75, 1, new Color(.57f, .36f, .20f));
                }
            }

            Box(helper, 0, 660, 1280, 60, new Color(.10f, .065f, .055f));
            Box(helper, 60, 661, 1160, 9, new Color(.43f, .24f, .13f));
            for (int i = 0; i < 9; i++)
            {
                Box(helper, 80 + i * 140, 673, 16, 47, new Color(.29f, .15f, .10f));
            }
        }

        private void BuildDummy(VertexHelper helper)
        {
            // Broad weighted bag base, cylindrical wooden trunk, arms and angled training leg.
            Ellipse(helper, 110, 27, 101, 19, new Color(0f, 0f, 0f, .28f));
            Ellipse(helper, 110, 46, 82, 30, new Color(.10f, .075f, .065f));
            Box(helper, 28, 43, 164, 21, new Color(.18f, .14f, .12f));
            Ellipse(helper, 110, 63, 78, 26, new Color(.27f, .21f, .16f));
            Ellipse(helper, 110, 66, 62, 19, new Color(.34f, .27f, .19f));
            Box(helper, 99, 64, 22, 46, new Color(.17f, .13f, .11f));
            Box(helper, 101, 85, 18, 16, new Color(.76f, .53f, .26f));
            Limb(helper, new Vector2(130, 164), new Vector2(167, 140), 14, new Color(.27f, .14f, .075f));
            Limb(helper, new Vector2(167, 140), new Vector2(185, 77), 14, new Color(.27f, .14f, .075f));
            Limb(helper, new Vector2(130, 168), new Vector2(171, 143), 7, new Color(.66f, .40f, .18f));
            Limb(helper, new Vector2(171, 143), new Vector2(189, 80), 7, new Color(.66f, .40f, .18f));
            Box(helper, 64, 103, 92, 260, new Color(.20f, .105f, .065f));
            Box(helper, 69, 103, 82, 260, new Color(.48f, .27f, .12f));
            Box(helper, 77, 103, 15, 260, new Color(.64f, .39f, .17f));
            Box(helper, 92, 103, 42, 260, new Color(.57f, .32f, .13f));
            Box(helper, 140, 103, 8, 260, new Color(.34f, .17f, .085f));
            Ellipse(helper, 110, 363, 46, 18, new Color(.24f, .13f, .075f));
            Ellipse(helper, 110, 369, 41, 13, new Color(.73f, .48f, .23f));
            Ellipse(helper, 110, 369, 29, 8, new Color(.56f, .32f, .14f));
            for (int i = 0; i < 3; i++)
            {
                float y = 307 - i * 42;
                Box(helper, 67, y - 9, 86, 18, new Color(.18f, .13f, .10f));
                Limb(helper, new Vector2(79, y), new Vector2(32, y + 15), 12, new Color(.27f, .14f, .075f));
                Limb(helper, new Vector2(142, y + 1), new Vector2(192, y + 14), 12, new Color(.27f, .14f, .075f));
                Limb(helper, new Vector2(79, y + 4), new Vector2(32, y + 19), 6, new Color(.75f, .49f, .23f));
                Limb(helper, new Vector2(142, y + 5), new Vector2(192, y + 18), 6, new Color(.75f, .49f, .23f));
            }

            // The menu is inked on a separate vertical timber board fastened to the trunk.
            Box(helper, 72, 131, 80, 207, new Color(.18f, .09f, .055f));
            Box(helper, 76, 135, 72, 199, new Color(.60f, .36f, .16f));
            Box(helper, 80, 139, 64, 191, new Color(.87f, .65f, .34f));
            Box(helper, 80, 326, 64, 4, new Color(1f, .82f, .49f));
            Box(helper, 139, 139, 5, 187, new Color(.69f, .45f, .21f));
            for (int i = 0; i < 7; i++)
            {
                Box(helper, 84 + i * 8, 146 + (i % 3) * 10, 1, 166 - (i % 2) * 20,
                    new Color(.48f, .27f, .12f, .25f));
            }

            foreach (float y in new[] { 145f, 316f })
            {
                Box(helper, 84, y, 5, 5, new Color(.30f, .21f, .13f));
                Box(helper, 132, y, 5, 5, new Color(.30f, .21f, .13f));
                Box(helper, 85, y + 2, 2, 2, new Color(.93f, .74f, .39f));
            }
        }

        private Vector2 Position(Vector2 point)
        {
            var rect = rectTransform.rect;
            var size = new Vector2(220, 420);
            if (interior)
            {
                size = new Vector2(1280, 720);
            }

            return rect.min + Vector2.Scale(point / size, rect.size);
        }

        private Color Tint(Color tint)
        {
            return tint * color;
        }

        private void Box(VertexHelper helper, float x, float y, float width, float height, Color tint)
        {
            Polygon(helper, new[] { new Vector2(x, y), new Vector2(x, y + height),
                new Vector2(x + width, y + height), new Vector2(x + width, y) }, tint);
        }

        private void Limb(VertexHelper helper, Vector2 start, Vector2 end, float width, Color tint)
        {
            var normal = new Vector2(-(end - start).y, (end - start).x).normalized * width * .5f;
            Polygon(helper, new[] { start - normal, start + normal, end + normal, end - normal }, tint);
        }

        private void Ellipse(VertexHelper helper, float x, float y, float width, float height, Color tint)
        {
            var points = new Vector2[20];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                points[i] = new Vector2(x + Mathf.Cos(angle) * width, y + Mathf.Sin(angle) * height);
            }

            Polygon(helper, points, tint);
        }

        private void Polygon(VertexHelper helper, Vector2[] points, Color tint)
        {
            int index = helper.currentVertCount;
            foreach (var point in points)
            {
                helper.AddVert(Position(point), Tint(tint), Vector2.zero);
            }

            for (int i = 1; i < points.Length - 1; i++)
            {
                helper.AddTriangle(index, index + i, index + i + 1);
            }
        }
    }
}
