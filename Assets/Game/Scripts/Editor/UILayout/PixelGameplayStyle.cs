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
                "Assets/Game/Fonts/Mona12TextKR.ttf"
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

            // Composite the world camera inside the frame, over the supplied dojo art.
            var background = UiElements.Canvas("Pixel Dojo Gameplay Backdrop");
            background.renderMode = RenderMode.ScreenSpaceOverlay;
            background.sortingOrder = -100;
            background.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;
            var raycaster = background.GetComponent<GraphicRaycaster>();
            if (raycaster)
            {
                UnityEngine.Object.DestroyImmediate(raycaster);
            }

            var backdrop = Panel("Dojo Scenery", background.transform, Color.white, 0, 0, 1, 1);
            var image = backdrop.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/PixelDojo/TitleBackground.asset");
            if (!image.sprite)
            {
                throw new InvalidOperationException("Pixel dojo background missing.");
            }
            var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            Panel("Scenery Shade", background.transform, new Color(.08f, .04f, .025f, .62f), 0, 0, 1, 1);
            var field = Panel("Training Floor Frame", background.transform, new Color(.055f, .035f, .025f), .285f, .015f, .715f, .922f);
            Frame(field, new Color(.055f, .035f, .025f), 10);
            var scrollFrame = field.Find("Wooden Frame").GetComponent<Image>();
            scrollFrame.sprite = DojoGameplayAssets.Scroll;
            scrollFrame.type = Image.Type.Simple;
            camera.backgroundColor = new Color(.055f, .035f, .025f, 0f);
            var viewport = new GameObject("Gameplay Viewport", typeof(RectTransform), typeof(RawImage));
            viewport.transform.SetParent(field, false);
            var viewportRect = (RectTransform)viewport.transform;
            Bounds(viewportRect, 0, 0, 1, .93f);
            viewportRect.offsetMin = new Vector2(10, 10);
            viewportRect.offsetMax = new Vector2(-10, 0);
            viewport.GetComponent<RawImage>().raycastTarget = false;
            viewport.AddComponent<GameplayViewport>().gameplayCamera = camera;
            view.lanes.pixelTheme = true;
            view.lanes.font = headingFont;
            view.lanes.sprite = DojoGameplayAssets.Tile;

            var header = view.transform.Find("Training Header");
            Frame((RectTransform)header, new Color(.24f, .10f, .075f), 8);
            header.Find("Header Red Line").gameObject.SetActive(false);
            header.Find("Dojo Brand").GetComponent<Text>().text = "리듬 도장  /  수련";
            foreach (var text in header.GetComponentsInChildren<Text>())
            {
                text.color = Cream;
            }

            var song = view.transform.Find("Training Information");
            var record = view.transform.Find("Judgment Record");
            foreach (var panel in new[] { song, record })
            {
                Frame((RectTransform)panel, Paper, 12);
                panel.Find(panel.name + " Accent").gameObject.SetActive(false);
                foreach (var text in panel.GetComponentsInChildren<Text>())
                {
                    text.color = Ink;
                }
            }
            view.songTitle.fontStyle = FontStyle.Bold;
            view.songTitle.resizeTextForBestFit = true;
            view.songTitle.resizeTextMinSize = 18;
            view.songTitle.resizeTextMaxSize = 27;
            view.sessionState.color = new Color(.46f, .18f, .12f);
            view.accuracy.color = new Color(.20f, .34f, .20f);

            var feedback = view.transform.Find("Rhythm Feedback");
            Frame((RectTransform)feedback, new Color(.22f, .085f, .065f), 12);
            feedback.Find("Rhythm Feedback Accent").gameObject.SetActive(false);
            foreach (var text in feedback.GetComponentsInChildren<Text>(true))
            {
                text.color = Cream;
            }
            feedback.Find("Combo Caption").GetComponent<Text>().text = "연속 타격  /  COMBO";
            view.combo.fontStyle = FontStyle.Bold;
            feedback.Find("Feedback Divider").GetComponent<Image>().color = new Color(.64f, .40f, .22f);
            feedback.Find("Health Track").GetComponent<Image>().color = new Color(.10f, .055f, .035f);
            view.transform.Find("Resolved Track").GetComponent<Image>().color = new Color(.23f, .13f, .075f);
            view.progressFill.color = new Color(.84f, .57f, .27f);
            view.progress.color = Cream;

            Frame((RectTransform)view.returnButton.transform, new Color(.40f, .12f, .085f), 8);
            view.returnButton.GetComponentInChildren<Text>().color = Cream;
            var outline = view.returnButton.GetComponent<Outline>();
            if (outline)
            {
                outline.enabled = false;
            }
            var result = Panel("Result Scroll", view.gameOverPanel.transform, Paper, .28f, .33f, .72f, .65f);
            Frame(result, new Color(.24f, .10f, .075f), 16);
            result.SetAsFirstSibling();
            view.gameOverText.fontStyle = FontStyle.Bold;
            foreach (var text in view.GetComponentsInChildren<Text>(true))
            {
                text.font = headingFont;
                text.fontStyle = FontStyle.Normal;
            }
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
