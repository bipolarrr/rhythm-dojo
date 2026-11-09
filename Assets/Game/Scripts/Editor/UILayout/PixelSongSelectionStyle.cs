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
            if (!view.GetComponent<CanvasGroup>())
            {
                view.gameObject.AddComponent<CanvasGroup>();
            }

            foreach (var panel in view.entrancePanels)
            {
                if (!panel.GetComponent<CanvasGroup>())
                {
                    panel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            surface.GetComponent<Image>().enabled = false;
            surface.Find("Ambient Light").gameObject.SetActive(false);
            var backdrop = new GameObject("Pixel Dojo Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            backdrop.transform.SetParent(surface, false);
            backdrop.transform.SetAsFirstSibling();
            Bounds((RectTransform)backdrop.transform, 0, 0, 1, 1);
            backdrop.transform.localScale = Vector3.one * DojoEntranceView.InteriorScale;
            var image = backdrop.GetComponent<Image>();
            image.sprite = DojoEntranceAssets.Load("SelectionBackground");
            image.raycastTarget = false;
            var fit = backdrop.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;

            foreach (var label in view.GetComponentsInChildren<Text>(true))
            {
                label.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
                label.color = Ink;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            Bounds((RectTransform)header, .045f, .865f, .47f, .965f);
            foreach (Transform child in header)
            {
                child.gameObject.SetActive(false);
            }
            Frame((RectTransform)header, Paper, 12);
            var heading = UiElements.Label("Pixel Heading", header, "곡 선택", Vector2.zero, Vector2.zero, 48, role: UiTextRole.Title);
            heading.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            heading.fontStyle = FontStyle.Bold;
            heading.color = Ink;
            heading.alignment = TextAnchor.MiddleCenter;
            EmbossText(heading);
            Bounds(heading.rectTransform, 0, 0, 1, 1);

            // Retain view contracts; song information moves to the right, browser to the left.
            Bounds((RectTransform)left, .49f, .145f, .955f, .85f);
            Frame((RectTransform)left, Paper, 20);
            foreach (Transform child in left)
            {
                if (child.name == "Divider")
                {
                    child.gameObject.SetActive(false);
                }
            }
            Bounds((RectTransform)view.album.transform.parent, .055f, .51f, .41f, .935f);
            Frame((RectTransform)view.album.transform.parent, new Color(.25f, .13f, .09f), 8);
            var interior = new GameObject("Album Interior", typeof(RectTransform), typeof(RectMask2D));
            interior.transform.SetParent(view.album.transform.parent, false);
            Bounds((RectTransform)interior.transform, .035f, .035f, .965f, .965f);
            view.album.transform.SetParent(interior.transform, false);
            Bounds(view.songTitle.rectTransform, .445f, .805f, .945f, .955f);
            view.songTitle.fontStyle = FontStyle.Bold;
            view.songTitle.resizeTextForBestFit = true;
            view.songTitle.resizeTextMinSize = 28;
            StyleSongTitle(view.songTitle, 48, 2);
            Bounds(view.artistBpm.rectTransform, .445f, .55f, .945f, .80f);
            view.artistBpm.fontSize = 42;
            view.artistBpm.resizeTextForBestFit = true;
            view.artistBpm.resizeTextMinSize = 34;
            view.artistBpm.resizeTextMaxSize = 42;
            view.artistBpm.lineSpacing = 1.45f;
            view.artistBpm.alignment = TextAnchor.MiddleLeft;
            var caption = left.Find("Difficulty Caption").GetComponent<Text>();
            caption.text = "수련 난이도";
            caption.fontSize = 26;
            caption.color = new Color(.48f, .22f, .10f);
            EmbossText(caption);
            Bounds(caption.rectTransform, .055f, .44f, .95f, .495f);
            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                var button = view.difficultyButtons[i];
                float width = .89f / view.difficultyButtons.Length;
                Bounds((RectTransform)button.transform, .055f + i * width + .005f, .35f, .055f + (i + 1) * width - .005f, .435f);
                Frame((RectTransform)button.transform, Paper, 8);
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = 30;
                text.fontStyle = FontStyle.Bold;
                var accent = button.transform.Find("Difficulty Accent");
                if (accent)
                {
                    accent.gameObject.SetActive(false);
                }
                button.GetComponent<Outline>().effectColor = new Color(1, .56f, .22f);
            }
            Bounds(view.record.rectTransform, .055f, .025f, .435f, .335f);
            view.record.fontSize = 40;
            view.record.resizeTextForBestFit = true;
            view.record.resizeTextMinSize = 34;
            view.record.resizeTextMaxSize = 40;
            view.record.alignment = TextAnchor.UpperLeft;
            view.record.lineSpacing = 1.35f;
            view.comboRecord = UiElements.Label("Combo and Completion Record", left, "", Vector2.zero, Vector2.zero, 36);
            Bounds(view.comboRecord.rectTransform, .495f, .025f, .945f, .335f);
            view.comboRecord.color = Ink;
            view.comboRecord.alignment = TextAnchor.UpperLeft;
            view.comboRecord.resizeTextForBestFit = true;
            view.comboRecord.resizeTextMinSize = 32;
            view.comboRecord.resizeTextMaxSize = 36;
            view.comboRecord.lineSpacing = 1.08f;
            var recordDivider = new GameObject("Record Column Divider", typeof(RectTransform), typeof(Image));
            recordDivider.transform.SetParent(left, false);
            Bounds((RectTransform)recordDivider.transform, .462f, .055f, .465f, .32f);
            recordDivider.GetComponent<Image>().color = new Color(.46f, .25f, .12f, .45f);
            recordDivider.GetComponent<Image>().raycastTarget = false;

            Bounds((RectTransform)right, .045f, .145f, .47f, .85f);
            right.GetComponent<Image>().color = Color.clear;
            right.Find("List Header").gameObject.SetActive(false);
            Bounds((RectTransform)view.scroll.transform, .015f, .005f, .965f, .88f);
            Bounds((RectTransform)view.scroll.verticalScrollbar.transform, .978f, .005f, .995f, .88f);
            Bounds((RectTransform)view.favoritesTab.transform, .015f, .905f, .295f, .99f);
            Bounds((RectTransform)view.allTab.transform, .31f, .905f, .57f, .99f);
            Bounds((RectTransform)view.sortList.transform, .59f, .905f, .965f, .99f);
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
            view.details.fontSize = 20;
            view.details.color = new Color(1, .91f, .74f);
            StyleButton(view.back, new Color(.23f, .13f, .11f));
            StyleButton(view.play, Red);
            StyleButton(view.modeButton, new Color(.25f, .15f, .11f));
            StyleButton(view.settingsButton, new Color(.25f, .15f, .11f));
            view.play.GetComponentInChildren<Text>().text = "입장  ▶";
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
            foreach (var frame in view.GetComponentsInChildren<PixelDojoFrameGraphic>(true))
            {
                frame.artwork = SettingsArtAssets.Load("SettingsControl");
                frame.borderWidth = 12;
            }
            var informationFrame = left.GetComponentInChildren<PixelDojoFrameGraphic>();
            informationFrame.artwork = SettingsArtAssets.Load("SettingsPanel");
            informationFrame.color = Color.white;
            informationFrame.borderWidth = 24;
            header.GetComponentInChildren<PixelDojoFrameGraphic>().color = Color.white;
            foreach (var dialog in new[] { view.backPopup.transform.Find("Dialog"),
                view.modePopup.transform.Find("Mode Dialog") })
            {
                var frame = dialog.GetComponentInChildren<PixelDojoFrameGraphic>();
                frame.artwork = SettingsArtAssets.Load("SettingsPanel");
                frame.color = Color.white;
                frame.borderWidth = 20;
            }
            view.sortList.image.sprite = SettingsArtAssets.Load("SettingsControl");
            view.sortList.image.type = Image.Type.Sliced;
            view.sortList.image.pixelsPerUnitMultiplier = 10;
            view.sortList.image.color = Color.white;
            view.sortList.captionText.fontSize = 26;
            view.sortList.captionText.resizeTextForBestFit = true;
            view.sortList.captionText.resizeTextMinSize = 20;
            view.sortList.captionText.resizeTextMaxSize = 26;
            view.RefreshDifficultyButtons();
            StyleSupportingText(view);
            StyleFooterText(view);
            StyleBackPopupText(view);
            StyleModePopupText(view);
            StyleSortDropdown(view);
        }

        public static void StyleSupportingText(SongSelectionView view)
        {
            var edge = new Color(.045f, .23f, .24f);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Resources/UI/SelectionWoodText.mat");
            if (!material)
            {
                var shader = Shader.Find("RhythmDojo/UI/Selection Wood Text");
                if (!shader)
                {
                    throw new System.InvalidOperationException("Selection wood text shader missing.");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, "Assets/Game/Resources/UI/SelectionWoodText.mat");
            }
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                if (text == view.songTitle || text == view.rowTemplate.title)
                {
                    continue;
                }
                if (text.transform.IsChildOf(view.details.transform.parent))
                {
                    continue;
                }

                var outline = text.GetComponent<Outline>();
                bool styled = text.material == material;
                if (outline && outline.effectColor == edge)
                {
                    styled = true;
                }
                if (!styled)
                {
                    int size = text.fontSize;
                    if (text.resizeTextForBestFit)
                    {
                        size = text.resizeTextMaxSize;
                    }
                    else
                    {
                        text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(size * .85f));
                    }
                    text.fontSize = Mathf.CeilToInt(size * 1.08f);
                    text.resizeTextMaxSize = text.fontSize;
                    text.resizeTextForBestFit = true;
                }

                text.color = new Color(.84f, .66f, .45f);
                text.material = material;
                var shadow = text.GetComponent<Shadow>();
                if (!shadow)
                {
                    shadow = text.gameObject.AddComponent<Shadow>();
                }
                shadow.enabled = true;
                shadow.effectColor = new Color(.20f, .09f, .035f, .85f);
                shadow.effectDistance = new Vector2(1, -2);
                shadow.useGraphicAlpha = true;
                if (!outline)
                {
                    outline = text.gameObject.AddComponent<Outline>();
                }
                outline.enabled = true;
                outline.effectColor = new Color(.34f, .17f, .075f);
                outline.effectDistance = new Vector2(1, -1);
                outline.useGraphicAlpha = true;
            }
        }

        public static void RefineHierarchy(SongSelectionView view)
        {
            view.songTitle.resizeTextMaxSize = 54;
            view.songTitle.fontSize = 54;
            view.artistBpm.resizeTextMinSize = 23;
            view.artistBpm.resizeTextMaxSize = 31;
            view.artistBpm.fontSize = 31;
            view.artistBpm.lineSpacing = 1.18f;
            view.record.resizeTextMinSize = 18;
            view.record.resizeTextMaxSize = 29;
            view.record.fontSize = 29;
            view.record.lineSpacing = 1.12f;
            view.comboRecord.resizeTextMinSize = 18;
            view.comboRecord.resizeTextMaxSize = 27;
            view.comboRecord.fontSize = 27;

            var metadata = new[] { view.artistBpm, view.record, view.comboRecord, view.rowTemplate.artist,
                view.rowTemplate.indexLabel };
            SceneResources.ApplyReadableFont(metadata);
            foreach (var label in metadata)
            {
                label.color = new Color(.28f, .16f, .105f);
                ClearTextEffects(label);
            }

            foreach (var button in view.difficultyButtons)
            {
                var label = button.GetComponentInChildren<Text>(true);
                label.resizeTextMaxSize = 36;
                label.fontSize = 36;
                ClearTextEffects(label);
            }
        }

        private static void ClearTextEffects(Text label)
        {
            label.material = null;
            foreach (var effect in label.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
        }

        public static void StyleFooterText(SongSelectionView view)
        {
            StyleFooterLabel(view.back.GetComponentInChildren<Text>(), 46, 36);
            StyleFooterLabel(view.modeLabel, 42, 28);
            StyleFooterLabel(view.settingsButton.GetComponentInChildren<Text>(), 42, 34);
            StyleFooterLabel(view.play.GetComponentInChildren<Text>(), 58, 48);
            StyleFooterLabel(view.details, 24, 20);

            var backText = view.back.GetComponentInChildren<Text>();
            backText.text = "뒤로";
            Bounds(backText.rectTransform, .34f, 0, .88f, 1);
            StyleFooterArrow(view.back, MainMenuIcon.IconKind.Back, .12f, .34f);
            var playText = view.play.GetComponentInChildren<Text>();
            playText.text = "입장";
            Bounds(playText.rectTransform, .24f, 0, .55f, 1);
            StyleFooterArrow(view.play, MainMenuIcon.IconKind.Play, .59f, .75f);

            var icon = view.settingsButton.GetComponentInChildren<MainMenuIcon>(true);
            if (icon)
            {
                icon.color = Color.white;
            }
        }

        public static void StyleBackPopupText(SongSelectionView view)
        {
            foreach (var button in new[] { view.confirmBack, view.cancelBack })
            {
                var text = button.GetComponentInChildren<Text>(true);
                text.fontSize = 44;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 34;
                text.resizeTextMaxSize = 44;
                text.alignment = TextAnchor.MiddleCenter;
                text.rectTransform.offsetMin = new Vector2(10, 4);
                text.rectTransform.offsetMax = new Vector2(-10, -4);
            }

            var question = view.backPopup.transform.Find("Dialog/Question").GetComponent<Text>();
            question.fontSize = 48;
            question.resizeTextForBestFit = true;
            question.resizeTextMinSize = 30;
            question.resizeTextMaxSize = 48;
            question.alignment = TextAnchor.MiddleCenter;
        }

        public static void StyleSortDropdown(SongSelectionView view)
        {
            var dropdown = view.sortList;
            Bounds((RectTransform)dropdown.transform, .59f, .895f, .985f, .995f);
            dropdown.captionText.fontSize = 36;
            dropdown.captionText.resizeTextForBestFit = true;
            dropdown.captionText.resizeTextMinSize = 30;
            dropdown.captionText.resizeTextMaxSize = 36;
            dropdown.captionText.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText.rectTransform.offsetMin = new Vector2(18, 4);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-42, -4);

            dropdown.itemText.fontSize = 32;
            dropdown.itemText.resizeTextForBestFit = true;
            dropdown.itemText.resizeTextMinSize = 28;
            dropdown.itemText.resizeTextMaxSize = 32;
            dropdown.itemText.alignment = TextAnchor.MiddleLeft;
            dropdown.itemText.rectTransform.offsetMin = new Vector2(30, 4);
            dropdown.itemText.rectTransform.offsetMax = new Vector2(-16, -4);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 184);
            var item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(item.sizeDelta.x, 56);
            var content = (RectTransform)item.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 56);

            var indicator = dropdown.transform.Find("Expand Indicator").GetComponent<Text>();
            indicator.fontSize = 30;
            indicator.resizeTextForBestFit = false;
            StyleSortDropdownSurface(dropdown);
        }

        private static void StyleSortDropdownSurface(Dropdown dropdown)
        {
            var artwork = SettingsArtAssets.Load("SettingsControl");
            var background = dropdown.template.GetComponent<Image>();
            background.sprite = artwork;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = dropdown.image.pixelsPerUnitMultiplier;
            background.color = Color.white;

            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                var image = toggle.targetGraphic as Image;
                if (image)
                {
                    image.sprite = artwork;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = dropdown.image.pixelsPerUnitMultiplier;
                    image.color = Color.white;
                    Bounds(image.rectTransform, 0, 0, 1, 1);
                    image.rectTransform.offsetMin = new Vector2(2, 2);
                    image.rectTransform.offsetMax = new Vector2(-2, -2);
                }

                var colors = dropdown.colors;
                toggle.colors = colors;
                if (toggle.graphic)
                {
                    toggle.graphic.raycastTarget = false;
                    toggle.graphic.gameObject.SetActive(false);
                }
            }
        }

        public static void StyleModePopupText(SongSelectionView view)
        {
            foreach (var button in new[] { view.normalModeButton, view.hardModeButton })
            {
                var text = button.GetComponentInChildren<Text>(true);
                text.fontSize = 52;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 40;
                text.resizeTextMaxSize = 52;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.offsetMin = new Vector2(16, 8);
                text.rectTransform.offsetMax = new Vector2(-16, -8);
            }

            var title = view.modePopup.transform.Find("Mode Dialog/Mode Title").GetComponent<Text>();
            title.fontSize = 48;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 40;
            title.resizeTextMaxSize = 48;

            var close = view.cancelMode.GetComponentInChildren<Text>(true);
            close.fontSize = 34;
            close.resizeTextForBestFit = true;
            close.resizeTextMinSize = 28;
            close.resizeTextMaxSize = 34;
            close.alignment = TextAnchor.MiddleCenter;
        }

        private static void StyleFooterArrow(Button button, MainMenuIcon.IconKind kind, float x0, float x1)
        {
            var arrow = button.transform.Find("Footer Arrow");
            if (!arrow)
            {
                var go = new GameObject("Footer Arrow", typeof(RectTransform), typeof(MainMenuIcon));
                go.transform.SetParent(button.transform, false);
                arrow = go.transform;
            }

            Bounds((RectTransform)arrow, x0, .15f, x1, .85f);
            var icon = arrow.GetComponent<MainMenuIcon>();
            icon.kind = kind;
            icon.color = Color.white;
            icon.raycastTarget = false;
        }

        private static void StyleFooterLabel(Text text, int size, int minimumSize)
        {
            text.color = Color.white;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = size;
            foreach (var effect in text.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
        }

        private static void StyleRow(SongRowView row)
        {
            row.pixelTheme = true;
            row.GetComponent<LayoutElement>().preferredHeight = 132;
            Frame((RectTransform)row.transform, Paper, 10);
            Bounds((RectTransform)row.cover.transform.parent, .095f, .12f, .29f, .88f);
            row.indexLabel = UiElements.Label("Song Number", row.transform, "", Vector2.zero, Vector2.zero, 21);
            row.indexLabel.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            row.indexLabel.color = Ink;
            row.indexLabel.alignment = TextAnchor.MiddleCenter;
            Bounds(row.indexLabel.rectTransform, .02f, .1f, .09f, .9f);
            Bounds(row.title.rectTransform, .32f, .38f, .89f, .88f);
            Bounds(row.artist.rectTransform, .32f, .10f, .88f, .42f);
            row.title.fontStyle = FontStyle.Bold;
            row.title.resizeTextForBestFit = true;
            row.title.resizeTextMinSize = 18;
            StyleSongTitle(row.title, 32, 1);
            row.artist.fontSize = 23;
            row.artist.color = Ink;
            row.artist.color = new Color(.43f, .26f, .16f);
            Bounds(row.bpm.rectTransform, .65f, .10f, .88f, .37f);
            row.bpm.gameObject.SetActive(false);
            row.difficulty.gameObject.SetActive(false);
            Bounds((RectTransform)row.favorite.transform, .89f, .32f, .985f, .78f);
            row.favorite.GetComponent<Image>().color = Color.clear;
            row.favoriteLabel.color = Red;
        }

        public static void StyleSongTitle(Text text, int size, float depth)
        {
            text.fontSize = size;
            text.resizeTextMaxSize = size;
            text.color = new Color(1f, .89f, .63f);
            var shadow = text.GetComponent<Shadow>();
            if (!shadow)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(.18f, .055f, .025f, .95f);
            shadow.effectDistance = new Vector2(depth, -depth * 2);
            shadow.useGraphicAlpha = true;
            var outline = text.GetComponent<Outline>();
            if (!outline)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(.59f, .12f, .065f);
            outline.effectDistance = new Vector2(depth, -depth);
            outline.useGraphicAlpha = true;
        }

        private static void EmbossText(Text text)
        {
            var shadow = text.GetComponent<Shadow>();
            if (!shadow)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(1, .91f, .73f, .65f);
            shadow.effectDistance = new Vector2(1, -1);
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
