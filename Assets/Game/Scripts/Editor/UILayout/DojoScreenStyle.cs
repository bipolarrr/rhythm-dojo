using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    // Authored surfaces shared by the training HUD, report and settings popup.
    internal static class DojoScreenStyle
    {
        internal static readonly Color Ink = new Color(.19f, .16f, .12f);
        internal static readonly Color Muted = new Color(.42f, .37f, .28f);
        internal static readonly Color Paper = new Color(.94f, .95f, .89f);
        internal static readonly Color Vermilion = new Color(.53f, .17f, .12f);
        internal static readonly Color Cream = new Color(1f, .93f, .77f);

        internal static void PaperFrame(RectTransform rect, float width)
        {
            var frame = PixelSongSelectionStyle.Frame(rect, Paper, width);
            frame.artwork = SettingsArtAssets.Load("SettingsPanel");
            frame.RefreshArtwork();
        }

        internal static Button Action(string name, Transform parent, string caption,
            Vector2 position, Vector2 size, bool primary = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)go.transform, position, size);
            var button = go.GetComponent<Button>();
            var label = UiElements.Label("Text", go.transform, caption, Vector2.zero, size, 28);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(12, 3);
            label.rectTransform.offsetMax = new Vector2(-12, -3);
            label.alignment = TextAnchor.MiddleCenter;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            button.targetGraphic = go.GetComponent<Image>();
            Button(button, primary);
            Heading(label);
            return button;
        }

        internal static void Button(Button button, bool primary = false)
        {
            button.image.sprite = null;
            button.image.color = Color.clear;
            button.transition = Selectable.Transition.None;
            var label = button.GetComponentInChildren<Text>(true);
            var focus = Focus(button, label);
            focus.restingColor = Ink;
            focus.selectedColor = Vermilion;
            if (primary)
            {
                focus.restingColor = Vermilion;
            }
            label.color = focus.restingColor;
        }

        internal static DojoControlFocus Focus(Selectable control, Text label = null)
        {
            var focus = control.GetComponent<DojoControlFocus>();
            if (focus)
            {
                return focus;
            }
            focus = control.gameObject.AddComponent<DojoControlFocus>();
            focus.control = control;
            focus.label = label;
            focus.restingColor = Ink;
            focus.selectedColor = Vermilion;
            var stroke = Rule("Selection Ink", control.transform, Vermilion, Vector2.zero, Vector2.zero);
            stroke.anchorMin = new Vector2(.08f, 0);
            stroke.anchorMax = new Vector2(.92f, 0);
            stroke.pivot = new Vector2(0, .5f);
            stroke.offsetMin = new Vector2(0, -1);
            stroke.offsetMax = new Vector2(0, 5);
            focus.stroke = stroke;
            return focus;
        }

        internal static Dropdown Dropdown(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var dropdown = UiElements.Dropdown(name, parent, position, size);
            var go = dropdown.gameObject;
            var template = dropdown.template;
            var captionText = dropdown.captionText;
            var itemText = dropdown.itemText;
            Object.DestroyImmediate(dropdown);
            dropdown = go.AddComponent<DojoOptionDropdown>();
            dropdown.template = template;
            dropdown.captionText = captionText;
            dropdown.itemText = itemText;
            dropdown.targetGraphic = go.GetComponent<Image>();
            return dropdown;
        }

        internal static void Option(Dropdown dropdown)
        {
            dropdown.image.sprite = null;
            dropdown.image.color = Color.clear;
            dropdown.transition = Selectable.Transition.None;
            Focus(dropdown, dropdown.captionText);
            var rule = Rule("Value Rule", dropdown.transform, new Color(.3f, .25f, .17f, .28f),
                Vector2.zero, Vector2.zero);
            rule.anchorMin = new Vector2(.03f, 0);
            rule.anchorMax = new Vector2(.97f, 0);
            rule.offsetMin = Vector2.zero;
            rule.offsetMax = new Vector2(0, 2);
            var template = dropdown.template.GetComponent<Image>();
            template.sprite = SettingsArtAssets.Load("SettingsPanel");
            template.type = Image.Type.Sliced;
            template.pixelsPerUnitMultiplier = 12;
            template.color = Paper;
            dropdown.captionText.color = Ink;
            dropdown.itemText.color = Ink;
            dropdown.captionText.rectTransform.offsetMin = new Vector2(16, 2);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-34, -2);
            var arrow = dropdown.transform.Find("Arrow").GetComponent<Image>();
            Object.DestroyImmediate(arrow);
            var indicator = dropdown.transform.Find("Arrow").gameObject.AddComponent<Text>();
            SceneResources.ApplyReadableFont(indicator);
            indicator.text = "‹  ›";
            indicator.fontSize = 18;
            indicator.color = Muted;
            indicator.alignment = TextAnchor.MiddleCenter;
            indicator.raycastTarget = false;
            indicator.rectTransform.sizeDelta = new Vector2(32, 24);
            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                var colors = toggle.colors;
                colors.normalColor = Color.clear;
                colors.highlightedColor = new Color(.53f, .17f, .12f, .12f);
                colors.selectedColor = colors.highlightedColor;
                toggle.colors = colors;
                var image = (Image)toggle.targetGraphic;
                image.sprite = null;
                image.color = Color.white;
                var mark = toggle.graphic.rectTransform;
                Object.DestroyImmediate(toggle.graphic);
                mark.sizeDelta = new Vector2(16, 4);
                var ink = mark.gameObject.AddComponent<DojoInkGraphic>();
                ink.color = Vermilion;
                ink.raycastTarget = false;
                toggle.graphic = ink;
            }

            var scrollbar = dropdown.template.GetComponentInChildren<Scrollbar>(true);
            scrollbar.GetComponent<Image>().color = Color.clear;
            scrollbar.transition = Selectable.Transition.None;
            scrollbar.image.sprite = null;
            scrollbar.image.color = Vermilion;
            var area = (RectTransform)scrollbar.handleRect.parent;
            area.anchorMin = new Vector2(.5f, 0);
            area.anchorMax = new Vector2(.5f, 1);
            area.sizeDelta = new Vector2(5, -8);
            scrollbar.handleRect.sizeDelta = new Vector2(0, 8);
        }

        internal static void Slider(Slider slider)
        {
            var hit = slider.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            ReplaceTrack(slider.transform.Find("Background").GetComponent<Image>(), Muted);
            ReplaceTrack(slider.fillRect.GetComponent<Image>(), Vermilion);
            var fillArea = (RectTransform)slider.fillRect.parent;
            fillArea.anchorMin = new Vector2(0, .5f);
            fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(fillArea.sizeDelta.x, 5);
            fillArea.anchoredPosition = new Vector2(fillArea.anchoredPosition.x, 0);
            var handle = (Image)slider.targetGraphic;
            var handleArea = (RectTransform)handle.rectTransform.parent;
            handleArea.anchorMin = new Vector2(0, .5f);
            handleArea.anchorMax = new Vector2(1, .5f);
            handleArea.sizeDelta = new Vector2(handleArea.sizeDelta.x, 12);
            handleArea.anchoredPosition = new Vector2(handleArea.anchoredPosition.x, 0);
            handle.sprite = null;
            handle.color = Vermilion;
            handle.rectTransform.sizeDelta = new Vector2(12, 0);
            handle.rectTransform.anchorMin = new Vector2(handle.rectTransform.anchorMin.x, .25f);
            handle.rectTransform.anchorMax = new Vector2(handle.rectTransform.anchorMax.x, .75f);
            handle.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .75f, .45f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1, 1, 1, .35f);
            slider.colors = colors;
            Focus(slider);
        }

        private static void ReplaceTrack(Image image, Color color)
        {
            var rect = image.rectTransform;
            Object.DestroyImmediate(image);
            var ink = rect.gameObject.AddComponent<DojoInkGraphic>();
            ink.color = color;
            ink.raycastTarget = false;
            rect.anchorMin = new Vector2(rect.anchorMin.x, .4f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, .6f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static void Typography(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var style = text.GetComponent<UiTextStyle>();
                bool heading = style && style.role == UiTextRole.Title;
                var button = text.GetComponentInParent<Button>(true);
                if (button && button.GetComponent<DojoControlFocus>())
                {
                    heading = true;
                }
                if (heading)
                {
                    Heading(text);
                }
                else
                {
                    SceneResources.ApplyReadableFont(text);
                }
            }
        }

        internal static void Heading(Text text)
        {
            var scope = text.GetComponent<UiFontScope>();
            if (!scope)
            {
                scope = text.gameObject.AddComponent<UiFontScope>();
            }
            scope.font = SceneResources.Require<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            scope.style = FontStyle.Normal;
            UiFontSettings.Apply(text);
        }

        internal static RectTransform Rule(string name, Transform parent, Color color,
            Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(DojoInkGraphic))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (size.y > size.x)
            {
                UiElements.Position(rect, position, new Vector2(size.y, size.x));
                rect.localRotation = Quaternion.Euler(0, 0, -90);
            }
            else
            {
                UiElements.Position(rect, position, size);
            }
            var ink = rect.GetComponent<DojoInkGraphic>();
            ink.color = color;
            ink.raycastTarget = false;
            return rect;
        }
    }
}
