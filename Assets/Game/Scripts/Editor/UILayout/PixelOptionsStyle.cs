using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class PixelOptionsStyle
    {
        private static readonly Color Paper = new Color(.89f, .76f, .55f);
        private static readonly Color Ink = new Color(.24f, .12f, .085f);
        private static readonly Color Cream = new Color(1f, .90f, .70f);
        private static readonly Color Red = new Color(.49f, .14f, .10f);

        public static void Apply(SettingsView view)
        {
            view.pixelTheme = true;
            var backdrop = new GameObject("Pixel Dojo Backdrop", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            view.backdrop = backdrop;
            backdrop.transform.SetParent(view.transform, false);
            backdrop.transform.SetAsFirstSibling();
            Stretch((RectTransform)backdrop.transform);
            var image = backdrop.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/PixelDojo/TitleBackground.asset");
            image.raycastTarget = false;
            var fit = backdrop.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            view.transform.Find("Modal Shade").GetComponent<Image>().color = new Color(.075f, .035f, .02f, .62f);
            var popup = view.transform.Find("Settings Popup");
            PixelSongSelectionStyle.Frame((RectTransform)popup, Paper, 16);
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                text.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
                text.color = Ink;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            var title = popup.Find("Title").GetComponent<Text>();
            title.text = "도장 설정";
            title.fontStyle = FontStyle.Bold;
            popup.Find("Header Line").GetComponent<Image>().color = new Color(.55f, .32f, .16f);
            popup.Find("Tab Divider").GetComponent<Image>().color = new Color(.55f, .32f, .16f);
            foreach (var button in new[] { view.soundTab, view.graphicsTab, view.gameplayTab, view.back })
            {
                Button(button, new Color(.79f, .63f, .42f), Ink);
            }
            Button(view.apply, Red, Cream);
            foreach (var slider in view.GetComponentsInChildren<Slider>(true))
            {
                StyleSlider(slider);
            }
            foreach (var dropdown in view.GetComponentsInChildren<Dropdown>(true))
            {
                StyleDropdown(dropdown);
            }
            foreach (var input in view.GetComponentsInChildren<InputField>(true))
            {
                input.image.color = new Color(.98f, .88f, .68f);
            }
        }

        public static void ApplyQuit(MainMenuView view)
        {
            var dialog = view.quitPopup.transform.Find("Dialog");
            PixelSongSelectionStyle.Frame((RectTransform)dialog, Paper, 14);
            dialog.GetComponent<Outline>().enabled = false;
            view.quitQuestion.color = Ink;
            view.quitQuestion.fontStyle = FontStyle.Bold;
            Button(view.confirmQuit, Red, Cream);
            Button(view.cancelQuit, new Color(.79f, .63f, .42f), Ink);
        }

        public static void ApplySelection(SongSelectionView view)
        {
            view.modePopup.transform.Find("Mode Dialog/Mode Accent").gameObject.SetActive(false);
            foreach (var text in view.modePopup.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            Button(view.normalModeButton, new Color(.48f, .32f, .18f), Cream);
            Button(view.hardModeButton, Red, Cream);
            foreach (var slider in new[] { view.speedSlider, view.timingOffset })
            {
                StyleSlider(slider);
            }
            StyleDropdown(view.sortList);
        }

        private static void StyleSlider(Slider slider)
        {
            var track = slider.transform.Find("Background");
            if (track)
            {
                track.GetComponent<Image>().color = new Color(.35f, .20f, .11f);
            }
            slider.fillRect.GetComponent<Image>().color = new Color(.62f, .30f, .12f);
            slider.targetGraphic.color = new Color(.97f, .74f, .38f);
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .91f, .64f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.76f, .53f, .30f);
            slider.colors = colors;
        }

        private static void StyleDropdown(Dropdown dropdown)
        {
            dropdown.image.color = new Color(.98f, .87f, .66f);
            var colors = dropdown.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .91f, .64f);
            colors.selectedColor = colors.highlightedColor;
            dropdown.colors = colors;
            dropdown.template.GetComponent<Image>().color = Paper;
            foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
            {
                image.color = Paper;
                if (image.name == "Checkmark")
                {
                    image.color = Red;
                }
            }
            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                var toggleColors = toggle.colors;
                toggleColors.normalColor = Color.white;
                toggleColors.highlightedColor = new Color(1f, .83f, .50f);
                toggleColors.selectedColor = toggleColors.highlightedColor;
                toggle.colors = toggleColors;
            }
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            var arrow = dropdown.transform.Find("Arrow");
            if (arrow)
            {
                arrow.GetComponent<Image>().color = Ink;
            }
        }

        private static void Button(Button button, Color fill, Color textColor)
        {
            PixelSongSelectionStyle.Frame((RectTransform)button.transform, fill, 6);
            button.GetComponentInChildren<Text>().color = textColor;
            button.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            var outline = button.GetComponent<Outline>();
            if (outline)
            {
                outline.enabled = false;
            }
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .91f, .70f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.73f, .54f, .34f);
            button.colors = colors;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
