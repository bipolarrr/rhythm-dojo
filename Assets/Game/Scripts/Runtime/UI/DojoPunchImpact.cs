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
        [SerializeField]
        private Texture2D stampTexture;
        [SerializeField]
        private Vector2 stampOffset;
        [SerializeField]
        private Vector2 stampSize;
        [SerializeField]
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
            // The prefab stores the baked imprint and geometry for its extended cursor frame.
            if (!stampTexture)
            {
                throw new System.InvalidOperationException("Baked fist imprint missing.");
            }

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
            if (!RhythmDojo.Input.ApplicationFocusState.UsesPlatformEvents)
            {
                return;
            }

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
            nextImpact = 0;
            SetVerticesDirty();
        }
    }
}
