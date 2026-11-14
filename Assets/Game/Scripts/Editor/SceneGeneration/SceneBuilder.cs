using System;
using System.IO;
using System.Linq;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.EditorTools
{
    public static class SceneBuilder
    {
        public const string TitlePath = "Assets/Game/Scenes/Title.unity";
        public const string ModeSelectionPath = "Assets/Game/Scenes/ModeSelection.unity";
        public const string TutorialPath = "Assets/Game/Scenes/Tutorial.unity";
        public const string BootstrapPath = "Assets/Game/Scenes/Bootstrap.unity";
        public const string SelectionPath = "Assets/Game/Scenes/SongSelection.unity";
        public const string GameplayPath = "Assets/Game/Scenes/Gameplay.unity";
        public const string SettingsPath = "Assets/Game/Scenes/Settings.unity";
        public const string ResultsPath = "Assets/Game/Scenes/Results.unity";
        public const string ChartPath = "Assets/Game/Settings/TestChart.asset";
        public const string ConfigPath = "Assets/Game/Settings/RhythmGameConfig.asset";

        [MenuItem("Game Tools/Scenes/Build All Scenes")]
        public static void BuildAll()
        {
            Guard();

            DojoCursorAssetBuilder.Build();
            var content = SceneResources.Load();
            SaveGameplay(content);
            Save(ScenePresentationBuilder.Selection(content), SelectionPath);
            Save(ScenePresentationBuilder.Settings(content), SettingsPath);
            Save(ScenePresentationBuilder.Bootstrap(content), BootstrapPath);
            ChartEditorSceneBuilder.Build();
            BuildResults();
            BuildModeSelection();
            BuildTutorial();
            BuildTitle();
            AssetDatabase.SaveAssets();
            Debug.Log("Rhythm Dojo: nine scenes generated and validated.");
        }

        [MenuItem("Game Tools/Scenes/Build Tutorial")]
        public static void BuildTutorial()
        {
            Guard();
            Save(TutorialSceneBuilder.Create(), TutorialPath);
            AssetDatabase.ImportAsset(TutorialPath, ImportAssetOptions.ForceSynchronousImport);
            RegisterScenes();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Game Tools/Scenes/Build Mode Selection")]
        public static void BuildModeSelection()
        {
            Guard();
            var existing = SceneManager.GetSceneByPath(ModeSelectionPath);
            var scene = MainMenuSceneBuilder.Create(true);
            if (existing.IsValid() && existing.isLoaded)
            {
                EditorSceneManager.CloseScene(existing, true);
            }
            Save(scene, ModeSelectionPath);
            RegisterScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ModeSelectionPath, OpenSceneMode.Single);
        }

        [MenuItem("Game Tools/Scenes/Build Title")]
        public static void BuildTitle()
        {
            Guard();

            var existing = SceneManager.GetSceneByPath(TitlePath);
            var scene = MainMenuSceneBuilder.Create();

            if (existing.IsValid() && existing.isLoaded)
            {
                EditorSceneManager.CloseScene(existing, true);
            }

            Save(scene, TitlePath);
            RegisterScenes();
            AssetDatabase.SaveAssets();

            // Open only the menu so other scenes cannot render or receive input.
            EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            MainMenuEditorStartup.Apply();
            Debug.Log("Rhythm Dojo: Title generated, validated and opened.");
        }

        [MenuItem("Game Tools/Scenes/Build Bootstrap")]
        public static void BuildBootstrap()
        {
            Guard();
            Save(ScenePresentationBuilder.Bootstrap(SceneResources.Load()), BootstrapPath);
            RegisterScenes();
        }

        [MenuItem("Game Tools/Scenes/Build Gameplay")]
        public static void BuildGameplay()
        {
            Guard();
            SaveGameplay(SceneResources.Load());
            RegisterScenes();
        }

        [MenuItem("Game Tools/Scenes/Build Song Selection")]
        public static void BuildSelection()
        {
            Guard();
            Save(ScenePresentationBuilder.Selection(SceneResources.Load()), SelectionPath);
            RegisterScenes();
        }

        [MenuItem("Game Tools/Scenes/Build Settings")]
        public static void BuildSettings()
        {
            Guard();
            Save(ScenePresentationBuilder.Settings(SceneResources.Load()), SettingsPath);
            RegisterScenes();
        }

        [MenuItem("Game Tools/Scenes/Build Results")]
        public static void BuildResults()
        {
            Guard();
            var content = SceneResources.Load();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .075f, .11f);
            camera.gameObject.AddComponent<AudioListener>();
            var view = ResultsLayoutBuilder.Build();
            SceneDependencyAssembler.Assemble(view, content.Settings);
            RhythmDojo.UI.UiElements.EventSystem(
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/InputSystem_Actions.inputactions"
                )
            );
            Save(scene, ResultsPath);
            RegisterScenes();
            AssetDatabase.SaveAssets();
        }

        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before generating scenes.");
            }

            if (
                !UnityEngine.Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
            )
            {
                throw new OperationCanceledException(
                    "Scene generation canceled to preserve unsaved work."
                );
            }
        }

        private static void SaveGameplay(GeneratedContent content)
        {
            var parts = ScenePresentationBuilder.Gameplay(content);

            try
            {
                SceneDependencyAssembler.Assemble(parts, content);
                ScenePresentationBuilder.Preview(parts, content);
                Save(parts.Scene, GameplayPath);
            }
            catch
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                throw;
            }
        }

        internal static void PrepareArtwork(Scene scene)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var frame in root.GetComponentsInChildren<PixelDojoFrameGraphic>(true))
                {
                    frame.RefreshArtwork();
                }
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void Save(Scene scene, string path)
        {
            PrepareArtwork(scene);
            GameSceneValidator.Validate(scene);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException("Cannot save scene: " + path);
            }
        }

        public static void Wire(
            UnityEngine.Object target,
            string property,
            UnityEngine.Object value
        ) => SceneDependencyAssembler.Wire(target, property, value);

        public static void ValidateScene(Scene scene) => GameSceneValidator.Validate(scene);

        private static void RegisterScenes()
        {
            var paths = new[]
            {
                TitlePath,
                ModeSelectionPath,
                BootstrapPath,
                SelectionPath,
                GameplayPath,
                SettingsPath,
                ResultsPath,
                ChartEditorSceneBuilder.ScenePath,
                TutorialPath,
            };
            var other = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path));
            EditorBuildSettings.scenes = paths
                .Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(other)
                .ToArray();
        }
    }
}
