using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Bounded presentation pool, independent of gameplay and the EventSystem.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DojoPunchImpact : MaskableGraphic
    {
        private const int Capacity = 8;
        private const float TravelDuration = .1f;
        private const float Duration = .32f;
        private readonly Vector2[] positions = new Vector2[Capacity];
        private readonly Vector2[] origins = new Vector2[Capacity];
        private readonly float[] ages = new float[Capacity];
        private readonly bool[] active = new bool[Capacity];
        private readonly RawImage[] fists = new RawImage[Capacity];
        private readonly System.Random random = new System.Random();
        private int nextImpact;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;

            var texture = Resources.Load<Texture2D>("UI/DojoFistCursor");
            var bounds = GetVisibleBounds(texture);

            for (int i = 0; i < Capacity; i++)
            {
                var fistObject = new GameObject("Incoming Fist " + i, typeof(RectTransform), typeof(RawImage));
                fistObject.transform.SetParent(transform, false);
                var fist = fistObject.GetComponent<RawImage>();
                fist.texture = texture;
                fist.uvRect = bounds;
                fist.raycastTarget = false;
                fist.rectTransform.anchorMin = fist.rectTransform.anchorMax = new Vector2(.5f, .5f);
                // The leading knuckles reach the click point, rather than the image center.
                fist.rectTransform.pivot = new Vector2(.5f, 1);
                float aspect = 1;

                if (texture)
                {
                    aspect = texture.width * bounds.width / (texture.height * bounds.height);
                }

                fist.rectTransform.sizeDelta = new Vector2(64 * aspect, 64);
                fists[i] = fist;
                fistObject.SetActive(false);
            }
        }

        private static Rect GetVisibleBounds(Texture2D texture)
        {
            if (!texture)
            {
                return new Rect(0, 0, 1, 1);
            }

            var pixels = texture.GetPixels32();
            int left = texture.width;
            int right = 0;
            int bottom = texture.height;
            int top = 0;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128)
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
                return new Rect(0, 0, 1, 1);
            }

            return new Rect((float)left / texture.width, (float)bottom / texture.height,
                (float)(right - left + 1) / texture.width, (float)(top - bottom + 1) / texture.height);
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
                UpdateFist(i);
                changed = true;
            }

            if (changed)
            {
                SetVerticesDirty();
            }
        }

        public void PlayAt(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled)
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
                rectTransform, screenPosition, null, out var point))
            {
                return;
            }

            positions[nextImpact] = point;
            float angle = (float)random.NextDouble() * Mathf.PI * 2;
            float distance = Mathf.Lerp(70, 110, (float)random.NextDouble());
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            var bounds = rectTransform.rect;
            var origin = point + offset;
            origin.x = Mathf.Clamp(origin.x, bounds.xMin + 32, bounds.xMax - 32);
            origin.y = Mathf.Clamp(origin.y, bounds.yMin + 32, bounds.yMax - 32);

            if ((origin - point).sqrMagnitude < 1600)
            {
                origin = point - offset;
                origin.x = Mathf.Clamp(origin.x, bounds.xMin + 32, bounds.xMax - 32);
                origin.y = Mathf.Clamp(origin.y, bounds.yMin + 32, bounds.yMax - 32);
            }

            origins[nextImpact] = origin;
            ages[nextImpact] = 0;
            active[nextImpact] = true;
            var direction = point - origin;
            fists[nextImpact].rectTransform.localRotation = Quaternion.Euler(
                0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
            fists[nextImpact].transform.SetAsLastSibling();
            UpdateFist(nextImpact);
            nextImpact = (nextImpact + 1) % Capacity;
            SetVerticesDirty();
        }

        private void UpdateFist(int index)
        {
            var fist = fists[index];
            fist.gameObject.SetActive(active[index]);

            if (!active[index])
            {
                return;
            }

            float progress = Mathf.Clamp01(ages[index] / TravelDuration);
            // Accelerate into the target; then briefly hold the contact pose and recoil.
            float eased = progress * progress;
            var point = Vector2.Lerp(origins[index], positions[index], eased);
            float opacity = 1;
            float scale = Mathf.Lerp(.8f, 1.1f, eased);

            if (ages[index] > TravelDuration + .035f)
            {
                float fade = Mathf.Clamp01((ages[index] - TravelDuration - .035f) / .185f);
                point = Vector2.Lerp(positions[index], origins[index], fade * .2f);
                opacity = 1 - fade;
                scale = Mathf.Lerp(1.1f, .9f, fade);
            }

            fist.rectTransform.anchoredPosition = point;
            fist.rectTransform.localScale = Vector3.one * scale;
            fist.color = new Color(1, 1, 1, opacity);
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

                if (ages[i] < TravelDuration)
                {
                    continue;
                }

                float progress = Mathf.Clamp01((ages[i] - TravelDuration) / (Duration - TravelDuration));
                float opacity = (1 - progress) * (1 - progress);
                float radius = Mathf.Lerp(8, 35, 1 - (1 - progress) * (1 - progress));
                var tint = new Color(1, .78f, .38f, opacity);

                for (int spark = 0; spark < 8; spark++)
                {
                    float angle = spark * Mathf.PI / 4;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var side = new Vector2(-direction.y, direction.x);
                    var center = positions[i] + direction * radius;
                    float length = Mathf.Lerp(11, 3, progress);
                    float width = Mathf.Lerp(3, 1, progress);
                    Quad(helper, center, direction * length * .5f, side * width, tint);
                }

                if (progress < .3f)
                {
                    var flash = new Color(1, .94f, .75f, 1 - progress / .3f);
                    Quad(helper, positions[i], new Vector2(7, 0), new Vector2(0, 3), flash);
                    Quad(helper, positions[i], new Vector2(3, 0), new Vector2(0, 7), flash);
                }
            }
        }

        private static void Quad(VertexHelper helper, Vector2 center, Vector2 along, Vector2 side, Color tint)
        {
            int index = helper.currentVertCount;
            helper.AddVert(center - along - side, tint, Vector2.zero);
            helper.AddVert(center - along + side, tint, Vector2.zero);
            helper.AddVert(center + along + side, tint, Vector2.zero);
            helper.AddVert(center + along - side, tint, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
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

        public void Clear()
        {
            System.Array.Clear(active, 0, active.Length);

            foreach (var fist in fists)
            {
                if (fist)
                {
                    fist.gameObject.SetActive(false);
                }
            }

            SetVerticesDirty();
        }
    }
}
