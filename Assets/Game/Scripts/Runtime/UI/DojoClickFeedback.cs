using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [DisallowMultipleComponent]
    public sealed class DojoClickFeedback : MonoBehaviour
    {
        private const int RippleCount = 8;
        private RectTransform surface;
        private DojoClickRippleGraphic[] ripples;
        private int nextRipple;
        private AudioSource clickSource;
        private AudioClip clickSound;

        private void Awake()
        {
            var canvasObject = new GameObject("Click Feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            surface = (RectTransform)canvasObject.transform;
            // Deliberately no GraphicRaycaster: these visuals cannot consume a click.
            ripples = new DojoClickRippleGraphic[RippleCount];
            for (int i=0; i<RippleCount; i++)
            {
                var rippleObject = new GameObject("Click Ripple " + i, typeof(RectTransform), typeof(DojoClickRippleGraphic));
                rippleObject.transform.SetParent(surface, false);
                var rect = (RectTransform)rippleObject.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(150,150);
                ripples[i] = rippleObject.GetComponent<DojoClickRippleGraphic>();
                ripples[i].raycastTarget = false; rippleObject.SetActive(false);
            }
            clickSound = Resources.Load<AudioClip>("UI/DojoClick");
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false; clickSource.loop = false; clickSource.spatialBlend = 0;
            clickSource.volume = .32f; clickSource.priority = 128; clickSource.ignoreListenerPause = true;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !UnityEngine.Application.isFocused || Cursor.lockState == CursorLockMode.Locked) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            var position = mouse.position.ReadValue();
            if (position.x < 0 || position.y < 0 || position.x >= Screen.width || position.y >= Screen.height) return;
            PlayAt(position);
        }

        public void PlayAt(Vector2 screenPosition)
        {
            if (!isActiveAndEnabled || !surface) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screenPosition, null, out var point)) return;
            var ripple = ripples[nextRipple]; nextRipple = (nextRipple+1)%RippleCount;
            ripple.rectTransform.anchoredPosition = point;
            ripple.Play();
            if (clickSound) clickSource.PlayOneShot(clickSound);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Clear();
        }

        private void OnDisable() => Clear();

        private void Clear()
        {
            if (clickSource) clickSource.Stop();
            if (ripples == null) return;
            foreach (var ripple in ripples) if (ripple) ripple.gameObject.SetActive(false);
        }
    }
}
