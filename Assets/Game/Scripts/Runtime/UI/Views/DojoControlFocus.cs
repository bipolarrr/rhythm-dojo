using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Shares the EventSystem selection with pointer input; creates no runtime presentation objects.
    public sealed class DojoControlFocus : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public Selectable control;
        public Text label;
        public RectTransform stroke;
        public Color restingColor;
        public Color selectedColor;
        public ScrollRect scroll;
        public bool activateOnSelect;
        public bool active;
        private float emphasis;

        private void Awake()
        {
            if (!control || !stroke)
            {
                throw new System.InvalidOperationException("Authored dojo control focus references missing.");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // A closing dropdown also sends Enter to the stationary pointer underneath it.
            // Only deliberate pointer movement should replace keyboard focus.
            if (eventData.delta.sqrMagnitude < .01f)
            {
                return;
            }
            if (control.IsInteractable() && EventSystem.current)
            {
                control.Select();
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (activateOnSelect && control.IsInteractable())
            {
                ((Button)control).onClick.Invoke();
            }
            if (!scroll)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                scroll.viewport, (RectTransform)transform);
            float delta = 0;
            if (bounds.min.y < scroll.viewport.rect.yMin)
            {
                delta = scroll.viewport.rect.yMin - bounds.min.y;
            }
            else if (bounds.max.y > scroll.viewport.rect.yMax)
            {
                delta = scroll.viewport.rect.yMax - bounds.max.y;
            }
            scroll.content.anchoredPosition += new Vector2(0, delta);
        }

        private void OnEnable()
        {
            emphasis = 0;
            Apply();
        }

        private void LateUpdate()
        {
            bool selected = active;
            if (EventSystem.current)
            {
                selected |= EventSystem.current.currentSelectedGameObject == gameObject;
            }
            float target = 0;
            if (selected && control.IsInteractable())
            {
                target = 1;
            }
            emphasis = Mathf.MoveTowards(emphasis, target, Time.unscaledDeltaTime * 7);
            Apply();
        }

        private void Apply()
        {
            if (!control || !stroke)
            {
                return;
            }
            float eased = 1 - Mathf.Pow(1 - emphasis, 3);
            stroke.localScale = new Vector3(eased, 1, 1);
            if (label)
            {
                var color = Color.Lerp(restingColor, selectedColor, eased);
                if (!control.IsInteractable())
                {
                    color.a *= .45f;
                }
                label.color = color;
                label.rectTransform.localScale = Vector3.one * (1 + eased * .025f);
            }
        }

        private void OnDisable()
        {
            emphasis = 0;
            Apply();
        }
    }
}
