using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelDojoFrameGraphic : MaskableGraphic
    {
        public Color borderColor = new Color(.29f, .13f, .09f);
        public Color trimColor = new Color(.72f, .46f, .24f);
        public float borderWidth = 10;
        public Sprite artwork;

        [SerializeField]
        private Image artworkBorder;
        private Sprite previousArtwork;
        private float previousBorderWidth;
        private Vector2 previousSize;
        private float previousReferencePixelsPerUnit;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshArtwork();
        }

        public void RefreshArtwork()
        {
            previousArtwork = artwork;
            previousBorderWidth = borderWidth;
            previousSize = rectTransform.rect.size;
            previousReferencePixelsPerUnit = ReferencePixelsPerUnit();
            if (!artwork)
            {
                if (artworkBorder)
                {
                    artworkBorder.enabled = false;
                }
                base.SetVerticesDirty();
                return;
            }

            if (!artworkBorder)
            {
                var item = new GameObject("Artwork Border", typeof(RectTransform), typeof(Image));
                item.transform.SetParent(transform, false);
                item.transform.SetAsFirstSibling();
                artworkBorder = item.GetComponent<Image>();
                var rect = artworkBorder.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            artworkBorder.gameObject.layer = gameObject.layer;
            var border = artwork.border;
            float largest = Mathf.Max(border.x, border.y, border.z, border.w);
            artworkBorder.enabled = isActiveAndEnabled && largest > 0;
            artworkBorder.sprite = artwork;
            artworkBorder.type = Image.Type.Sliced;
            artworkBorder.fillCenter = false;
            artworkBorder.color = Color.white;
            artworkBorder.raycastTarget = false;
            artworkBorder.maskable = maskable;
            artworkBorder.material = material;
            float edge = ArtworkEdge();
            float pixelsPerUnit = artwork.pixelsPerUnit / previousReferencePixelsPerUnit;
            if (largest > 0)
            {
                artworkBorder.pixelsPerUnitMultiplier = largest / (Mathf.Max(.001f, edge) * pixelsPerUnit);
            }
            artworkBorder.canvasRenderer.SetColor(canvasRenderer.GetColor());
            base.SetVerticesDirty();
        }

        private void LateUpdate()
        {
            bool changed = previousArtwork != artwork || previousBorderWidth != borderWidth;
            changed |= previousSize != rectTransform.rect.size;
            changed |= previousReferencePixelsPerUnit != ReferencePixelsPerUnit();
            if (artwork && !artworkBorder)
            {
                changed = true;
            }
            if (artworkBorder)
            {
                changed |= artworkBorder.gameObject.layer != gameObject.layer;
                changed |= artworkBorder.maskable != maskable || artworkBorder.material != material;
                artworkBorder.canvasRenderer.SetColor(canvasRenderer.GetColor());
            }
            if (changed)
            {
                RefreshArtwork();
            }
        }

        protected override void OnDisable()
        {
            if (artworkBorder)
            {
                artworkBorder.enabled = false;
            }
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (artworkBorder)
            {
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(artworkBorder.gameObject);
                }
                else
                {
                    DestroyImmediate(artworkBorder.gameObject);
                }
            }
            base.OnDestroy();
        }

        private float ReferencePixelsPerUnit()
        {
            if (canvas)
            {
                return canvas.referencePixelsPerUnit;
            }
            return 100;
        }

        private float ArtworkEdge()
        {
            var rect = GetPixelAdjustedRect();
            return Mathf.Max(0, Mathf.Min(borderWidth, Mathf.Min(rect.width, rect.height) * .5f));
        }

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
            // The native sliced Image draws only the border. This quad occupies only the center,
            // so translucent artwork is sampled once and the center tint cannot affect the border.
            var rect = GetPixelAdjustedRect();
            var border = artwork.border;
            float largest = Mathf.Max(border.x, border.y, border.z, border.w);
            if (largest > 0)
            {
                border *= ArtworkEdge() / largest;
                rect = Rect.MinMaxRect(rect.xMin + border.x, rect.yMin + border.y,
                    rect.xMax - border.z, rect.yMax - border.w);
            }
            var uv = UnityEngine.Sprites.DataUtility.GetInnerUV(artwork);
            helper.AddVert(new Vector2(rect.xMin, rect.yMin), color, new Vector2(uv.x, uv.y));
            helper.AddVert(new Vector2(rect.xMin, rect.yMax), color, new Vector2(uv.x, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMax), color, new Vector2(uv.z, uv.w));
            helper.AddVert(new Vector2(rect.xMax, rect.yMin), color, new Vector2(uv.z, uv.y));
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(0, 2, 3);
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
