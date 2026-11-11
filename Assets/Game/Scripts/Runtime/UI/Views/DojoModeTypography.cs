using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Each mode owns an authored composition. Motion offsets never replace its layout.
    public sealed class DojoModeTypography : MonoBehaviour
    {
        public CanvasGroup titleGroup, copyGroup;
        public Text heading, description, detail;
        public RectTransform brush;
        [Min(.05f)] public float enterSeconds = .23f;
        [Min(0f)] public float copyDelay = .08f;
        [Min(.05f)] public float copySeconds = .16f;
        [Min(0f)] public float entryDistance = 96f;
        [Range(0f, 2f)] public float overshoot = .8f;
        private Vector2 titleHome, copyHome;
        private bool initialized;

        public void Validate()
        {
            if (!titleGroup || !copyGroup || !brush)
            {
                throw new InvalidOperationException("Dojo typography motion references missing.");
            }
            if (!heading || !description || !detail)
            {
                throw new InvalidOperationException("Dojo typography text references missing.");
            }
        }

        public void Initialize()
        {
            Validate();
            if (initialized)
            {
                return;
            }
            titleHome = ((RectTransform)titleGroup.transform).anchoredPosition;
            copyHome = ((RectTransform)copyGroup.transform).anchoredPosition;
            initialized = true;
        }

        public void SetRest(bool visible)
        {
            Initialize();
            float alpha = 0f;
            if (visible)
            {
                alpha = 1f;
            }
            titleGroup.alpha = copyGroup.alpha = alpha;
            ((RectTransform)titleGroup.transform).anchoredPosition = titleHome;
            ((RectTransform)copyGroup.transform).anchoredPosition = copyHome;
            titleGroup.transform.localScale = Vector3.one;
            brush.localScale = Vector3.one;
        }

        public void Exit(float progress, int direction)
        {
            Initialize();
            progress = Mathf.Clamp01(progress);
            titleGroup.alpha = 1f - progress;
            copyGroup.alpha = Mathf.Clamp01(1f - progress * 1.7f);
            ((RectTransform)titleGroup.transform).anchoredPosition =
                titleHome + Vector2.right * (direction * entryDistance * progress * progress);
            ((RectTransform)copyGroup.transform).anchoredPosition =
                copyHome + Vector2.right * (direction * 28f * progress);
        }

        public void Enter(float seconds, int direction)
        {
            Initialize();
            float t = Mathf.Clamp01(seconds / enterSeconds);
            float shifted = t - 1f;
            float settle = 1f + (overshoot + 1f) * shifted * shifted * shifted
                + overshoot * shifted * shifted;
            titleGroup.alpha = Mathf.Clamp01(t * 3f);
            ((RectTransform)titleGroup.transform).anchoredPosition =
                titleHome + Vector2.right * (-direction * entryDistance * (1f - settle));
            titleGroup.transform.localScale = Vector3.one * (1f + .035f * (1f - settle));
            brush.localScale = new Vector3(Mathf.SmoothStep(0, 1, t), 1, 1);
            float copy = Mathf.SmoothStep(0, 1, Mathf.Clamp01((seconds - copyDelay) / copySeconds));
            copyGroup.alpha = copy;
            ((RectTransform)copyGroup.transform).anchoredPosition = copyHome + Vector2.down * (9f * (1f - copy));
        }
    }
}
