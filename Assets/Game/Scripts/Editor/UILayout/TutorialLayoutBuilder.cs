using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class TutorialLayoutBuilder
    {
        public static TutorialView Build(int lessonCount = 7)
        {
            var canvas = UiElements.Canvas("Tutorial Story");
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<TutorialView>();
            var background = Rect("Lesson Room", canvas.transform, Vector2.zero, Vector2.one);
            view.backdrop = background.gameObject.AddComponent<Image>();
            view.backdrop.sprite = DojoRenewalLayoutBuilder.Load("LessonRoom");
            view.backdrop.raycastTarget = false;
            var fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = view.backdrop.sprite.rect.width / view.backdrop.sprite.rect.height;

            // Training controls and the common dialogue overlay are authored by the scene builders.
            view.storyRoot = Rect("Story Stage", canvas.transform, Vector2.zero, Vector2.one);
            var title = Rect("Story Introduction", canvas.transform,
                new Vector2(.5f, .76f), new Vector2(.5f, .76f));
            title.sizeDelta = new Vector2(500, 150);
            view.introduction = title.gameObject.AddComponent<CanvasGroup>();
            view.introduction.blocksRaycasts = false;
            var ink = Rect("Title Ink", title, new Vector2(-.05f, -.1f), new Vector2(1.05f, 1.1f))
                .gameObject.AddComponent<DojoInkGraphic>();
            ink.contrastStroke = true;
            ink.color = new Color(1, .95f, .82f, .88f);
            ink.raycastTarget = false;
            view.heading = UiElements.Label("Story Title", title, "비급 전수", Vector2.zero, Vector2.zero, 96);
            view.heading.rectTransform.pivot = new Vector2(.5f, .5f);
            Stretch(view.heading.rectTransform);
            view.heading.font = SceneResources.Require<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            view.heading.fontStyle = FontStyle.Normal;
            view.heading.color = new Color(.12f, .065f, .04f);
            view.heading.alignment = TextAnchor.MiddleCenter;
            view.heading.raycastTarget = false;
            var scope = title.gameObject.AddComponent<UiFontScope>();
            scope.font = view.heading.font;
            scope.style = FontStyle.Normal;
            scope.useSmoothRendering = true;
            UiFontSettings.Apply(view.heading);
            DojoInkGraphic.FitInk(ink.rectTransform, view.heading, 0, 14, 8);

            view.backButton = UiElements.Button("Return to Modes", canvas.transform,
                "〈  모드 선택", Vector2.zero, Vector2.zero);
            var buttonRect = (RectTransform)view.backButton.transform;
            buttonRect.anchorMin = new Vector2(.035f, .90f);
            buttonRect.anchorMax = new Vector2(.18f, .965f);
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            view.backButton.GetComponent<Image>().color = Color.clear;
            var caption = view.backButton.GetComponentInChildren<Text>();
            view.backLabel = caption;
            caption.color = new Color(1, .95f, .82f);
            caption.fontSize = 21;
            var backing = Rect("Return Ink", buttonRect, Vector2.zero, Vector2.one)
                .gameObject.AddComponent<DojoInkGraphic>();
            backing.transform.SetAsFirstSibling();
            backing.contrastStroke = true;
            backing.color = new Color(.045f, .065f, .07f, .93f);
            backing.raycastTarget = false;
            view.backButton.targetGraphic = caption;
            BuildTraining(view);
            var choices = Rect("Lesson Selection", view.storyRoot, new Vector2(.04f, .12f), new Vector2(.27f, .65f));
            view.lessonButtons = new Button[lessonCount];
            view.lessonLabels = new Text[lessonCount];
            for (int i = 0; i < lessonCount; i++)
            {
                var button = UiElements.Button("Lesson " + i, choices, "", Vector2.zero, Vector2.zero);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(0, 1 - (i + 1f) / lessonCount + .012f);
                rect.anchorMax = new Vector2(1, 1 - i / (float)lessonCount - .012f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                button.GetComponent<Image>().color = new Color(.045f, .065f, .07f, .94f);
                var colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
                view.lessonButtons[i] = button;
                view.lessonLabels[i] = button.GetComponentInChildren<Text>();
                view.lessonLabels[i].fontSize = 23;
                view.lessonLabels[i].color = new Color(1, .95f, .82f);
            }
            view.Validate();
            return view;
        }

        private static void BuildTraining(TutorialView view)
        {
            var panel = Rect("Training", view.storyRoot, new Vector2(.3f, .12f), new Vector2(.96f, .65f));
            panel.gameObject.AddComponent<Image>().color = new Color(.045f, .065f, .07f, .94f);
            view.lessonTitle = TrainingLabel("Lesson Title", panel, .79f, .96f, 30);
            view.progress = TrainingLabel("Progress", panel, .67f, .79f, 20);
            view.instructions = TrainingLabel("Instructions", panel, .36f, .67f, 21);
            view.feedback = TrainingLabel("Feedback", panel, .18f, .36f, 21);
            view.startButton = UiElements.Button("Start Training", panel, "수련 시작", Vector2.zero, Vector2.zero);
            var rect = (RectTransform)view.startButton.transform;
            rect.anchorMin = new Vector2(.31f, .035f);
            rect.anchorMax = new Vector2(.69f, .17f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            view.startLabel = view.startButton.GetComponentInChildren<Text>();
        }

        private static Text TrainingLabel(string name, Transform parent, float bottom, float top, int size)
        {
            var label = UiElements.Label(name, parent, "", Vector2.zero, Vector2.zero, size);
            label.rectTransform.anchorMin = new Vector2(.035f, bottom);
            label.rectTransform.anchorMax = new Vector2(.965f, top);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1, .95f, .82f);
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 minimum, Vector2 maximum)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
