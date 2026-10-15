using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    internal static class AudioTimelineUi
    {
        public static RectTransform Panel(Transform parent, string name, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = tint;
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static Text Label(Transform parent, string name, string caption,
            float x, float y, float width, float height = 28)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, width, height);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.color = new Color(0.84f, 0.88f, 0.93f);
            text.text = caption;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, string name, string caption,
            float x, float y, float width, UnityAction action)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, width, 30);
            Style(go);
            var text = go.GetComponentInChildren<Text>();
            text.text = caption;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        public static InputField Field(Transform parent, string name, string caption,
            float x, float y, float width, string value)
        {
            Label(parent, name + " Label", caption, x, y, width, 19).fontSize = 12;
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y + 19, width, 30);
            Style(go);
            var field = go.GetComponent<InputField>();
            field.text = value;
            return field;
        }

        public static Dropdown Dropdown(Transform parent, string name, float x, float y,
            float width, string[] options, UnityAction<int> action)
        {
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, width, 30);
            Style(go);
            var dropdown = go.GetComponent<Dropdown>();
            var arrow = go.transform.Find("Arrow");
            if (arrow)
            {
                arrow.GetComponent<Image>().enabled = false;
                var glyph = Label(arrow, "Arrow Glyph", "▾", 0, 0, 20, 20);
                glyph.fontSize = 14;
                glyph.text = "▾";
                glyph.alignment = TextAnchor.MiddleCenter;
                glyph.raycastTarget = false;
                glyph.rectTransform.anchorMin = Vector2.zero;
                glyph.rectTransform.anchorMax = Vector2.one;
                glyph.rectTransform.offsetMin = Vector2.zero;
                glyph.rectTransform.offsetMax = Vector2.zero;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.onValueChanged.AddListener(action);
            return dropdown;
        }

        private static void Style(GameObject go)
        {
            foreach (var image in go.GetComponentsInChildren<Image>(true))
            {
                image.color = new Color(0.18f, 0.23f, 0.3f);
                if (image.name == "Handle" || image.name == "Item Checkmark")
                {
                    image.color = new Color(0.4f, 0.65f, 0.8f);
                }
            }

            foreach (var text in go.GetComponentsInChildren<Text>(true))
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = new Color(0.9f, 0.94f, 1);
            }
        }

        public static AudioTimelineGraphic Wave(Transform parent, string name,
            float x, float y, float height, ChartAudioTimeline owner, bool overview)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.offsetMin = new Vector2(x, -y - height);
            rect.offsetMax = new Vector2(-x, -y);
            var graphic = go.AddComponent<AudioTimelineGraphic>();
            graphic.Owner = owner;
            graphic.Overview = overview;
            graphic.InitializeLabels();
            return graphic;
        }
    }

}
