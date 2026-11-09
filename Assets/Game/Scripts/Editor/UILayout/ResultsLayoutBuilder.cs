using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ResultsLayoutBuilder
    {
        private static readonly Color Ink = new Color(.24f, .12f, .085f);
        private static readonly Color Paper = new Color(.89f, .76f, .55f);
        private static readonly Color PanelColor = new Color(.98f, .88f, .68f);
        private static readonly Color BorderColor = new Color(.46f, .25f, .12f);

        public static ResultsView Build()
        {
            var canvas = UiElements.Canvas("Play Results");
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            if (!font)
            {
                throw new System.InvalidOperationException("Results brush font missing.");
            }
            canvas.gameObject.AddComponent<UiFontScope>().font = font;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<ResultsView>();
            view.fallbackCover = DojoEntranceAssets.Load("SelectionBackground");
            view.legacyFallbackCover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/DemoCover.png");
            var surface = Panel("Background", canvas.transform, Color.clear, 0, 0, 1, 1);
            var backdrop = Panel("Dojo Scenery", surface, Color.white, 0, 0, 1, 1);
            var scenery = backdrop.GetComponent<Image>();
            scenery.sprite = DojoEntranceAssets.Load("SelectionBackground");
            var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = scenery.sprite.rect.width / scenery.sprite.rect.height;
            backdrop.localScale = Vector3.one * DojoEntranceView.InteriorScale;
            Panel("Scenery Shade", surface, new Color(.08f, .035f, .02f, .16f), 0, 0, 1, 1);
            var header = Panel("Result Header", surface, Color.clear, .045f, .86f, .955f, .965f);
            TexturedFrame(header, "SettingsControl", 12, Color.white);
            var heading = Label("Heading", header, "수련 결과", .05f, .08f, .95f, .92f, 64);
            heading.alignment = TextAnchor.MiddleCenter;

            view.backButton = Button("Back", surface, "곡 선택", .045f, .03f, .225f, .115f);

            view.backButton.GetComponentInChildren<Text>().fontSize = 56;

            var summary = Panel("Song Summary", surface, PanelColor, .045f, .155f, .69f, .65f);
            Border(summary);
            var album = Panel("Album", summary, Color.clear, .04f, .16f, .36f, .935f);
            TexturedFrame(album, "SettingsControl", 10, new Color(.60f, .43f, .27f));
            var interior = Panel("Album Interior", album, Color.clear, .035f, .035f, .965f, .965f);
            interior.gameObject.AddComponent<RectMask2D>();
            view.cover = Panel("Cover", interior, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            var coverFit = view.cover.gameObject.AddComponent<AspectRatioFitter>();
            coverFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            coverFit.aspectRatio = 1;
            view.cover.enabled = false;
            view.coverPlaceholder = Label("Album Placeholder", album, "앨범", .08f, .1f, .92f, .9f, 48);
            view.coverPlaceholder.alignment = TextAnchor.MiddleCenter;

            view.title = Label("Song Title", summary, "노래 제목", .40f, .77f, .95f, .945f, 68);
            view.artist = Label("Artist", summary, "작곡가", .40f, .65f, .95f, .755f, 46);
            view.difficulty = Label("Difficulty", summary, "", .40f, .535f, .95f, .64f, 44);
            view.perfect = Stat(summary, "Perfect", .40f);
            view.good = Stat(summary, "Good", .27f);
            view.miss = Stat(summary, "Miss", .14f);
            view.accuracy = Label("Accuracy", summary, "정확도  —", .04f, .025f, .49f, .115f, 44);
            view.maxCombo = Label("Max Combo", summary, "최대 콤보  —", .53f, .025f, .96f, .115f, 44);
            Panel("Record Divider", summary, new Color(.46f, .25f, .12f, .35f), .40f, .525f, .95f, .528f);

            view.feedback = Panel("Feedback", surface, PanelColor, .045f, .675f, .955f, .835f);
            Border(view.feedback);
            BuildScorePanel(view);
            view.moreButton = Button("More", surface, "더보기", .73f, .03f, .955f, .115f);
            view.moreButton.GetComponentInChildren<Text>().fontSize = 56;
            var result = Panel("Result Plaque", surface, PanelColor, .715f, .155f, .955f, .65f);
            Border(result);
            var resultCaption = Label("Result Caption", result, "수련 판정", .08f, .85f, .92f, .965f, 46);
            resultCaption.alignment = TextAnchor.MiddleCenter;
            var seal = Panel("Wooden Result Seal", result, Color.white, .10f, .18f, .90f, .85f);
            seal.GetComponent<Image>().sprite = DojoEntranceAssets.Load("RoundPlaque");
            seal.GetComponent<Image>().preserveAspect = true;
            view.outcome = Label("Outcome", result, "플레이 결과", .18f, .255f, .82f, .775f, 220);
            view.outcome.alignment = TextAnchor.MiddleCenter;
            view.outcome.alignByGeometry = true;
            view.outcome.color = new Color(1f, .91f, .69f);
            var outcomeShadow = view.outcome.gameObject.AddComponent<Shadow>();
            outcomeShadow.effectColor = new Color(.12f, .045f, .02f, .95f);
            outcomeShadow.effectDistance = new Vector2(2, -2);
            var resultNote = Label("Result Note", result, "이번 수련의 결과", .08f, .04f, .92f, .15f, 36);
            resultNote.alignment = TextAnchor.MiddleCenter;
            view.progressNote = resultNote;
            Label("Aggregate Help", summary, "전체 판정 집계에는 유지 판정 포함", .04f, .115f, .36f, .16f, 24);
            view.review = BuildReview(surface);
            UiFontSettings.ApplyTo(view.transform);
            RefineScoreHierarchy(view);
            AddFooterArrow(view.backButton, "←", 46, -82, -2);
            AddFooterArrow(view.moreButton, "▶", -44, 71);
            view.Validate();

            return view;
        }

        private static void AddFooterArrow(Button button, string arrow, float labelX, float arrowX, float arrowY = 0)
        {
            var label = button.GetComponentInChildren<Text>();
            var icon = UiElements.Label("Arrow", button.transform, arrow, Vector2.zero, Vector2.zero, label.fontSize);
            icon.color = label.color;
            icon.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 42;
            CenterFooterText(label, labelX, 168);
            CenterFooterText(icon, arrowX, 72, arrowY);
        }

        private static void CenterFooterText(Text text, float x, float width, float y = 0)
        {
            text.alignByGeometry = true;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(.5f, 0);
            rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 0);
        }

        public static void BuildScorePanel(ResultsView view)
        {
            view.scoreSummary = Label("Score Summary", view.feedback, "", .035f, .12f, .71f, .88f, 42);
            view.retryButton = Button("Retry", view.feedback, "다시 하기", .73f, .26f, .97f, .76f);
            view.retryButton.GetComponentInChildren<Text>().fontSize = 40;
            view.retryStatus = Label("Retry Status", view.feedback, "", .68f, .03f, .97f, .25f, 25);
            view.retryStatus.alignment = TextAnchor.MiddleCenter;
        }

        public static void RefineScoreHierarchy(ResultsView view)
        {
            if (!view.scoreValue)
            {
                Label("Score Caption", view.feedback, "이번 점수", .035f, .70f, .36f, .91f, 31);
                view.scoreValue = Label("Score Value", view.feedback, "—", .035f, .12f, .38f, .70f, 86);
                var badge = Panel("New Record Badge", view.feedback, new Color(.26f, .37f, .22f),
                    .39f, .64f, .51f, .87f);
                var badgeText = Label("New Record Text", badge, "신기록", 0, 0, 1, 1, 28);
                badgeText.color = new Color(1f, .95f, .76f);
                badgeText.alignment = TextAnchor.MiddleCenter;
                view.newRecordBadge = badge.gameObject;
                view.newRecordBadge.SetActive(false);
            }

            Place(view.scoreSummary.rectTransform, .39f, .32f, .68f, .65f);
            view.scoreSummary.resizeTextMinSize = 18;
            view.scoreSummary.resizeTextMaxSize = 31;
            var retryFrame = view.retryButton.GetComponentInChildren<PixelDojoFrameGraphic>();
            retryFrame.color = new Color(.57f, .14f, .10f);
            var retryLabel = view.retryButton.GetComponentInChildren<Text>();
            retryLabel.fontSize = 44;
            retryLabel.color = new Color(1f, .92f, .76f);
            view.EnsureScoreActions();
            view.moreButton.GetComponentInChildren<PixelDojoFrameGraphic>().color = Color.white;
            view.moreButton.GetComponentInChildren<Text>().color = Ink;
            SceneResources.ApplyReadableFont(view.scoreValue, view.scoreSummary, view.perfect, view.good,
                view.miss, view.accuracy, view.maxCombo);
        }

        private static ResultsReviewView BuildReview(Transform parent)
        {
            var panel = Panel("Chart Review", parent, Paper, 0, 0, 1, 1);
            Border(panel);
            panel.GetComponent<Image>().raycastTarget = true;
            var view = panel.gameObject.AddComponent<ResultsReviewView>();
            var review = panel.gameObject.AddComponent<ResultsChartReview>();
            SceneDependencyAssembler.Wire(review, "view", view);
            view.heading = Label("Review Heading", panel, "전체 채보 판정", .05f, .89f, .75f, .965f, 38);
            view.closeButton = Button("Close Review", panel, "결과로 돌아가기", .79f, .89f, .95f, .965f);
            view.lanes = Label("Review Lanes", panel, "", .07f, .84f, .63f, .89f, 24);
            var background = Panel("Chart Background", panel, new Color(.10f, .065f, .045f), .07f, .2f, .63f, .83f);
            var chart = new GameObject("Judgment Chart", typeof(RectTransform), typeof(CanvasRenderer));
            chart.transform.SetParent(background, false);
            Place((RectTransform)chart.transform, 0, 0, 1, 1);
            view.chart = chart.AddComponent<ResultsChartGraphic>();
            view.position = Slider("Chart Position", panel, .07f, .13f, .63f, .17f);
            view.range = Label("Review Range", panel, "", .07f, .075f, .63f, .12f, 21);
            Label("Review Legend", panel,
                "<color=#167A68>Perfect</color>  <color=#946218>Good</color>  <color=#B9382A>Miss</color>  <color=#6D6054>미판정</color>  · 롱노트 색상: 시작·끝 중 낮은 판정",
                .07f, .02f, .75f, .07f, 21);
            view.zoom = UiElements.Dropdown("Review Zoom", panel, Vector2.zero, Vector2.zero);
            Place((RectTransform)view.zoom.transform, .65f, .825f, .95f, .885f);
            view.zoom.ClearOptions();
            view.zoom.AddOptions(new System.Collections.Generic.List<string> { "8박씩 보기", "16박씩 보기", "32박씩 보기", "64박씩 보기" });
            view.zoom.value = 1;
            SetWoodSurface(view.zoom.image);
            view.zoom.captionText.color = Ink;
            view.zoom.itemText.color = Ink;
            SetWoodSurface(view.zoom.template.GetComponent<Image>());
            SetWoodSurface(view.zoom.template.Find("Viewport/Content/Item/Item Background").GetComponent<Image>());
            view.zoom.template.Find("Viewport/Content/Item/Item Checkmark").gameObject.SetActive(false);
            view.zoom.transform.Find("Arrow").GetComponent<Image>().color = Ink;
            view.zoom.captionText.fontSize = 28;
            view.zoom.itemText.fontSize = 28;
            view.zoom.captionText.rectTransform.offsetMin = new Vector2(20, 6);
            view.zoom.captionText.rectTransform.offsetMax = new Vector2(-40, -6);
            view.zoom.itemText.rectTransform.offsetMin = new Vector2(20, 6);
            view.zoom.itemText.rectTransform.offsetMax = new Vector2(-20, -6);
            view.zoom.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 248);
            var item = (RectTransform)view.zoom.template.Find("Viewport/Content/Item");
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            ((RectTransform)item.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
            var selected = Panel("Selected Note Frame", panel, PanelColor, .65f, .64f, .95f, .82f);
            Border(selected);
            view.selected = Label("Selected Note", selected, "", .06f, .08f, .94f, .92f, 30);
            view.selected.alignment = TextAnchor.UpperLeft;
            view.selected.resizeTextMinSize = 26;
            var events = Panel("Judgment Events Frame", panel, PanelColor, .65f, .23f, .95f, .62f);
            Border(events);
            view.events = Label("Judgment Events", events, "", .06f, .06f, .94f, .94f, 28);
            view.events.alignment = TextAnchor.UpperLeft;
            view.events.resizeTextMinSize = 24;
            view.previousButton = Button("Previous Note", panel, "이전 노트", .67f, .1f, .81f, .16f);
            view.nextButton = Button("Next Note", panel, "다음 노트", .82f, .1f, .96f, .16f);
            Label("Review Help", panel, "노트 클릭: 상세 판정  /  Escape: 결과로 복귀", .67f, .055f, .97f, .095f, 18);
            view.Validate();
            panel.gameObject.SetActive(false);

            return view;
        }

        private static Slider Slider(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x0, y0, x1, y1);
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0;
            SetWoodSurface(go.transform.Find("Background").GetComponent<Image>());
            go.transform.Find("Background").GetComponent<Image>().color = BorderColor;
            SetWoodSurface(slider.fillRect.GetComponent<Image>());
            slider.fillRect.GetComponent<Image>().color = new Color(.95f, .66f, .28f);
            SetWoodSurface((Image)slider.targetGraphic);

            return slider;
        }

        private static Text Stat(Transform parent, string name, float y)
        {
            var label = Label(name + " Label", parent, name, .40f, y, .72f, y + .12f, 49);
            if (name == "Perfect")
            {
                label.color = new Color(.13f, .37f, .28f);
            }
            else if (name == "Good")
            {
                label.color = new Color(.52f, .30f, .10f);
            }
            else
            {
                label.color = new Color(.57f, .16f, .12f);
            }

            var value = Label(name + " Count", parent, "—", .74f, y, .95f, y + .12f, 54);
            value.alignment = TextAnchor.MiddleRight;

            return value;
        }

        private static RectTransform Panel(
            string name, Transform parent, Color color, float x0, float y0, float x1, float y1
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, x0, y0, x1, y1);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        private static void Border(RectTransform parent)
        {
            TexturedFrame(parent, "SettingsPanel", 20, Color.white);
        }

        private static void TexturedFrame(RectTransform parent, string artwork, float width, Color tint)
        {
            var frame = PixelSongSelectionStyle.Frame(parent, tint, width);
            frame.artwork = SettingsArtAssets.Load(artwork);
        }

        private static void SetWoodSurface(Image image)
        {
            image.sprite = SettingsArtAssets.Load("SettingsControl");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 12;
            image.color = Color.white;
        }

        private static Text Label(
            string name, Transform parent, string text,
            float x0, float y0, float x1, float y1, int size
        )
        {
            var label = UiElements.Label(name, parent, text, Vector2.zero, Vector2.zero, size);
            UiFontSettings.Apply(label);
            label.color = Ink;
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = size;
            Place(label.rectTransform, x0, y0, x1, y1);

            return label;
        }

        private static Button Button(
            string name, Transform parent, string caption, float x0, float y0, float x1, float y1
        )
        {
            var button = UiElements.Button(name, parent, caption, Vector2.zero, Vector2.zero);
            Place((RectTransform)button.transform, x0, y0, x1, y1);
            var image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = Color.white;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .94f, .79f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.74f, .58f, .39f);
            colors.disabledColor = new Color(.64f, .57f, .48f);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>();
            UiFontSettings.Apply(label);
            label.fontSize = 32;
            label.color = Ink;
            TexturedFrame((RectTransform)button.transform, "SettingsControl", 10, Color.white);

            return button;
        }

        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
