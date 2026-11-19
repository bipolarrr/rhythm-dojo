using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed partial class DojoButtonFocus
    {
        [SerializeField]
        private DojoBrushCircle[] hoverCircles;
        private HoverState[] hoverStates;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly List<DojoHoverGraphic> candidates = new List<DojoHoverGraphic>();
        private PointerEventData pointer;
        private EventSystem pointerSystem;
        private Vector2 lastPointer;
        private Selectable hovered;

        public Selectable Hovered => hovered;

        private sealed class HoverState
        {
            public DojoBrushCircle circle;
            public Selectable control;
            public readonly List<DojoHoverGraphic> graphics = new List<DojoHoverGraphic>();
            public readonly List<RectMask2D> masks = new List<RectMask2D>();
            public float emphasis;
            public float elapsed;
        }

        private void Awake()
        {
            if (hoverCircles == null || hoverCircles.Length != 2)
            {
                throw new System.InvalidOperationException("Authored hover circles missing.");
            }
            hoverStates = new HoverState[hoverCircles.Length];
            for (int i = 0; i < hoverCircles.Length; i++)
            {
                if (!hoverCircles[i])
                {
                    throw new System.InvalidOperationException("Authored hover circle missing.");
                }
                hoverStates[i] = new HoverState { circle = hoverCircles[i] };
                hoverCircles[i].raycastTarget = false;
                hoverCircles[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += ClearHoverOnSceneChange;
        }

        private bool PointerMoved()
        {
            if (Mouse.current == null)
            {
                return false;
            }
            var position = Mouse.current.position.ReadValue();
            bool moved = (position - lastPointer).sqrMagnitude > .25f;
            lastPointer = position;
            return moved;
        }

        private void UpdateHover()
        {
            if (!RhythmDojo.Input.ApplicationFocusState.IsFocused || Cursor.lockState == CursorLockMode.Locked)
            {
                ClearHover();
                return;
            }

            var next = FindHovered();
            if (next != hovered)
            {
                hovered = next;
                if (next)
                {
                    BeginHover(next);
                }
            }

            foreach (var state in hoverStates)
            {
                AnimateHover(state);
            }
        }

        private Selectable FindHovered()
        {
            var system = EventSystem.current;
            if (keyboardFocus || Mouse.current == null)
            {
                return null;
            }
            if (!system || !system.isActiveAndEnabled)
            {
                return null;
            }
            if (lastPointer.x < 0 || lastPointer.x >= Screen.width)
            {
                return null;
            }
            if (lastPointer.y < 0 || lastPointer.y >= Screen.height)
            {
                return null;
            }
            if (pointerSystem != system)
            {
                pointerSystem = system;
                pointer = new PointerEventData(system);
            }
            pointer.Reset();
            pointer.position = lastPointer;
            hits.Clear();
            system.RaycastAll(pointer, hits);
            if (hits.Count == 0)
            {
                return null;
            }

            // Only the frontmost hit can own hover; modal shades and disabled controls block those behind them.
            var artwork = hits[0].gameObject.GetComponent<DojoHoverGraphic>();
            if (!artwork)
            {
                return null;
            }
            var control = artwork.control;
            if (!CanHover(control))
            {
                return null;
            }
            DojoHoverGraphic.Collect(control, candidates);
            foreach (var candidate in candidates)
            {
                if (candidate.TryGetBounds(out _))
                {
                    return control;
                }
            }
            return null;
        }

        private static bool CanHover(Selectable control)
        {
            return control && control.isActiveAndEnabled && control.IsInteractable();
        }

        private void BeginHover(Selectable control)
        {
            var state = hoverStates[0];
            foreach (var item in hoverStates)
            {
                if (item.control == control)
                {
                    state = item;
                    break;
                }
                if (item.emphasis < state.emphasis)
                {
                    state = item;
                }
            }
            ResetHover(state);
            state.control = control;
            DojoHoverGraphic.Collect(control, state.graphics);
            control.GetComponentsInParent(false, state.masks);
            state.circle.gameObject.SetActive(true);
        }

        private void AnimateHover(HoverState state)
        {
            if (!state.control)
            {
                ResetHover(state);
                return;
            }
            if (!CanHover(state.control))
            {
                ResetHover(state);
                return;
            }

            bool entering = state.control == hovered;
            float target = 0;
            if (entering)
            {
                target = 1;
                state.elapsed += Time.unscaledDeltaTime;
            }
            state.emphasis = Mathf.MoveTowards(state.emphasis, target, Time.unscaledDeltaTime / .14f);
            if (state.emphasis == 0)
            {
                ResetHover(state);
                return;
            }

            bool found = false;
            var bounds = new Bounds();
            bool foundCaption = false;
            var captionBounds = new Bounds();
            foreach (var graphic in state.graphics)
            {
                if (!graphic || !graphic.TryGetBounds(out var visible))
                {
                    continue;
                }
                if (!found)
                {
                    bounds = visible;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(visible);
                }
                if (graphic.IsCaption)
                {
                    if (!foundCaption)
                    {
                        captionBounds = visible;
                        foundCaption = true;
                    }
                    else
                    {
                        captionBounds.Encapsulate(visible);
                    }
                }
            }
            if (!found)
            {
                ResetHover(state);
                return;
            }

            float eased = 1 - Mathf.Pow(1 - state.emphasis, 3);
            float scale = 1 + .14f * eased;
            float captionScale = scale;
            var selection = state.control.GetComponent<DojoControlFocus>();
            foreach (var graphic in state.graphics)
            {
                if (graphic)
                {
                    float graphicScale = scale;
                    if (selection && selection.label && graphic.gameObject == selection.label.gameObject)
                    {
                        // These labels already gain 2.5% from selection; keep their combined scale at 1.14x.
                        graphicScale = 1 + (1.14f / 1.025f - 1) * eased;
                        captionScale = graphicScale;
                    }
                    graphic.Present(graphicScale, bounds.center);
                }
            }
            // Encircle the caption instead of including a much larger panel or ink backing.
            if (foundCaption)
            {
                captionBounds.center = bounds.center + (captionBounds.center - bounds.center) * captionScale;
                PlaceCircle(state, captionBounds);
            }
            else
            {
                PlaceCircle(state, bounds);
            }
            float reveal = Mathf.Clamp01(state.elapsed / .22f);
            reveal = 1 - Mathf.Pow(1 - reveal, 2);
            state.circle.Present(reveal, eased,
                2.8f * Mathf.Clamp(state.circle.rectTransform.rect.height * .09f, 3.5f, 8));
        }

        private static void PlaceCircle(HoverState state, Bounds bounds)
        {
            var rect = state.circle.rectTransform;
            var parent = (RectTransform)rect.parent;
            var canvas = state.control.GetComponentInParent<Canvas>();
            Camera camera = null;
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var corner = bounds.min;
                if ((i & 1) != 0)
                {
                    corner.x = bounds.max.x;
                }
                if ((i & 2) != 0)
                {
                    corner.y = bounds.max.y;
                }
                var screen = RectTransformUtility.WorldToScreenPoint(camera, state.control.transform.TransformPoint(corner));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var point);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            rect.anchoredPosition = (min + max) * .5f;
            rect.sizeDelta = Vector2.Scale(max - min, new Vector2(1.14f, 1.24f)) + new Vector2(6, 6);

            var clip = new Rect(0, 0, Screen.width, Screen.height);
            foreach (var mask in state.masks)
            {
                if (!mask || !mask.isActiveAndEnabled)
                {
                    continue;
                }
                var low = RectTransformUtility.WorldToScreenPoint(camera,
                    mask.rectTransform.TransformPoint(mask.rectTransform.rect.min));
                var high = RectTransformUtility.WorldToScreenPoint(camera,
                    mask.rectTransform.TransformPoint(mask.rectTransform.rect.max));
                clip = Rect.MinMaxRect(Mathf.Max(clip.xMin, low.x), Mathf.Max(clip.yMin, low.y),
                    Mathf.Min(clip.xMax, high.x), Mathf.Min(clip.yMax, high.y));
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, clip.min, null, out var clipMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, clip.max, null, out var clipMax);
            state.circle.canvasRenderer.EnableRectClipping(Rect.MinMaxRect(clipMin.x, clipMin.y, clipMax.x, clipMax.y));
        }

        private static void ResetHover(HoverState state)
        {
            foreach (var graphic in state.graphics)
            {
                if (graphic)
                {
                    graphic.Present(1, Vector3.zero);
                }
            }
            state.graphics.Clear();
            state.masks.Clear();
            state.control = null;
            state.emphasis = 0;
            state.elapsed = 0;
            if (state.circle)
            {
                state.circle.Present(0, 0, 0);
                state.circle.gameObject.SetActive(false);
            }
        }

        private void ClearHoverOnSceneChange(Scene previous, Scene next)
        {
            ClearHover();
        }

        private void ClearHover()
        {
            hovered = null;
            if (hoverStates == null)
            {
                return;
            }
            foreach (var state in hoverStates)
            {
                ResetHover(state);
            }
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= ClearHoverOnSceneChange;
            ClearHover();
            HideBorder();
        }
    }
}
