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
    public static class MainMenuSceneBuilder
    {
        public const string TitleMusicPath = "Assets/Game/Settings/TitleMusic.asset";

        public static void BuildAppFlowPrefab()
        {
            var root = new GameObject("AppFlow", typeof(AudioSource), typeof(TitleMusicPlayer), typeof(AppFlowController));
            try
            {
                var source = root.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                var music = root.GetComponent<TitleMusicPlayer>();
                SceneDependencyAssembler.Wire(music, "source", source);
                var flow = root.GetComponent<AppFlowController>();
                SceneDependencyAssembler.Wire(flow, "music", music);
                SceneDependencyAssembler.Wire(flow, "titleMusic", SceneResources.Require<TitleMusicSettings>(TitleMusicPath));
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Game/Resources/AppFlow.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static Scene Create() => Create(false);

        public static Scene Create(bool modeSelection)
        {
            // Additive creation preserves other open scenes and their unsaved changes.
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            SceneManager.SetActiveScene(scene);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.AddComponent<AudioListener>();

            var view = MainMenuLayoutBuilder.Build(modeSelection);
            SceneManager.MoveGameObjectToScene(view.gameObject, scene);
            var effects = view.gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            if (view.modeSelection)
            {
                view.modeSelection.effects = effects;
                view.modeSelection.contact = view.knock;
                view.modeSelection.footstep = DojoPresentationAudio.Load(false);
                view.modeSelection.wind = DojoPresentationAudio.Load(true);
            }
            var root = view.gameObject.AddComponent<MainMenuCompositionRoot>();
            SceneDependencyAssembler.Wire(root, "view", view);
            SceneDependencyAssembler.Wire(root, "sceneCamera", camera);
            if (modeSelection)
            {
                var screen = view.gameObject.AddComponent<MainMenuScreen>();
                SceneDependencyAssembler.Wire(screen, "view", view);
                SceneDependencyAssembler.Wire(screen, "effects", effects);
                SceneDependencyAssembler.Wire(root, "screen", screen);
            }
            else
            {
                var entranceScreen = view.gameObject.AddComponent<MainMenuEntranceScreen>();
                SceneDependencyAssembler.Wire(entranceScreen, "sky", TitleSkyLayoutBuilder.Build(view));
                SceneDependencyAssembler.Wire(entranceScreen, "view", view);
                SceneDependencyAssembler.Wire(entranceScreen, "effects", effects);
                SceneDependencyAssembler.Wire(root, "entranceScreen", entranceScreen);
            }
            SceneDependencyAssembler.Wire(
                root,
                "gameSettings",
                SceneResources.Require<GameSettings>("Assets/Game/Settings/GameSettings.asset")
            );
            UiElements.EventSystem(
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/InputSystem_Actions.inputactions"
                )
            );
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                var events = gameObject.GetComponent<UnityEngine.EventSystems.EventSystem>();
                if (events)
                {
                    SceneDependencyAssembler.Wire(root, "events", events);
                }
            }

            // UiElements creates the EventSystem in the new active scene.
            root.Validate();

            return scene;
        }
    }
}
