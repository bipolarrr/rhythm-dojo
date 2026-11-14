using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class GameplayAtmosphereBuilder
    {
        private static readonly Color Ink = new Color(.065f, .075f, .075f, .8f);

        public static GameplayAtmosphere Build(Transform parent, RectTransform backdrop)
        {
            var atmosphere = parent.gameObject.AddComponent<GameplayAtmosphere>();
            backdrop.GetComponent<Image>().color = new Color(.84f, .86f, .86f);

            // Static depth, behind the world viewport. No wash can obscure an incoming note.
            Brush("Quiet Corridor", parent, new Color(.045f, .065f, .07f, .62f),
                .23f, -.08f, .77f, .98f, true);
            Brush("Left Edge Ink", parent, Ink, -.22f, -.16f, .23f, 1.05f, true);
            Brush("Right Edge Ink", parent, Ink, .77f, -.16f, 1.22f, 1.05f, true);

            var light = Group("Window Atmosphere", backdrop);
            atmosphere.windowLight = light.gameObject.AddComponent<CanvasGroup>();
            atmosphere.windowLight.blocksRaycasts = false;
            atmosphere.windowLight.alpha = .58f;
            var shafts = Group("Window Shafts", light).gameObject.AddComponent<GameplayWindowLight>();
            shafts.color = new Color(1, .83f, .55f);
            shafts.raycastTarget = false;
            var dust = Group("Sparse Window Dust", light).gameObject.AddComponent<GameplayWindowLight>();
            dust.dust = true;
            dust.color = new Color(1, .90f, .69f);
            dust.raycastTarget = false;

            var floor = Group("Floor Ink Atmosphere", parent);
            atmosphere.floorInk = floor.gameObject.AddComponent<CanvasGroup>();
            atmosphere.floorInk.alpha = .72f;
            atmosphere.floorInk.blocksRaycasts = false;
            atmosphere.farInk = Brush("Distant Floor Wash", floor,
                new Color(.08f, .12f, .13f, .55f), .12f, -.12f, .93f, .33f, true).rectTransform;
            atmosphere.nearInk = Brush("Foreground Dry Ink", floor, Ink,
                -.06f, -.055f, 1.04f, .10f, false).rectTransform;
            atmosphere.nearInk.localRotation = Quaternion.Euler(0, 0, -2);
            Brush("Foreground Lifted Bristles", floor, new Color(.14f, .18f, .17f, .38f),
                .08f, .015f, .94f, .065f, false);
            atmosphere.Validate();
            return atmosphere;
        }

        public static void RefineHud(GameplayHudView view)
        {
            var song = (RectTransform)view.songInformationPanel.transform;
            Bounds(song, .04f, .31f, .24f, .75f);
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
