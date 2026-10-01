using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class MainMenuLayoutBuilder
    {
        public const string FontPath = "Assets/Game/Fonts/NotoSansKR-Regular.otf";

        public static MainMenuView Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (!font) throw new InvalidOperationException("Main menu Korean font missing: " + FontPath);
            var canvas = UiElements.Canvas("Main Menu");
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var view = canvas.gameObject.AddComponent<MainMenuView>();
            view.start = Button("Start Surface", canvas.transform, Color.black);
            Stretch((RectTransform)view.start.transform);

            view.title = Label("Title", canvas.transform, "rythm-dojo", font, 64);
            Anchor(view.title.rectTransform, new Vector2(.5f, .6f), Vector2.zero, new Vector2(1000, 110));
            view.startPrompt = Label("Start Prompt", canvas.transform, "Click to Start", font, 28);
            Anchor(view.startPrompt.rectTransform, new Vector2(.5f, .2f), Vector2.zero, new Vector2(600, 60));

            view.quit = IconButton("Quit", canvas.transform, MainMenuIcon.IconKind.Power, new Vector2(-32, 32));
            view.settings = IconButton("Settings", canvas.transform, MainMenuIcon.IconKind.Settings, new Vector2(-32, 112));

            var overlay = Panel("Quit Popup", canvas.transform, new Color(0, 0, 0, .75f));
            Stretch(overlay.rectTransform);
            view.quitPopup = overlay.gameObject;
            var dialog = Panel("Dialog", overlay.transform, new Color(.09f, .09f, .09f, 1));
            Anchor(dialog.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 240));
            var outline = dialog.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.6f, .6f, .6f); outline.effectDistance = new Vector2(1, -1);

            view.quitQuestion = Label("Question", dialog.transform, "게임을 종료 하시겠습니까?", font, 26);
            Anchor(view.quitQuestion.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 45), new Vector2(480, 80));
            view.confirmQuit = TextButton("Yes", dialog.transform, "예", font, new Vector2(-105, -55));
            view.cancelQuit = TextButton("No", dialog.transform, "아니요", font, new Vector2(105, -55));
            overlay.gameObject.SetActive(false);
            view.Validate();
            return view;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>(); image.color = color; return image;
        }

        private static Button Button(string name, Transform parent, Color color)
        {
            var image = Panel(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.65f, .65f, .65f);
            colors.pressedColor = new Color(.4f, .4f, .4f); colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; button.colors = colors;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            return button;
        }

        private static Button IconButton(string name, Transform parent, MainMenuIcon.IconKind kind, Vector2 position)
        {
            var button = Button(name, parent, new Color(.06f, .06f, .06f));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(64, 64);
            var iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(button.transform, false);
            Stretch((RectTransform)iconObject.transform);
            var icon = iconObject.AddComponent<MainMenuIcon>();
            icon.kind = kind; icon.color = Color.white; icon.raycastTarget = false;
            return button;
        }

        private static Text Label(string name, Transform parent, string caption, Font font, int size)
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size);
            text.font = font; text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button TextButton(string name, Transform parent, string caption, Font font, Vector2 position)
        {
            var button = Button(name, parent, new Color(.18f, .18f, .18f));
            Anchor((RectTransform)button.transform, new Vector2(.5f, .5f), position, new Vector2(170, 52));
            var label = Label("Caption", button.transform, caption, font, 24);
            Stretch(label.rectTransform); return button;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}