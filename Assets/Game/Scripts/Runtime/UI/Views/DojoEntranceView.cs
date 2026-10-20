using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DojoEntranceView : MonoBehaviour
    {
        public const float InteriorScale = 1.08f;

        public RectTransform leftDoor;
        public RectTransform rightDoor;
        public CanvasGroup frame;

        public void SetOpening(float amount)
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            float width = ((RectTransform)transform).rect.width;
            leftDoor.anchoredPosition = new Vector2(-width * .55f * eased, 0f);
            rightDoor.anchoredPosition = new Vector2(width * .55f * eased, 0f);
            frame.alpha = 1f;
        }

        public void SetApproach(float amount)
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            // Finish at the same framing used by the song selection backdrop.
            // Only the nearby doorway should sweep past the edges of the screen.
            float frameScale = 1f / (1f - .46f * eased);
            float backgroundScale = Mathf.Lerp(1f, InteriorScale, eased);
            transform.localScale = Vector3.one * backgroundScale;
            frame.transform.localScale = Vector3.one * (frameScale / backgroundScale);
        }
    }
}
