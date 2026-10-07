using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;
using RhythmDojo.Gameplay;

namespace RhythmDojo.EditorTools
{
    public static class SongSelectionLayoutBuilder
    {
        private static readonly Color Background = new Color(.025f, .035f, .045f);
        private static readonly Color PanelColor = new Color(.055f, .068f, .08f, .98f);
        private static readonly Color Line = new Color(.46f, .55f, .57f, .24f);
        private static Font font;
        public static SongSelectionView Build()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            if (!font) throw new InvalidOperationException("Korean font missing.");
            var canvas = UiElements.Canvas("Song Selection");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<SongSelectionView>();
            var surface = GradientPanel("Background", canvas.transform, new Color(.025f, .035f, .045f), new Color(.065f, .045f, .055f), 0, 0, 1, 1);
            var glow = GradientPanel("Ambient Light", surface, new Color(.94f, .22f, .24f, .035f), new Color(.15f, .37f, .35f, 0), .13f, .075f, .93f, .9f);
            glow.localRotation = Quaternion.Euler(0, 0, -14);
            var header = Panel("Header", surface, new Color(.035f, .045f, .055f, 1f), 0, .92f, 1, 1);
            Label("Brand", header, "RHYTHM DOJO  /  리듬 도장", .016f, 0, .34f, 1, 25);
            Panel("Header Accent", header, new Color(.94f,.25f,.26f), .025f, 0, .325f, .035f);

            Label("Section", header, "곡 선택  /  MUSIC SELECT", .355f, 0, .75f, 1, 23);
            Label("Mode", header, "4 KEY  ·  FREE PLAY", .78f, 0, .985f, 1, 20);
            var footer = Panel("Footer", surface, new Color(.025f, .035f, .045f, 1f), 0, 0, 1, .075f);
            var left = Panel("Song Information", surface, PanelColor, .025f, .095f, .325f, .89f);
            var right = Panel("Song Browser", surface, new Color(.045f, .055f, .065f, .82f), .355f, .095f, .975f, .89f);
            view.songTitle = Label("Song Title", left, "곡을 선택하세요", 0, .925f, 1, 1, 32);
            view.artistBpm = Label("Artist and BPM", left, "", 0, .865f, 1, .925f, 20);
            var artwork = Panel("Album Frame", left, Color.black, 0, .48f, 1, .865f);
            artwork.gameObject.AddComponent<RectMask2D>();
            var album = Panel("Album", artwork, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            var fitter = album.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio = 1;
            album.raycastTarget = false; view.album = album;
            view.fallbackCover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/DemoCover.png");
            view.album.sprite = view.fallbackCover;
            Label("Difficulty Caption", left, "TRAINING LEVEL / 난이도", 0, .445f, 1, .48f, 16).verticalOverflow = VerticalWrapMode.Overflow;
            view.difficultyList = Dropdown("Judgment difficulty", left, .04f, .36f, .96f, .442f);
            // Preserve the existing Dropdown and its value/event contract as the selection source.
            view.difficultyList.enabled = false;
            view.difficultyList.GetComponent<Image>().enabled = false;
            foreach (var text in view.difficultyList.GetComponentsInChildren<Text>()) text.enabled = false;
            var profiles = SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath).difficulties;
            view.difficultyButtons = new Button[profiles.Length];
            view.difficultyIndicators = new GameObject[profiles.Length];
            for (int i = 0; i < profiles.Length; i++)
            {
                float width = .92f / profiles.Length;
                var button = Button("Difficulty " + profiles[i].name, left, profiles[i].DisplayName,
                    .04f + i * width + .003f, .36f, .04f + (i + 1) * width - .003f, .442f);
                view.difficultyButtons[i] = button;
                var caption = button.GetComponentInChildren<Text>(); caption.fontSize = 20;
                Bounds(caption.rectTransform, 0, .15f, 1, .9f);
                caption.rectTransform.offsetMin = new Vector2(3, 2); caption.rectTransform.offsetMax = new Vector2(-3, -2);
                Panel("Difficulty Accent", button.transform, SongSelectionView.DifficultyColor(i), 0, .93f, 1, 1);
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = SongSelectionView.DifficultyColor(i); outline.effectDistance = new Vector2(2, -2);
                outline.enabled = false;
                var indicator = Panel("Selection Indicator", button.transform, Color.white, .12f, .05f, .88f, .10f);
                view.difficultyIndicators[i] = indicator.gameObject;
                indicator.gameObject.SetActive(false);
            }
            view.record = Label("Best Record", left, "기록 없음", 0, .20f, 1, .355f, 21);
            Label("Options Caption", left, "PLAY OPTIONS / 옵션", 0, .165f, 1, .20f, 16).verticalOverflow = VerticalWrapMode.Overflow;
            // Retain existing field names and programmatic value contracts without exposing the old controls.
            view.multiplierList = Dropdown("Scroll multiplier", left, .2f, .078f, .47f, .115f);
            view.scrollModeList = Dropdown("Scroll mode", left, .5f, .078f, .96f, .115f);
            view.multiplierList.gameObject.SetActive(false); view.scrollModeList.gameObject.SetActive(false);
            view.speedLabel = Label("Speed Caption", left, "속도  1.0x", 0, .108f, 1, .165f, 18);
            var speedObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            speedObject.name = "Note Speed"; speedObject.transform.SetParent(left, false);
            Bounds((RectTransform)speedObject.transform, .04f, .083f, .96f, .105f);
            var originalSlider = speedObject.GetComponent<Slider>();
            var speedFill = originalSlider.fillRect; var speedHandle = originalSlider.handleRect;
            var speedGraphic = originalSlider.targetGraphic;
            UnityEngine.Object.DestroyImmediate(originalSlider);
            view.speedSlider = speedObject.AddComponent<SteppedSpeedSlider>();
            view.speedSlider.fillRect = speedFill; view.speedSlider.handleRect = speedHandle;
            view.speedSlider.targetGraphic = speedGraphic;
            view.speedSlider.minValue = 1; view.speedSlider.maxValue = 10;
            foreach (var image in speedObject.GetComponentsInChildren<Image>()) image.color = Line;
            view.speedSlider.targetGraphic.color = new Color(.55f, .82f, .79f);
            view.speedSlider.fillRect.GetComponent<Image>().color = new Color(.25f, .43f, .44f);
            view.timingLabel = Label("Timing Caption", left, "입력 타이밍 보정  0 ms", 0, .03f, 1, .079f, 18);
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "Input Timing Offset"; sliderObject.transform.SetParent(left, false);
            Bounds((RectTransform)sliderObject.transform, .04f, .008f, .96f, .029f);
            view.timingOffset = sliderObject.GetComponent<Slider>();
            view.timingOffset.minValue = -200; view.timingOffset.maxValue = 200; view.timingOffset.wholeNumbers = true;
            foreach (var image in sliderObject.GetComponentsInChildren<Image>()) image.color = Line;
            view.timingOffset.targetGraphic.color = new Color(.55f,.82f,.79f);
            view.timingOffset.fillRect.GetComponent<Image>().color = new Color(.25f,.43f,.44f);
            view.sortList = Dropdown("Sort", right, .52f, .921f, .985f, .985f);
            view.settingsButton = Button("Settings", footer, "설정", .725f, .16f, .815f, .84f);
            var iconObject = new GameObject("Settings Icon", typeof(RectTransform));
            iconObject.transform.SetParent(view.settingsButton.transform, false);
            Bounds((RectTransform)iconObject.transform, .06f, .15f, .32f, .85f);
            var icon = iconObject.AddComponent<MainMenuIcon>();
            icon.kind = MainMenuIcon.IconKind.Settings; icon.color = Color.white; icon.raycastTarget = false;
            Bounds(view.settingsButton.GetComponentInChildren<Text>().rectTransform, .32f, 0, 1, 1);
            view.allTab = Button("All Songs", right, "전체 곡", .213f, .921f, .405f, .985f);
            view.favoritesTab = Button("Favorites", right, "★ 즐겨찾기", .015f, .921f, .205f, .985f);
            var columns = Panel("List Header", right, new Color(.025f, .035f, .045f, .9f), .015f, .858f, .985f, .909f);
            Label("Song Column", columns, "TITLE / COMPOSER", .075f, 0, .57f, 1, 16);
            Label("Tempo Column", columns, "BPM", .59f, 0, .76f, 1, 16);
            Label("Difficulty Column", columns, "난이도", .77f, 0, .93f, 1, 16);
            var scrollObject = new GameObject("Song List", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(right, false);
            Bounds((RectTransform)scrollObject.transform, .015f, .025f, .975f, .849f);
            view.scroll = scrollObject.GetComponent<ScrollRect>(); view.scroll.horizontal = false;
            var viewport = Panel("Viewport", scrollObject.transform, new Color(0, 0, 0, 0), 0, 0, 1, 1);
            viewport.gameObject.AddComponent<RectMask2D>(); view.scroll.viewport = viewport;
            var content = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false); view.rowContent = (RectTransform)content.transform;
            view.rowContent.anchorMin = new Vector2(0, 1); view.rowContent.anchorMax = Vector2.one;
            view.rowContent.pivot = new Vector2(.5f, 1); view.rowContent.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.scroll.content = view.rowContent;
            var barObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
            barObject.name = "Song Scrollbar"; barObject.transform.SetParent(right, false);
            Bounds((RectTransform)barObject.transform, .981f, .025f, .99f, .849f);
            var bar = barObject.GetComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
            view.scroll.verticalScrollbar = bar;
            bar.GetComponent<Image>().color = PanelColor;
            bar.targetGraphic.color = new Color(.48f,.56f,.58f);
            view.emptyMessage = Label("Empty List", right, "", .05f, .4f, .95f, .7f, 30);
            view.emptyMessage.alignment = TextAnchor.MiddleCenter;
            var rowButton = Button("Song Row Template", right, "", 0, 0, 1, 1);
            rowButton.GetComponentInChildren<Text>().gameObject.SetActive(false);
            var row = rowButton.gameObject.AddComponent<SongRowView>(); row.select = rowButton;
            row.background = rowButton.GetComponent<Image>();
            var element = row.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 94;
            var coverFrame = Panel("Cover Frame", row.transform, Color.black, .006f, .07f, .077f, .93f);
            coverFrame.gameObject.AddComponent<RectMask2D>();
            row.cover = Panel("Cover", coverFrame, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            var coverFit = row.cover.gameObject.AddComponent<AspectRatioFitter>();
            coverFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; coverFit.aspectRatio = 1;
            row.cover.raycastTarget = false;
            row.title = Label("Title", row.transform, "", .085f, .37f, .59f, .96f, 27);
            row.artist = Label("Artist", row.transform, "", .085f, .05f, .59f, .42f, 18);
            row.artist.color = new Color(.62f, .70f, .72f);
            row.bpm = Label("BPM", row.transform, "", .59f, .12f, .76f, .88f, 23);
            row.difficulty = Label("Difficulty", row.transform, "", .76f, .12f, .92f, .88f, 21);
            row.favorite = Button("Favorite", row.transform, "☆", .928f, .19f, .988f, .81f);
            row.favoriteLabel = row.favorite.GetComponentInChildren<Text>();
            var noNavigation = row.favorite.navigation; noNavigation.mode = Navigation.Mode.None; row.favorite.navigation = noNavigation;
            view.rowTemplate = row; row.gameObject.SetActive(false);
            view.back = Button("Back", footer, "← 뒤로", .025f, .16f, .13f, .84f);
            view.play = Button("Play", footer, "플레이  ▶", .835f, .16f, .975f, .84f);
            view.details = Label("Details", footer, "곡 선택 · Enter 플레이 · Esc 뒤로가기", .15f, .04f, .70f, .96f, 17);
            foreach (float y in new[] { .925f, .865f, .48f, .355f, .20f })
                Panel("Divider", left, Line, 0, y, 1, y).sizeDelta = new Vector2(0, 2);
            var playGraphic = (SelectionGradientImage)view.play.targetGraphic;
            playGraphic.useGradient = true; playGraphic.color = Color.white;
            playGraphic.leftColor = new Color(.82f, .15f, .19f); playGraphic.rightColor = new Color(.55f, .075f, .12f);
            var overlay = Panel("Back Popup", canvas.transform, new Color(0, 0, 0, .8f), 0, 0, 1, 1);
            overlay.GetComponent<Image>().raycastTarget = true;
            view.backPopup = overlay.gameObject;
            var dialog = Panel("Dialog", overlay, PanelColor, .32f, .34f, .68f, .66f);
            Label("Question", dialog, "타이틀 화면으로 돌아갈까요?", .05f, .45f, .95f, .95f, 30);
            view.confirmBack = Button("Confirm Back", dialog, "돌아가기", .07f, .13f, .47f, .36f);
            view.cancelBack = Button("Cancel Back", dialog, "취소", .53f, .13f, .93f, .36f);
            overlay.gameObject.SetActive(false);
            view.Validate(); return view;
        }
        private static RectTransform GradientPanel(string name, Transform parent, Color from, Color to,
            float x0, float y0, float x1, float y1)
        {
            var rect = Panel(name, parent, Color.white, x0, y0, x1, y1);
            var image = rect.GetComponent<SelectionGradientImage>();
            image.useGradient = true; image.leftColor = from; image.rightColor = to;
            return rect;
        }
        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static RectTransform Panel(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(SelectionGradientImage)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)go.transform; Bounds(rect, x0, y0, x1, y1); return rect;
        }
        private static Text Label(string name, Transform parent, string caption, float x0, float y0, float x1, float y1, int size)
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size);
            text.font = font; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            Bounds(text.rectTransform, x0, y0, x1, y1); text.rectTransform.offsetMin = new Vector2(20, 4);
            text.rectTransform.offsetMax = new Vector2(-20, -4); return text;
        }
        private static Button Button(string name, Transform parent, string caption, float x0, float y0, float x1, float y1)
        {
            var rect = Panel(name, parent, new Color(.09f, .115f, .13f, .96f), x0, y0, x1, y1);
            var image = rect.GetComponent<Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Label("Caption", rect, caption, 0, 0, 1, 1, 22);
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.offsetMin = new Vector2(4, 2); text.rectTransform.offsetMax = new Vector2(-4, -2);
            text.color = Color.white;
            return button;
        }
        private static Dropdown Dropdown(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var dropdown = UiElements.Dropdown(name, parent, Vector2.zero, Vector2.zero);
            Bounds((RectTransform)dropdown.transform, x0, y0, x1, y1);
            dropdown.GetComponent<Image>().color = new Color(.09f, .115f, .13f);
            foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
                image.color = new Color(.09f, .115f, .13f);
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            { text.font = font; text.fontSize = 20; text.color = new Color(.9f, .94f, 1);
                text.verticalOverflow = VerticalWrapMode.Overflow; }
            var arrow = dropdown.transform.Find("Arrow");
            if (arrow) arrow.GetComponent<Image>().enabled = false;
            var chevron = UiElements.Label("Expand Indicator", dropdown.transform, "▾", Vector2.zero, Vector2.zero, 18);
            chevron.font = font; chevron.alignment = TextAnchor.MiddleCenter;
            chevron.verticalOverflow = VerticalWrapMode.Overflow;
            Bounds(chevron.rectTransform, .9f, .1f, .98f, .9f);
            return dropdown;
        }
    }
}