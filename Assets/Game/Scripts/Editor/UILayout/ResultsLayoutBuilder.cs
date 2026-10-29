using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ResultsLayoutBuilder
    {
        private static readonly Color Ink = new Color(.9f, .93f, .97f);
        private static readonly Color Paper = new Color(.055f, .075f, .11f);
        private static readonly Color PanelColor = new Color(.105f, .135f, .18f);
        private static readonly Color BorderColor = new Color(.28f, .36f, .46f);

        public static ResultsView Build()
        {
            var canvas = UiElements.Canvas("Play Results");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<ResultsView>();
            var surface = Panel("Background", canvas.transform, Paper, 0, 0, 1, 1);

            view.backButton = Button("Back", surface, "←  뒤로", .025f, .835f, .135f, .935f);
            Label("Heading", surface, "수련 결과", .81f, .86f, .97f, .95f, 48);

            var summary = Panel("Song Summary", surface, PanelColor, .155f, .435f, .725f, .83f);
            Border(summary);
            var album = Panel("Album", summary, new Color(.15f, .19f, .25f), 0, 0, .435f, 1);
            view.cover = Panel("Cover", album, Color.white, .025f, .025f, .975f, .975f).GetComponent<Image>();
            view.cover.preserveAspect = true;
            view.cover.enabled = false;
            view.coverPlaceholder = Label("Album Placeholder", album, "앨범", .08f, .1f, .92f, .9f, 48);
            view.coverPlaceholder.alignment = TextAnchor.MiddleCenter;

            view.title = Label("Song Title", summary, "노래 제목", .465f, .8f, .97f, .98f, 35);
            view.artist = Label("Artist", summary, "작곡가", .465f, .68f, .97f, .8f, 23);
            view.difficulty = Label("Difficulty", summary, "", .465f, .55f, .97f, .68f, 21);
            view.perfect = Stat(summary, "Perfect", .41f);
            view.good = Stat(summary, "Good", .27f);
            view.miss = Stat(summary, "Miss", .13f);
            view.accuracy = Label("Accuracy", summary, "정확도  —", .465f, .025f, .73f, .12f, 18);
            view.maxCombo = Label("Max Combo", summary, "최대 콤보  —", .735f, .025f, .98f, .12f, 18);

            // Intentionally empty: feedback content and the character will be added later.
            view.feedback = Panel("Feedback", surface, PanelColor, .065f, .065f, .725f, .38f);
            Border(view.feedback);
            view.moreButton = Button("More", surface, "더보기", .555f, .027f, .75f, .115f);
            view.outcome = Label("Outcome", surface, "플레이 결과", .79f, .69f, .975f, .82f, 45);
            view.outcome.alignment = TextAnchor.MiddleCenter;
            Label("Aggregate Help", surface, "전체 판정 집계에는 유지 판정 포함", .155f, .39f, .725f, .435f, 21);
            view.review = BuildReview(surface);
            view.Validate();

            return view;
        }

        private static ResultsReviewView BuildReview(Transform parent)
        {
            var panel = Panel("Chart Review", parent, Paper, 0, 0, 1, 1);
            panel.GetComponent<Image>().raycastTarget = true;
            var view = panel.gameObject.AddComponent<ResultsReviewView>();
            var review = panel.gameObject.AddComponent<ResultsChartReview>();
            SceneDependencyAssembler.Wire(review, "view", view);
            view.heading = Label("Review Heading", panel, "전체 채보 판정", .04f, .91f, .79f, .98f, 32);
            view.closeButton = Button("Close Review", panel, "결과로 돌아가기", .8f, .91f, .97f, .975f);
            view.lanes = Label("Review Lanes", panel, "", .07f, .84f, .63f, .89f, 24);
            var background = Panel("Chart Background", panel, new Color(.09f, .09f, .12f), .07f, .2f, .63f, .83f);
            var chart = new GameObject("Judgment Chart", typeof(RectTransform), typeof(CanvasRenderer));
            chart.transform.SetParent(background, false);
            Place((RectTransform)chart.transform, 0, 0, 1, 1);
            view.chart = chart.AddComponent<ResultsChartGraphic>();
            view.position = Slider("Chart Position", panel, .07f, .13f, .63f, .17f);
            view.range = Label("Review Range", panel, "", .07f, .075f, .63f, .12f, 21);
            Label("Review Legend", panel,
                "<color=#38D4B8>Perfect</color>  <color=#FFC54D>Good</color>  <color=#FF5966>Miss</color>  <color=#808794>미판정</color>  · 롱노트 색상: 시작·끝 중 낮은 판정",
                .07f, .02f, .75f, .07f, 21);
            view.zoom = UiElements.Dropdown("Review Zoom", panel, Vector2.zero, Vector2.zero);
            Place((RectTransform)view.zoom.transform, .67f, .84f, .94f, .89f);
            view.zoom.ClearOptions();
            view.zoom.AddOptions(new System.Collections.Generic.List<string> { "8박씩 보기", "16박씩 보기", "32박씩 보기", "64박씩 보기" });
            view.zoom.value = 1;
            view.zoom.targetGraphic.color = PanelColor;
            view.zoom.captionText.color = Ink;
            view.zoom.itemText.color = Ink;
            view.zoom.template.GetComponent<Image>().color = PanelColor;
            view.zoom.template.Find("Viewport/Content/Item/Item Background").GetComponent<Image>().color = PanelColor;
            view.zoom.template.Find("Viewport/Content/Item/Item Checkmark").GetComponent<Image>().color = Ink;
            view.zoom.transform.Find("Arrow").GetComponent<Image>().color = Ink;
            view.selected = Label("Selected Note", panel, "", .67f, .64f, .96f, .81f, 25);
            view.selected.alignment = TextAnchor.UpperLeft;
            view.events = Label("Judgment Events", panel, "", .67f, .23f, .96f, .62f, 22);
            view.events.alignment = TextAnchor.UpperLeft;
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
            go.transform.Find("Background").GetComponent<Image>().color = PanelColor;
            slider.fillRect.GetComponent<Image>().color = new Color(.35f, .58f, .79f);

            return slider;
        }

        private static Text Stat(Transform parent, string name, float y)
        {
            Label(name + " Label", parent, name, .465f, y, .76f, y + .14f, 29);
            var value = Label(name + " Count", parent, "—", .77f, y, .97f, y + .14f, 29);
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
            Panel("Top Border", parent, BorderColor, 0, .994f, 1, 1);
            Panel("Bottom Border", parent, BorderColor, 0, 0, 1, .006f);
            Panel("Left Border", parent, BorderColor, 0, 0, .002f, 1);
            Panel("Right Border", parent, BorderColor, .998f, 0, 1, 1);
        }

        private static Text Label(
            string name, Transform parent, string text,
            float x0, float y0, float x1, float y1, int size
        )
        {
            var label = UiElements.Label(name, parent, text, Vector2.zero, Vector2.zero, size);
            label.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
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
            colors.normalColor = PanelColor;
            colors.highlightedColor = new Color(.19f, .26f, .35f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.075f, .1f, .14f);
            colors.disabledColor = new Color(.075f, .09f, .12f);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>();
            label.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            label.fontSize = 32;
            label.color = Ink;
            Border((RectTransform)button.transform);

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
