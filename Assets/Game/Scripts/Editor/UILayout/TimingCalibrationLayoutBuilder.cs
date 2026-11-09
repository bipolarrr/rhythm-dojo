using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.EditorTools
{
    public static class TimingCalibrationLayoutBuilder
    {
        private const float LaneWidth = 300;
        private const float LaneHeight = 452;
        private const float HitLine = 393;

        public static void Build(SettingsView view)
        {
            var result = view.gameObject.AddComponent<TimingCalibrationView>();
            result.entry = CreateButton(
                "Timing Calibration",
                view.gameplayPanel.transform,
                "박자 테스트",
                new Vector2(375, -77),
                new Vector2(155, 38),
                view.back
            );
            result.speedEntry = CreateButton(
                "Speed Test",
                view.gameplayPanel.transform,
                "속도 테스트",
                new Vector2(540, -77),
                new Vector2(155, 38),
                view.back
            );

            result.overlay = new GameObject("Timing Calibration Overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = (RectTransform)result.overlay.transform;
            overlayRect.SetParent(view.transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.offsetMin = Vector2.zero;
            result.overlay.GetComponent<Image>().color = new Color(.13f, .07f, .04f, .85f);

            var panel = new GameObject("Timing Calibration Panel", typeof(RectTransform), typeof(Image));
            result.panelRect = (RectTransform)panel.transform;
            result.panelRect.SetParent(overlayRect, false);
            result.panelRect.pivot = new Vector2(.5f, .5f);
            result.panelRect.anchorMax = result.panelRect.pivot;
            result.panelRect.anchorMin = result.panelRect.pivot;
            result.panelRect.sizeDelta = new Vector2(660, 320);
            panel.GetComponent<Image>().color = new Color(.89f, .76f, .55f);

            result.heading = UiElements.Label(
                "Timing Calibration Heading",
                result.panelRect,
                "입력 타이밍 테스트",
                new Vector2(30, -25),
                new Vector2(600, 50),
                34,
                role: UiTextRole.Title
            );
            result.status = UiElements.Label(
                "Timing Calibration Status",
                result.panelRect,
                "",
                new Vector2(38, -83),
                new Vector2(584, 135),
                23
            );
            result.status.color = new Color(.25f, .12f, .08f);
            result.heading.color = result.status.color;
            result.status.alignment = TextAnchor.MiddleCenter;
            result.heading.alignment = TextAnchor.MiddleCenter;

            result.start = CreateButton(
                "Start Calibration",
                result.panelRect,
                "테스트 시작",
                new Vector2(38, -240),
                new Vector2(176, 52),
                view.apply,
                UiTextRole.Title
            );
            result.apply = CreateButton(
                "Apply Calibration",
                result.panelRect,
                "보정값 적용",
                new Vector2(240, -240),
                new Vector2(180, 52),
                view.apply,
                UiTextRole.Title
            );
            result.close = CreateButton(
                "Close Calibration",
                result.panelRect,
                "닫기",
                new Vector2(446, -240),
                new Vector2(176, 52),
                view.back
            );
            result.speedLane = BuildSpeedLane(result.panelRect);

            result.source = result.overlay.AddComponent<AudioSource>();
            result.source.playOnAwake = false;
            result.timingLayout = new TimingCalibrationLayout
            {
                panelSize = new Vector2(660, 320),
                headingPosition = new Vector2(30, -25),
                headingSize = new Vector2(600, 50),
                statusParent = result.panelRect,
                statusPosition = new Vector2(38, -83),
                statusSize = new Vector2(584, 135),
                startPosition = new Vector2(38, -240),
                startSize = new Vector2(176, 52),
                closePosition = new Vector2(446, -240),
                closeSize = new Vector2(176, 52),
            };
            result.speedLayout = new TimingCalibrationLayout
            {
                panelSize = new Vector2(336, 572),
                headingPosition = new Vector2(18, -4),
                headingSize = new Vector2(300, 48),
                statusParent = result.speedLane.rail,
                statusPosition = new Vector2(18, -174),
                statusSize = new Vector2(264, 82),
                startPosition = new Vector2(18, -518),
                startSize = new Vector2(147, 42),
                closePosition = new Vector2(171, -518),
                closeSize = new Vector2(147, 42),
            };
            result.overlay.SetActive(false);
            view.calibration = result;
            result.Validate();
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string caption,
            Vector2 position,
            Vector2 size,
            Button style,
            UiTextRole role = UiTextRole.Body
        )
        {
            var button = Object.Instantiate(style, parent);
            button.name = name;
            UiElements.Position((RectTransform)button.transform, position, size);

            button.onClick = new Button.ButtonClickedEvent();
            button.navigation = Navigation.defaultNavigation;
            button.interactable = true;

            var label = button.GetComponentInChildren<Text>();
            label.text = caption;
            UiFontSettings.Apply(label, role);

            return button;
        }

        private static SpeedTestLaneView BuildSpeedLane(Transform parent)
        {
            var result = parent.gameObject.AddComponent<SpeedTestLaneView>();
            result.notes = new RectTransform[32];
            result.rail = Image(
                "Speed Test Lane",
                parent,
                new Vector2(18, -56),
                new Vector2(LaneWidth, LaneHeight),
                new Color(.12f, .085f, .07f)
            );
            var lane = new GameObject("Perspective Rail", typeof(RectTransform), typeof(SpeedTestLaneGraphic));
            lane.transform.SetParent(result.rail, false);
            UiElements.Position((RectTransform)lane.transform, Vector2.zero, result.rail.sizeDelta);

            var surface = lane.GetComponent<SpeedTestLaneGraphic>();
            surface.color = new Color(.145f, .16f, .18f);
            surface.raycastTarget = false;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Resources/UI/SongpanNote.png");
            for (int i = 0; i < 32; i++)
            {
                var note = Image("Beat Note " + i, result.rail, Vector2.zero, new Vector2(136, 17), Color.white);
                note.GetComponent<Image>().sprite = sprite;
                result.notes[i] = note;
            }

            float hitWidth = WidthAt(HitLine);
            Image(
                "Speed Test Hit Line",
                result.rail,
                new Vector2((LaneWidth - hitWidth) * .5f, -HitLine + 2),
                new Vector2(hitWidth, 4),
                new Color(.26f, .92f, .91f)
            );

            var display = new GameObject("Speed Test Feedback", typeof(RectTransform), typeof(CanvasGroup));
            display.transform.SetParent(result.rail, false);
            UiElements.Position(
                (RectTransform)display.transform,
                new Vector2(0, -HitLine + 102),
                new Vector2(LaneWidth, 80)
            );

            result.feedback = display.GetComponent<CanvasGroup>();
            result.feedback.interactable = false;
            result.feedback.blocksRaycasts = false;
            result.judgment = CreateFeedbackLabel("Judgment Grade", display.transform, Vector2.zero, 26, 42, UiTextRole.Title);
            result.timingLabel = CreateFeedbackLabel("Judgment Timing", display.transform, new Vector2(0, -37), 16, 28);
            result.timingLabel.color = new Color(.98f, .92f, .77f);

            result.rail.gameObject.SetActive(false);
            return result;
        }

        private static Text CreateFeedbackLabel(
            string name,
            Transform parent,
            Vector2 position,
            int size,
            float height,
            UiTextRole role = UiTextRole.Body
        )
        {
            var label = UiElements.Label(name, parent, "", position, new Vector2(LaneWidth, height), size, role);
            label.alignment = TextAnchor.MiddleCenter;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.08f, .05f, .035f, .9f);
            outline.effectDistance = new Vector2(1, -1);

            return label;
        }

        private static float WidthAt(float depth)
        {
            return Mathf.Lerp(SpeedTestLaneGraphic.FarWidth, SpeedTestLaneGraphic.NearWidth, depth / LaneHeight);
        }

        private static RectTransform Image(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            UiElements.Position(rect, position, size);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

    }
}
