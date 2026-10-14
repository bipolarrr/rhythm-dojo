using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class SettingsLayoutBuilder
    {
        private static readonly Color accent = new Color(.27f, .82f, .88f);

        public static SettingsView Build()
        {
            var canvas = UiElements.Canvas("Settings");
            canvas.sortingOrder = 100;

            var view = canvas.gameObject.AddComponent<SettingsView>();
            var shade = Panel(
                "Modal Shade",
                canvas.transform,
                Vector2.zero,
                new Vector2(1280, 720),
                new Color(0, 0, 0, .48f)
            );
            shade.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            shade.GetComponent<RectTransform>().anchorMax = Vector2.one;
            shade.GetComponent<RectTransform>().offsetMin = shade
                .GetComponent<RectTransform>()
                .offsetMax = Vector2.zero;

            var popup = Panel(
                "Settings Popup",
                canvas.transform,
                new Vector2(130, -65),
                new Vector2(1020, 590),
                new Color(.015f, .02f, .025f, .88f)
            );
            UiElements.Label(
                "Title",
                popup.transform,
                "설정",
                new Vector2(32, -24),
                new Vector2(700, 45),
                32
            );
            Panel(
                "Header Line",
                popup.transform,
                new Vector2(30, -84),
                new Vector2(960, 2),
                new Color(1, 1, 1, .14f)
            );
            view.soundTab = Tab("Sound Tab", popup.transform, "사운드", new Vector2(28, -108));
            view.graphicsTab = Tab(
                "Graphics Tab",
                popup.transform,
                "그래픽",
                new Vector2(28, -175)
            );
            view.gameplayTab = Tab(
                "Gameplay Tab",
                popup.transform,
                "게임플레이",
                new Vector2(28, -242)
            );
            Panel(
                "Tab Divider",
                popup.transform,
                new Vector2(202, -104),
                new Vector2(2, 380),
                new Color(1, 1, 1, .14f)
            );
            view.soundPanel = Panel(
                "Sound Panel",
                popup.transform,
                new Vector2(238, -110),
                new Vector2(740, 380),
                Color.clear
            );
            view.graphicsPanel = Panel(
                "Graphics Panel",
                popup.transform,
                new Vector2(238, -104),
                new Vector2(740, 390),
                Color.clear
            );
            view.gameplayPanel = Panel(
                "Gameplay Panel",
                popup.transform,
                new Vector2(238, -110),
                new Vector2(740, 380),
                Color.clear
            );
            view.speedLabel = UiElements.Label(
                "Speed Caption",
                view.gameplayPanel.transform,
                "속도  1.0x",
                Vector2.zero,
                new Vector2(700, 32),
                21
            );
            view.speedSlider = Volume("Note Speed", view.gameplayPanel.transform, -42);

            var speedObject = view.speedSlider.gameObject;
            var speedFill = view.speedSlider.fillRect;
            var speedHandle = view.speedSlider.handleRect;
            var speedGraphic = view.speedSlider.targetGraphic;
            UnityEngine.Object.DestroyImmediate(view.speedSlider);

            // Replace the default slider with the existing 0.1-step keyboard control.
            view.speedSlider = speedObject.AddComponent<SteppedSpeedSlider>();
            view.speedSlider.fillRect = speedFill;
            view.speedSlider.handleRect = speedHandle;
            view.speedSlider.targetGraphic = speedGraphic;
            view.speedSlider.wholeNumbers = false;
            view.speedSlider.minValue = 1;
            view.speedSlider.maxValue = 10;
            view.timingLabel = UiElements.Label(
                "Timing Caption",
                view.gameplayPanel.transform,
                "입력 타이밍 보정  0 ms",
                new Vector2(0, -82),
                new Vector2(700, 32),
                21
            );
            view.timingOffset = Volume("Input Timing Offset", view.gameplayPanel.transform, -124);
            view.timingOffset.minValue = -200;
            view.timingOffset.maxValue = 200;
            UiElements.Label(
                "Hard Mode Heading",
                view.gameplayPanel.transform,
                "하드 모드 설정  ·  회복 없음",
                new Vector2(0, -162),
                new Vector2(700, 28),
                20
            );
            view.hardRuleLists = new Dropdown[3];
            view.hardLossCounts = new InputField[3];

            var grades = new[] { "Perfect", "Good", "Miss" };

            for (int i = 0; i < grades.Length; i++)
            {
                float y = -202 - i * 44;
                UiElements.Label(
                    grades[i] + " Rule Label",
                    view.gameplayPanel.transform,
                    grades[i],
                    new Vector2(0, y),
                    new Vector2(120, 34),
                    20
                );
                view.hardRuleLists[i] = UiElements.Dropdown(
                    grades[i] + " Health Rule",
                    view.gameplayPanel.transform,
                    new Vector2(128, y),
                    new Vector2(330, 34)
                );
                StyleHardRuleDropdown(view.hardRuleLists[i]);

                var countObject = DefaultControls.CreateInputField(new DefaultControls.Resources());
                countObject.name = grades[i] + " Loss Count";
                countObject.transform.SetParent(view.gameplayPanel.transform, false);
                UiElements.Position(
                    (RectTransform)countObject.transform,
                    new Vector2(480, y),
                    new Vector2(105, 34)
                );
                view.hardLossCounts[i] = countObject.GetComponent<InputField>();
                view.hardLossCounts[i].contentType = InputField.ContentType.IntegerNumber;
                view.hardLossCounts[i].characterLimit = 4;

                foreach (var text in countObject.GetComponentsInChildren<Text>(true))
                {
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 18;
                    text.alignment = TextAnchor.MiddleCenter;
                }

                UiElements.Label(
                    grades[i] + " Count Unit",
                    view.gameplayPanel.transform,
                    "회",
                    new Vector2(595, y),
                    new Vector2(90, 34),
                    18
                );
            }

            view.hardModeHint = UiElements.Label(
                "Hard Mode Help",
                view.gameplayPanel.transform,
                "하드 모드에서만 적용 · 체력 감소 5회 = 판정마다 20 감소 · 자동 저장",
                new Vector2(0, -338),
                new Vector2(700, 34),
                14
            );

            foreach (Transform child in view.gameplayPanel.transform)
            {
                if (child.GetComponent<RectTransform>().anchoredPosition.y <= -162)
                {
                    child.gameObject.SetActive(false);
                }
            }

            view.playRuleLists = new Dropdown[5];

            var ruleLabels = new[] { "판정", "플레이", "롱노트 끝", "Gauge 실패", "Challenge" };

            for (int i = 0; i < view.playRuleLists.Length; i++)
            {
                float y = -164 - i * 39;
                UiElements.Label(
                    "Play Rule " + i,
                    view.gameplayPanel.transform,
                    ruleLabels[i],
                    new Vector2(0, y),
                    new Vector2(215, 34),
                    18
                );
                view.playRuleLists[i] = UiElements.Dropdown(
                    "Play Rule Option " + i,
                    view.gameplayPanel.transform,
                    new Vector2(230, y),
                    new Vector2(470, 34)
                );
                StyleHardRuleDropdown(view.playRuleLists[i]);
            }

            view.masterLabel = UiElements.Label(
                "Master Volume Label",
                view.soundPanel.transform,
                "전체 소리 100%",
                Vector2.zero,
                new Vector2(680, 38),
                24
            );
            view.masterVolume = Volume("Master Volume", view.soundPanel.transform, -60);
            view.backgroundLabel = UiElements.Label(
                "Background Volume Label",
                view.soundPanel.transform,
                "배경음악 100%",
                new Vector2(0, -114),
                new Vector2(680, 38),
                24
            );
            view.backgroundVolume = Volume("Background Volume", view.soundPanel.transform, -174);
            view.gameplayLabel = UiElements.Label(
                "Gameplay Volume Label",
                view.soundPanel.transform,
                "플레이 음악 100%",
                new Vector2(0, -228),
                new Vector2(680, 38),
                24
            );
            view.gameplayVolume = Volume("Gameplay Volume", view.soundPanel.transform, -288);

            var parent = view.graphicsPanel.transform;
            Caption(parent, "화면 비율", 0);
            view.aspectList = UiElements.Dropdown(
                "Aspect Ratio",
                parent,
                new Vector2(230, 0),
                new Vector2(470, 40)
            );
            Caption(parent, "해상도", -54);
            view.resolutionList = UiElements.Dropdown(
                "Resolution",
                parent,
                new Vector2(230, -54),
                new Vector2(470, 40)
            );
            Caption(parent, "FPS", -108);
            view.fpsList = UiElements.Dropdown(
                "Frame Rate",
                parent,
                new Vector2(230, -108),
                new Vector2(470, 40)
            );
            view.qualityButtons = System.Array.Empty<Button>();
            view.qualityLabels = System.Array.Empty<Text>();
            Caption(parent, "DSP 버퍼", -162);
            view.bufferList = UiElements.Dropdown(
                "DSP Buffer",
                parent,
                new Vector2(230, -162),
                new Vector2(470, 40)
            );
            view.displayState = UiElements.Label(
                "Display State",
                parent,
                "",
                new Vector2(0, -216),
                new Vector2(700, 50),
                17
            );
            view.actualState = UiElements.Label(
                "Actual Audio State",
                parent,
                "",
                new Vector2(0, -276),
                new Vector2(700, 45),
                16
            );
            view.status = UiElements.Label(
                "Audio Status",
                parent,
                "",
                new Vector2(0, -326),
                new Vector2(700, 40),
                14
            );
            view.apply = Tab("Apply", popup.transform, "적용", new Vector2(640, -514));
            view.back = Tab("Back", popup.transform, "닫기", new Vector2(816, -514));

            var nav = view.bufferList.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = view.fpsList;
            nav.selectOnDown = view.apply;
            view.bufferList.navigation = nav;
            nav = view.apply.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = view.bufferList;
            nav.selectOnRight = view.back;
            view.apply.navigation = nav;
            nav = view.back.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = view.apply;
            nav.selectOnUp = view.graphicsTab;
            view.back.navigation = nav;
            nav = view.speedSlider.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = view.gameplayTab;
            nav.selectOnDown = view.timingOffset;
            view.speedSlider.navigation = nav;
            nav = view.timingOffset.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = view.speedSlider;
            nav.selectOnDown = view.apply;
            view.timingOffset.navigation = nav;
            view.soundPanel.SetActive(true);
            view.graphicsPanel.SetActive(false);
            view.gameplayPanel.SetActive(false);

            PixelOptionsStyle.Apply(view);

            return view;
        }

        private static void StyleHardRuleDropdown(Dropdown dropdown)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);

            if (!font)
            {
                throw new InvalidOperationException("Settings Korean font missing.");
            }

            var background = new Color(.09f, .115f, .13f);
            dropdown.GetComponent<Image>().color = background;

            foreach (var image in dropdown.template.GetComponentsInChildren<Image>(true))
            {
                image.color = background;
            }

            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            {
                text.font = font;
                text.fontSize = 18;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            dropdown.captionText.rectTransform.offsetMin = new Vector2(12, 0);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-32, 0);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 140);

            var content = dropdown.template.Find("Viewport/Content") as RectTransform;
            var item = dropdown.template.Find("Viewport/Content/Item") as RectTransform;

            if (!content || !item)
            {
                throw new InvalidOperationException("Hard rule dropdown template missing.");
            }

            content.sizeDelta = new Vector2(content.sizeDelta.x, 44);
            item.sizeDelta = new Vector2(item.sizeDelta.x, 44);
            dropdown.itemText.rectTransform.offsetMin = new Vector2(28, 2);
            dropdown.itemText.rectTransform.offsetMax = new Vector2(-12, -2);

            var arrow = dropdown.transform.Find("Arrow");

            if (arrow)
            {
                arrow.GetComponent<Image>().enabled = false;
            }

            var indicator = UiElements.Label(
                "Rule Expand Indicator",
                dropdown.transform,
                "▾",
                new Vector2(302, -3),
                new Vector2(20, 28),
                18
            );
            indicator.font = font;
            indicator.color = accent;
            indicator.alignment = TextAnchor.MiddleCenter;
            indicator.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void Caption(Transform parent, string caption, float y) =>
            UiElements.Label(
                caption + " Label",
                parent,
                caption,
                new Vector2(0, y - 4),
                new Vector2(215, 36),
                23
            );

        private static GameObject Panel(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)go.transform, position, size);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = color.a > 0;

            return go;
        }

        private static Button Tab(string name, Transform parent, string caption, Vector2 position)
        {
            var button = UiElements.Button(name, parent, caption, position, new Vector2(158, 52));
            button.image.color = new Color(.13f, .17f, .20f, .95f);

            var text = button.GetComponentInChildren<Text>();
            text.color = Color.white;
            text.fontSize = 23;

            var colors = button.colors;
            colors.highlightedColor = new Color(.5f, .9f, .95f);
            colors.selectedColor = new Color(.5f, .9f, .95f);
            button.colors = colors;

            return button;
        }

        private static Slider Volume(string name, Transform parent, float y)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            UiElements.Position(
                (RectTransform)go.transform,
                new Vector2(0, y),
                new Vector2(700, 26)
            );

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.fillRect.GetComponent<Image>().color = accent;

            return slider;
        }
    }
}
