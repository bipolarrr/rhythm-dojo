using System;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class SongSelectionLayoutBuilder
    {
        public const string BrushFontPath = MainMenuLayoutBuilder.FontPath;
        private static readonly Color PanelColor = new Color(.10f, .085f, .07f, .98f);
        private static Font font;

        public static SongSelectionView Build()
        {
            ConfigureBrushFont();
            font = AssetDatabase.LoadAssetAtPath<Font>(BrushFontPath);
            if (!font)
            {
                throw new InvalidOperationException("Korean font missing.");
            }
            var canvas = UiElements.Canvas("Song Selection");
            canvas.gameObject.AddComponent<UiFontScope>().font = font;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<SongSelectionView>();
            view.inputGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            view.fallbackCover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/DemoCover.png");
            PracticeSelectionLayoutBuilder.Build(view);
            var practice = view.practice;
            // These references point to the sole visible controls and labels on the authored stage.
            view.play = practice.play;
            view.back = practice.back;
            view.settingsButton = practice.settings;
            view.previewButton = practice.preview;
            view.songTitle = practice.title;
            view.artistBpm = practice.artist;
            view.details = practice.chart;
            view.record = practice.record;
            view.comboRecord = practice.comboRecord;
            view.album = practice.rig.jacket;
            view.difficultyButtons = practice.difficulties;
            view.entrancePanels = new[] { (RectTransform)practice.interfaceGroup.transform };
            view.previewSource = view.gameObject.AddComponent<AudioSource>();
            view.previewSource.playOnAwake = false;
            view.previewSource.spatialBlend = 0;

            var overlay = Panel("Song Library Overlay", canvas.transform, new Color(0, 0, 0, .65f), 0, 0, 1, 1);
            overlay.GetComponent<Image>().raycastTarget = true;
            view.libraryPanel = overlay.gameObject;
            view.libraryGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            var dialog = Panel("Song Library Panel", overlay, PanelColor, .07f, .12f, .93f, .91f);
            dialog.GetComponent<Image>().raycastTarget = true;
            Label("Library Heading", dialog, "곡 목록 · 연습 옵션", .025f, .9f, .8f, .99f, 34, UiTextRole.Title);
            view.closeLibrary = Button("Close Song Library", dialog, "닫기", .87f, .91f, .98f, .98f);
            var right = Panel("Song Browser", dialog, Color.clear, .02f, .04f, .68f, .9f);
            view.allTab = Button("All Songs", right, "전체 곡", .015f, .87f, .22f, .98f);
            view.favoritesTab = Button("Favorites", right, "★ 즐겨찾기", .23f, .87f, .46f, .98f);
            view.sortList = Dropdown("Sort", right, .51f, .87f, .98f, .98f);
            var left = Panel("Selection Options", dialog, new Color(.15f, .12f, .09f), .70f, .07f, .98f, .88f);
            Label("Options Heading", left, "플레이 옵션", .04f, .87f, .96f, .98f, 27);
            view.scrollModeList = Dropdown("Scroll mode", left, .06f, .73f, .94f, .82f);
            view.multiplierList = Dropdown("Scroll multiplier", left, .06f, .59f, .94f, .68f);
            view.speedLabel = Label("Speed Caption", left, "속도", .02f, .45f, .98f, .55f, 24);
            view.speedSlider = BuildSlider("Note Speed", left, .08f, .40f, .92f, .44f, 1, 10, true);
            view.timingLabel = Label("Timing Caption", left, "입력 타이밍 보정", .02f, .25f, .98f, .36f, 21);
            view.timingOffset = BuildSlider("Input Timing Offset", left, .08f, .19f, .92f, .23f, -200, 200, false);
            view.timingOffset.wholeNumbers = true;
            view.modeButton = Button("Mode Switch", left, "일반 · Gauge", .06f, .04f, .94f, .14f);
            view.modeLabel = view.modeButton.GetComponentInChildren<Text>();
            var scrollObject = new GameObject(
                "Song List",
                typeof(RectTransform),
                typeof(ScrollRect)
            );
            scrollObject.transform.SetParent(right, false);
            Bounds((RectTransform)scrollObject.transform, .015f, .06f, .975f, .849f);
            view.scroll = scrollObject.GetComponent<ScrollRect>();
            view.scroll.horizontal = false;

            var viewport = Panel(
                "Viewport",
                scrollObject.transform,
                new Color(0, 0, 0, 0),
                0,
                0,
                1,
                1
            );
            viewport.gameObject.AddComponent<RectMask2D>();
            view.scroll.viewport = viewport;

            var content = new GameObject(
                "Rows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            content.transform.SetParent(viewport, false);
            view.rowContent = (RectTransform)content.transform;
            view.rowContent.anchorMin = new Vector2(0, 1);
            view.rowContent.anchorMax = Vector2.one;
            view.rowContent.pivot = new Vector2(.5f, 1);
            view.rowContent.sizeDelta = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            view.scroll.content = view.rowContent;

            var barObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
            barObject.name = "Song Scrollbar";
            barObject.transform.SetParent(right, false);
            Bounds((RectTransform)barObject.transform, .981f, .06f, .99f, .849f);

            var bar = barObject.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            view.scroll.verticalScrollbar = bar;
            bar.GetComponent<Image>().color = PanelColor;
            bar.targetGraphic.color = new Color(.48f, .56f, .58f);
            view.emptyMessage = Label("Empty List", right, "", .05f, .4f, .95f, .7f, 30);
            view.emptyMessage.alignment = TextAnchor.MiddleCenter;

            var rowButton = Button("Song Row Template", right, "", 0, 0, 1, 1);
            rowButton.GetComponentInChildren<Text>().gameObject.SetActive(false);

            var row = rowButton.gameObject.AddComponent<SongRowView>();
            row.select = rowButton;
            row.background = rowButton.GetComponent<Image>();

            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 94;

            var coverFrame = Panel(
                "Cover Frame",
                row.transform,
                Color.black,
                .006f,
                .07f,
                .077f,
                .93f
            );
            coverFrame.gameObject.AddComponent<RectMask2D>();
            row.cover = Panel("Cover", coverFrame, Color.white, 0, 0, 1, 1).GetComponent<Image>();

            var coverFit = row.cover.gameObject.AddComponent<AspectRatioFitter>();
            coverFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            coverFit.aspectRatio = 1;
            row.cover.raycastTarget = false;
            row.title = Label("Title", row.transform, "", .085f, .37f, .59f, .96f, 27, role: UiTextRole.Title);
            row.artist = Label("Artist", row.transform, "", .085f, .05f, .59f, .42f, 18);
            row.artist.color = new Color(.62f, .70f, .72f);
            row.bpm = Label("BPM", row.transform, "", .59f, .12f, .76f, .88f, 23);
            row.difficulty = Label("Difficulty", row.transform, "", .76f, .12f, .92f, .88f, 21);
            row.favorite = Button("Favorite", row.transform, "☆", .928f, .19f, .988f, .81f);
            row.favoriteLabel = row.favorite.GetComponentInChildren<Text>();

            var noNavigation = row.favorite.navigation;
            noNavigation.mode = Navigation.Mode.None;
            row.favorite.navigation = noNavigation;
            view.rowTemplate = row;
            row.gameObject.SetActive(false);
            overlay.gameObject.SetActive(false);
            var modeOverlay = Panel("Mode Popup", canvas.transform, new Color(0, 0, 0, .8f), 0, 0, 1, 1);
            modeOverlay.GetComponent<Image>().raycastTarget = true;
            view.modePopup = modeOverlay.gameObject;
            var modeDialog = Panel("Mode Dialog", modeOverlay, PanelColor, .32f, .33f, .68f, .67f);
            Label("Mode Title", modeDialog, "모드 선택", .07f, .70f, .80f, .94f, 30, UiTextRole.Title);
            view.normalModeButton = Button("Normal Mode", modeDialog, "일반 · Gauge", .07f, .27f, .47f, .61f);
            view.hardModeButton = Button("Hard Mode", modeDialog, "하드 · Challenge", .53f, .27f, .93f, .61f);
            view.cancelMode = Button("Close Mode", modeDialog, "닫기", .69f, .06f, .93f, .20f);
            modeOverlay.gameObject.SetActive(false);
            UiFontSettings.ApplyTo(view.transform);
            view.Validate();
            return view;
        }

        private static Slider BuildSlider(string name, Transform parent, float x0, float y0,
            float x1, float y1, float minimum, float maximum, bool speed)
        {
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = name;
            sliderObject.transform.SetParent(parent, false);
            Bounds((RectTransform)sliderObject.transform, x0, y0, x1, y1);
            var slider = sliderObject.GetComponent<Slider>();
            if (speed)
            {
                var fill = slider.fillRect;
                var handle = slider.handleRect;
                var graphic = slider.targetGraphic;
                UnityEngine.Object.DestroyImmediate(slider);
                slider = sliderObject.AddComponent<SteppedSpeedSlider>();
                slider.fillRect = fill;
                slider.handleRect = handle;
                slider.targetGraphic = graphic;
            }
            slider.minValue = minimum;
            slider.maxValue = maximum;
            return slider;
        }

        private static void ConfigureBrushFont()
        {
            var importer = AssetImporter.GetAtPath(BrushFontPath) as TrueTypeFontImporter;
            var fallback = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Mona12TextKR.ttf");
            if (!importer || !fallback)
            {
                throw new InvalidOperationException("Selection brush font or fallback missing.");
            }

            bool hasFallback = Array.Exists(importer.fontReferences, item => item == fallback);
            if (importer.includeFontData && hasFallback)
            {
                return;
            }

            importer.includeFontData = true;
            importer.fontNames = new[] { importer.fontTTFName, fallback.fontNames[0] };
            importer.fontReferences = new[] { fallback };
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform Panel(
            string name,
            Transform parent,
            Color color,
            float x0,
            float y0,
            float x1,
            float y1
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(SelectionGradientImage));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;

            var rect = (RectTransform)go.transform;
            Bounds(rect, x0, y0, x1, y1);

            return rect;
        }

        private static Text Label(
            string name,
            Transform parent,
            string caption,
            float x0,
            float y0,
            float x1,
            float y1,
            int size,
            UiTextRole role = UiTextRole.Body
        )
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size, role);
            text.font = font;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Bounds(text.rectTransform, x0, y0, x1, y1);
            text.rectTransform.offsetMin = new Vector2(20, 4);
            text.rectTransform.offsetMax = new Vector2(-20, -4);

            return text;
        }

        private static Button Button(
            string name,
            Transform parent,
            string caption,
            float x0,
            float y0,
            float x1,
            float y1,
            UiTextRole role = UiTextRole.Body
        )
        {
            var rect = Panel(name, parent, new Color(.09f, .115f, .13f, .96f), x0, y0, x1, y1);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = Label("Caption", rect, caption, 0, 0, 1, 1, 22, role);
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.offsetMin = new Vector2(4, 2);
            text.rectTransform.offsetMax = new Vector2(-4, -2);
            text.color = Color.white;

            return button;
        }

        private static Dropdown Dropdown(
            string name,
            Transform parent,
            float x0,
            float y0,
            float x1,
            float y1
        )
        {
            var dropdown = UiElements.Dropdown(name, parent, Vector2.zero, Vector2.zero);
            Bounds((RectTransform)dropdown.transform, x0, y0, x1, y1);
            dropdown.GetComponent<Image>().color = new Color(.09f, .115f, .13f);

            foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
            {
                image.color = new Color(.09f, .115f, .13f);
            }

            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
                text.fontSize = 20;
                text.color = new Color(.9f, .94f, 1);
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            var arrow = dropdown.transform.Find("Arrow");

            if (arrow)
            {
                arrow.GetComponent<Image>().enabled = false;
            }

            var chevron = UiElements.Label(
                "Expand Indicator",
                dropdown.transform,
                "▾",
                Vector2.zero,
                Vector2.zero,
                18
            );
            chevron.font = font;
            chevron.alignment = TextAnchor.MiddleCenter;
            chevron.verticalOverflow = VerticalWrapMode.Overflow;
            Bounds(chevron.rectTransform, .9f, .1f, .98f, .9f);

            return dropdown;
        }
    }
}
