using System;
using RhythmDojo.Dialogue;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class DialogueLayoutBuilder
    {
        public static DialogueView Build(Transform parent)
        {
            var container = Rect("Dialogue", parent, Vector2.zero, Vector2.one);
            var view = container.gameObject.AddComponent<DialogueView>();
            var root = Rect("Dialogue Overlay", container, Vector2.zero, Vector2.one);
            view.root = root.gameObject;
            view.group = root.gameObject.AddComponent<CanvasGroup>();
            var blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(.035f, .045f, .04f, .24f);

            view.actor = Rect("Senior Student", root, new Vector2(.025f, .035f), new Vector2(.385f, .97f));
            view.previousPortrait = Portrait("Previous Expression", view.actor);
            view.portrait = Portrait("Expression", view.actor);
            var names = Enum.GetNames(typeof(DialogueExpression));
            view.expressions = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                view.expressions[i] = SceneResources.Require<Sprite>(
                    "Assets/Game/Art/AlphaDojo/Portraits/SeniorStudent" + names[i] + ".png");
            }
            view.portrait.sprite = view.expressions[0];
            view.previousPortrait.sprite = view.expressions[0];

            var caption = Label("Story Caption", root, "사형의 가르침", .48f, .72f, .92f, .85f, 54);
            caption.font = SceneResources.Require<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            caption.color = new Color(1, .94f, .79f);
            UiFontSettings.Apply(caption);
            Ink("Caption Ink", caption.rectTransform, new Color(.06f, .09f, .08f, .9f));
            caption.transform.SetAsLastSibling();
            Label("Story Subtitle", root, "비급 전수   /   마음을 가다듬고 귀를 기울여라",
                .5f, .67f, .94f, .73f, 18).color = new Color(.20f, .13f, .08f);

            view.paper = Rect("Open Manuscript", root, new Vector2(.315f, .055f), new Vector2(.97f, .43f));
            var shadow = Rect("Paper Shadow", view.paper, new Vector2(-.012f, -.035f), new Vector2(1.012f, .97f))
                .gameObject.AddComponent<DialoguePaperGraphic>();
            shadow.color = new Color(.06f, .065f, .05f, .65f);
            shadow.raycastTarget = false;
            var sheet = Rect("Hanji", view.paper, Vector2.zero, Vector2.one).gameObject.AddComponent<DialoguePaperGraphic>();
            sheet.paperTexture = SceneResources.Require<Texture2D>("Assets/Game/Art/DojoGameplay/PaperPanel.png");
            sheet.color = new Color(1, .98f, .92f);
            sheet.raycastTarget = false;
            var rule = Rect("Ink Rule", view.paper, new Vector2(.06f, .69f), new Vector2(.94f, .715f))
                .gameObject.AddComponent<DojoInkGraphic>();
            rule.color = new Color(.26f, .19f, .10f, .6f);
            rule.raycastTarget = false;

            view.speaker = Label("Speaker", view.paper, "사형", .07f, .73f, .55f, .98f, 46);
            view.speaker.font = caption.font;
            UiFontSettings.Apply(view.speaker);
            var seal = Label("Transmission Seal", view.paper, "전\n수", .015f, .70f, .055f, .96f, 17);
            seal.color = new Color(.62f, .14f, .10f);
            seal.alignment = TextAnchor.MiddleCenter;
            view.counter = Label("Line Count", view.paper, "01 / 03", .79f, .77f, .94f, .92f, 17);
            view.counter.alignment = TextAnchor.MiddleRight;
            view.body = Label("Dialogue Text", view.paper, "", .07f, .25f, .94f, .68f, 28);
            view.body.supportRichText = false;
            view.body.lineSpacing = 1.18f;
            view.body.alignment = TextAnchor.UpperLeft;
            view.hint = Label("Continue Hint", view.paper, "", .07f, .07f, .68f, .22f, 16);
            view.hint.color = new Color(.34f, .27f, .18f);

            view.advance = Control("Advance Dialogue", view.paper, "", Vector2.zero, Vector2.one);
            view.advance.transform.SetAsLastSibling();
            view.skip = Control("Skip Read Dialogue", view.paper, "읽은 대사 건너뛰기 · Tab",
                new Vector2(.68f, .055f), new Vector2(.965f, .23f));
            var skipText = view.skip.GetComponentInChildren<Text>();
            skipText.fontSize = 15;
            skipText.color = new Color(.35f, .17f, .12f);
            view.skip.targetGraphic = skipText;
            var colors = view.skip.colors;
            colors.disabledColor = new Color(.35f, .35f, .35f, .65f);
            view.skip.colors = colors;

            root.gameObject.SetActive(false);
            return view;
        }

        private static Image Portrait(string name, Transform parent)
        {
            var image = Rect(name, parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Ink(string name, RectTransform parent, Color color)
        {
            var ink = Rect(name, parent.parent, parent.anchorMin, parent.anchorMax).gameObject.AddComponent<DojoInkGraphic>();
            ink.contrastStroke = true;
            ink.color = color;
            ink.raycastTarget = false;
            DojoInkGraphic.FitInk(ink.rectTransform, parent.GetComponent<Text>(), 0, 20, 10);
        }

        private static Button Control(string name, Transform parent, string caption, Vector2 minimum, Vector2 maximum)
        {
            var button = UiElements.Button(name, parent, caption, Vector2.zero, Vector2.zero);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.image.color = Color.clear;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static Text Label(string name, Transform parent, string text,
            float left, float bottom, float right, float top, int size)
        {
            var label = UiElements.Label(name, parent, text, Vector2.zero, Vector2.zero, size);
            label.rectTransform.anchorMin = new Vector2(left, bottom);
            label.rectTransform.anchorMax = new Vector2(right, top);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.color = new Color(.15f, .105f, .065f);
            label.alignment = TextAnchor.MiddleLeft;
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
    }
}
