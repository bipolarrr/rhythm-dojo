using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.EditorTools
{
    public static class TutorialSceneBuilder
    {
        public static Scene Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .065f, .07f);
            var view = TutorialLayoutBuilder.Build();
            var screen = view.gameObject.AddComponent<TutorialScreen>();
            SceneDependencyAssembler.Wire(screen, "view", view);
            var root = view.gameObject.AddComponent<TutorialCompositionRoot>();
            SceneDependencyAssembler.Wire(root, "screen", screen);
            SceneDependencyAssembler.Wire(root, "gameSettings",
                SceneResources.Require<GameSettings>("Assets/Game/Settings/GameSettings.asset"));
            var audio = new GameObject("Story Music", typeof(AudioSource), typeof(TitleMusicPlayer));
            audio.transform.SetParent(view.transform, false);
            var source = audio.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            var music = audio.GetComponent<TitleMusicPlayer>();
            SceneDependencyAssembler.Wire(music, "source", source);
            SceneDependencyAssembler.Wire(root, "music", music);
            SceneDependencyAssembler.Wire(root, "titleMusic",
                SceneResources.Require<TitleMusicSettings>(MainMenuSceneBuilder.TitleMusicPath));
            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions"));
            root.Validate();
            return scene;
        }
    }
}
