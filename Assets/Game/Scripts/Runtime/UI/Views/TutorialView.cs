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
        public Button[] lessonButtons;
        public Text[] lessonLabels;
        public string[] lessonIds;
        public GameObject[] completionSeals;
        public GameObject lessonMap;

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
            if (!lessonMap)
            {
                throw new InvalidOperationException("Tutorial map is missing.");
            }
            if (lessonButtons == null || lessonLabels == null)
            {
                throw new InvalidOperationException("Tutorial lesson selection missing. Rebuild Tutorial.");
            }
            if (lessonButtons.Length != 7 || lessonButtons.Length != lessonLabels.Length)
            {
                throw new InvalidOperationException("Tutorial lesson selection is incomplete.");
            }
            if (lessonIds?.Length != lessonButtons.Length || completionSeals?.Length != lessonButtons.Length)
            {
                throw new InvalidOperationException("Tutorial tablet identities or seals are missing.");
            }
            var buttons = new System.Collections.Generic.HashSet<Button>();
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < lessonButtons.Length; i++)
            {
                if (!lessonButtons[i] || !lessonLabels[i])
                {
                    throw new InvalidOperationException("Tutorial lesson button missing.");
                }
                if (!completionSeals[i] || string.IsNullOrEmpty(lessonIds[i]))
                {
                    throw new InvalidOperationException("Tutorial tablet references are incomplete.");
                }
                if (!buttons.Add(lessonButtons[i]) || !ids.Add(lessonIds[i]))
                {
                    throw new InvalidOperationException("Tutorial tablet is duplicated.");
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
