using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class MainMenuLayoutBuilder
    {
        public const string FontPath = "Assets/Game/Fonts/Mona12TextKR.ttf";

        public static MainMenuView Build()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            if (!font)
            {
                throw new InvalidOperationException("Main menu Korean font missing: " + FontPath);
            }

            var canvas = UiElements.Canvas("Main Menu");
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;

            var view = canvas.gameObject.AddComponent<MainMenuView>();
            view.alwaysVisibleMenu = false;
            view.logoOpenPosition = new Vector2(-220f, 0f);
            view.menuOpenPosition = new Vector2(230f, 0f);
            var entrance = new GameObject("Dojo Entrance", typeof(RectTransform));
            entrance.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)entrance.transform);
            var entranceFit = entrance.AddComponent<AspectRatioFitter>();
            entranceFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            view.entrance = entrance.AddComponent<DojoEntranceView>();
            var wall = Panel("Dojo Wall", entrance.transform, Color.white);
            Stretch(wall.rectTransform);
            wall.sprite = LoadSprite("TitleBackground");
            entranceFit.aspectRatio = wall.sprite.rect.width / wall.sprite.rect.height;
            wall.raycastTarget = false;
            // Crop to fill ultrawide/portrait windows without stretching the artwork.
            var fit = wall.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = wall.sprite.rect.width / wall.sprite.rect.height;
            BuildEntrance(view.entrance);
            view.knock = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoKnock.wav");
            view.boardCrack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoBoardCrack.wav");
            view.doorSlide = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoDoorSlide.wav");

            var logoObject = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logoObject.transform.SetParent(canvas.transform, false);
            var logoImage = logoObject.GetComponent<Image>();
            logoImage.sprite = DojoEntranceAssets.Load("RoundPlaque");
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = true;
            logoObject.AddComponent<DojoPlaqueHitArea>();
            view.logoGroup = logoObject.AddComponent<CanvasGroup>();
            view.logo = logoObject.AddComponent<Button>();
            view.logo.targetGraphic = logoImage;
            view.logo.transition = Selectable.Transition.None;
            var logoNavigation = view.logo.navigation;
            logoNavigation.mode = Navigation.Mode.Explicit;
            view.logo.navigation = logoNavigation;
            Anchor((RectTransform)view.logo.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(360, 360));
            var logoArt = Panel("Game Logo", logoObject.transform, Color.white);
            logoArt.sprite = LoadSprite("TitleLogo");
            logoArt.preserveAspect = true;
            logoArt.raycastTarget = false;
            view.logoArtwork = logoArt.rectTransform;
            Anchor(logoArt.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 12), new Vector2(420, 150));
            view.title = Label("Title", view.logo.transform, "리듬 도장", font, 48);
            Anchor(view.title.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(270, 90));
            view.title.enabled = false;
            view.startPrompt = Label("Start Prompt", view.logo.transform, "두드려 시작", font, 20);
            view.startPrompt.color = new Color(1f, .88f, .64f);
            Anchor(view.startPrompt.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(250, 40));
            view.startPrompt.raycastTarget = false;

            var menu = new GameObject("Menu Buttons", typeof(RectTransform), typeof(CanvasGroup));
            menu.transform.SetParent(canvas.transform, false);
            view.menuPanel = menu.GetComponent<CanvasGroup>();
            Anchor((RectTransform)menu.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(320, 300));
            view.start = ArtButton("Play", menu.transform, "PlayBoard", 105);
            view.settings = ArtButton("Settings", menu.transform, "SettingsBoard", 0);
            view.quit = ArtButton("Quit", menu.transform, "QuitBoard", -105);
            SetNavigation(view.start, view.quit, view.settings);
            SetNavigation(view.settings, view.start, view.quit);
            SetNavigation(view.quit, view.settings, view.start);
            logoNavigation.selectOnRight = view.start;
            view.logo.navigation = logoNavigation;
            foreach (var button in new[] { view.start, view.settings, view.quit })
            {
                var navigation = button.navigation;
                navigation.selectOnLeft = view.logo;
                button.navigation = navigation;
            }

            view.SetExpansion(0f);
            view.menuPanel.gameObject.SetActive(false);

            var overlay = Panel("Quit Popup", canvas.transform, new Color(0, 0, 0, .75f));
            Stretch(overlay.rectTransform);
            view.quitPopup = overlay.gameObject;

            var dialog = Panel("Dialog", overlay.transform, new Color(.12f, .075f, .06f, 1));
            Anchor(
                dialog.rectTransform,
                new Vector2(.5f, .5f),
                Vector2.zero,
                new Vector2(520, 240)
            );

            var outline = dialog.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.6f, .6f, .6f);
            outline.effectDistance = new Vector2(1, -1);
            view.quitQuestion = Label(
                "Question",
                dialog.transform,
                "게임을 종료 하시겠습니까?",
                font,
                26
            );
            Anchor(
                view.quitQuestion.rectTransform,
                new Vector2(.5f, .5f),
                new Vector2(0, 45),
                new Vector2(480, 80)
            );
            view.confirmQuit = TextButton(
                "Yes",
                dialog.transform,
                "예",
                font,
                new Vector2(-105, -55)
            );
            view.cancelQuit = TextButton(
                "No",
                dialog.transform,
                "아니요",
                font,
                new Vector2(105, -55)
            );
            overlay.gameObject.SetActive(false);
            PixelOptionsStyle.ApplyQuit(view);
            view.Validate();

            return view;
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/PixelDojo/" + name + ".asset");
            if (!sprite)
            {
                throw new InvalidOperationException("Pixel dojo sprite missing: " + name);
            }

            return sprite;
        }

        private static void BuildEntrance(DojoEntranceView entrance)
        {
            var left = Panel("Left Door", entrance.transform, Color.white);
            left.sprite = DojoEntranceAssets.Load("DoorPanel");
            left.raycastTarget = false;
            left.rectTransform.anchorMin = Vector2.zero;
            left.rectTransform.anchorMax = new Vector2(.5f, 1f);
            left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
            entrance.leftDoor = left.rectTransform;

            var right = Panel("Right Door", entrance.transform, Color.white);
            right.sprite = left.sprite;
            right.raycastTarget = false;
            right.rectTransform.anchorMin = new Vector2(.5f, 0f);
            right.rectTransform.anchorMax = Vector2.one;
            right.rectTransform.offsetMin = right.rectTransform.offsetMax = Vector2.zero;
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            entrance.rightDoor = right.rectTransform;

            var frame = Panel("Entrance Frame", entrance.transform, Color.white);
            frame.sprite = DojoEntranceAssets.Load("EntranceFrame");
            frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            entrance.frame = frame.gameObject.AddComponent<CanvasGroup>();
            entrance.SetOpening(0f);
        }

        private static Button ArtButton(string name, Transform parent, string spriteName, float y)
        {
            var button = Button(name, parent, Color.white);
            var image = button.GetComponent<Image>();
            image.sprite = DojoEntranceAssets.Load(spriteName);
            image.preserveAspect = false;
            Anchor((RectTransform)button.transform, new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(320, 78));
            button.gameObject.AddComponent<DojoBoardBreak>();
            var colors = button.colors;
            colors.normalColor = new Color(.84f, .80f, .76f);
            colors.highlightedColor = new Color(1f, .97f, .86f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.62f, .53f, .43f);
            colors.disabledColor = new Color(.5f, .5f, .5f);
            colors.fadeDuration = .06f;
            button.colors = colors;
            return button;
        }

        private static void SetNavigation(Button button, Button up, Button down)
        {
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            button.navigation = navigation;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = color;

            return image;
        }

        private static Button Button(string name, Transform parent, Color color)
        {
            var image = Panel(name, parent, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.65f, .65f, .65f);
            colors.pressedColor = new Color(.4f, .4f, .4f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            return button;
        }

        private static Text Label(
            string name,
            Transform parent,
            string caption,
            Font font,
            int size
        )
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size);
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            return text;
        }

        private static Button TextButton(
            string name,
            Transform parent,
            string caption,
            Font font,
            Vector2 position
        )
        {
            var button = Button(name, parent, new Color(.18f, .18f, .18f));
            Anchor(
                (RectTransform)button.transform,
                new Vector2(.5f, .5f),
                position,
                new Vector2(170, 52)
            );

            var label = Label("Caption", button.transform, caption, font, 24);
            Stretch(label.rectTransform);

            return button;
        }

        private static Button MenuButton(
            string name,
            Transform parent,
            string caption,
            Font font,
            float x
        )
        {
            var button = Button(name, parent, new Color(.12f, .055f, .065f));
            Anchor(
                (RectTransform)button.transform,
                new Vector2(.5f, .5f),
                new Vector2(x, 0),
                new Vector2(190, 76)
            );

            var border = button.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.94f, .25f, .26f, .8f);
            border.effectDistance = new Vector2(1, -1);

            var captionText = Label("Caption", button.transform, caption, font, 28);
            Stretch(captionText.rectTransform);

            return button;
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size
        )
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
