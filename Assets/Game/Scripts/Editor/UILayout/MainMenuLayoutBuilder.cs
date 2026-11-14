using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class MainMenuLayoutBuilder
    {
        public const string FontPath = "Assets/Game/Fonts/MaruBuri-SemiBold.otf";

        public static MainMenuView Build() => Build(false);

        public static MainMenuView Build(bool modeSelection)
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
            view.logoOpenPosition = Vector2.zero;
            view.menuOpenPosition = Vector2.zero;
            var entrance = new GameObject("Dojo Entrance", typeof(RectTransform));
            entrance.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)entrance.transform);
            var entranceFit = entrance.AddComponent<AspectRatioFitter>();
            entranceFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            view.entrance = entrance.AddComponent<DojoEntranceView>();
            var wall = Panel("Dojo Wall", entrance.transform, Color.white);
            view.entrance.interiorBackdrop = wall.gameObject.AddComponent<CanvasGroup>();
            Stretch(wall.rectTransform);
            wall.sprite = DojoEntranceAssets.Load("SelectionBackground");
            entranceFit.aspectRatio = wall.sprite.rect.width / wall.sprite.rect.height;
            wall.raycastTarget = false;
            // Crop to fill ultrawide/portrait windows without stretching the artwork.
            var fit = wall.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = wall.sprite.rect.width / wall.sprite.rect.height;
            var interior = new GameObject("Training Dojo Interior", typeof(RectTransform));
            interior.transform.SetParent(entrance.transform, false);
            Stretch((RectTransform)interior.transform);
            var interiorArt = interior.AddComponent<DojoTrainingGraphic>();
            interiorArt.interior = true;
            interiorArt.artwork = DojoEntranceAssets.Load("TrainingInterior", UiArtworkCrop.Full);
            interiorArt.raycastTarget = false;
            view.trainingInterior = (RectTransform)interior.transform;
            view.entrance.trainingInterior = view.trainingInterior;
            view.entranceTrigger = Button("Door Interaction", entrance.transform, Color.clear);
            Stretch((RectTransform)view.entranceTrigger.transform);
            view.entranceTrigger.transition = Selectable.Transition.None;
            BuildEntrance(view.entrance);
            view.knock = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoKnock.wav");
            view.boardCrack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoBoardCrack.wav");
            view.doorSlide = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoDoorSlide.wav");

            var logoClip = new GameObject("Doorway Logo Clip", typeof(RectTransform), typeof(RectMask2D));
            logoClip.transform.SetParent(entrance.transform, false);
            var logoClipRect = (RectTransform)logoClip.transform;
            // Match the transparent opening of EntranceFrame; the columns occlude the sliding logo.
            logoClipRect.anchorMin = new Vector2(.165f, .08f);
            logoClipRect.anchorMax = new Vector2(.835f, .84f);
            logoClipRect.offsetMin = logoClipRect.offsetMax = Vector2.zero;
            logoClip.transform.SetSiblingIndex(view.entrance.frame.transform.GetSiblingIndex());
            view.entrance.logoClip = logoClipRect;
            var logoObject = new GameObject("Logo", typeof(RectTransform), typeof(DojoSlidingImage));
            logoObject.transform.SetParent(logoClip.transform, false);
            var logoImage = logoObject.GetComponent<Image>();
            logoImage.sprite = DojoEntranceAssets.Load("RoundPlaque", UiArtworkCrop.Alpha);
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = true;
            logoObject.AddComponent<DojoPlaqueHitArea>();
            view.slidingPlaque = logoObject.GetComponent<DojoSlidingImage>();
            view.slidingPlaque.splitDepth = 12f;
            view.logoGroup = logoObject.AddComponent<CanvasGroup>();
            view.logo = logoObject.AddComponent<Button>();
            view.logo.targetGraphic = logoImage;
            view.logo.transition = Selectable.Transition.None;
            var logoNavigation = view.logo.navigation;
            logoNavigation.mode = Navigation.Mode.Explicit;
            view.logo.navigation = logoNavigation;
            Anchor((RectTransform)view.logo.transform, new Vector2(.5f, .42f / .76f), Vector2.zero, new Vector2(360, 360));
            var logoArtObject = new GameObject("Game Logo", typeof(RectTransform), typeof(DojoSlidingImage));
            logoArtObject.transform.SetParent(logoObject.transform, false);
            var logoArt = logoArtObject.GetComponent<DojoSlidingImage>();
            view.slidingLogo = logoArt;
            view.entrance.slidingPlaque = view.slidingPlaque;
            view.entrance.slidingLogo = view.slidingLogo;
            logoArt.sprite = LoadSprite("TitleLogo");
            logoArt.preserveAspect = true;
            logoArt.raycastTarget = false;
            view.logoArtwork = logoArt.rectTransform;
            Anchor(logoArt.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 12), new Vector2(420, 150));
            view.title = Label("Title", view.logo.transform, "리듬 도장", font, 48, role: UiTextRole.Title);
            Anchor(view.title.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(270, 90));
            view.title.enabled = false;
            view.startPrompt = Label("Start Prompt", view.logo.transform, "두드려 시작", font, 20, role: UiTextRole.Title);
            view.startPrompt.color = new Color(1f, .88f, .64f);
            Anchor(view.startPrompt.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(250, 40));
            view.startPrompt.raycastTarget = false;
            view.entrance.startPrompt = view.startPrompt.rectTransform;

            var menu = new GameObject("Menu Buttons", typeof(RectTransform), typeof(CanvasGroup));
            menu.transform.SetParent(canvas.transform, false);
            var menuFont = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            if (!menuFont)
            {
                throw new InvalidOperationException("Main menu brush font missing.");
            }
            menu.AddComponent<UiFontScope>().font = menuFont;
            view.menuPanel = menu.GetComponent<CanvasGroup>();
            view.entrance.trainingMenu = view.menuPanel;
            Anchor((RectTransform)menu.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(350, 290));
            view.settings = MenuButton("Settings", menu.transform, "설정", font, -45);
            view.editor = MenuButton("Showcase Editor", menu.transform, "에디터", font, 45);
            view.quit = MenuButton("Quit", menu.transform, "종료", font, 135);
            // Keep plaques above the doors but behind both logo halves throughout reveal and reversal.
            menu.transform.SetParent(entrance.transform, false);
            menu.transform.SetSiblingIndex(view.entrance.logoClip.GetSiblingIndex());
            SetNavigation(view.settings, view.quit, view.editor);
            SetNavigation(view.editor, view.settings, view.quit);
            SetNavigation(view.quit, view.editor, view.settings);
            logoNavigation.selectOnRight = view.settings;
            logoNavigation.selectOnDown = view.settings;
            view.logo.navigation = logoNavigation;

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
                26,
                role: UiTextRole.Title
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
            UiFontSettings.ApplyTo(view.transform);
            ApplyMenuTextStyle(view);
            AlphaDojoLayoutBuilder.Apply(view, modeSelection);
            // Reuse the established builders and assets, then serialize only the
            // composition owned by this scene. No presentation is rebuilt at runtime.
            if (modeSelection)
            {
                UnityEngine.Object.DestroyImmediate(view.entrance.gameObject);
                view.alwaysVisibleMenu = true;
                view.menuPanel.gameObject.SetActive(true);
                view.SetExpansion(1f);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(view.menuPanel.gameObject);
                UnityEngine.Object.DestroyImmediate(view.quitPopup);
                canvas.sortingOrder = 10;
                view.SetExpansion(0f);
            }
            view.Validate();

            return view;
        }

        public static void ApplyMenuTextStyle(MainMenuView view)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Resources/UI/SelectionWoodText.mat");
            if (!material)
            {
                throw new InvalidOperationException("Selection wood text material missing.");
            }

            foreach (var button in new[] { view.settings, view.editor, view.quit })
            {
                var text = button.GetComponentInChildren<Text>(true);
                UiFontSettings.Apply(text);
                text.color = new Color(.84f, .66f, .45f);
                text.material = material;
                var shadow = text.GetComponent<Shadow>();
                if (!shadow)
                {
                    shadow = text.gameObject.AddComponent<Shadow>();
                }
                shadow.effectColor = new Color(.20f, .09f, .035f, .85f);
                shadow.effectDistance = new Vector2(1, -2);
                var outline = text.GetComponent<Outline>();
                if (!outline)
                {
                    outline = text.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = new Color(.34f, .17f, .075f);
                outline.effectDistance = new Vector2(1, -1);
            }
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
            left.sprite = DojoEntranceAssets.Load("DoorPanel", UiArtworkCrop.Full);
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
            frame.sprite = DojoEntranceAssets.Load("EntranceFrame", UiArtworkCrop.Full);
            frame.raycastTarget = false;
            Stretch(frame.rectTransform);
            entrance.frame = frame.gameObject.AddComponent<CanvasGroup>();
            entrance.SetOpening(0f);
        }

        private static Button ArtButton(string name, Transform parent, string spriteName, float y)
        {
            var button = Button(name, parent, Color.white);
            var image = button.GetComponent<Image>();
            image.sprite = DojoEntranceAssets.Load(spriteName, UiArtworkCrop.Alpha);
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
            navigation.selectOnLeft = up;
            navigation.selectOnRight = down;
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
            int size,
            UiTextRole role = UiTextRole.Body
        )
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size, role);
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
            var button = Button(name, parent, Color.white);
            Anchor(
                (RectTransform)button.transform,
                new Vector2(.5f, .5f),
                new Vector2(x, 0),
                new Vector2(80, 290)
            );

            var graphic = button.GetComponent<Image>();
            graphic.sprite = DojoEntranceAssets.Load("VerticalMenuBoard", UiArtworkCrop.Alpha);
            graphic.preserveAspect = true;
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, .88f, .63f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.72f, .56f, .38f);
            colors.disabledColor = new Color(.65f, .60f, .53f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            var captionText = Label("Caption", button.transform, string.Join("\n", caption.ToCharArray()), font, 30, UiTextRole.Title);
            captionText.color = new Color(1f, .95f, .84f);
            captionText.fontStyle = FontStyle.Bold;
            var captionHighlight = captionText.gameObject.AddComponent<Shadow>();
            captionHighlight.effectColor = new Color(.12f, .055f, .025f, .85f);
            captionHighlight.effectDistance = new Vector2(0, -1);
            captionText.raycastTarget = false;
            Anchor(captionText.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -8), new Vector2(54, 180));

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
