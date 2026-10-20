using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Keep the original screen projection (including hit feedback), but expose only
    // the portion inside the gameplay window. UI masks alone cannot clip world meshes.
    [ExecuteAlways]
    [RequireComponent(typeof(RawImage))]
    public sealed class GameplayViewport : MonoBehaviour
    {
        public Camera gameplayCamera;

        private RawImage image;
        private RenderTexture target;
        private RenderTexture previousTarget;
        private float previousAspect;
        private Camera boundCamera;
        private readonly Vector3[] corners = new Vector3[4];

        private void OnEnable()
        {
            image = GetComponent<RawImage>();
            Canvas.willRenderCanvases += Refresh;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!gameplayCamera || !image || !image.canvas)
            {
                return;
            }

            var canvas = image.canvas.rootCanvas;
            var size = ((RectTransform)canvas.transform).rect.size * canvas.scaleFactor;
            int width = Mathf.Max(1, Mathf.RoundToInt(size.x));
            int height = Mathf.Max(1, Mathf.RoundToInt(size.y));
            if (boundCamera != gameplayCamera)
            {
                Release();
                boundCamera = gameplayCamera;
                previousTarget = gameplayCamera.targetTexture;
                previousAspect = gameplayCamera.aspect;
            }

            if (!target || target.width != width || target.height != height)
            {
                ReleaseTexture();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Gameplay Window",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                };
                target.Create();
                gameplayCamera.targetTexture = target;
                image.texture = target;
            }

            gameplayCamera.aspect = (float)width / height;
            image.rectTransform.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            image.uvRect = new Rect(min.x / width, min.y / height,
                (max.x - min.x) / width, (max.y - min.y) / height);
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= Refresh;
            Release();
        }

        private void Release()
        {
            if (boundCamera)
            {
                boundCamera.targetTexture = previousTarget;
                boundCamera.aspect = previousAspect;
            }

            ReleaseTexture();
            boundCamera = null;
        }

        private void ReleaseTexture()
        {
            if (image)
            {
                image.texture = null;
            }

            if (!target)
            {
                return;
            }

            if (boundCamera && boundCamera.targetTexture == target)
            {
                boundCamera.targetTexture = previousTarget;
            }

            target.Release();
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }

            target = null;
        }
    }
}
