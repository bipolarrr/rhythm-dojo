using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class DojoButtonFocus : MonoBehaviour
    {
        [SerializeField]
        private RectTransform border;

        [SerializeField]
        private RectTransform borderTemplate;
        private bool keyboardFocus;

        private void LateUpdate()
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                keyboardFocus = false;
            }
            else if (Keyboard.current?.anyKey.wasPressedThisFrame == true)
            {
                keyboardFocus = true;
            }

            if (!keyboardFocus || DojoCursor.UsesDefaultPointer)
            {
                HideBorder();
                return;
            }

            var system = EventSystem.current;
            if (!system || !system.isActiveAndEnabled)
            {
                HideBorder();
                return;
            }

            var selected = system.currentSelectedGameObject;
            if (!selected || !selected.activeInHierarchy)
            {
                HideBorder();
                return;
            }

            if (!selected.TryGetComponent<Selectable>(out var control))
            {
                HideBorder();
                return;
            }

            if (!control.isActiveAndEnabled || !control.IsInteractable())
            {
                HideBorder();
                return;
            }

            if (!border)
            {
                // The previous border can be destroyed with the selected scene object.
                border = Instantiate(borderTemplate);
                border.name = "Dojo Focus Border";
            }

            if (border.parent != control.transform)
            {
                border.SetParent(control.transform, false);
                border.SetAsLastSibling();
                border.anchorMin = Vector2.zero;
                border.anchorMax = Vector2.one;
                border.offsetMin = new Vector2(2, 2);
                border.offsetMax = new Vector2(-2, -2);
            }

            border.gameObject.SetActive(true);
        }

        private void HideBorder()
        {
            if (border)
            {
                border.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (border && UnityEngine.Application.isPlaying)
            {
                Destroy(border.gameObject);
            }
        }
    }
}
