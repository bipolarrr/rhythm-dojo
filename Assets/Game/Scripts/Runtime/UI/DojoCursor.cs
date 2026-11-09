using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RhythmDojo.UI
{
    // Native fist frames keep the input hotspot fixed while the artwork punches forward and back.
    public sealed class DojoCursor : MonoBehaviour
    {
        private static DojoCursor instance;
        [SerializeField]
        private Texture2D normalTexture,
            hoverTexture;
        private Texture2D currentTexture;
        private static readonly Vector2 Hotspot = new Vector2(48, 48);
        [SerializeField]
        private Texture2D[] punchFrames;
        private float punchElapsed = PunchDuration;
        private const float PunchDuration = .18f;
        [SerializeField]
        private DojoPunchImpact impact;

        [SerializeField]
        private DojoClickFeedback feedback;
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

            // A single authored cursor survives scene changes, including direct scene play.
            var prefab = Resources.Load<DojoCursor>("UI/DojoCursor");
            if (!prefab)
            {
                throw new System.InvalidOperationException("Dojo cursor prefab missing.");
            }

            instance = Instantiate(prefab);
            instance.name = "Dojo Cursor";
            DontDestroyOnLoad(instance.gameObject);
        }

        private void Awake()
        {
            if (!normalTexture || !hoverTexture || !impact)
            {
                throw new System.InvalidOperationException("Authored cursor references missing.");
            }

            if (!feedback || punchFrames == null || punchFrames.Length != 7)
            {
                throw new System.InvalidOperationException("Authored cursor animation missing.");
            }

            if (System.Array.Exists(punchFrames, frame => !frame))
            {
                throw new System.InvalidOperationException("Authored cursor frame missing.");
            }

            impact.Initialize(punchFrames[3], Hotspot);
            feedback.Initialize(impact);
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

            if (Mouse.current == null || !RhythmDojo.Input.ApplicationFocusState.IsFocused)
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
            if (!RhythmDojo.Input.ApplicationFocusState.UsesPlatformEvents)
            {
                return;
            }

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
