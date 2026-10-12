using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // A native cursor: it never adds a Graphic or intercepts game/UI input.
    public sealed class DojoCursor : MonoBehaviour
    {
        private static DojoCursor instance;
        private Texture2D normalTexture, hoverTexture;
        private Texture2D currentTexture;
        private PointerEventData pointer;
        private EventSystem pointerSystem;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private static readonly Vector2 Hotspot = new Vector2(4, 4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance) return;
            var cursorObject = new GameObject("Dojo Cursor");
            DontDestroyOnLoad(cursorObject);
            instance = cursorObject.AddComponent<DojoCursor>();
        }

        private void Awake()
        {
            normalTexture = Resources.Load<Texture2D>("UI/DojoCursor");
            hoverTexture = Resources.Load<Texture2D>("UI/DojoCursorHover");
            gameObject.AddComponent<DojoClickFeedback>();
        }

        private void Update()
        {
            if (Mouse.current == null || !UnityEngine.Application.isFocused) return;
            bool interactive = false;
            var system = EventSystem.current;
            if (system)
            {
                if (pointerSystem != system)
                {
                    pointerSystem = system;
                    pointer = new PointerEventData(system);
                }
                pointer.Reset(); pointer.position = Mouse.current.position.ReadValue();
                hits.Clear(); system.RaycastAll(pointer, hits);
                // Respect the front-most hit, including modal blockers and disabled controls.
                if (hits.Count > 0)
                {
                    var control = hits[0].gameObject.GetComponentInParent<Selectable>();
                    interactive = control && control.IsActive() && control.IsInteractable();
                }
            }
            Apply(interactive && hoverTexture ? hoverTexture : normalTexture);
        }

        private void Apply(Texture2D texture)
        {
            if (currentTexture == texture) return;
            Cursor.SetCursor(texture, texture ? Hotspot : Vector2.zero, CursorMode.Auto);
            currentTexture = texture;
        }

        private void OnApplicationFocus(bool focused)
        {
            currentTexture = null;
            Cursor.SetCursor(focused ? normalTexture : null, focused ? Hotspot : Vector2.zero, CursorMode.Auto);
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            currentTexture = null;
            if (instance == this) instance = null;
        }
    }
}
