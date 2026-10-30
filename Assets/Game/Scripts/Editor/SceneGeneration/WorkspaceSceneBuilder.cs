using System;
using System.Collections.Generic;
using System.Linq;
using RhythmDojo.Authoring;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor.Events;

namespace RhythmDojo.EditorTools
{
    public static class WorkspaceSceneBuilder
    {
        [MenuItem("Game Tools/Scenes/Build Extended Chart Editor")]
        public static void Build()
        {
            Positions.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before generating scenes.");
            }

            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Build the shared foundation before applying the current editor interface.
            ChartEditorSceneBuilder.BuildBase();
            var scene = EditorSceneManager.OpenScene(ChartEditorSceneBuilder.ScenePath);

            var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>()).Single();
            var screen = canvas.GetComponentInChildren<ChartEditorScreen>();
            var serializedScreen = new SerializedObject(screen);
            serializedScreen.FindProperty("initialSlot").intValue = (int)RhythmDojo.Application.ChartSlot.Easy;
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();
            var workspace = canvas.gameObject.AddComponent<WorkspaceEditor>();
            var view = canvas.gameObject.AddComponent<WorkspaceView>();
            workspace.view = view;
            workspace.screen = screen;
            workspace.menu = canvas.GetComponent<ChartEditorMenu>();
            workspace.timeline = screen.GetComponent<ChartAudioTimeline>();
            workspace.grid = canvas.GetComponentInChildren<ChartBeatGrid>();

            var menuBar = canvas.transform.Find("Menu Bar");
            menuBar.GetComponent<Image>().color = MenuSurface;
            ((RectTransform)menuBar).offsetMin = new Vector2(0, -26);
            var activeChart = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Active Chart");
            activeChart.color = MenuText;
            activeChart.fontSize = 14;
            activeChart.alignment = TextAnchor.MiddleRight;
            UiElements.Position(activeChart.rectTransform, new Vector2(-10, -2), new Vector2(210, 22));
            activeChart.rectTransform.anchorMin = activeChart.rectTransform.anchorMax = new Vector2(1, 1);
            activeChart.rectTransform.pivot = new Vector2(1, 1);

            foreach (string name in new[] { "파일", "설정", "도구", "테스트", "File Panel", "Settings Panel", "Tools Panel" })
            {
                var item = canvas.transform.Find(name);
                if (item)
                {
                    item.gameObject.SetActive(false);
                }
            }

            var bindings = new List<WorkspaceActionBinding>();
            var menus = new List<WorkspaceMenuPopup>();
            var dismissArea = Panel("Menu Dismiss Area", canvas.transform, Vector2.zero, Vector2.zero);
            dismissArea.anchorMin = Vector2.zero;
            dismissArea.anchorMax = Vector2.one;
            dismissArea.offsetMin = dismissArea.offsetMax = Vector2.zero;
            dismissArea.GetComponent<Image>().color = Color.clear;
            var dismissEvents = dismissArea.gameObject.AddComponent<EventTrigger>();
            var dismissEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            UnityEventTools.AddVoidPersistentListener(dismissEntry.callback, view.CloseMenus);
            dismissEvents.triggers.Add(dismissEntry);
            view.menuDismissArea = dismissArea.gameObject;
            dismissArea.gameObject.SetActive(false);
            var file = Menu(canvas.transform, view, "파일", 0, menus);
            Action(file, "새로 만들기", WorkspaceAction.New, bindings);
            Heading(file, "최근 파일 · 최신 저장 순");
            view.recent = Dropdown(file, "Recent Projects", new[] { "저장된 파일" });
            Action(file, "파일 열기", WorkspaceAction.Open, bindings);
            Action(file, "저장", WorkspaceAction.Save, bindings);
            Action(file, "다른 이름으로 저장", WorkspaceAction.SaveAs, bindings);
            Action(file, "PUPA 음원 + 채보 가져오기", WorkspaceAction.ImportPupa, bindings);
            Action(file, "종료 · 타이틀로", WorkspaceAction.Exit, bindings);

            var settings = Menu(canvas.transform, view, "설정", 1, menus);
            view.title = Field(settings, "곡 제목", "제목");
            view.artist = Field(settings, "작곡가", "작곡가 이름");
            view.jacketPath = Field(settings, "자켓 이미지 전체 경로", "PNG / JPG 경로 입력 후 Enter");
            Action(settings, "자켓 이미지 적용", WorkspaceAction.ImportJacket, bindings);
            Heading(settings, "난이도");
            var slots = new GameObject("Difficulty", typeof(RectTransform)).GetComponent<RectTransform>();
            slots.SetParent(settings, false);
            UiElements.Position(slots, new Vector2(4, Next(settings, 24)), new Vector2(478, 24));
            view.difficulty = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var button = canvas.transform.Find("Chart Slot " + i).GetComponent<Button>();
                view.difficulty[i] = button;
                button.transform.SetParent(slots, false);
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 3f, 0);
                rect.anchorMax = new Vector2((i + 1) / 3f, 1);
                rect.offsetMin = new Vector2(1, 0);
                rect.offsetMax = new Vector2(-1, 0);
                button.GetComponentInChildren<Text>().text = new[] { "Easy", "Standard", "Hard" }[i];
                StyleMenuButton(button, TextAnchor.MiddleCenter, 14);
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.offsetMin = new Vector2(4, 0);
                label.rectTransform.offsetMax = new Vector2(-4, 0);
            }
            Heading(settings, "오디오");
            view.audioPath = Field(settings, "음원 전체 경로", "WAV / OGG / MP3 경로 입력 후 Enter");
            Action(settings, "음원 가져오기", WorkspaceAction.ImportAudio, bindings);
            view.volume = Field(settings, "음원 볼륨 (%)", "65");
            view.volume.enabled = false;
            view.volume.textComponent.alignment = TextAnchor.MiddleCenter;
            view.volume.textComponent.raycastTarget = false;
            view.volume.GetComponent<Image>().color = new Color(.78f, .8f, .83f);
            var volumeTrack = Panel("Volume Track", view.volume.transform, Vector2.zero, Vector2.zero);
            volumeTrack.anchorMin = Vector2.zero;
            volumeTrack.anchorMax = Vector2.one;
            volumeTrack.offsetMin = volumeTrack.offsetMax = Vector2.zero;
            volumeTrack.SetAsFirstSibling();
            volumeTrack.GetComponent<Image>().color = new Color(.78f, .8f, .83f);
            var volumeFill = Panel("Volume Fill", volumeTrack, Vector2.zero, Vector2.zero);
            volumeFill.anchorMin = Vector2.zero;
            volumeFill.anchorMax = Vector2.one;
            volumeFill.offsetMin = volumeFill.offsetMax = Vector2.zero;
            volumeFill.SetAsFirstSibling();
            volumeFill.GetComponent<Image>().raycastTarget = false;
            view.volumeControl = volumeTrack.gameObject.AddComponent<Slider>();
            view.volumeControl.transition = Selectable.Transition.None;
            view.volumeControl.fillRect = volumeFill;
            view.volumeControl.direction = Slider.Direction.LeftToRight;
            view.volumeControl.minValue = 0;
            view.volumeControl.maxValue = 100;
            view.volumeControl.wholeNumbers = true;
            view.volumeControl.value = 65;
            view.bpm = Field(settings, "BPM", "120");
            Action(settings, "BPM 자동 측정", WorkspaceAction.EstimateBpm, bindings);
            view.offset = Field(settings, "악곡 시작 타이밍 (ms)", "0");
            Action(settings, "설정 적용", WorkspaceAction.ApplySettings, bindings);

            var edit = Menu(canvas.transform, view, "편집", 2, menus);
            Action(edit, "실행 취소", WorkspaceAction.Undo, bindings);
            Action(edit, "다시 실행", WorkspaceAction.Redo, bindings);
            Action(edit, "복사", WorkspaceAction.Copy, bindings);
            Action(edit, "현재 커서에 붙여넣기", WorkspaceAction.Paste, bindings);
            Action(edit, "선택 범위 삭제", WorkspaceAction.Delete, bindings);
            Action(edit, "모두 선택", WorkspaceAction.SelectAll, bindings);
            Action(edit, "탭 노트 모두 선택", WorkspaceAction.SelectTaps, bindings);
            Action(edit, "롱노트 모두 선택", WorkspaceAction.SelectHolds, bindings);
            Action(edit, "선택 해제", WorkspaceAction.Deselect, bindings);
            Action(edit, "선택 노트 좌우 반전", WorkspaceAction.Mirror, bindings);
            Action(edit, "선택 노트 랜덤 배치", WorkspaceAction.Randomize, bindings);

            var tools = Menu(canvas.transform, view, "도구", 3, menus);
            Action(tools, "선택", WorkspaceAction.Select, bindings);
            Action(tools, "그리기", WorkspaceAction.Pencil, bindings);
            Action(tools, "지우개", WorkspaceAction.Eraser, bindings);
            Action(tools, "잡기", WorkspaceAction.Position, bindings);
            view.position = Field(tools, "작업 위치 (초)", "0");
            BuildToolBar(canvas.transform, view);
            Heading(tools, "퀀타이즈 · 4분음표 기준");
            view.quantization = Dropdown(tools, "Quantization", new[]
            {
                "1/4", "1/8", "1/12", "1/16", "1/24", "1/32", "1/48", "1/64", "1/96", "1/192"
            });
            view.quantization.value = 0;
            var timelineView = workspace.timeline.GetComponent<ChartAudioTimelineView>();
            timelineView.extendedQuantization = true;
            timelineView.noteSnapControl.ClearOptions();
            timelineView.noteSnapControl.AddOptions(view.quantization.options.Select(option => option.text).ToList());
            timelineView.noteSnapControl.value = 0;

            var play = Menu(canvas.transform, view, "재생", 4, menus);
            Action(play, "재생 / 일시정지", WorkspaceAction.PlayPause, bindings);
            Action(play, "처음으로", WorkspaceAction.Stop, bindings);
            Action(play, "테스트 플레이", WorkspaceAction.Test, bindings);
            Action(play, "현재 위치에서 테스트 플레이", WorkspaceAction.TestHere, bindings);
            Action(play, "테스트 플레이 (오토)", WorkspaceAction.AutoTest, bindings);
            Action(play, "현재 위치에서 테스트 플레이 (오토)", WorkspaceAction.AutoTestHere, bindings);

            view.status = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Progress Label");
            view.status.text = "채보 에디터 · 클릭으로 메뉴 열기 / 열린 상태에서 마우스로 전환 / Esc로 닫기";
            view.selection = UiElements.Label("Workspace Selection", canvas.transform,
                "선택 0", new Vector2(820, -265), new Vector2(400, 45), 18);
            var jacket = new GameObject("Workspace Jacket", typeof(RectTransform), typeof(RawImage));
            jacket.transform.SetParent(canvas.transform, false);
            view.jacket = jacket.GetComponent<RawImage>();
            view.jacket.raycastTarget = false;
            view.jacket.enabled = false;
            UiElements.Position(view.jacket.rectTransform, new Vector2(820, -70), new Vector2(150, 150));
            var help = canvas.GetComponentsInChildren<Text>(true).Single(text => text.name == "Help");
            help.text = "채보 에디터\n\n메뉴: 클릭 열기 / 마우스로 전환\n바깥 클릭 / Esc: 메뉴 닫기\n선택: 클릭 토글 / 드래그 범위\n붙여넣기: 현재 파형 커서\n\n설정: 자켓 / 음원 / BPM\nSpace: 재생 / 일시정지\n게임 Esc: 편집 화면 복귀";
            help.fontSize = 14;
            UiElements.Position(help.rectTransform, new Vector2(176, -18), new Vector2(230, 210));

            // The existing help/preview area remains part of the cloned editor.
            var popup = Panel("Unsaved Changes", canvas.transform, new Vector2(0, 0), new Vector2(1280, 720));
            popup.anchorMin = Vector2.zero;
            popup.anchorMax = Vector2.one;
            popup.offsetMin = popup.offsetMax = Vector2.zero;
            view.exitPopup = popup.gameObject;
            UiElements.Label("Question", popup, "저장하지 않은 변경이 있습니다.",
                new Vector2(390, -240), new Vector2(560, 60), 28);
            PopupAction(popup, "저장 후 종료", WorkspaceAction.SaveExit, 300, bindings);
            PopupAction(popup, "저장하지 않고 종료", WorkspaceAction.DiscardExit, 550, bindings);
            PopupAction(popup, "취소", WorkspaceAction.CancelExit, 800, bindings);
            popup.gameObject.SetActive(false);

            var newPopup = Panel("New Project Confirmation", canvas.transform, Vector2.zero, new Vector2(1280, 720));
            newPopup.anchorMin = Vector2.zero;
            newPopup.anchorMax = Vector2.one;
            newPopup.offsetMin = newPopup.offsetMax = Vector2.zero;
            view.newPopup = newPopup.gameObject;
            UiElements.Label("Question", newPopup, "현재 파일을 저장하시겠습니까?",
                new Vector2(360, -240), new Vector2(560, 60), 28).alignment = TextAnchor.MiddleCenter;
            PopupAction(newPopup, "YES", WorkspaceAction.SaveNew, 390, bindings);
            PopupAction(newPopup, "NO", WorkspaceAction.DiscardNew, 660, bindings);
            newPopup.gameObject.SetActive(false);

            var saveAsPopup = Panel("Save As Dialog", canvas.transform, Vector2.zero, Vector2.zero);
            saveAsPopup.anchorMin = Vector2.zero;
            saveAsPopup.anchorMax = Vector2.one;
            saveAsPopup.offsetMin = saveAsPopup.offsetMax = Vector2.zero;
            view.saveAsPopup = saveAsPopup.gameObject;
            var dialog = Panel("Dialog", saveAsPopup, Vector2.zero, new Vector2(480, 220));
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            dialog.anchoredPosition = Vector2.zero;
            dialog.GetComponent<Image>().color = MenuSurface;
            UiElements.Label("Title", dialog, "다른 이름으로 저장", new Vector2(24, -16),
                new Vector2(432, 32), 20).color = MenuText;
            Positions[dialog] = 52;
            view.saveAsName = Field(dialog, "이름", "이름을 입력하세요");
            dialog.sizeDelta = new Vector2(480, 220);
            UiElements.Position(dialog.Find("Heading").GetComponent<RectTransform>(), new Vector2(24, -52), new Vector2(432, 22));
            UiElements.Position(view.saveAsName.GetComponent<RectTransform>(), new Vector2(24, -80), new Vector2(432, 30));
            view.saveAsError = UiElements.Label("Error", dialog, "", new Vector2(24, -114), new Vector2(432, 36), 14);
            view.saveAsError.color = new Color(.7f, .12f, .12f);
            var confirmSaveAs = UiElements.Button("Confirm Save As", dialog, "저장", new Vector2(24, -164), new Vector2(204, 32));
            StyleMenuButton(confirmSaveAs, TextAnchor.MiddleCenter, 14);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.ConfirmSaveAs, button = confirmSaveAs });
            var cancelSaveAs = UiElements.Button("Cancel Save As", dialog, "취소", new Vector2(252, -164), new Vector2(204, 32));
            StyleMenuButton(cancelSaveAs, TextAnchor.MiddleCenter, 14);
            bindings.Add(new WorkspaceActionBinding { action = WorkspaceAction.CancelSaveAs, button = cancelSaveAs });
            saveAsPopup.gameObject.SetActive(false);
            view.actions = bindings.ToArray();
            view.menus = menus.ToArray();
            foreach (var menu in menus)
            {
                SizeMenu(menu);
            }

            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(s => s.path != WorkspaceEditor.ScenePath && s.path != "Assets/Game/Scenes/ChartWorkspace.unity")
                .Concat(new[] { new EditorBuildSettingsScene(WorkspaceEditor.ScenePath, true) }).ToArray();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Game/Scenes/ChartWorkspace.unity"))
            {
                AssetDatabase.DeleteAsset("Assets/Game/Scenes/ChartWorkspace.unity");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Rhythm Dojo: chart editor generated with the workspace interface.");
        }

        private static readonly Dictionary<Transform, float> Positions = new Dictionary<Transform, float>();
        private static readonly Color MenuSurface = new Color(.94f, .94f, .94f);
        private static readonly Color MenuText = new Color(.16f, .16f, .16f);
        private static readonly Color MenuHover = new Color(.78f, .87f, .96f);

        private static RectTransform Menu(Transform parent, WorkspaceView view, string caption,
            int index, List<WorkspaceMenuPopup> menus)
        {
            var root = new GameObject("Workspace " + caption, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)root.transform, new Vector2(4 + index * 48, -1), new Vector2(48, 24));
            var trigger = UiElements.Button("Trigger", root.transform, caption, Vector2.zero, new Vector2(48, 24));
            StyleMenuButton(trigger, TextAnchor.MiddleCenter, 14);
            var viewport = Panel("Panel", root.transform, new Vector2(0, -24), new Vector2(510, 570));
            viewport.GetComponent<Image>().color = MenuSurface;
            var outline = viewport.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .25f);
            outline.effectDistance = new Vector2(1, -1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            UiElements.Position(content, Vector2.zero, new Vector2(510, 570));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            Positions[content] = 4;
            var menu = root.AddComponent<WorkspaceMenuPopup>();
            menu.owner = view;
            menu.panel = viewport.gameObject;
            menu.trigger = trigger;
            viewport.gameObject.SetActive(false);
            menus.Add(menu);
            return content;
        }

        private static float Next(Transform parent, float height)
        {
            float y = Positions[parent];
            Positions[parent] += height + 2;
            var rect = (RectTransform)parent;
            rect.sizeDelta = new Vector2(510, Mathf.Max(570, Positions[parent]));
            return -y;
        }

        private static void Action(Transform parent, string caption, WorkspaceAction action,
            List<WorkspaceActionBinding> bindings)
        {
            string shortcut = WorkspaceShortcuts.Label(action);
            var button = UiElements.Button(action.ToString(), parent, caption,
                new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            StyleMenuButton(button, TextAnchor.MiddleLeft, 14);
            if (!string.IsNullOrEmpty(shortcut))
            {
                var shortcutLabel = AddShortcutLabel(button, shortcut, 14);
                button.GetComponentInChildren<Text>().rectTransform.offsetMax =
                    new Vector2(-shortcutLabel.preferredWidth - 30, 0);
            }
            bindings.Add(new WorkspaceActionBinding { action = action, button = button });
        }

        private static Text AddShortcutLabel(Button button, string shortcut, int fontSize)
        {
            var label = UiElements.Label("Shortcut", button.transform, shortcut, Vector2.zero, Vector2.zero, fontSize);
            label.color = MenuText;
            label.alignment = TextAnchor.MiddleRight;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = new Vector2(-10, 0);
            return label;
        }

        private static void BuildToolBar(Transform parent, WorkspaceView view)
        {
            var tool = parent.Find("Editor Content/Tool Space");
            var toolLabel = tool.Find("Tool Space Label").GetComponent<Text>();
            toolLabel.gameObject.SetActive(false);
            var captions = new[] { "선택", "그리기", "지우개", "잡기" };
            var actions = new[] { WorkspaceAction.Select, WorkspaceAction.Pencil,
                WorkspaceAction.Eraser, WorkspaceAction.Position };
            view.toolActions = new WorkspaceActionBinding[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                var button = UiElements.Button("Tool " + actions[i], tool, captions[i],
                    new Vector2(4, -6 - i * 50), new Vector2(44, 44));
                StyleMenuButton(button, TextAnchor.MiddleLeft, 10);
                var label = button.GetComponentInChildren<Text>();
                label.gameObject.SetActive(false);
                var shortcutLabel = AddShortcutLabel(button, WorkspaceShortcuts.Label(actions[i]), 9);
                UiElements.Position(shortcutLabel.rectTransform, new Vector2(0, -29), new Vector2(41, 12));
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(WorkspaceToolIcon))
                    .GetComponent<WorkspaceToolIcon>();
                icon.transform.SetParent(button.transform, false);
                UiElements.Position(icon.rectTransform, new Vector2(9, -9), new Vector2(26, 26));
                icon.kind = (WorkspaceToolIcon.IconKind)i;
                icon.color = MenuText;
                icon.raycastTarget = false;
                view.toolActions[i] = new WorkspaceActionBinding { action = actions[i], button = button };
            }
            view.quantizationCycle = UiElements.Button("Quantization Cycle", tool, "",
                new Vector2(4, -206), new Vector2(44, 44));
            StyleMenuButton(view.quantizationCycle, TextAnchor.MiddleCenter, 9);
            var caption = view.quantizationCycle.GetComponentInChildren<Text>();
            caption.gameObject.SetActive(false);
            caption.raycastTarget = false;
            view.quantizationCycleValue = UiElements.Label("Value", view.quantizationCycle.transform,
                "1/4", Vector2.zero, new Vector2(44, 44), 13);
            view.quantizationCycleValue.alignment = TextAnchor.MiddleCenter;
            view.quantizationCycleValue.color = MenuText;
            view.quantizationCycleValue.raycastTarget = false;
        }

        private static void StyleMenuButton(Button button, TextAnchor alignment, int fontSize)
        {
            var image = button.GetComponent<Image>();
            image.sprite = null;
            image.color = Color.white;
            var colors = button.colors;
            colors.normalColor = MenuSurface;
            colors.highlightedColor = MenuHover;
            colors.pressedColor = new Color(.65f, .79f, .93f);
            colors.selectedColor = MenuHover;
            colors.fadeDuration = .08f;
            button.colors = colors;

            var label = button.GetComponentInChildren<Text>();
            label.color = MenuText;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.rectTransform.offsetMin = new Vector2(10, 0);
            label.rectTransform.offsetMax = new Vector2(-10, 0);
        }

        private static void SizeMenu(WorkspaceMenuPopup menu)
        {
            var scroll = menu.panel.GetComponent<ScrollRect>();
            float width = 240;
            foreach (Transform item in scroll.content)
            {
                if (item.name == "Difficulty")
                {
                    float slotWidth = item.GetComponentsInChildren<Text>().Max(text => text.preferredWidth);
                    width = Mathf.Max(width, slotWidth * 3 + 40);
                }
                var label = item.GetComponent<Text>();
                if (!label)
                {
                    var button = item.GetComponent<Button>();
                    if (button)
                    {
                        label = button.GetComponentInChildren<Text>();
                    }
                }

                if (label)
                {
                    float requiredWidth = label.preferredWidth + 32;
                    var shortcut = item.Find("Shortcut");
                    if (shortcut)
                    {
                        requiredWidth = label.preferredWidth + shortcut.GetComponent<Text>().preferredWidth + 52;
                    }
                    width = Mathf.Max(width, requiredWidth);
                }
            }

            float height = Positions[scroll.content] + 2;
            scroll.content.sizeDelta = new Vector2(width, height);
            scroll.viewport.sizeDelta = new Vector2(width, Mathf.Min(620, height));
            foreach (RectTransform item in scroll.content)
            {
                item.sizeDelta = new Vector2(width - 8, item.sizeDelta.y);
            }
        }

        private static Text Heading(Transform parent, string caption)
        {
            var label = UiElements.Label("Heading", parent, caption,
                new Vector2(4, Next(parent, 22)), new Vector2(478, 22), 14);
            label.color = new Color(.38f, .38f, .38f);
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private static InputField Field(Transform parent, string caption, string initial)
        {
            Heading(parent, caption);
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = caption;
            go.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)go.transform, new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            foreach (var text in go.GetComponentsInChildren<Text>(true))
            {
                text.font = UiFontSettings.Current;
                text.fontSize = 14;
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(8, 2);
                text.rectTransform.offsetMax = new Vector2(-8, -2);
            }
            var field = go.GetComponent<InputField>();
            field.text = "";
            ((Text)field.placeholder).text = initial;
            if (double.TryParse(initial, out double numeric))
            {
                field.text = initial;
            }
            return field;
        }

        private static Dropdown Dropdown(Transform parent, string name, string[] options)
        {
            var dropdown = UiElements.Dropdown(name, parent,
                new Vector2(4, Next(parent, 24)), new Vector2(478, 24));
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = 14;
            }
            dropdown.captionText.rectTransform.offsetMin = new Vector2(8, 2);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-24, -2);
            dropdown.ClearOptions();
            dropdown.AddOptions(options.ToList());
            return dropdown;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(.075f, .09f, .13f, .98f);
            var rect = (RectTransform)panel.transform;
            UiElements.Position(rect, position, size);
            return rect;
        }

        private static void PopupAction(Transform parent, string caption, WorkspaceAction action, float x,
            List<WorkspaceActionBinding> bindings)
        {
            var button = UiElements.Button(action.ToString(), parent, caption,
                new Vector2(x, -340), new Vector2(230, 52));
            bindings.Add(new WorkspaceActionBinding { action = action, button = button });
        }
    }
}
