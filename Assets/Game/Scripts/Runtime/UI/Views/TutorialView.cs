using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class TutorialView : MonoBehaviour
    {
        public Image backdrop;
        public RectTransform storyRoot;
        public CanvasGroup introduction;
        public Text heading;
        public Button backButton;

        public void Validate()
        {
            if (!backdrop || !storyRoot || !introduction)
            {
                throw new InvalidOperationException("Tutorial stage composition missing.");
            }
            if (!heading || !backButton)
            {
                throw new InvalidOperationException("Tutorial navigation composition missing.");
            }
        }

        public void Reveal(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float remaining = 1 - t;
            introduction.alpha = Mathf.Clamp01(t * 2);
            introduction.transform.localScale = Vector3.one * (1 - .025f * remaining * remaining);
        }
    }
}
