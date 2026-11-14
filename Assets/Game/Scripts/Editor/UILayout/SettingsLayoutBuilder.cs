using System;
using RhythmDojo.Gameplay;
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
            view.dimOpacity = shade.AddComponent<CanvasGroup>();
            view.dismiss = shade.AddComponent<Button>();
            view.dismiss.transition = Selectable.Transition.None;
            view.dismiss.targetGraphic = shade.GetComponent<Image>();
            view.dismiss.navigation = new Navigation { mode = Navigation.Mode.None };

            var popup = Panel(
                "Settings Popup",
                canvas.transform,
                new Vector2(130, -35),
                new Vector2(1020, 650),
                new Color(.015f, .02f, .025f, .88f)
            );
            view.panelRect = (RectTransform)popup.transform;
            view.panelRect.anchorMin = view.panelRect.anchorMax = new Vector2(.5f, .5f);
            view.panelRect.pivot = new Vector2(.5f, .5f);
            view.panelRect.anchoredPosition = Vector2.zero;
            view.panelOpacity = popup.AddComponent<CanvasGroup>();
            UiElements.Label(
                "Title",
                popup.transform,
                "설정",
                new Vector2(32, -24),
                new Vector2(700, 45),
                32,
                role: UiTextRole.Title
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
                20,
                role: UiTextRole.Title
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
                    text.font = RhythmDojo.UI.UiFontSettings.Current;
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
                14,
                role: UiTextRole.Help
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
                new Vector2(180, 0),
                new Vector2(520, 64)
            );
            Caption(parent, "해상도", -94);
            view.resolutionList = UiElements.Dropdown(
                "Resolution",
                parent,
                new Vector2(180, -94),
                new Vector2(520, 64)
            );
            Caption(parent, "FPS", -188);
            view.fpsList = UiElements.Dropdown(
                "Frame Rate",
                parent,
                new Vector2(180, -188),
                new Vector2(520, 64)
            );
            view.qualityButtons = System.Array.Empty<Button>();
            view.qualityLabels = System.Array.Empty<Text>();
            Caption(parent, "DSP 버퍼", -282);
            view.bufferList = UiElements.Dropdown(
                "DSP Buffer",
                parent,
                new Vector2(180, -282),
                new Vector2(520, 64)
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
                new Vector2(0, -350),
                new Vector2(700, 40),
                16
            );
            // Buffer validation reveals its status only while checking or after an application.
            view.displayState.gameObject.SetActive(false);
            view.actualState.gameObject.SetActive(false);
            view.status.gameObject.SetActive(false);
            foreach (var dropdown in new[] { view.aspectList, view.resolutionList, view.fpsList, view.bufferList })
            {
                dropdown.captionText.fontSize = 24;
                dropdown.itemText.fontSize = 24;
                dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 240);
                var item = (RectTransform)dropdown.itemText.transform.parent;
                item.sizeDelta = new Vector2(item.sizeDelta.x, 48);
                var content = (RectTransform)item.parent;
                content.sizeDelta = new Vector2(content.sizeDelta.x, 48);
            }

            view.apply = Tab("Apply", popup.transform, "적용", new Vector2(640, -574));
            view.back = Tab("Back", popup.transform, "닫기", new Vector2(816, -574));

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
            BuildKeySettings(view);

            view.focusHudButton = UiElements.Button("Focus HUD", view.gameplayPanel.transform,
                "HUD · 기본", new Vector2(0, -408), new Vector2(230, 38));
            UiElements.Label("Rail Opacity Label", view.gameplayPanel.transform, "레인 불투명도",
                new Vector2(260, -408), new Vector2(190, 38), 22);
            view.railOpacity = Volume("Rail Opacity", view.gameplayPanel.transform, -416);
            UiElements.Position((RectTransform)view.railOpacity.transform,
                new Vector2(460, -416), new Vector2(240, 26));
            PixelOptionsStyle.Apply(view);
            PixelOptionsStyle.StyleKeySettings(view);
            ApplyFont(view);
            ApplyCaptionSizes(view);
            SetCaptionSize(view.focusHudButton.GetComponentInChildren<Text>(), 26, 22);
            ApplyPlayRuleSizes(view);
            BuildJudgmentPosition(view);
            TimingCalibrationLayoutBuilder.Build(view);
            var help = UiElements.Label("Navigation Help", popup.transform, "Esc  닫기",
                new Vector2(38, -584), new Vector2(220, 28), 18, role: UiTextRole.Help);
            help.color = new Color(.40f, .28f, .18f);

            view.back.GetComponentInChildren<Text>().text = "닫기";
            foreach (var selectable in view.GetComponentsInChildren<Selectable>(true))
            {
                var colors = selectable.colors;
                colors.fadeDuration = .09f;
                selectable.colors = colors;
            }

            return view;
        }

        internal static void BuildJudgmentPosition(SettingsView view)
        {
            var parent = view.gameplayPanel.transform;
            var label = UiElements.Label(
                "Judgment Position Label",
                parent,
                "판정 표시 위치",
                new Vector2(320, 0),
                new Vector2(168, 34),
                24
            );
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(.24f, .12f, .085f);

            var dropdown = UiElements.Dropdown(
                "Judgment Position",
                parent,
                new Vector2(500, 0),
                new Vector2(200, 34)
            );
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string> { "상단", "중앙", "하단" });
            dropdown.SetValueWithoutNotify(1);
            PixelOptionsStyle.ApplyDropdownStyle(dropdown);
            SetCaptionSize(dropdown.captionText, 24, 20);
            SetCaptionSize(dropdown.itemText, 24, 20);
            dropdown.captionText.rectTransform.offsetMin = new Vector2(12, 2);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-40, -2);
            view.judgmentPositionList = dropdown;
        }

        public static void BuildKeySettings(SettingsView view)
        {
            if (view.keySettingsTab)
            {
                return;
            }
            var popup = view.transform.Find("Settings Popup");
            view.keySettingsTab = Tab("Key Settings Tab", popup, "키 셋팅", new Vector2(28, -309));
            view.keySettingsPanel = Panel("Key Settings Panel", popup, new Vector2(238, -110),
                new Vector2(740, 380), Color.clear);
            var parent = view.keySettingsPanel.transform;
            UiElements.Label("Key Settings Heading", parent, "플레이 키 설정", Vector2.zero,
                new Vector2(700, 44), 34, role: UiTextRole.Title);
            var mode = SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath).catalog[0].Mode;
            var viewport = Panel("Key Rows Viewport", parent, new Vector2(0, -58),
                new Vector2(700, 208), Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            view.keyRowsScroll = viewport.AddComponent<ScrollRect>();
            view.keyRowsScroll.horizontal = false;
            view.keyRowsScroll.vertical = true;
            view.keyRowsScroll.scrollSensitivity = 28;
            view.keyRowsScroll.movementType = ScrollRect.MovementType.Clamped;
            view.keyRowsScroll.viewport = (RectTransform)viewport.transform;
            var content = new GameObject("Key Rows", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            view.keyRowsContainer = (RectTransform)content.transform;
            view.keyRowsContainer.anchorMin = new Vector2(0, 1);
            view.keyRowsContainer.anchorMax = new Vector2(1, 1);
            view.keyRowsContainer.pivot = new Vector2(0, 1);
            view.keyRowsContainer.anchoredPosition = Vector2.zero;
            view.keyRowsContainer.sizeDelta = new Vector2(0, mode.LaneCount * 52);
            view.keyRowsScroll.content = view.keyRowsContainer;

            var template = new GameObject("Key Row Template", typeof(RectTransform), typeof(KeyBindingRowView));
            template.transform.SetParent(parent, false);
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0, 1);
            templateRect.anchorMax = new Vector2(1, 1);
            templateRect.pivot = new Vector2(0, 1);
            templateRect.sizeDelta = new Vector2(0, 52);
            view.keyRowTemplate = template.GetComponent<KeyBindingRowView>();
            view.keyRowTemplate.caption = UiElements.Label("Lane Caption", template.transform,
                "", Vector2.zero, new Vector2(410, 48), 26);
            view.keyRowTemplate.button = UiElements.Button("Lane Key", template.transform,
                "", new Vector2(430, 0), new Vector2(250, 46));
            SetCaptionSize(view.keyRowTemplate.button.GetComponentInChildren<Text>(), 30, 18);
            template.SetActive(false);
            view.keyBindingButtons = new Button[mode.LaneCount];
            for (int i = 0; i < view.keyBindingButtons.Length; i++)
            {
                var row = UnityEngine.Object.Instantiate(view.keyRowTemplate, view.keyRowsContainer);
                row.name = "Lane Row " + i;
                row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -i * 52);
                row.caption.text = (i + 1) + " · " + mode.GetLane(i).displayName;
                row.button.GetComponentInChildren<Text>().text = mode.GetLane(i).displayName;
                view.keyBindingButtons[i] = row.button;
            }
            UiElements.Label("Key Settings Help", parent,
                "바꿀 키를 누른 뒤 원하는 키보드 키를 입력하세요.\nEsc: 변경 취소 · 시작·복귀 키 및 중복 키 사용 불가",
                new Vector2(0, -274), new Vector2(700, 52), 23, role: UiTextRole.Help);
            view.keyBindingStatus = UiElements.Label("Key Binding Status", parent,
                "변경한 키는 이 모드에 자동 저장됩니다.",
                new Vector2(0, -328), new Vector2(700, 34), 22);
            view.resetKeys = UiElements.Button("Reset Play Keys", parent, "기본값으로 복원",
                new Vector2(0, -366), new Vector2(250, 40));
            view.resetKeys.GetComponentInChildren<Text>().fontSize = 26;
            view.keySettingsPanel.SetActive(false);
        }

        public static void ApplyFont(SettingsView view)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            if (!font)
            {
                throw new InvalidOperationException("Settings brush font missing.");
            }
            var scope = view.GetComponent<UiFontScope>();
            if (!scope)
            {
                scope = view.gameObject.AddComponent<UiFontScope>();
            }
            scope.font = font;
            UiFontSettings.Apply(view.apply.GetComponentInChildren<Text>(true), UiTextRole.Title);
            UiFontSettings.ApplyTo(view.transform);
        }

        public static void ApplyCaptionSizes(SettingsView view)
        {
            foreach (var text in new[] { view.masterLabel, view.backgroundLabel, view.gameplayLabel })
            {
                SetCaptionSize(text, 32, 28);
            }
            SetCaptionSize(view.speedLabel, 30, 26);
            SetCaptionSize(view.timingLabel, 30, 26);
            foreach (Transform child in view.graphicsPanel.transform)
            {
                var text = child.GetComponent<Text>();
                if (text && child.name.EndsWith(" Label", StringComparison.Ordinal))
                {
                    SetCaptionSize(text, 30, 26);
                }
            }
            foreach (Transform child in view.gameplayPanel.transform)
            {
                var text = child.GetComponent<Text>();
                if (text && child.name.StartsWith("Play Rule ", StringComparison.Ordinal))
                {
                    SetCaptionSize(text, 26, 23);
                }
            }
        }

        public static void ApplyPlayRuleSizes(SettingsView view)
        {
            for (int i = 0; i < view.playRuleLists.Length; i++)
            {
                float y = -156 - i * 48;
                var text = view.gameplayPanel.transform.Find("Play Rule " + i).GetComponent<Text>();
                UiElements.Position(text.rectTransform, new Vector2(0, y), new Vector2(200, 44));
                SetCaptionSize(text, 30, 28);
                text.alignment = TextAnchor.MiddleLeft;

                var dropdown = view.playRuleLists[i];
                UiElements.Position((RectTransform)dropdown.transform, new Vector2(210, y), new Vector2(490, 44));
                SetCaptionSize(dropdown.captionText, 30, 28);
                dropdown.captionText.alignment = TextAnchor.MiddleLeft;
                dropdown.captionText.rectTransform.offsetMin = new Vector2(16, 2);
                dropdown.captionText.rectTransform.offsetMax = new Vector2(-44, -2);
                SetCaptionSize(dropdown.itemText, 30, 28);
                dropdown.itemText.alignment = TextAnchor.MiddleLeft;
                dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 208);
                var item = (RectTransform)dropdown.itemText.transform.parent;
                item.sizeDelta = new Vector2(item.sizeDelta.x, 52);
                var content = (RectTransform)item.parent;
                content.sizeDelta = new Vector2(content.sizeDelta.x, 52);
            }
        }

        private static void SetCaptionSize(Text text, int size, int minimumSize)
        {
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = size;
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
                new Vector2(0, y - 12),
                new Vector2(170, 40),
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
