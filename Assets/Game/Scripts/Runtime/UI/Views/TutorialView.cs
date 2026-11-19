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
        public Text backLabel;
        public Text lessonTitle;
        public Text instructions;
        public Text progress;
        public Text feedback;
        public Button startButton;
        public Text startLabel;
        public Button[] lessonButtons;
        public Text[] lessonLabels;

        public void Validate()
        {
            if (!backdrop || !storyRoot || !introduction)
            {
                throw new InvalidOperationException("Tutorial stage composition missing.");
            }
            if (!heading || !backButton || !backLabel)
            {
                throw new InvalidOperationException("Tutorial navigation composition missing.");
            }
            if (!lessonTitle || !instructions || !progress)
            {
                throw new InvalidOperationException("Tutorial lesson composition missing.");
            }
            if (!feedback || !startButton || !startLabel)
            {
                throw new InvalidOperationException("Tutorial training controls missing.");
            }
            if (lessonButtons == null || lessonLabels == null)
            {
                throw new InvalidOperationException("Tutorial lesson selection missing. Rebuild Tutorial.");
            }
            if (lessonButtons.Length == 0 || lessonButtons.Length != lessonLabels.Length)
            {
                throw new InvalidOperationException("Tutorial lesson selection is incomplete.");
            }
            for (int i = 0; i < lessonButtons.Length; i++)
            {
                if (!lessonButtons[i] || !lessonLabels[i])
                {
                    throw new InvalidOperationException("Tutorial lesson button missing.");
                }
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
