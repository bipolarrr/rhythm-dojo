using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Fixed-position contact imprints; no incoming fists, random directions or input raycasts.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoPunchImpact : MaskableGraphic
    {
        private const int Capacity = 8;
        private const float Duration = .65f;
        private readonly Vector2[] positions = new Vector2[Capacity];
        private readonly float[] ages = new float[Capacity];
        private readonly bool[] active = new bool[Capacity];
        private int nextImpact;
        private Texture2D stampTexture;
        private Vector2 stampOffset;
        private Vector2 stampSize;
        private Rect stampUv = new Rect(0, 0, 1, 1);

        public override Texture mainTexture
        {
            get
            {
                if (stampTexture)
                {
                    return stampTexture;
                }

                return Texture2D.whiteTexture;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Initialize(Texture2D texture, Vector2 hotspot)
        {
            Clear();
            if (stampTexture)
            {
                Destroy(stampTexture);
            }

            stampTexture = null;
            if (!texture)
            {
                return;
            }

            var sourcePixels = texture.GetPixels32();
            int left = texture.width;
            int right = -1;
            int bottom = texture.height;
            int top = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (sourcePixels[y * texture.width + x].a < 128)
                    {
                        continue;
                    }

                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }

            if (right < left || top < bottom)
            {
                return;
            }

            var artwork = Resources.Load<Texture2D>("UI/DojoFistImprint");
            if (!artwork)
            {
                throw new System.InvalidOperationException("Gray frontal fist imprint artwork missing.");
            }

            var pixels = artwork.GetPixels32();
            int imprintLeft = artwork.width;
            int imprintRight = -1;
            int imprintBottom = artwork.height;
            int imprintTop = -1;
            for (int y = 0; y < artwork.height; y++)
            {
                for (int x = 0; x < artwork.width; x++)
                {
                    int index = y * artwork.width + x;
                    var pixel = pixels[index];
                    byte gray = (byte)((pixel.r * 77 + pixel.g * 150 + pixel.b * 29) >> 8);
                    pixels[index] = new Color32(gray, gray, gray, pixel.a);
                    if (pixel.a < 128)
                    {
                        continue;
                    }

                    imprintLeft = Mathf.Min(imprintLeft, x);
                    imprintRight = Mathf.Max(imprintRight, x);
                    imprintBottom = Mathf.Min(imprintBottom, y);
                    imprintTop = Mathf.Max(imprintTop, y);
                }
            }

            if (imprintRight < imprintLeft || imprintTop < imprintBottom)
            {
                throw new System.InvalidOperationException("Gray frontal fist imprint artwork is empty.");
            }

            // Preserve the generated pixel shading, with strictly neutral gray and genuine alpha.
            stampTexture = new Texture2D(artwork.width, artwork.height, TextureFormat.RGBA32, false);
            stampTexture.name = "DojoFistImprint";
            stampTexture.filterMode = FilterMode.Point;
            stampTexture.wrapMode = TextureWrapMode.Clamp;
            stampTexture.SetPixels32(pixels);
            stampTexture.Apply();
            float width = right - left + 1;
            float height = top - bottom + 1;
            float imprintWidth = imprintRight - imprintLeft + 1;
            float imprintHeight = imprintTop - imprintBottom + 1;
            stampUv = new Rect(imprintLeft / (float)artwork.width, imprintBottom / (float)artwork.height,
                imprintWidth / artwork.width, imprintHeight / artwork.height);
            stampSize = new Vector2(imprintWidth, imprintHeight)
                * (Mathf.Round(Mathf.Max(width, height) * .5f) / Mathf.Max(imprintWidth, imprintHeight));
            // Place the print at the leading knuckles of the extended diagonal cursor.
            // Cursor hotspots use top-left coordinates; canvas positions use bottom-left coordinates.
            stampOffset = new Vector2(left + width * .28f - hotspot.x,
                bottom + height * .78f - texture.height + hotspot.y);
            SetMaterialDirty();
        }

        public void PlayAt(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled || !stampTexture)
            {
                return;
            }

            if (screenPosition.x < 0 || screenPosition.x >= Screen.width)
            {
                return;
            }

            if (screenPosition.y < 0 || screenPosition.y >= Screen.height)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition + stampOffset, null, out var point))
            {
                return;
            }

            positions[nextImpact] = point;
            ages[nextImpact] = 0;
            active[nextImpact] = true;
            nextImpact = (nextImpact + 1) % Capacity;
            SetVerticesDirty();
        }

        private void Update()
        {
            bool changed = false;
            for (int i = 0; i < Capacity; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                ages[i] += Time.unscaledDeltaTime;
                active[i] = ages[i] < Duration;
                changed = true;
            }

            if (changed)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            for (int i = 0; i < Capacity; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                float opacity = .42f * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.1f, Duration, ages[i])));
                var tint = new Color(1f, 1f, 1f, opacity);
                var minimum = positions[i] - stampSize * .5f;
                var maximum = positions[i] + stampSize * .5f;
                int index = helper.currentVertCount;
                helper.AddVert(minimum, tint, new Vector2(stampUv.xMin, stampUv.yMin));
                helper.AddVert(new Vector2(minimum.x, maximum.y), tint, new Vector2(stampUv.xMin, stampUv.yMax));
                helper.AddVert(maximum, tint, new Vector2(stampUv.xMax, stampUv.yMax));
                helper.AddVert(new Vector2(maximum.x, minimum.y), tint, new Vector2(stampUv.xMax, stampUv.yMin));
                helper.AddTriangle(index, index + 1, index + 2);
                helper.AddTriangle(index, index + 2, index + 3);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Clear();
            }
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (stampTexture)
            {
                Destroy(stampTexture);
            }

            base.OnDestroy();
        }

        public void Clear()
        {
            System.Array.Clear(active, 0, active.Length);
            nextImpact = 0;
            SetVerticesDirty();
        }
    }
}
