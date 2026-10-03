using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.Core;
using RhythmDojo.UI;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;

namespace RhythmDojo.EditorTools
{
    public static class MainMenuSceneBuilder
    {
        public const string TitleMusicPath = "Assets/Game/Settings/TitleMusic.asset";
        public static Scene Create()
        {
            // Additive creation preserves other open scenes and their unsaved changes.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();
            var view = MainMenuLayoutBuilder.Build();
            SceneManager.MoveGameObjectToScene(view.gameObject, scene);
            var screen = view.gameObject.AddComponent<MainMenuScreen>();
            SceneDependencyAssembler.Wire(screen, "view", view);
            var root = view.gameObject.AddComponent<MainMenuCompositionRoot>();
            SceneDependencyAssembler.Wire(root, "screen", screen);
            var titleMusic = AssetDatabase.LoadAssetAtPath<TitleMusicSettings>(TitleMusicPath);
            if (!titleMusic) throw new System.InvalidOperationException("Title music settings missing: " + TitleMusicPath);
            SceneDependencyAssembler.Wire(root, "titleMusic", titleMusic);
            SceneDependencyAssembler.Wire(root, "gameSettings", SceneResources.Require<GameSettings>("Assets/Game/Settings/GameSettings.asset"));
            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            // UiElements creates the EventSystem in the new active scene.
            root.Validate();
            return scene;
        }
    }
}
