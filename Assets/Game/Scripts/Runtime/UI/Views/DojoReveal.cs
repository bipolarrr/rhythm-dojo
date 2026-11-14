using UnityEngine;

namespace RhythmDojo.UI
{
    // One authored motion group, reused on enable and restored on interruption.
    public sealed class DojoReveal : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform brush;
        public float delay;
        private RectTransform rect;
        private Vector2 home;
        private float elapsed;
        private bool captured;

        private void OnEnable()
        {
            if (!group)
            {
                throw new System.InvalidOperationException("Authored reveal group missing.");
            }
            if (!captured)
            {
                rect = (RectTransform)transform;
                home = rect.anchoredPosition;
                captured = true;
            }
            elapsed = 0;
            Apply(0);
        }

        private void Update()
        {
            if (elapsed >= delay + .28f)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01((elapsed - delay) / .28f));
        }

        private void Apply(float t)
        {
            float shifted = t - 1;
            float settle = 1 + 1.7f * shifted * shifted * shifted + .7f * shifted * shifted;
            group.alpha = Mathf.Clamp01(t * 4);
            rect.anchoredPosition = home + Vector2.left * (28 * (1 - settle));
            rect.localScale = new Vector3(1 + .06f * (1 - settle), 1 - .03f * (1 - settle), 1);
            if (brush)
            {
                brush.localScale = new Vector3(1 - Mathf.Pow(1 - t, 4), 1, 1);
            }
        }

        private void OnDisable()
        {
            if (captured)
            {
                Apply(1);
            }
        }
    }
}
