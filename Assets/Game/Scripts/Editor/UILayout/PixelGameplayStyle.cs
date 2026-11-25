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

            var host = PresentationAssetBuilder.Group("Gameplay Viewport", view.transform);
            host.SetAsFirstSibling();
            Bounds(host, .27f, .01f, .73f, .985f);
            var field = PresentationAssetBuilder.BuildField(host);
            view.viewport = field.viewport;
            view.lanes.laneRoot.SetParent(view.viewport.field, false);
            view.lanes.template = BuildKeyTemplate(headingFont);

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
            Bounds(railOpacityLabel.rectTransform, .14f, .375f, .44f, .485f);
            var railOpacity = (RectTransform)view.railOpacity.transform;
            railOpacity.SetParent(dialog, false);
            Bounds(railOpacity, .44f, .375f, .86f, .485f);
            view.railOpacity.targetGraphic.color = new Color(.50f, .18f, .10f);
            Bounds((RectTransform)menu.resumeButton.transform, .14f, .65f, .86f, .79f);
            Bounds((RectTransform)menu.restartButton.transform, .14f, .50f, .86f, .62f);
            Bounds((RectTransform)menu.exitButton.transform, .14f, .23f, .86f, .37f);
            Bounds(menu.recoveryStatus.rectTransform, .08f, .025f, .92f, .075f);
            var hint = UiElements.Label("Settings Menu Hint", dialog,
                "↑↓ 이동 · ←→ 불투명도 조절 · Enter / Space 선택 · Esc 이어하기",
                Vector2.zero, Vector2.zero, 14, role: UiTextRole.Help);
            Bounds(hint.rectTransform, .06f, .09f, .94f, .21f);
            hint.color = DojoScreenStyle.Muted;
            hint.alignment = TextAnchor.MiddleCenter;
            SceneResources.ApplyReadableFont(view.songDetails, view.healthLabel, view.timing, railOpacityLabel);
        }

        private static Image BuildKeyTemplate(Font font)
        {
            var root = PresentationAssetBuilder.Group("Key Legend", null);
            try
            {
                root.anchorMin = root.anchorMax = new Vector2(.5f, 0);
                root.sizeDelta = new Vector2(26, 10);
                var face = root.gameObject.AddComponent<Image>();
                face.sprite = SceneResources.Require<Sprite>(PresentationAssetBuilder.Art + "Keycap.png");
                face.raycastTarget = false;
                var key = UiElements.Label("Key", root, "", Vector2.zero, new Vector2(76, 30), 21);
                key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(.5f, .5f);
                key.rectTransform.pivot = new Vector2(.5f, .5f);
                key.rectTransform.anchoredPosition = Vector2.zero;
                key.rectTransform.localScale = Vector3.one / 3;
                key.alignment = TextAnchor.MiddleCenter;
                key.raycastTarget = false;
                key.font = font;
                return PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Game/Prefabs/LaneKey.prefab")
                    .GetComponent<Image>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
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
