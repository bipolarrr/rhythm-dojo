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

            var navigationInk = Contrast("Navigation Ink Background", bottom, new Vector2(630, 661),
                new Vector2(584, 56), new Color(.045f, .065f, .07f, .93f));
            var help = Label("Navigation Help", bottom, "← →  이동     Enter  선택     Esc  대문으로", 16, Paper);
            Place(help.rectTransform, new Vector2(650, 674), new Vector2(540, 30));
            help.alignment = TextAnchor.MiddleRight;
            UiFontSettings.Apply(help);
            FitInk(navigationInk, help, 0, 18, 7);
            Shadow(help, new Color(.03f, .04f, .05f, .95f));
            mode.selectionMarks = System.Array.Empty<Image>();

            StyleButton(view.settings, parent, "설정", new Vector2(1050, 14), new Vector2(82, 44), 18);
            StyleButton(view.quit, parent, "종료", new Vector2(1150, 14), new Vector2(66, 44), 18);
            StyleButton(view.editor, parent, "율보 편찬", new Vector2(68, 14), new Vector2(130, 44), 18);
            view.editor.gameObject.SetActive(false);
            mode.previous = Control("Previous Mode", parent, "〈", new Vector2(28, 316));
            mode.next = Control("Next Mode", parent, "〉", new Vector2(1188, 316));
            mode.interfaceGroups = new CanvasGroup[7];
            var controls = new[] { top.gameObject, bottom.gameObject,
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
            string description = "처음이지? 내가 옆에서 박자를 짚어 줄게.";
            var position = new Vector2(380, 87);
            var size = new Vector2(540, 140);
            var copyPosition = new Vector2(371, 229);

            int fontSize = 114;
            Color color = Ink;

            switch (index)
            {
                case 1:
                    caption = "자유\n연습";
                    description = "자유롭게 연습하자!";

                    position = new Vector2(118, 112);
                    size = new Vector2(380, 250);
                    copyPosition = new Vector2(134, 394);

                    fontSize = 145;
                    color = Paper;

                    composition.overshoot = 1.1f;
                    break;
                case 2:
                    caption = "실력 검정";
                    description = "하산할 준비가 되었다고 생각하나?";

                    position = new Vector2(280, 72);
                    size = new Vector2(610, 155);
                    copyPosition = new Vector2(307, 217);

                    fontSize = 134;
                    composition.enterSeconds = .28f;
                    composition.entryDistance = 65;
                    composition.overshoot = .35f;
                    break;
                case 3:
                    caption = "율보 편찬";
                    description = "속세에서는 그것을 에디터라 부른다더라";

                    position = new Vector2(100, 174);
                    size = new Vector2(550, 140);
                    copyPosition = new Vector2(114, 326);

                    fontSize = 118;
                    color = Paper;

                    composition.entryDistance = 48;
                    composition.enterSeconds = .30f;
                    composition.copyDelay = .14f;
                    composition.overshoot = .25f;
                    break;
            }

            Color contrast = new Color(1f, .95f, .82f, .92f);
            if (index == 1 || index == 3)
            {
                contrast = new Color(.045f, .065f, .07f, .92f);
            }
            var titleInk = Contrast("Title Ink Background", title,
                position + new Vector2(-24, -4), new Vector2(size.x + 20, size.y), contrast);
            RectTransform lowerInk = null;
            if (index == 1)
            {
                titleInk.sizeDelta = new Vector2(340, 146);
                lowerInk = Contrast("Title Lower Ink Background", title, position + new Vector2(-12, 110),
                    new Vector2(350, 146), contrast);
            }
            if (index == 3)
            {
                titleInk.localRotation = Quaternion.Euler(0, 0, 3);
            }
            // The tutorial replaces its copy with feedback, so retain the full authored copy width.
            float copyWidth = 584;
            if (index == 1)
            {
                copyWidth = 250;
            }
            else if (index == 2)
            {
                copyWidth = 430;
            }
            else if (index == 3)
            {
                copyWidth = 470;
            }
            var descriptionInk = Contrast("Description Ink Background", copy, copyPosition + new Vector2(-20, -5),
                new Vector2(copyWidth, 48), contrast);

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
            FitInk(titleInk, composition.heading, 0, 16, 10);
            if (index == 2)
            {
                // Keep the upper edge fixed and lift only the lower edge by 16 canvas pixels.
                titleInk.sizeDelta -= new Vector2(0, 16);
                titleInk.anchoredPosition += new Vector2(0, 8);
            }
            if (lowerInk)
            {
                FitInk(lowerInk, composition.heading, 1, 16, 10);
            }
            composition.description = Label("Mode Description", copy, description, 23, color);
            Place(composition.description.rectTransform, copyPosition, new Vector2(560, 38));
            if (index == 3)
            {
                composition.heading.rectTransform.localRotation = Quaternion.Euler(0, 0, 3);
                composition.description.fontSize = 21;
                FitInk(titleInk, composition.heading, 0, 16, 10);
            }
            UiFontSettings.Apply(composition.description);
            FitInk(descriptionInk, composition.description, 0, 14, 4);
            // Tutorial feedback reuses the one-line description instead of a second text block.
            composition.detail = composition.description;
            if (index == 1 || index == 3)
            {
                Shadow(composition.heading, new Color(.03f, .04f, .05f, .85f));
                Shadow(composition.description, new Color(.03f, .04f, .05f, .95f));
            }
            return composition;
        }

        private static void FitInk(RectTransform ink, Text text, int line, float horizontal, float vertical)
        {
            DojoInkGraphic.FitInk(ink, text, line, horizontal, vertical);
        }

        private static RectTransform Contrast(string name, Transform parent,
            Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(DojoInkGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<DojoInkGraphic>();
            graphic.contrastStroke = true;
            graphic.color = color;
            graphic.raycastTarget = false;
            Place(graphic.rectTransform, position, size);
            return graphic.rectTransform;
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
            var buttonInk = Contrast("Control Ink Background", button.transform,
                Vector2.zero, size, new Color(.045f, .065f, .07f, .9f));
            FitInk(buttonInk, text, 0, 6, 3);
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
