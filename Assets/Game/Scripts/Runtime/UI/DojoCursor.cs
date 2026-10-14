using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    // A native cursor: it never adds a Graphic or intercepts game/UI input.
    public sealed class DojoCursor : MonoBehaviour
    {
        private static DojoCursor instance;
        private Texture2D normalTexture,
            hoverTexture;
        private Texture2D currentTexture;
        private static readonly Vector2 Hotspot = new Vector2(4, 4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance)
            {
                return;
            }

            var cursorObject = new GameObject("Dojo Cursor");
            DontDestroyOnLoad(cursorObject);
            instance = cursorObject.AddComponent<DojoCursor>();
        }

        private void Awake()
        {
            normalTexture = CreateCursor(Resources.Load<Texture2D>("UI/PixelDojoCursor"));
            hoverTexture = CreateCursor(Resources.Load<Texture2D>("UI/PixelDojoCursorPressed"));
            gameObject.AddComponent<DojoClickFeedback>();
        }

        private void Update()
        {
            if (Mouse.current == null || !UnityEngine.Application.isFocused)
            {
                return;
            }

            RefreshTexture();
        }

        private void RefreshTexture()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (Mouse.current.leftButton.isPressed && hoverTexture)
            {
                Apply(hoverTexture);
            }
            else
            {
                Apply(normalTexture);
            }
        }

        private static Texture2D CreateCursor(Texture2D original)
        {
            if (!original)
            {
                return null;
            }

            var pixels = original.GetPixels32();
            int left = original.width, right = 0, bottom = original.height, top = 0;
            for (int y = 0; y < original.height; y++)
            {
                for (int x = 0; x < original.width; x++)
                {
                    if (pixels[y * original.width + x].a < 128)
                    {
                        continue;
                    }
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }

            int width = right - left + 1, height = top - bottom + 1;
            if (width <= 0 || height <= 0)
            {
                return null;
            }
            float scale = 56f / Mathf.Max(width, height);
            int outputWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int outputHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            var output = new Color32[64 * 64];
            for (int y = 0; y < outputHeight; y++)
            {
                for (int x = 0; x < outputWidth; x++)
                {
                    int sourceX = left + Mathf.Min(width - 1, Mathf.FloorToInt(x / scale));
                    int sourceY = bottom + Mathf.Min(height - 1, Mathf.FloorToInt(y / scale));
                    output[(64 - 4 - outputHeight + y) * 64 + 4 + x] = pixels[sourceY * original.width + sourceX];
                }
            }

            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = original.name;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(output);
            texture.Apply();
            return texture;
        }

        private void Apply(Texture2D texture)
        {
            if (currentTexture == texture)
            {
                return;
            }

            if (texture)
            {
                Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(texture, Vector2.zero, CursorMode.Auto);
            }

            currentTexture = texture;
        }

        private void OnApplicationFocus(bool focused)
        {
            currentTexture = null;

            if (focused)
            {
                Cursor.SetCursor(normalTexture, Hotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void OnDestroy()
        {
            if (normalTexture)
            {
                Destroy(normalTexture);
            }
            if (hoverTexture)
            {
                Destroy(hoverTexture);
            }
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            currentTexture = null;

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
