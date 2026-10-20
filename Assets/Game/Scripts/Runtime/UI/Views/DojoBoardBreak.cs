using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class DojoBoardBreak : MonoBehaviour
    {
        private Image image;
        private DojoBoardFragments fragments;

        public IEnumerator Play()
        {
            image = GetComponent<Image>();
            if (!fragments)
            {
                var go = new GameObject("Board Fragments", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                fragments = go.AddComponent<DojoBoardFragments>();
                fragments.raycastTarget = false;
                fragments.sprite = image.sprite;
            }

            fragments.gameObject.SetActive(true);
            image.enabled = false;
            float elapsed = 0f;
            while (elapsed < .25f)
            {
                elapsed += Time.unscaledDeltaTime;
                fragments.progress = Mathf.Clamp01(elapsed / .25f);
                fragments.SetVerticesDirty();
                yield return null;
            }
        }

        public void Restore()
        {
            if (image)
            {
                image.enabled = true;
            }

            if (fragments)
            {
                fragments.gameObject.SetActive(false);
            }
        }
    }
}
