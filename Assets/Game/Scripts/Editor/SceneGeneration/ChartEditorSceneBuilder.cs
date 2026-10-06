using System;
using System.Linq;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using RhythmDojo.Authoring;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ChartEditorSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Scenes/ChartEditor.unity";
        private static readonly Color Background = new Color(0.035f, 0.045f, 0.07f);
        private static readonly Color Surface = new Color(0.09f, 0.105f, 0.14f);
        private static readonly Color Line = new Color(0.24f, 0.27f, 0.32f);

        [MenuItem("Game Tools/Scenes/Build Chart Editor")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before generating the chart editor.");
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene generation canceled to preserve unsaved work.");

            var mode = SceneResources.Require<GameModeDefinition>("Assets/Game/Settings/FourLaneMode.asset");
            if (mode.LaneCount != 4) throw new InvalidOperationException("Chart editor requires the four-lane mode.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;

            var canvas = UiElements.Canvas("Chart Editor");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0;
            StretchPanel("Background", canvas.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Background);
            StretchPanel("Menu Bar", canvas.transform, new Vector2(0, 1), Vector2.one,
                new Vector2(0, -52), Vector2.zero, Surface);

            string[] captions = { "파일", "설정", "도구", "테스트" };
            var menuButtons = new Button[captions.Length];
            for (int i = 0; i < captions.Length; i++)
            {
                var button = UiElements.Button(captions[i], canvas.transform, captions[i],
                    new Vector2(28 + i * 118, -9), new Vector2(108, 34));
                menuButtons[i] = button;
                button.GetComponent<Image>().color = new Color(0.18f, 0.21f, 0.27f);
                var label = button.GetComponentInChildren<Text>();
                label.color = Color.white;
                label.fontSize = 17;
            }

            var row = new GameObject("Editor Content", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(canvas.transform, false);
            Stretch(row, Vector2.zero, Vector2.one, new Vector2(28, 183), new Vector2(-28, -58));
            var tool = StretchPanel("Tool Space", row, Vector2.zero, new Vector2(0, 1),
                Vector2.zero, new Vector2(52, 0), Surface);
            var toolLabel = UiElements.Label("Tool Space Label", tool, "도\n구",
                new Vector2(11, -30), new Vector2(30, 90), 20);
            toolLabel.alignment = TextAnchor.UpperCenter;

            var content = new GameObject("Chart And Preview", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(row, false);
            Stretch(content, Vector2.zero, Vector2.one, new Vector2(66, 0), Vector2.zero);
            var grid = StretchPanel("Chart Grid", content, Vector2.zero, new Vector2(0.62f, 1),
                Vector2.zero, new Vector2(-10, 0), new Color(0.055f, 0.065f, 0.085f));
            grid.GetComponent<Image>().raycastTarget = true;
            for (int lane = 1; lane < 4; lane++)
            {
                float x = lane / 4f;
                StretchPanel("Lane Divider", grid, new Vector2(x, 0), new Vector2(x, 1),
                    new Vector2(-1, 0), new Vector2(1, 0), Line);
            }
            for (int beat = 0; beat <= 16; beat++)
            {
                var color = beat % 4 == 0 ? new Color(0.65f, 0.61f, 0.23f) : Line;
                float y = 1f - beat / 16f;
                float halfHeight = beat % 4 == 0 ? 1 : 0.5f;
                StretchPanel("Beat " + beat, grid, new Vector2(0, y), new Vector2(1, y),
                    new Vector2(0, -halfHeight), new Vector2(0, halfHeight), color);
            }
            var noteLayer = new GameObject("Notes", typeof(RectTransform)).GetComponent<RectTransform>();
            noteLayer.SetParent(grid, false);
            Stretch(noteLayer, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var screen = grid.gameObject.AddComponent<ChartEditorScreen>();
            screen.Configure(mode, noteLayer);

            var sidebar = StretchPanel("Preview Space", content, new Vector2(0.62f, 0), Vector2.one,
                new Vector2(10, 0), Vector2.zero, Surface);
            UiElements.Label("Help", sidebar, "좌클릭: 노트 배치\n위로 드래그: 롱노트\n우클릭: 삭제\n휠: 이동 / Ctrl + 휠: 확대\n\n파일 → 음원 가져오기\n설정 → 제목과 BPM 적용\n테스트 → 실제 플레이\n플레이 종료 시 편집 화면으로 복귀",
                new Vector2(18, -18), new Vector2(380, 290), 19);
            var footer = StretchPanel("Progress Space", canvas.transform, Vector2.zero, new Vector2(1, 0),
                new Vector2(28, 18), new Vector2(-28, 169), Surface);
            var progress = UiElements.Label("Progress Label", footer, "BPM 및 노래 진행 상황",
                new Vector2(22, -20), new Vector2(1150, 112), 18);
            progress.color = new Color(0.8f, 0.83f, 0.88f);
            BuildMenus(canvas, screen, progress, menuButtons);

            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions"));
            GameSceneValidator.Validate(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save chart editor scene.");
            var scenes = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = scenes.Where(item => item.path != ScenePath)
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Debug.Log("Rhythm Dojo: chart editor scene generated.");
        }

        private static void BuildMenus(Canvas canvas, ChartEditorScreen screen, Text status, Button[] buttons)
        {
            var menu = canvas.gameObject.AddComponent<ChartEditorMenu>();
            var controls = canvas.gameObject.AddComponent<CanvasGroup>();
            SceneBuilder.Wire(menu, "screen", screen);
            SceneBuilder.Wire(menu, "status", status);
            SceneBuilder.Wire(menu, "controls", controls);
            var file = MenuPanel("File Panel", canvas.transform, 390);
            var settings = MenuPanel("Settings Panel", canvas.transform, 470);
            var tools = MenuPanel("Tools Panel", canvas.transform, 210);
            SceneBuilder.Wire(menu, "filePanel", file.gameObject);
            SceneBuilder.Wire(menu, "settingsPanel", settings.gameObject);
            SceneBuilder.Wire(menu, "toolsPanel", tools.gameObject);
            SceneBuilder.Wire(menu, "projectId", Field(file, "Project ID", "곡 ID (불러오기)", 20));
            ActionButton(file, "Save", "저장", 104, menu.Save);
            ActionButton(file, "Load", "불러오기 (현재 작업 자동 저장)", 148, menu.Load);
            SceneBuilder.Wire(menu, "audioPath", Field(file, "Audio Path", "음원 파일 전체 경로 (WAV / OGG / MP3)", 210));
            ActionButton(file, "Import Audio", "음원 가져오기", 294, menu.ImportAudio);
            UiElements.Label("Storage Hint", file, "저장 위치는 저장 완료 후 아래에 표시됩니다.",
                new Vector2(16, -348), new Vector2(480, 28), 16);
            SceneBuilder.Wire(menu, "title", Field(settings, "Title", "곡 제목", 12));
            SceneBuilder.Wire(menu, "artist", Field(settings, "Artist", "아티스트", 88));
            SceneBuilder.Wire(menu, "bpm", Field(settings, "BPM", "시작 BPM (이후 BPM 변경점은 유지)", 164));
            SceneBuilder.Wire(menu, "offset", Field(settings, "Offset", "오디오 오프셋 (초)", 240));
            SceneBuilder.Wire(menu, "duration", Field(settings, "Duration", "채보 종료 시간 (초)", 316));
            ActionButton(settings, "Apply Settings", "설정 적용", 408, menu.ApplySettings);
            SceneBuilder.Wire(menu, "undo", ActionButton(tools, "Undo", "실행 취소", 18, menu.Undo));
            SceneBuilder.Wire(menu, "redo", ActionButton(tools, "Redo", "다시 실행", 72, menu.Redo));
            ActionButton(tools, "Reset View", "처음 위치 / 기본 확대", 126, menu.ResetView);
            UnityEventTools.AddPersistentListener(buttons[0].onClick, menu.ToggleFile);
            UnityEventTools.AddPersistentListener(buttons[1].onClick, menu.ToggleSettings);
            UnityEventTools.AddPersistentListener(buttons[2].onClick, menu.ToggleTools);
            UnityEventTools.AddPersistentListener(buttons[3].onClick, menu.Playtest);
            file.gameObject.SetActive(false); settings.gameObject.SetActive(false); tools.gameObject.SetActive(false);
            var composition = new GameObject("Chart Editor Composition").AddComponent<ChartEditorCompositionRoot>();
            SceneBuilder.Wire(composition, "settings", SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath));
            SceneBuilder.Wire(composition, "screen", screen);
            SceneBuilder.Wire(composition, "menu", menu);
        }

        private static RectTransform MenuPanel(string name, Transform parent, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            UiElements.Position(rect, new Vector2(28, -56), new Vector2(520, height));
            go.GetComponent<Image>().color = Surface;
            return rect;
        }

        private static InputField Field(Transform parent, string name, string caption, float y)
        {
            UiElements.Label(name + " Label", parent, caption, new Vector2(16, -y), new Vector2(488, 25), 17);
            var go = DefaultControls.CreateInputField(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            UiElements.Position((RectTransform)go.transform, new Vector2(16, -y - 28), new Vector2(488, 36));
            foreach (var text in go.GetComponentsInChildren<Text>(true))
            { text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 17; }
            var input = go.GetComponent<InputField>();
            ((Text)input.placeholder).text = caption;
            return input;
        }

        private static Button ActionButton(Transform parent, string name, string caption, float y, UnityEngine.Events.UnityAction action)
        {
            var button = UiElements.Button(name, parent, caption, new Vector2(16, -y), new Vector2(488, 36));
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static RectTransform StretchPanel(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0, 1);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
