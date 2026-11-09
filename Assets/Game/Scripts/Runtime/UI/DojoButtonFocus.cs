using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    internal sealed class DojoButtonFocus : MonoBehaviour
    {
        private RectTransform border;
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
                BuildBorder();
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

        private void BuildBorder()
        {
            border = new GameObject("Dojo Focus Border", typeof(RectTransform)).GetComponent<RectTransform>();
            AddEdge("Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
            AddEdge("Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
            AddEdge("Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
            AddEdge("Right", new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
        }

        private void AddEdge(string name, Vector2 minimum, Vector2 maximum, Vector2 inset, Vector2 outset)
        {
            var edge = new GameObject(name, typeof(Image), typeof(Shadow)).GetComponent<Image>();
            var rect = edge.rectTransform;
            rect.SetParent(border, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = inset;
            rect.offsetMax = outset;
            edge.color = new Color(1f, .94f, .70f);
            edge.raycastTarget = false;
            var shadow = edge.GetComponent<Shadow>();
            shadow.effectColor = new Color(.13f, .05f, .025f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
