using UnityEngine;

namespace RhythmDojo.UI
{
    // Scale the entire authored composition together so actors keep their footing.
    // Only viewport-dependent transforms change; the scene owns every object.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DojoCompositionFit : MonoBehaviour
    {
        public Vector2 referenceSize = new Vector2(1280, 720);
        public bool coverViewport;

        private void OnEnable()
        {
            Canvas.willRenderCanvases += Fit;
            Fit();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= Fit;
        }

        public void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (!parent || referenceSize.x <= 0 || referenceSize.y <= 0)
            {
                return;
            }
            float scale = Mathf.Min(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            if (coverViewport)
            {
                scale = Mathf.Max(parent.rect.width / referenceSize.x, parent.rect.height / referenceSize.y);
            }
            var center = new Vector2(.5f, .5f);
            if (rect.anchorMin != center || rect.anchorMax != center)
            {
                rect.anchorMin = rect.anchorMax = center;
            }
            if (rect.pivot != center || rect.sizeDelta != referenceSize)
            {
                rect.pivot = center;
                rect.sizeDelta = referenceSize;
            }
            if (rect.anchoredPosition != Vector2.zero)
            {
                rect.anchoredPosition = Vector2.zero;
            }
            if (rect.localScale != Vector3.one * scale)
            {
                rect.localScale = Vector3.one * scale;
            }
        }
    }
}
