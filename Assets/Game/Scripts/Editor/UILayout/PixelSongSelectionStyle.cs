using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class PixelSongSelectionStyle
    {
        private static readonly Color Paper = new Color(.89f, .76f, .55f);
        private static readonly Color Ink = new Color(.24f, .12f, .085f);
        private static readonly Color Red = new Color(.57f, .14f, .10f);

        public static void Apply(SongSelectionView view)
        {
            view.pixelTheme = true;
            var surface = view.transform.Find("Background");
            var header = surface.Find("Header");
            var footer = surface.Find("Footer");
            var left = surface.Find("Song Information");
            var right = surface.Find("Song Browser");
            view.entrancePanels = new[]
            {
                (RectTransform)header,
                (RectTransform)left,
                (RectTransform)right,
                (RectTransform)footer,
                (RectTransform)view.backPopup.transform,
                (RectTransform)view.modePopup.transform
            };
            surface.GetComponent<Image>().enabled = false;
            surface.Find("Ambient Light").gameObject.SetActive(false);
            var backdrop = new GameObject("Pixel Dojo Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            backdrop.transform.SetParent(surface, false);
            backdrop.transform.SetAsFirstSibling();
            Bounds((RectTransform)backdrop.transform, 0, 0, 1, 1);
            backdrop.transform.localScale = Vector3.one * DojoEntranceView.InteriorScale;
            var image = backdrop.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/PixelDojo/TitleBackground.asset");
            image.raycastTarget = false;
            var fit = backdrop.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;

            foreach (var label in view.GetComponentsInChildren<Text>(true))
            {
                label.color = Ink;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
            Bounds((RectTransform)header, .16f, .805f, .415f, .915f);
            foreach (Transform child in header)
            {
                child.gameObject.SetActive(false);
            }
            Frame((RectTransform)header, Paper, 12);
            var heading = UiElements.Label("Pixel Heading", header, "곡 선택", Vector2.zero, Vector2.zero, 43);
            heading.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            heading.fontStyle = FontStyle.Bold;
            heading.color = Ink;
            heading.alignment = TextAnchor.MiddleCenter;
            Bounds(heading.rectTransform, 0, 0, 1, 1);

            // Retain view contracts; song information moves to the right, browser to the left.
            Bounds((RectTransform)left, .45f, .17f, .86f, .815f);
            Frame((RectTransform)left, Paper, 20);
            foreach (Transform child in left)
            {
                if (child.name == "Divider")
                {
                    child.gameObject.SetActive(false);
                }
            }
            Bounds((RectTransform)view.album.transform.parent, .055f, .47f, .435f, .905f);
            Frame((RectTransform)view.album.transform.parent, new Color(.25f, .13f, .09f), 8);
            var interior = new GameObject("Album Interior", typeof(RectTransform), typeof(RectMask2D));
            interior.transform.SetParent(view.album.transform.parent, false);
            Bounds((RectTransform)interior.transform, .035f, .035f, .965f, .965f);
            view.album.transform.SetParent(interior.transform, false);
            Bounds(view.songTitle.rectTransform, .48f, .785f, .95f, .95f);
            view.songTitle.fontStyle = FontStyle.Bold;
            view.songTitle.resizeTextForBestFit = true;
            view.songTitle.resizeTextMinSize = 23;
            view.songTitle.resizeTextMaxSize = 38;
            Bounds(view.artistBpm.rectTransform, .48f, .49f, .95f, .755f);
            view.artistBpm.fontSize = 23;
            view.artistBpm.alignment = TextAnchor.UpperLeft;
            var caption = left.Find("Difficulty Caption").GetComponent<Text>();
            caption.text = "수련 난이도";
            caption.fontSize = 20;
            Bounds(caption.rectTransform, .055f, .415f, .95f, .46f);
            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                var button = view.difficultyButtons[i];
                float width = .89f / view.difficultyButtons.Length;
                Bounds((RectTransform)button.transform, .055f + i * width + .005f, .25f, .055f + (i + 1) * width - .005f, .405f);
                Frame((RectTransform)button.transform, Paper, 8);
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = 27;
                text.fontStyle = FontStyle.Bold;
                var accent = button.transform.Find("Difficulty Accent");
                if (accent)
                {
                    accent.gameObject.SetActive(false);
                }
                button.GetComponent<Outline>().effectColor = new Color(1, .56f, .22f);
            }
            Bounds(view.record.rectTransform, .055f, .035f, .95f, .235f);
            view.record.resizeTextForBestFit = true;
            view.record.resizeTextMinSize = 13;
            view.record.resizeTextMaxSize = 20;

            Bounds((RectTransform)right, .13f, .15f, .435f, .80f);
            right.GetComponent<Image>().color = Color.clear;
            right.Find("List Header").gameObject.SetActive(false);
            Bounds((RectTransform)view.scroll.transform, .015f, .005f, .965f, .905f);
            Bounds((RectTransform)view.scroll.verticalScrollbar.transform, .978f, .005f, .995f, .905f);
            Bounds((RectTransform)view.favoritesTab.transform, .015f, .925f, .295f, .995f);
            Bounds((RectTransform)view.allTab.transform, .31f, .925f, .57f, .995f);
            Bounds((RectTransform)view.sortList.transform, .59f, .925f, .965f, .995f);
            view.rowContent.GetComponent<VerticalLayoutGroup>().spacing = 12;
            view.legacyFallbackCover = view.fallbackCover;
            view.fallbackCover = image.sprite;
            view.rowTemplate.legacyFallbackCover = view.legacyFallbackCover;
            StyleRow(view.rowTemplate);
            StyleButton(view.allTab, Paper);
            StyleButton(view.favoritesTab, Paper);
            view.sortList.GetComponent<Image>().color = Paper;

            Bounds((RectTransform)footer, 0, .015f, 1, .135f);
            footer.GetComponent<Image>().color = Color.clear;
            Bounds((RectTransform)view.back.transform, .035f, .15f, .18f, .83f);
            Bounds((RectTransform)view.play.transform, .71f, .15f, .955f, .94f);
            Bounds((RectTransform)view.modeButton.transform, .43f, .08f, .56f, .67f);
            Bounds((RectTransform)view.settingsButton.transform, .575f, .08f, .67f, .67f);
            Bounds(view.details.rectTransform, .21f, .04f, .405f, .95f);
            view.details.fontSize = 15;
            view.details.color = new Color(1, .91f, .74f);
            StyleButton(view.back, new Color(.23f, .13f, .11f));
            StyleButton(view.play, Red);
            StyleButton(view.modeButton, new Color(.25f, .15f, .11f));
            StyleButton(view.settingsButton, new Color(.25f, .15f, .11f));
            view.play.GetComponentInChildren<Text>().text = "시작  ▶";
            view.play.GetComponentInChildren<Text>().fontSize = 38;
            foreach (var button in new[] { view.play, view.back, view.modeButton, view.settingsButton })
            {
                button.GetComponentInChildren<Text>().color = new Color(1, .92f, .76f);
            }
            view.emptyMessage.color = new Color(1, .92f, .76f);
            foreach (var dropdownImage in view.sortList.template.GetComponentsInChildren<Image>(true))
            {
                dropdownImage.color = Paper;
            }
            Frame((RectTransform)view.backPopup.transform.Find("Dialog"), Paper, 14);
            Frame((RectTransform)view.modePopup.transform.Find("Mode Dialog"), Paper, 14);
            StyleButton(view.confirmBack, Red);
            view.confirmBack.GetComponentInChildren<Text>().color = new Color(1, .92f, .76f);
            StyleButton(view.cancelBack, Paper);
            StyleButton(view.cancelMode, Paper);
            foreach (var button in new[] { view.normalModeButton, view.hardModeButton })
            {
                button.GetComponentInChildren<Text>().color = new Color(1, .92f, .76f);
            }
            PixelOptionsStyle.ApplySelection(view);
            view.RefreshDifficultyButtons();
        }

        private static void StyleRow(SongRowView row)
        {
            row.pixelTheme = true;
            row.GetComponent<LayoutElement>().preferredHeight = 118;
            Frame((RectTransform)row.transform, Paper, 10);
            Bounds((RectTransform)row.cover.transform.parent, .095f, .12f, .29f, .88f);
            row.indexLabel = UiElements.Label("Song Number", row.transform, "", Vector2.zero, Vector2.zero, 21);
            row.indexLabel.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            row.indexLabel.color = Ink;
            row.indexLabel.alignment = TextAnchor.MiddleCenter;
            Bounds(row.indexLabel.rectTransform, .02f, .1f, .09f, .9f);
            Bounds(row.title.rectTransform, .32f, .38f, .89f, .88f);
            Bounds(row.artist.rectTransform, .32f, .10f, .88f, .42f);
            row.title.fontSize = 29;
            row.title.fontStyle = FontStyle.Bold;
            row.title.resizeTextForBestFit = true;
            row.title.resizeTextMinSize = 18;
            row.title.resizeTextMaxSize = 29;
            row.artist.fontSize = 18;
            row.artist.color = Ink;
            row.title.color = Ink;
            Bounds(row.bpm.rectTransform, .65f, .10f, .88f, .37f);
            row.bpm.gameObject.SetActive(false);
            row.difficulty.gameObject.SetActive(false);
            Bounds((RectTransform)row.favorite.transform, .89f, .32f, .985f, .78f);
            row.favorite.GetComponent<Image>().color = Color.clear;
            row.favoriteLabel.color = Red;
        }

        private static void StyleButton(Button button, Color fill)
        {
            Frame((RectTransform)button.transform, fill, 7);
            var text = button.GetComponentInChildren<Text>();
            text.color = Ink;
            text.fontStyle = FontStyle.Bold;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .92f, .72f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.75f, .65f, .5f);
            button.colors = colors;
        }

        public static PixelDojoFrameGraphic Frame(RectTransform parent, Color fill, float width)
        {
            var image = parent.GetComponent<Image>();
            if (image)
            {
                image.color = Color.clear;
                var gradient = image as SelectionGradientImage;
                if (gradient)
                {
                    gradient.useGradient = false;
                }
            }
            var go = new GameObject("Pixel Frame", typeof(RectTransform), typeof(PixelDojoFrameGraphic));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            Bounds((RectTransform)go.transform, 0, 0, 1, 1);
            var frame = go.GetComponent<PixelDojoFrameGraphic>();
            frame.color = fill;
            frame.borderWidth = width;
            frame.raycastTarget = false;
            var button = parent.GetComponent<Button>();
            if (button)
            {
                button.targetGraphic = frame;
            }
            return frame;
        }

        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
