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
            image.sprite = DojoEntranceAssets.Load("TrainingInterior", UiArtworkCrop.Full);
            image.raycastTarget = false;
            var fit = backdrop.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            view.transform.Find("Modal Shade").GetComponent<Image>().color = new Color(.075f, .035f, .02f, .62f);
            var popup = view.transform.Find("Settings Popup");
            var popupFrame = PixelSongSelectionStyle.Frame((RectTransform)popup, Color.white, 24);
            popupFrame.artwork = SettingsArtAssets.Load("SettingsPanel", UiArtworkCrop.Full,
                new Vector4(128, 128, 128, 128));
            popupFrame.RefreshArtwork();
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
                StyleSettingsButton(button);
            }
            view.back.GetComponentInChildren<PixelDojoFrameGraphic>().color = Color.white;
            Button(view.apply, Red, Cream);
            StyleSettingsButton(view.apply);
            foreach (var slider in view.GetComponentsInChildren<Slider>(true))
            {
                StyleSlider(slider);
                StyleSettingsSlider(slider);
            }
            foreach (var dropdown in view.GetComponentsInChildren<Dropdown>(true))
            {
                ApplyDropdownStyle(dropdown);
            }
            foreach (var input in view.GetComponentsInChildren<InputField>(true))
            {
                SetSettingsSurface(input.image);
            }
        }

        public static void StyleKeySettings(SettingsView view)
        {
            if (view.keyRowTemplate)
            {
                Button(view.keyRowTemplate.button, Color.white, Ink);
                StyleSettingsButton(view.keyRowTemplate.button);
            }
            foreach (var button in view.keyBindingButtons)
            {
                Button(button, Color.white, Ink);
                StyleSettingsButton(button);
            }
            foreach (var button in new[] { view.keySettingsTab, view.resetKeys })
            {
                Button(button, Color.white, Ink);
                StyleSettingsButton(button);
            }
            foreach (var text in view.keySettingsPanel.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
            }
        }

        private static void StyleSettingsButton(Button button)
        {
            var frame = button.GetComponentInChildren<PixelDojoFrameGraphic>();
            frame.artwork = SettingsArtAssets.Load("SettingsControl", UiArtworkCrop.Full,
                new Vector4(128, 128, 128, 128));
            frame.borderWidth = 8;
            frame.RefreshArtwork();
        }

        private static void SetSettingsSurface(Image image)
        {
            image.sprite = SettingsArtAssets.Load("SettingsControl", UiArtworkCrop.Full,
                new Vector4(128, 128, 128, 128));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 16;
            image.color = Color.white;
        }

        private static void StyleSettingsSlider(Slider slider)
        {
            var track = slider.transform.Find("Background");
            if (track)
            {
                var image = track.GetComponent<Image>();
                SetSettingsSurface(image);
                image.color = new Color(.32f, .20f, .12f);
            }

            var fill = slider.fillRect.GetComponent<Image>();
            SetSettingsSurface(fill);
            fill.color = new Color(.95f, .62f, .26f);
            SetSettingsSurface((Image)slider.targetGraphic);
        }

        internal static void ApplyDropdownStyle(Dropdown dropdown)
        {
            StyleDropdown(dropdown);
            StyleSettingsDropdown(dropdown);
        }

        private static void StyleSettingsDropdown(Dropdown dropdown)
        {
            SetSettingsSurface(dropdown.image);
            SetSettingsSurface(dropdown.template.GetComponent<Image>());
            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                var image = toggle.targetGraphic as Image;
                if (image)
                {
                    SetSettingsSurface(image);
                }

                if (toggle.graphic)
                {
                    toggle.graphic.raycastTarget = false;
                    toggle.graphic.gameObject.SetActive(false);
                }
            }

            var arrow = dropdown.transform.Find("Arrow");
            if (arrow)
            {
                arrow.GetComponent<Image>().enabled = false;
            }

            var ruleIndicator = dropdown.transform.Find("Rule Expand Indicator");
            if (ruleIndicator)
            {
                ruleIndicator.gameObject.SetActive(false);
            }

            var indicator = new GameObject("Settings Expand Indicator", typeof(RectTransform));
            indicator.transform.SetParent(dropdown.transform, false);
            var rect = (RectTransform)indicator.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-22f, 0f);
            rect.sizeDelta = new Vector2(16f, 12f);
            for (int i = 0; i < 3; i++)
            {
                var step = new GameObject("Arrow Step " + i, typeof(RectTransform), typeof(Image));
                step.transform.SetParent(indicator.transform, false);
                var stepRect = (RectTransform)step.transform;
                stepRect.anchorMin = stepRect.anchorMax = new Vector2(.5f, .5f);
                stepRect.anchoredPosition = new Vector2(0f, 3f - i * 3f);
                stepRect.sizeDelta = new Vector2(14f - i * 4f, 3f);
                var image = step.GetComponent<Image>();
                image.color = Ink;
                image.raycastTarget = false;
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
