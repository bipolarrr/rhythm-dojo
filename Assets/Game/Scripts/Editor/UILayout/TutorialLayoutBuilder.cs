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
            canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var view = canvas.gameObject.AddComponent<TutorialView>();
            var background = Rect("Lesson Room", canvas.transform, Vector2.zero, Vector2.one);
            view.backdrop = background.gameObject.AddComponent<Image>();
            view.backdrop.sprite = DojoRenewalLayoutBuilder.Load("LessonRoom");
            view.backdrop.raycastTarget = false;
            var fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = view.backdrop.sprite.rect.width / view.backdrop.sprite.rect.height;

            view.storyRoot = Rect("Story Stage", canvas.transform, Vector2.zero, Vector2.one);
            var title = Rect("Story Introduction", canvas.transform,
                new Vector2(.5f, .92f), new Vector2(.5f, .92f));
            title.sizeDelta = new Vector2(380, 100);
            view.introduction = title.gameObject.AddComponent<CanvasGroup>();
            view.introduction.blocksRaycasts = false;
            var ink = Rect("Title Ink", title, new Vector2(-.05f, -.1f), new Vector2(1.05f, 1.1f))
                .gameObject.AddComponent<DojoInkGraphic>();
            ink.contrastStroke = true;
            ink.color = new Color(1, .95f, .82f, .88f);
            ink.raycastTarget = false;
            view.heading = UiElements.Label("Story Title", title, "비급 전수", Vector2.zero, Vector2.zero, 64);
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
            TutorialMapLayoutBuilder.Build(view, lessonCount);
            view.Validate();
            return view;
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
