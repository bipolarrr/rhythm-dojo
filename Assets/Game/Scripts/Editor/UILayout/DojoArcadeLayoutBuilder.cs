using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class DojoArcadeLayoutBuilder
    {
        private static readonly Color Paper = new Color(1f, .95f, .82f);
        private static readonly Color Ink = new Color(.10f, .16f, .18f);
        private static readonly Color Vermilion = new Color(.76f, .23f, .15f);

        public static void Build(MainMenuView view, DojoModeView mode, Transform parent)
        {
            var scope = parent.gameObject.AddComponent<UiFontScope>();
            scope.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            scope.useSmoothRendering = true;
            var top = Group("Mode Heading", parent);
            var bottom = Group("Mode Footer", parent);
            mode.typography = new DojoModeTypography[4];
            for (int i = 0; i < mode.typography.Length; i++)
            {
                mode.typography[i] = Composition(top, i, view.title.font);
                mode.typography[i].SetRest(i == 1);
            }
            mode.heading = mode.typography[1].heading;
            mode.description = mode.typography[1].description;
            mode.detail = mode.typography[1].detail;

            // Soft local shading has no hard panel edge; the floor remains visible through the controls.
            var shade = Accent("Floor Contrast", bottom, new Vector2(640, 726),
                new Vector2(1650, 320), new Color(.035f, .055f, .065f, .9f), true);
            shade.transform.SetAsFirstSibling();
            var help = Label("Navigation Help", bottom, "← →  이동     Enter  선택     Esc  대문으로", 16, Paper);
            Place(help.rectTransform, new Vector2(90, 679), new Vector2(460, 26));
            mode.counter = Label("Mode Number", bottom, "02 / 04", 16, Paper);
            Place(mode.counter.rectTransform, new Vector2(594, 679), new Vector2(120, 26));
            mode.counter.alignment = TextAnchor.MiddleCenter;
            mode.selectionMarks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var mark = new GameObject("Mode Beat " + (i + 1), typeof(RectTransform), typeof(Image));
                mark.transform.SetParent(bottom, false);
                var image = mark.GetComponent<Image>();
                image.raycastTarget = false;
                Place(image.rectTransform, new Vector2(601 + i * 31, 658), new Vector2(7, 7));
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                mode.selectionMarks[i] = image;
            }

            StyleButton(view.start, parent, "수련 시작", new Vector2(970, 625), new Vector2(220, 62), 30);
            mode.actionLabel = view.start.GetComponentInChildren<Text>();
            var stroke = Accent("Ready Brush", view.start.transform, new Vector2(0, 54), new Vector2(210, 8), Paper);
            mode.readyMark = stroke.rectTransform;
            var enter = Label("Select Key", view.start.transform, "ENTER   /   선택", 13, Paper);
            Place(enter.rectTransform, new Vector2(0, -15), new Vector2(220, 22));
            enter.alignment = TextAnchor.MiddleCenter;
            StyleButton(view.settings, parent, "설정", new Vector2(1050, 14), new Vector2(82, 44), 18);
            StyleButton(view.quit, parent, "종료", new Vector2(1150, 14), new Vector2(66, 44), 18);
            StyleButton(view.editor, parent, "채보 공방", new Vector2(68, 14), new Vector2(130, 44), 18);
            mode.previous = Control("Previous Mode", parent, "〈", new Vector2(28, 316));
            mode.next = Control("Next Mode", parent, "〉", new Vector2(1188, 316));
            mode.interfaceGroups = new CanvasGroup[8];
            var controls = new[] { top.gameObject, bottom.gameObject, view.start.gameObject,
                view.settings.gameObject, view.quit.gameObject, view.editor.gameObject,
                mode.previous.gameObject, mode.next.gameObject };
            for (int i = 0; i < controls.Length; i++)
            {
                mode.interfaceGroups[i] = controls[i].AddComponent<CanvasGroup>();
            }
        }

        private static DojoModeTypography Composition(Transform parent, int index, Font font)
        {
            var root = Group("Mode Typography " + (index + 1), parent);
            var composition = root.gameObject.AddComponent<DojoModeTypography>();
            var title = Group("Title Motion", root);
            var copy = Group("Description Motion", root);
            composition.titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            composition.copyGroup = copy.gameObject.AddComponent<CanvasGroup>();
            composition.titleGroup.blocksRaycasts = composition.copyGroup.blocksRaycasts = false;
            string caption = "비급 전수";
            string eyebrow = "01    첫 박자를 새기다";
            string description = "처음이지? 내가 옆에서 박자를 짚어 줄게.";
            string detail = "선배 사형의 손짓에 맞춰 네 번 두드려 보세요.";
            var position = new Vector2(380, 87);
            var size = new Vector2(540, 140);
            var copyPosition = new Vector2(395, 229);
            var brushPosition = new Vector2(389, 208);
            var brushSize = new Vector2(350, 11);
            int fontSize = 114;
            Color color = Ink;
            Color accent = Vermilion;
            switch (index)
            {
                case 1:
                    caption = "자유\n연습";
                    eyebrow = "02    나만의 박자로";
                    description = "자유롭게 연습하자!";
                    detail = "곡과 난이도를 골라, 나만의 리듬을 완성하세요.";
                    position = new Vector2(118, 112);
                    size = new Vector2(380, 250);
                    copyPosition = new Vector2(134, 394);
                    brushPosition = new Vector2(125, 373);
                    brushSize = new Vector2(265, 14);
                    fontSize = 145;
                    color = Paper;
                    accent = new Color(1, .63f, .31f);
                    composition.overshoot = 1.1f;
                    Accent("Local Ink Wash", title, new Vector2(285, 275),
                        new Vector2(670, 640), new Color(.035f, .055f, .065f, .9f), true);
                    break;
                case 2:
                    caption = "실력 검정";
                    eyebrow = "03    한계를 넘어서";
                    description = "하산할 준비가 되었다고 생각하나?";
                    detail = "흔들림 없는 박자로, 수련의 깊이를 증명하세요.";
                    position = new Vector2(280, 72);
                    size = new Vector2(610, 155);
                    copyPosition = new Vector2(307, 217);
                    brushPosition = new Vector2(299, 203);
                    brushSize = new Vector2(470, 12);
                    fontSize = 134;
                    composition.enterSeconds = .28f;
                    composition.entryDistance = 65;
                    composition.overshoot = .35f;
                    Accent("Cloud Light", title, new Vector2(568, 176),
                        new Vector2(840, 330), new Color(1, .95f, .84f, .55f), true);
                    break;
                case 3:
                    caption = "새로운\n수련";
                    eyebrow = "04    다음 이야기는";
                    description = "다음 수련은 아직 준비 중입니다.";
                    detail = "다른 수련을 먼저 둘러보세요.  ← →";
                    position = new Vector2(165, 108);
                    size = new Vector2(490, 245);
                    copyPosition = new Vector2(186, 379);
                    brushPosition = new Vector2(178, 354);
                    brushSize = new Vector2(305, 10);
                    fontSize = 114;
                    color = Paper;
                    accent = new Color(.80f, .68f, .49f);
                    Accent("Local Ink Wash", title, new Vector2(360, 260),
                        new Vector2(760, 640), new Color(.035f, .055f, .065f, .95f), true);
                    break;
            }

            var kicker = Label("Mode Eyebrow", title, eyebrow, 16, color);
            Place(kicker.rectTransform, position + new Vector2(14, -25), new Vector2(500, 26));
            composition.heading = Label("Mode Title", title, caption, fontSize, color);
            composition.heading.font = font;
            var headingScope = composition.heading.gameObject.AddComponent<UiFontScope>();
            headingScope.font = font;
            headingScope.useSmoothRendering = true;
            composition.heading.lineSpacing = .76f;
            composition.heading.alignment = TextAnchor.UpperLeft;
            composition.heading.horizontalOverflow = HorizontalWrapMode.Overflow;
            composition.heading.verticalOverflow = VerticalWrapMode.Overflow;
            Place(composition.heading.rectTransform, position, size);
            UiFontSettings.Apply(composition.heading);
            composition.heading.material = null;
            composition.brush = Accent("Title Brush", title, brushPosition, brushSize, accent).rectTransform;
            composition.brush.pivot = new Vector2(0, .5f);
            composition.description = Label("Mode Description", copy, description, 23, color);
            Place(composition.description.rectTransform, copyPosition, new Vector2(560, 38));
            composition.detail = Label("Mode Detail", copy, detail, 18, Paper);
            Place(composition.detail.rectTransform, new Vector2(90, 614), new Vector2(720, 58));
            Shadow(composition.detail, new Color(.03f, .04f, .05f, .95f));
            if (index == 1 || index == 3)
            {
                Shadow(composition.heading, new Color(.03f, .04f, .05f, .85f));
                Shadow(composition.description, new Color(.03f, .04f, .05f, .95f));
            }
            return composition;
        }

        private static void StyleButton(Button button, Transform parent, string caption,
            Vector2 position, Vector2 size, int fontSize)
        {
            button.transform.SetParent(parent, false);
            Place((RectTransform)button.transform, position, size);
            var graphic = button.GetComponent<Image>();
            graphic.sprite = null;
            graphic.color = Color.clear;
            graphic.raycastTarget = true;
            var text = button.GetComponentInChildren<Text>();
            text.text = caption;
            text.fontSize = fontSize;
            text.color = Paper;
            text.material = null;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            foreach (var effect in text.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
            UiFontSettings.Apply(text);
            text.material = null;
            Shadow(text, new Color(.03f, .04f, .05f, .95f));
            button.targetGraphic = text;
            // The transparent Image owns the full accessible hit area; tint responds on the text.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, .75f, .45f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1, .48f, .24f);
            colors.disabledColor = new Color(.7f, .7f, .7f, .5f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            text.raycastTarget = true;
        }

        private static Button Control(string name, Transform parent, string caption, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var button = go.GetComponent<Button>();
            Label("Caption", go.transform, caption, 45, Paper);
            StyleButton(button, parent, caption, position, new Vector2(64, 88), 45);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static Text Label(string name, Transform parent, string caption, int size, Color color)
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;
            text.raycastTarget = false;
            text.material = null;
            return text;
        }

        private static void Shadow(Text text, Color color)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(1, -2);
        }

        private static DojoInkGraphic Accent(string name, Transform parent, Vector2 position,
            Vector2 size, Color color, bool wash = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(DojoInkGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<DojoInkGraphic>();
            graphic.raycastTarget = false;
            graphic.color = color;
            graphic.wash = wash;
            Place(graphic.rectTransform, position, size);
            if (wash)
            {
                graphic.rectTransform.pivot = new Vector2(.5f, .5f);
            }
            return graphic;
        }

        private static RectTransform Group(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
        }
    }
}
