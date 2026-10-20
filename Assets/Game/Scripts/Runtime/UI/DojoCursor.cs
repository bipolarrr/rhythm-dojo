using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    // Native pointer animation, with non-interactive feedback on a separate canvas.
    public sealed class DojoCursor : MonoBehaviour
    {
        private static DojoCursor instance;
        private Texture2D normalTexture,
            hoverTexture;
        private Texture2D currentTexture;
        private static readonly Vector2 Hotspot = new Vector2(16, 16);
        private Texture2D[] punchFrames;
        private float punchElapsed = PunchDuration;
        private const float PunchDuration = .18f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

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
            normalTexture = CreateCursor(16);
            hoverTexture = CreateCursor(12);
            punchFrames = new[]
            {
                CreateCursor(18),
                CreateCursor(16),
                CreateCursor(14),
                CreateCursor(12),
                CreateCursor(14)
            };
            var canvasObject = new GameObject("Punch Impact", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;

            // No GraphicRaycaster: effects never participate in UI input.
            var impactObject = new GameObject("Impact Sparks", typeof(RectTransform), typeof(DojoPunchImpact));
            impactObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)impactObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            gameObject.AddComponent<DojoClickFeedback>().Initialize(impactObject.GetComponent<DojoPunchImpact>());
        }

        private void Update()
        {
            if (Mouse.current == null || !UnityEngine.Application.isFocused)
            {
                return;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }

            punchElapsed += Time.unscaledDeltaTime;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                punchElapsed = 0;
            }

            RefreshTexture();
        }

        private void RefreshTexture()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (punchElapsed < PunchDuration)
            {
                int index = Mathf.Min(punchFrames.Length - 1,
                    Mathf.FloorToInt(punchElapsed / PunchDuration * punchFrames.Length));
                Apply(punchFrames[index]);
            }
            else if (Mouse.current.leftButton.isPressed && hoverTexture)
            {
                Apply(hoverTexture);
            }
            else
            {
                Apply(normalTexture);
            }
        }

        private static Texture2D CreateCursor(int size)
        {
            // Two-pixel cells give the monochrome ring a deliberately stepped edge.
            var output = new Color32[32 * 32];
            var tint = new Color32(255, 241, 213, 255);
            float outer = size * .5f;
            float inner = outer - 2.5f;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float sourceX = x / 2 * 2 + 1 - 16;
                    float sourceY = y / 2 * 2 + 1 - 16;
                    float distance = sourceX * sourceX + sourceY * sourceY;

                    if (distance > outer * outer || distance < inner * inner)
                    {
                        continue;
                    }

                    output[y * 32 + x] = tint;
                }
            }

            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = "DojoDotCursor";
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
            punchElapsed = PunchDuration;

            if (focused)
            {
                Apply(normalTexture);
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

            if (punchFrames != null)
            {
                foreach (var texture in punchFrames)
                {
                    if (texture)
                    {
                        Destroy(texture);
                    }
                }
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            currentTexture = null;
            punchElapsed = PunchDuration;
        }
    }
}
