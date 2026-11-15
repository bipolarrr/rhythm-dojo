using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ResultsLayoutBuilder
    {
        private static readonly Color Ink = DojoScreenStyle.Ink;
        private static readonly Color Paper = new Color(.89f, .76f, .55f);
        private static readonly Color PanelColor = new Color(.98f, .88f, .68f);

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
            view.fallbackCover = DojoRenewalLayoutBuilder.Load("PracticeHall");
            view.legacyFallbackCover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/DemoCover.png");
            var surface = Panel("Background", canvas.transform, Color.clear, 0, 0, 1, 1);
            var backdrop = Panel("Dojo Scenery", surface, Color.white, 0, 0, 1, 1);
            var scenery = backdrop.GetComponent<Image>();
            scenery.sprite = DojoRenewalLayoutBuilder.Load("RecordAlcove");
            var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = scenery.sprite.rect.width / scenery.sprite.rect.height;
            var report = Panel("Training Record Paper", surface, Color.clear, .055f, .15f, .945f, .86f);
            DojoScreenStyle.PaperFrame(report, 18);
            var header = Panel("Result Header", surface, Color.clear, .065f, .875f, .935f, .975f);
            var heading = Label("Heading", header, "수련 결과", .05f, .08f, .95f, .92f, 64, role: UiTextRole.Title);
            heading.color = DojoScreenStyle.Cream;
            heading.alignment = TextAnchor.MiddleLeft;

            var summary = Panel("Song Summary", surface, Color.clear, .075f, .19f, .69f, .635f);
            var album = Panel("Album", summary, Color.white, .035f, .19f, .36f, .935f);
            album.GetComponent<Image>().sprite = DojoGameplayAssets.Wood;
            album.GetComponent<Image>().type = Image.Type.Sliced;
            album.GetComponent<Image>().pixelsPerUnitMultiplier = 12;
            album.GetComponent<Image>().color = new Color(.78f, .83f, .82f);
            var interior = Panel("Album Interior", album, Color.clear, .045f, .045f, .955f, .955f);
            interior.gameObject.AddComponent<RectMask2D>();
            view.cover = Panel("Cover", interior, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            var coverFit = view.cover.gameObject.AddComponent<AspectRatioFitter>();
            coverFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            coverFit.aspectRatio = 1;
            view.cover.enabled = false;
            view.coverPlaceholder = Label("Album Placeholder", album, "앨범", .08f, .1f, .92f, .9f, 48);
            view.coverPlaceholder.alignment = TextAnchor.MiddleCenter;
            view.coverPlaceholder.color = DojoScreenStyle.Cream;

            view.title = Label("Song Title", summary, "노래 제목", .40f, .77f, .95f, .96f, 54, role: UiTextRole.Title);
            view.artist = Label("Artist", summary, "작곡가", .40f, .65f, .95f, .755f, 28);
            view.difficulty = Label("Difficulty", summary, "", .40f, .535f, .95f, .64f, 27);
            view.artist.color = DojoScreenStyle.Muted;
            view.difficulty.color = DojoScreenStyle.Muted;
            view.perfect = Stat(summary, "Perfect", .40f);
            view.good = Stat(summary, "Good", .27f);
            view.miss = Stat(summary, "Miss", .14f);
            view.accuracy = Label("Accuracy", summary, "정확도  —", .04f, .025f, .49f, .115f, 30);
            view.maxCombo = Label("Max Combo", summary, "최대 콤보  —", .53f, .025f, .96f, .115f, 30);
            Panel("Record Divider", summary, new Color(.3f, .25f, .17f, .25f), .40f, .515f, .95f, .518f);
            Panel("Totals Divider", summary, new Color(.3f, .25f, .17f, .25f), .04f, .135f, .95f, .138f);

            view.feedback = Panel("Feedback", surface, Color.clear, .075f, .67f, .925f, .835f);
            var divider = Panel("Score Divider", surface, new Color(.3f, .25f, .17f, .3f), .10f, .65f, .90f, .653f);
            BuildScorePanel(view);
            view.moreButton = Button("More", surface, "더보기", .765f, .045f, .935f, .115f);
            view.moreButton.GetComponentInChildren<Text>().fontSize = 34;
            var result = Panel("Result Plaque", surface, Color.clear, .715f, .22f, .915f, .61f);
            var resultCaption = Label("Result Caption", result, "수련 판정", .08f, .84f, .92f, .97f, 32);
            resultCaption.alignment = TextAnchor.MiddleCenter;
            resultCaption.color = DojoScreenStyle.Muted;
            var seal = Panel("Result Ink Seal", result, Color.clear, .15f, .25f, .85f, .81f);
            seal.GetComponent<Image>().color = new Color(.53f, .17f, .12f, .05f);
            DojoScreenStyle.Rule("Seal Top Stroke", seal, DojoScreenStyle.Vermilion, Vector2.zero, new Vector2(268, 5));
            DojoScreenStyle.Rule("Seal Bottom Stroke", seal, DojoScreenStyle.Vermilion, new Vector2(0, -231), new Vector2(268, 5));
            DojoScreenStyle.Rule("Seal Left Stroke", seal, DojoScreenStyle.Vermilion, Vector2.zero, new Vector2(4, 235));
            DojoScreenStyle.Rule("Seal Right Stroke", seal, DojoScreenStyle.Vermilion, new Vector2(264, 0), new Vector2(4, 235));
            view.outcome = Label("Outcome", result, "", .18f, .285f, .82f, .78f, 190, role: UiTextRole.Title);
            view.outcome.alignment = TextAnchor.MiddleCenter;
            view.outcome.alignByGeometry = true;
            view.outcome.color = DojoScreenStyle.Vermilion;
            var resultNote = Label("Result Note", result, "", .04f, .015f, .96f, .20f, 28, role: UiTextRole.Help);
            resultNote.alignment = TextAnchor.MiddleCenter;
            view.progressNote = resultNote;
            view.review = BuildReview(surface);
            UiFontSettings.ApplyTo(view.transform);
            RefineScoreHierarchy(view);
            DojoScreenStyle.Typography(view.transform);
            SceneResources.ApplyReadableFont(view.scoreValue);
            var help = Label("Navigation Help", surface, "↑↓ / ←→ 이동   ·   Enter / Space 선택   ·   Esc 돌아가기", .065f, .045f, .76f, .10f, 22);
            help.color = new Color(1f, .95f, .82f);
            help.alignment = TextAnchor.MiddleLeft;
            SceneResources.ApplyReadableFont(help);
            foreach (var button in new[] { view.moreButton })
            {
                var focus = button.GetComponent<DojoControlFocus>();
                focus.restingColor = DojoScreenStyle.Cream;
                focus.selectedColor = new Color(1, .77f, .35f);
                focus.stroke.GetComponent<DojoInkGraphic>().color = focus.selectedColor;
            }
            ResultsPresentationBuilder.Build(view, backdrop, report, summary, heading, seal, resultCaption, help, divider);
            view.Validate();

            return view;
        }

        public static void BuildScorePanel(ResultsView view)
        {
            view.scoreSummary = Label("Score Summary", view.feedback, "", .035f, .12f, .71f, .88f, 42);
            view.retryButton = Button("Retry", view.feedback, "다시 하기", .73f, .26f, .97f, .76f, role: UiTextRole.Title);
            view.retryButton.GetComponentInChildren<Text>().fontSize = 40;
            view.retryStatus = Label("Retry Status", view.feedback, "", .68f, .03f, .97f, .25f, 25);
            view.retryStatus.alignment = TextAnchor.MiddleCenter;
        }

        public static void RefineScoreHierarchy(ResultsView view)
        {
            if (!view.scoreValue)
            {
                Label("Score Caption", view.feedback, "이번 점수", .035f, .70f, .36f, .91f, 27).color = DojoScreenStyle.Muted;
                view.scoreValue = Label("Score Value", view.feedback, "—", .035f, .12f, .38f, .70f, 86, role: UiTextRole.Title);
                var badge = Panel("New Record Badge", view.feedback, DojoScreenStyle.Vermilion,
                    .39f, .64f, .51f, .87f);
                var badgeText = Label("New Record Text", badge, "신기록", 0, 0, 1, 1, 28, role: UiTextRole.Title);
                badgeText.color = new Color(1f, .95f, .76f);
                badgeText.alignment = TextAnchor.MiddleCenter;
                view.newRecordBadge = badge.gameObject;
                view.newRecordBadge.SetActive(false);
            }

            Place(view.scoreSummary.rectTransform, .39f, .32f, .68f, .65f);
            view.scoreSummary.resizeTextMinSize = 18;
            view.scoreSummary.resizeTextMaxSize = 31;
            EnsureScoreActions(view);
            DojoScreenStyle.Button(view.retryButton, true);
            DojoScreenStyle.Button(view.recommendationButton);
            view.moreButton.GetComponentInChildren<Text>().color = Ink;
            SceneResources.ApplyReadableFont(view.scoreValue, view.scoreSummary, view.perfect, view.good,
                view.miss, view.accuracy, view.maxCombo);
        }

        private static ResultsReviewView BuildReview(Transform parent)
        {
            var panel = Panel("Chart Review", parent, Paper, 0, 0, 1, 1);
            DojoScreenStyle.PaperFrame(panel, 18);
            panel.GetComponent<Image>().raycastTarget = true;
            var view = panel.gameObject.AddComponent<ResultsReviewView>();
            var review = panel.gameObject.AddComponent<ResultsChartReview>();
            SceneDependencyAssembler.Wire(review, "view", view);
            view.heading = Label("Review Heading", panel, "전체 채보 판정", .05f, .89f, .75f, .965f, 38, role: UiTextRole.Title);
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
                .07f, .02f, .75f, .07f, 21, role: UiTextRole.Help);
            view.zoom = DojoScreenStyle.Dropdown("Review Zoom", panel, Vector2.zero, Vector2.zero);
            Place((RectTransform)view.zoom.transform, .65f, .825f, .95f, .885f);
            view.zoom.ClearOptions();
            view.zoom.AddOptions(new System.Collections.Generic.List<string> { "8박씩 보기", "16박씩 보기", "32박씩 보기", "64박씩 보기" });
            view.zoom.value = 1;
            DojoScreenStyle.Option(view.zoom);
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
            view.selected = Label("Selected Note", selected, "", .06f, .08f, .94f, .92f, 30);
            view.selected.alignment = TextAnchor.UpperLeft;
            view.selected.resizeTextMinSize = 26;
            var events = Panel("Judgment Events Frame", panel, PanelColor, .65f, .23f, .95f, .62f);
            view.events = Label("Judgment Events", events, "", .06f, .06f, .94f, .94f, 28);
            view.events.alignment = TextAnchor.UpperLeft;
            view.events.resizeTextMinSize = 24;
            view.previousButton = Button("Previous Note", panel, "이전 노트", .67f, .1f, .81f, .16f);
            view.nextButton = Button("Next Note", panel, "다음 노트", .82f, .1f, .96f, .16f);
            Label("Review Help", panel, "↑↓ 이동 · ←→ 조절 · Enter / Space 선택 · Esc 복귀",
                .67f, .055f, .97f, .095f, 18, role: UiTextRole.Help);
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
            DojoScreenStyle.Slider(slider);

            return slider;
        }

        private static Text Stat(Transform parent, string name, float y)
        {
            var row = Panel(name + " Row", parent, Color.clear, .40f, y, .95f, y + .12f);
            var label = Label(name + " Label", row, name, 0, 0, .582f, 1, 30);
            if (name == "Perfect")
            {
                label.color = new Color(.19f, .38f, .29f);
            }
            else if (name == "Good")
            {
                label.color = new Color(.49f, .33f, .12f);
            }
            else
            {
                label.color = DojoScreenStyle.Vermilion;
            }

            var value = Label(name + " Count", row, "—", .618f, 0, 1, 1, 36);
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

        private static Text Label(
            string name, Transform parent, string text,
            float x0, float y0, float x1, float y1, int size,
            UiTextRole role = UiTextRole.Body
        )
        {
            var label = UiElements.Label(name, parent, text, Vector2.zero, Vector2.zero, size, role);
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
            string name, Transform parent, string caption, float x0, float y0, float x1, float y1,
            UiTextRole role = UiTextRole.Body
        )
        {
            var button = DojoScreenStyle.Action(name, parent, caption, Vector2.zero, Vector2.zero);
            Place((RectTransform)button.transform, x0, y0, x1, y1);
            button.GetComponentInChildren<Text>().fontSize = 36;

            return button;
        }

        private static void EnsureScoreActions(ResultsView view)
        {
            if (view.recommendationButton)
            {
                return;
            }

            PlaceScoreAction((RectTransform)view.retryButton.transform, new Vector2(.71f, .52f), new Vector2(.97f, .92f));
            PlaceScoreAction(view.retryStatus.rectTransform, new Vector2(.39f, .035f), new Vector2(.69f, .29f));

            view.recommendationButton = Object.Instantiate(view.retryButton, view.feedback);
            view.recommendationButton.name = "Result Timing Recommendation";
            view.recommendationButton.onClick = new Button.ButtonClickedEvent();
            view.recommendationButton.navigation = Navigation.defaultNavigation;
            PlaceScoreAction(
                (RectTransform)view.recommendationButton.transform,
                new Vector2(.71f, .08f),
                new Vector2(.97f, .46f)
            );

            var label = view.recommendationButton.GetComponentInChildren<Text>(true);
            label.color = new Color(.24f, .12f, .085f);
            UiFontSettings.Apply(label, UiTextRole.Title);
            label.text = "";
            label.fontSize = 30;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.resizeTextMaxSize = 30;
            view.recommendationButton.gameObject.SetActive(false);
        }

        private static void PlaceScoreAction(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
