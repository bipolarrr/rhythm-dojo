using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.EditorTools
{
    public static class SceneBuilder
    {
        public const string TitlePath = "Assets/Game/Scenes/Title.unity";
        public const string BootstrapPath = "Assets/Game/Scenes/Bootstrap.unity";
        public const string SelectionPath = "Assets/Game/Scenes/SongSelection.unity";
        public const string GameplayPath = "Assets/Game/Scenes/Gameplay.unity";
        public const string SettingsPath = "Assets/Game/Scenes/Settings.unity";
        public const string ChartPath = "Assets/Game/Settings/TestChart.asset";
        public const string ConfigPath = "Assets/Game/Settings/RhythmGameConfig.asset";
        [MenuItem("Game Tools/Scenes/Build All Scenes")]
        public static void BuildAll()
        {
            Guard(); var content = SceneResources.Load();
            SaveGameplay(content); Save(ScenePresentationBuilder.Selection(content), SelectionPath);
            Save(ScenePresentationBuilder.Settings(content), SettingsPath);
            Save(ScenePresentationBuilder.Bootstrap(content), BootstrapPath); BuildTitle();
            AssetDatabase.SaveAssets(); Debug.Log("Rhythm Dojo: five scenes generated and validated.");
        }
        [MenuItem("Game Tools/Scenes/Build Title")]
        public static void BuildTitle()
        {
            Guard();
            var existing = SceneManager.GetSceneByPath(TitlePath);
            var scene = MainMenuSceneBuilder.Create();
            if (existing.IsValid() && existing.isLoaded) EditorSceneManager.CloseScene(existing, true);
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
        { Guard(); Save(ScenePresentationBuilder.Bootstrap(SceneResources.Load()), BootstrapPath); RegisterScenes(); }
        [MenuItem("Game Tools/Scenes/Build Gameplay")]
        public static void BuildGameplay()
        { Guard(); SaveGameplay(SceneResources.Load()); RegisterScenes(); }
        [MenuItem("Game Tools/Scenes/Build Song Selection")]
        public static void BuildSelection()
        { Guard(); Save(ScenePresentationBuilder.Selection(SceneResources.Load()), SelectionPath); RegisterScenes(); }
        [MenuItem("Game Tools/Scenes/Build Settings")]
        public static void BuildSettings()
        { Guard(); Save(ScenePresentationBuilder.Settings(SceneResources.Load()), SettingsPath); RegisterScenes(); }
        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before generating scenes.");
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene generation canceled to preserve unsaved work.");
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
            catch { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); throw; }
        }
        private static void Save(Scene scene, string path)
        {
            GameSceneValidator.Validate(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Cannot save scene: " + path);
        }
        public static void Wire(UnityEngine.Object target, string property, UnityEngine.Object value) =>
            SceneDependencyAssembler.Wire(target, property, value);
        public static void ValidateScene(Scene scene) => GameSceneValidator.Validate(scene);
        private static void RegisterScenes()
        {
            var paths = new[] { TitlePath, BootstrapPath, SelectionPath, GameplayPath, SettingsPath };
            var other = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path));
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p,true)).Concat(other).ToArray();
        }
    }
}

