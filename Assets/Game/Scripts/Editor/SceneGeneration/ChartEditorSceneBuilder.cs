using System;
using System.Linq;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;
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
            for (int i = 0; i < captions.Length; i++)
            {
                var button = UiElements.Button(captions[i], canvas.transform, captions[i],
                    new Vector2(28 + i * 118, -9), new Vector2(108, 34));
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
            grid.gameObject.AddComponent<ChartEditorScreen>().Configure(mode, noteLayer);

            StretchPanel("Preview Space", content, new Vector2(0.62f, 0), Vector2.one,
                new Vector2(10, 0), Vector2.zero, Surface);
            var footer = StretchPanel("Progress Space", canvas.transform, Vector2.zero, new Vector2(1, 0),
                new Vector2(28, 18), new Vector2(-28, 169), Surface);
            var progress = UiElements.Label("Progress Label", footer, "BPM 및 노래 진행 상황",
                new Vector2(22, -20), new Vector2(760, 42), 22);
            progress.color = new Color(0.8f, 0.83f, 0.88f);

            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions"));
            GameSceneValidator.Validate(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save chart editor scene.");
            var scenes = EditorBuildSettings.scenes;
            if (!scenes.Any(item => item.path == ScenePath))
                EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Debug.Log("Rhythm Dojo: chart editor scene generated.");
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
