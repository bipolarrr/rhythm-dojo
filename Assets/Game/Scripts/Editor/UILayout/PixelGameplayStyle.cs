using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class PixelGameplayStyle
    {
        private static readonly Color Paper = new Color(.89f, .76f, .55f);
        private static readonly Color Ink = new Color(.24f, .12f, .085f);
        private static readonly Color Cream = new Color(.98f, .86f, .65f);

        public static void Apply(GameplayHudView view)
        {
            view.pixelTheme = true;
            var headingFont = AssetDatabase.LoadAssetAtPath<Font>(
                MainMenuLayoutBuilder.FontPath
            );
            if (!headingFont)
            {
                throw new InvalidOperationException("Gameplay heading font missing.");
            }

            var camera = Camera.main;
            if (!camera)
            {
                throw new InvalidOperationException("Gameplay camera missing.");
            }

            // Composite the world camera over the dojo art, with the lanes entering below the ceiling.
            var background = UiElements.Canvas("Pixel Dojo Gameplay Backdrop");
            background.renderMode = RenderMode.ScreenSpaceOverlay;
            background.sortingOrder = -100;
            background.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var raycaster = background.GetComponent<GraphicRaycaster>();
            if (raycaster)
            {
                UnityEngine.Object.DestroyImmediate(raycaster);
            }

            var field = Panel("Training Floor Frame", background.transform, Color.clear, .285f, .015f, .715f, .80f);
            camera.backgroundColor = new Color(.055f, .035f, .025f, 0f);
            var viewport = new GameObject("Gameplay Viewport", typeof(RectTransform), typeof(RawImage));
            viewport.transform.SetParent(field, false);
            var viewportRect = (RectTransform)viewport.transform;
            Bounds(viewportRect, 0, 0, 1, 1);
            viewportRect.offsetMin = new Vector2(10, 10);
            viewportRect.offsetMax = new Vector2(-10, 0);
            viewport.GetComponent<RawImage>().raycastTarget = false;
            viewport.AddComponent<GameplayViewport>().gameplayCamera = camera;

            var shade = new Color(.06f, .08f, .12f, .30f);
            AddLaneShade("Lane shade left", field, .26f, .40f, Color.clear, shade);
            AddLaneShade("Lane shade center", field, .40f, .60f, shade, shade);
            AddLaneShade("Lane shade right", field, .60f, .74f, shade, Color.clear);

            view.lanes.pixelTheme = true;
            view.lanes.font = headingFont;
            view.lanes.sprite = DojoGameplayAssets.Tile;

            var song = view.transform.Find("Training Information");
            foreach (var text in song.GetComponentsInChildren<Text>())
            {
                text.color = Cream;
            }
            view.songTitle.fontStyle = FontStyle.Bold;
            view.songTitle.resizeTextForBestFit = true;
            view.songTitle.resizeTextMinSize = 18;
            view.songTitle.resizeTextMaxSize = 27;
            view.sessionState.color = Cream;

            var feedback = view.transform.Find("Rhythm Feedback");
            foreach (var text in feedback.GetComponentsInChildren<Text>(true))
            {
                text.color = Cream;
            }
            view.combo.fontStyle = FontStyle.Bold;
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.03f, .02f, .01f, .9f);
                shadow.effectDistance = new Vector2(1, -2);
            }
            feedback.Find("Health Track").GetComponent<Image>().color = new Color(.10f, .055f, .035f);

            var focus = view.returnButton.GetComponent<DojoControlFocus>();
            focus.restingColor = Cream;
            focus.selectedColor = new Color(1, .77f, .35f);
            focus.stroke.GetComponent<DojoInkGraphic>().color = focus.selectedColor;
            var result = Panel("Result Scroll", view.gameOverPanel.transform, Paper, .28f, .33f, .72f, .65f);
            Frame(result, new Color(.24f, .10f, .075f), 16);
            result.SetAsFirstSibling();
            view.gameOverFrame = result.gameObject;
            view.gameOverText.fontStyle = FontStyle.Bold;
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                text.font = headingFont;
                text.fontStyle = UiFontSettings.Style;
            }
        }

        public static void RefineReadability(GameplayHudView view)
        {
            var feedback = (RectTransform)view.combo.transform.parent;
            Bounds(feedback, .76f, .57f, .965f, .88f);
            view.combo.fontSize = 58;
            Bounds(view.combo.rectTransform, .04f, .45f, .96f, .83f);
            Bounds(view.healthLabel.rectTransform, .12f, .19f, .88f, .30f);
            Bounds((RectTransform)feedback.Find("Health Track"), .12f, .105f, .88f, .17f);
            view.healthLabel.fontSize = 16;
            view.timing.color = Cream;

            var menu = view.GetComponent<GameplayPauseMenu>();
            var dialog = menu.resumeButton.transform.parent;
            var railOpacityLabel = (dialog.Find("Rail Opacity Label") ?? view.transform.Find("Rail Opacity Label"))
                .GetComponent<Text>();
            railOpacityLabel.transform.SetParent(dialog, false);
            railOpacityLabel.color = Ink;
            Bounds(railOpacityLabel.rectTransform, .14f, .095f, .44f, .18f);
            var railOpacity = (RectTransform)view.railOpacity.transform;
            railOpacity.SetParent(dialog, false);
            Bounds(railOpacity, .44f, .095f, .86f, .18f);
            view.railOpacity.targetGraphic.color = new Color(.50f, .18f, .10f);
            view.railOpacity.interactable = false;
            view.railOpacity.gameObject.SetActive(false);
            railOpacityLabel.gameObject.SetActive(false);
            Bounds((RectTransform)menu.resumeButton.transform, .14f, .65f, .86f, .79f);
            Bounds((RectTransform)menu.restartButton.transform, .14f, .42f, .86f, .56f);
            Bounds((RectTransform)menu.exitButton.transform, .14f, .23f, .86f, .37f);
            Bounds(menu.recoveryStatus.rectTransform, .08f, .025f, .92f, .075f);
            var hint = UiElements.Label("Settings Menu Hint", dialog,
                "↑↓ 이동 · Enter / Space 선택 · Esc 이어하기\n설정은 수련을 마친 뒤 메뉴에서 변경할 수 있습니다.",
                Vector2.zero, Vector2.zero, 14, role: UiTextRole.Help);
            Bounds(hint.rectTransform, .06f, .09f, .94f, .21f);
            hint.color = DojoScreenStyle.Muted;
            hint.alignment = TextAnchor.MiddleCenter;
            SceneResources.ApplyReadableFont(view.songDetails, view.healthLabel, view.timing, railOpacityLabel);
        }

        private static void AddLaneShade(string name, Transform field, float x0, float x1, Color left, Color right)
        {
            var shade = field.parent.Find(name);
            if (!shade)
            {
                shade = new GameObject(name, typeof(RectTransform), typeof(SelectionGradientImage)).transform;
                shade.SetParent(field.parent, false);
                shade.SetSiblingIndex(field.GetSiblingIndex());
            }

            var graphic = shade.GetComponent<SelectionGradientImage>();
            Bounds(graphic.rectTransform, x0, 0, x1, .825f);
            graphic.useGradient = true;
            graphic.leftColor = left;
            graphic.rightColor = right;
            graphic.raycastTarget = false;
        }

        private static void Frame(RectTransform rect, Color fill, float width)
        {
            var frame = new GameObject("Wooden Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(rect, false);
            frame.transform.SetAsFirstSibling();
            Bounds((RectTransform)frame.transform, 0, 0, 1, 1);
            var graphic = frame.GetComponent<Image>();
            graphic.sprite = DojoGameplayAssets.Wood;
            if (fill == Paper)
            {
                graphic.sprite = DojoGameplayAssets.Paper;
            }

            graphic.color = Color.white;
            graphic.type = Image.Type.Sliced;
            graphic.pixelsPerUnitMultiplier = 100 / width;
            graphic.raycastTarget = false;
            rect.GetComponent<Image>().color = Color.clear;
        }

        private static RectTransform Panel(string name, Transform parent, Color color, float x0, float y0, float x1, float y1)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)panel.transform;
            Bounds(rect, x0, y0, x1, y1);
            return rect;
        }

        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
