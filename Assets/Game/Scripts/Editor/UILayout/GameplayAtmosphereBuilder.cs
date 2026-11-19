using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class GameplayAtmosphereBuilder
    {
        public static void RefineHud(GameplayHudView view)
        {
            var song = (RectTransform)view.songInformationPanel.transform;
            Bounds(song, .065f, .34f, .235f, .70f);
            var frame = song.Find("Song Wood Frame").GetComponent<Image>();
            frame.color = new Color(.78f, .83f, .82f);
            frame.pixelsPerUnitMultiplier = 16;
            Bounds(view.album.rectTransform, .075f, .34f, .925f, .94f);
            Bounds(view.songTitle.rectTransform, .08f, .12f, .92f, .30f);
            Bounds(view.songDetails.rectTransform, .08f, .025f, .92f, .115f);
            var titleInk = Brush("Song Title Dry Ink", song, new Color(.035f, .045f, .045f, .78f),
                .02f, .105f, .98f, .31f, false);
            titleInk.transform.SetSiblingIndex(view.songTitle.transform.GetSiblingIndex());
            var brushFont = SceneResources.Require<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            view.songTitle.font = brushFont;
            view.songTitle.fontStyle = FontStyle.Normal;
            view.songTitle.resizeTextMaxSize = 33;
            view.songTitle.gameObject.AddComponent<UiFontScope>().font = brushFont;
            view.songDetails.color = new Color(.82f, .79f, .68f);

            var gaugeFrame = view.healthGauge.Find("Gauge Wood Frame").GetComponent<Image>();
            gaugeFrame.color = new Color(.74f, .82f, .80f);
            view.healthGauge.anchorMax = new Vector2(view.healthGauge.anchorMin.x + .029f,
                view.healthGauge.anchorMax.y);
            var grain = Brush("Gauge Pigment", view.healthFill.transform,
                new Color(.12f, .24f, .19f, .24f), .05f, 0, .32f, 1, false);
            grain.contrastStroke = true;
            Brush("Gauge Paper Light", view.healthFill.transform,
                new Color(1, .95f, .76f, .32f), .55f, -.08f, 1.08f, 1.08f, true);

            view.combo.font = brushFont;
            view.combo.fontStyle = FontStyle.Normal;
            var comboScope = view.combo.gameObject.AddComponent<UiFontScope>();
            comboScope.font = brushFont;
            comboScope.style = FontStyle.Normal;
            var outline = view.combo.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.09f, .13f, .15f, .96f);
            outline.effectDistance = new Vector2(2, -2);
            BuildJudgment(view, brushFont);
        }

        private static void BuildJudgment(GameplayHudView view, Font brushFont)
        {
            var parent = (RectTransform)view.judgmentGroup.transform;
            var motion = parent.gameObject.AddComponent<GameplayJudgmentMotion>();
            motion.group = view.judgmentGroup;
            motion.label = view.judgment;
            view.judgment.font = brushFont;
            view.judgment.fontStyle = FontStyle.Normal;
            view.judgment.gameObject.AddComponent<UiFontScope>().font = brushFont;
            Bounds(view.judgment.rectTransform, .10f, .30f, .90f, 1);
            Bounds(view.timing.rectTransform, .10f, -.02f, .90f, .30f);

            var echo = new GameObject("Judgment Brush Echo", typeof(RectTransform), typeof(Text));
            echo.transform.SetParent(parent, false);
            echo.transform.SetAsFirstSibling();
            motion.echo = echo.GetComponent<Text>();
            EditorUtility.CopySerialized(view.judgment, motion.echo);
            Bounds(motion.echo.rectTransform, .10f, .30f, .90f, 1);
            motion.echo.raycastTarget = false;
            motion.echo.text = "";
            motion.stroke = Brush("Judgment Dry Underline", parent, Color.white,
                .36f, .32f, .64f, .41f, false);
            motion.stroke.transform.SetAsFirstSibling();
            view.judgmentMotion = motion;
            motion.ResetPresentation();
        }

        private static RectTransform Group(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Bounds(rect, 0, 0, 1, 1);
            return rect;
        }

        private static DojoInkGraphic Brush(string name, Transform parent, Color color,
            float x0, float y0, float x1, float y1, bool wash)
        {
            var rect = Group(name, parent);
            Bounds(rect, x0, y0, x1, y1);
            var ink = rect.gameObject.AddComponent<DojoInkGraphic>();
            ink.color = color;
            ink.wash = wash;
            ink.raycastTarget = false;
            return ink;
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
