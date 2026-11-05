using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmDojo.UI
{
    // Native fist frames keep the input hotspot fixed while the artwork punches forward and back.
    public sealed class DojoCursor : MonoBehaviour
    {
        private static DojoCursor instance;
        private Texture2D normalTexture,
            hoverTexture;
        private Texture2D currentTexture;
        private static readonly Vector2 Hotspot = new Vector2(48, 48);
        private Texture2D[] punchFrames;
        private float punchElapsed = PunchDuration;
        private const float PunchDuration = .18f;
        private Color32[] cursorPixels;
        private RectInt cursorBounds;
        private int cursorSourceWidth;
        private DojoPunchImpact impact;
        private bool impactPending;

        public static bool UsesDefaultPointer => SceneManager.GetActiveScene().path == ChartEditorMenu.ScenePath;

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
            var texture = Resources.Load<Texture2D>("UI/DojoFistCursor");
            if (!texture)
            {
                throw new System.InvalidOperationException("Hand-wrap cursor artwork missing.");
            }

            cursorPixels = texture.GetPixels32();
            cursorSourceWidth = texture.width;
            cursorBounds = GetVisibleBounds(cursorPixels, texture.width, texture.height);
            normalTexture = CreateCursor(72);
            hoverTexture = CreateCursor(72);
            punchFrames = new[]
            {
                CreateCursor(69, new Vector2Int(6, -6)),
                CreateCursor(72),
                CreateCursor(78, new Vector2Int(-9, 9)),
                CreateCursor(81, new Vector2Int(-12, 12)),
                CreateCursor(78, new Vector2Int(-9, 9)),
                CreateCursor(75, new Vector2Int(-3, 3)),
                CreateCursor(72)
            };
            cursorPixels = null;
            var canvasObject = new GameObject("Fist Imprints", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32001;
            canvas.pixelPerfect = true;
            // No GraphicRaycaster: a mark can never consume a menu click.
            var impactObject = new GameObject("Fist Marks", typeof(RectTransform), typeof(DojoPunchImpact));
            impactObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)impactObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            impact = impactObject.GetComponent<DojoPunchImpact>();
            impact.Initialize(punchFrames[3], Hotspot);
            gameObject.AddComponent<DojoClickFeedback>().Initialize(impact);
            RefreshSceneStyle();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RefreshSceneStyle();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            RefreshSceneStyle();
        }

        private void RefreshSceneStyle()
        {
            punchElapsed = PunchDuration;
            impactPending = false;
            if (impact)
            {
                impact.Clear();
                impact.enabled = !UsesDefaultPointer;
            }

            var feedback = GetComponent<DojoClickFeedback>();
            if (feedback)
            {
                feedback.enabled = !UsesDefaultPointer;
            }

            if (UsesDefaultPointer)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                currentTexture = null;
                return;
            }

            Apply(normalTexture);
        }

        private void Update()
        {
            if (UsesDefaultPointer)
            {
                return;
            }

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
                var position = Mouse.current.position.ReadValue();
                if (position.x >= 0 && position.x < Screen.width)
                {
                    if (position.y >= 0 && position.y < Screen.height)
                    {
                        punchElapsed = 0;
                        impactPending = true;
                    }
                }
            }

            RefreshTexture();
        }

        private void RefreshTexture()
        {
            if (Mouse.current == null)
            {
                return;
            }

            // Stamp the exact extended frame once, when the fist reaches contact.
            if (impactPending && punchElapsed >= PunchDuration * 3f / punchFrames.Length)
            {
                impactPending = false;
                if (impact)
                {
                    impact.PlayAt(Mouse.current.position.ReadValue());
                }
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

        private Texture2D CreateCursor(int size)
        {
            return CreateCursor(size, Vector2Int.zero);
        }

        private Texture2D CreateCursor(int size, Vector2Int offset)
        {
            // Fit the complete v5 silhouette, including its loose tail, without filtering the pixels.
            // Extra transparent padding fits the longer punch; the matching hotspot shift
            // preserves the resting artwork's screen position and its visible size.
            const int canvasSize = 128;
            var output = new Color32[canvasSize * canvasSize];
            float scale = (float)size / Mathf.Max(cursorBounds.width, cursorBounds.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(cursorBounds.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(cursorBounds.height * scale));
            int left = (canvasSize - width) / 2 + offset.x;
            int bottom = (canvasSize - height) / 2 + offset.y;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceX = cursorBounds.x + Mathf.Min(cursorBounds.width - 1,
                        Mathf.FloorToInt((x + .5f) * cursorBounds.width / width));
                    int sourceY = cursorBounds.y + Mathf.Min(cursorBounds.height - 1,
                        Mathf.FloorToInt((y + .5f) * cursorBounds.height / height));
                    output[(bottom + y) * canvasSize + left + x] = cursorPixels[sourceY * cursorSourceWidth + sourceX];
                }
            }

            var texture = new Texture2D(canvasSize, canvasSize, TextureFormat.RGBA32, false);
            texture.name = "DojoHandrapV5Cursor";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(output);
            texture.Apply();
            return texture;
        }

        private static RectInt GetVisibleBounds(Color32[] pixels, int width, int height)
        {
            int left = width;
            int bottom = height;
            int right = -1;
            int top = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 128)
                    {
                        continue;
                    }

                    left = Mathf.Min(left, x);
                    bottom = Mathf.Min(bottom, y);
                    right = Mathf.Max(right, x);
                    top = Mathf.Max(top, y);
                }
            }

            if (right < left || top < bottom)
            {
                throw new System.InvalidOperationException("Hand-wrap cursor artwork is empty.");
            }

            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        private void Apply(Texture2D texture)
        {
            if (UsesDefaultPointer)
            {
                texture = null;
            }

            if (currentTexture == texture)
            {
                return;
            }

            if (texture)
            {
                // Hardware cursors can shrink the padded animation texture to the platform size.
                // Render it at its pixel size so the fist stays full-sized with the longer reach.
                Cursor.SetCursor(texture, Hotspot, CursorMode.ForceSoftware);
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
            impactPending = false;
            if (impact)
            {
                impact.Clear();
            }

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
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            currentTexture = null;
            punchElapsed = PunchDuration;
            impactPending = false;
            if (impact)
            {
                impact.Clear();
            }
        }
    }
}
