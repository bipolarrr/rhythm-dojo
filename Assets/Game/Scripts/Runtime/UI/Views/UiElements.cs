using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RhythmDojo.UI
{
    public static class UiElements
    {
        public static Canvas Canvas(string name)
        {
            var canvas = new GameObject(name, typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720);
            return canvas;
        }
        public static void EventSystem(UnityEngine.InputSystem.InputActionAsset actions)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
            if (actions) module.actionsAsset = actions;
        }
        public static void Position(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        public static Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize = 22)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = fontSize; label.color = Color.white; label.raycastTarget = false;
            Position(label.rectTransform, position, size); return label;
        }
        private static void Font(GameObject go)
        {
            foreach (var text in go.GetComponentsInChildren<Text>(true))
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public static Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources()); go.name = name;
            go.transform.SetParent(parent, false); Font(go);
            Position((RectTransform)go.transform, position, size);
            go.GetComponentInChildren<Text>().text = caption; return go.GetComponent<Button>();
        }
        public static Dropdown Dropdown(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources()); go.name = name;
            go.transform.SetParent(parent, false); Font(go);
            Position((RectTransform)go.transform, position, size); return go.GetComponent<Dropdown>();
        }
    }
}
