using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public sealed class GameplaySceneParts
    {
        public Scene Scene;
        public GameplayCompositionRoot Root;
        public RhythmGameController Controller;
        public SongClock Clock;
        public AudioSource Audio;
        public LaneKeyboardInput Input;
        public NotePresenter Notes;
        public PlayfieldPresenter Playfield;
        public RhythmHud Hud;
        public GameplayHudView HudView;
        public GameplayUiController Ui;
        public GameplayHitFeedback Hits;
    }

    public static class ScenePresentationBuilder
    {
        public static Scene Bootstrap(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();

            var bootstrap = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            bootstrap.EnsureCamera();
            SceneDependencyAssembler.Wire(bootstrap, "settings", content.Settings);

            return scene;
        }

        public static GameplaySceneParts Gameplay(GeneratedContent content)
        {
            var parts = new GameplaySceneParts
            {
                Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
            };
            content.ReloadAfterSceneChange();

            var displayCamera = new GameObject("Gameplay Display Camera").AddComponent<Camera>();
            displayCamera.tag = "MainCamera";
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = new Color(.025f, .035f, .065f);
            displayCamera.allowHDR = false;
            displayCamera.allowMSAA = false;
            displayCamera.gameObject.AddComponent<AudioListener>();

            parts.Audio = new GameObject("Song Audio").AddComponent<AudioSource>();
            parts.Audio.playOnAwake = false;
            parts.Audio.spatialBlend = 0;
            parts.Audio.volume = content.Settings.audio.Volume;
            parts.Audio.clip = content.Settings.catalog[0].AudioClip;
            parts.Clock = parts.Audio.gameObject.AddComponent<SongClock>();
            parts.Input = new GameObject("Lane Input").AddComponent<LaneKeyboardInput>();

            var game = new GameObject("Rhythm Game");
            parts.Controller = game.AddComponent<RhythmGameController>();
            parts.Root = game.AddComponent<GameplayCompositionRoot>();
            GameplayBackgroundBuilder.Build(displayCamera, parts.Root);
            parts.HudView = GameplayHudLayoutBuilder.Build();
            parts.Playfield = parts.HudView.viewport.field.GetComponentInChildren<PlayfieldPresenter>();
            var drawing = parts.HudView.viewport.field;
            parts.Notes = PresentationAssetBuilder.Group("Notes", drawing).gameObject.AddComponent<NotePresenter>();
            parts.HudView.lanes.laneRoot.SetAsLastSibling();
            parts.Hud = parts.HudView.gameObject.AddComponent<RhythmHud>();
            parts.Ui = parts.HudView.gameObject.AddComponent<GameplayUiController>();
            parts.Hits = parts.HudView.gameObject.AddComponent<GameplayHitFeedback>();
            GameplayFeedbackBuilder.Build(parts.Hits, parts.HudView.transform);
            SceneDependencyAssembler.Wire(parts.Root, "trainingView",
                TrainingPlaybackLayoutBuilder.Build(parts.HudView.transform));
            UiElements.EventSystem(
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/InputSystem_Actions.inputactions"
                )
            );

            ConfigureMenuInput(UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());

            return parts;
        }

        public static void Preview(GameplaySceneParts parts, GeneratedContent content)
        {
            var song = content.Settings.catalog[0];
            var chart = song.Chart.ToChartData();
            parts.Playfield.Build(song.Mode, content.Settings.presentation);
            parts.Notes.Configure(song.Mode, content.Settings.presentation);
            parts.Notes.Initialize(
                chart,
                song.Mode.ToRules(),
                content.Settings.scroll.CreateTimeline(
                    ScrollMode.Constant,
                    song.Timing.ToTempoMap(),
                    1
                )
            );
            EditorUtility.SetDirty(parts.Notes);
            parts.HudView.lanes.Build(song.Mode, content.Settings.presentation);
        }

        public static Scene Selection(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .065f);
            camera.gameObject.AddComponent<AudioListener>();
            SceneDependencyAssembler.Assemble(SongSelectionLayoutBuilder.Build(), content.Settings);
            UiElements.EventSystem(
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/InputSystem_Actions.inputactions"
                )
            );

            // Preparation loads this scene additively behind the title. Register input
            // only after direct composition or prepared-scene activation takes ownership.
            UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>().enabled = false;

            return scene;
        }

        public static Scene Settings(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .065f);
            camera.gameObject.AddComponent<AudioListener>();
            var view = SettingsLayoutBuilder.Build();
            var screen = view.gameObject.AddComponent<SettingsScreen>();
            SceneDependencyAssembler.Wire(screen, "view", view);

            var popupInput = new GameObject("Popup Input", typeof(UnityEngine.EventSystems.EventSystem));
            popupInput.transform.SetParent(view.transform, false);
            popupInput.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()
                .AssignDefaultActions();
            ConfigureMenuInput(popupInput.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());

            SceneBuilder.PrepareArtwork(scene);
            view.backdrop.SetActive(true);
            view.gameObject.SetActive(false);
            System.IO.Directory.CreateDirectory("Assets/Game/Resources/UI");
            PrefabUtility.SaveAsPrefabAsset(
                view.gameObject, "Assets/Game/Resources/UI/SettingsPopup.prefab");

            UnityEngine.Object.DestroyImmediate(popupInput);
            UnityEngine.Object.DestroyImmediate(screen);
            view.backdrop.SetActive(true);
            view.gameObject.SetActive(true);
            SceneDependencyAssembler.Assemble(view, content.Settings);
            UiElements.EventSystem(
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/InputSystem_Actions.inputactions"
                )
            );

            ConfigureMenuInput(UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>());
            return scene;
        }

        internal static void ConfigureMenuInput(UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
            const string path = "Assets/InputSystem_Actions.inputactions";
            module.actionsAsset = SceneResources.Require<UnityEngine.InputSystem.InputActionAsset>(path);
            // The module creates transient references when its asset changes. Replace them with
            // imported subassets so prefab serialization retains every binding.
            module.move = null;
            module.submit = null;
            module.cancel = null;
            module.point = null;
            module.leftClick = null;
            module.rightClick = null;
            module.middleClick = null;
            module.scrollWheel = null;
            module.trackedDevicePosition = null;
            module.trackedDeviceOrientation = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is UnityEngine.InputSystem.InputActionReference reference)
                {
                    if (reference.action.actionMap.name != "UI")
                    {
                        continue;
                    }
                    switch (reference.action.name)
                    {
                        case "Navigate":
                            module.move = reference;
                            break;
                        case "MenuSubmit":
                            module.submit = reference;
                            break;
                        case "Cancel":
                            module.cancel = reference;
                            break;
                        case "Point":
                            module.point = reference;
                            break;
                        case "Click":
                            module.leftClick = reference;
                            break;
                        case "RightClick":
                            module.rightClick = reference;
                            break;
                        case "MiddleClick":
                            module.middleClick = reference;
                            break;
                        case "ScrollWheel":
                            module.scrollWheel = reference;
                            break;
                        case "TrackedDevicePosition":
                            module.trackedDevicePosition = reference;
                            break;
                        case "TrackedDeviceOrientation":
                            module.trackedDeviceOrientation = reference;
                            break;
                    }
                }
            }
            module.deselectOnBackgroundClick = false;
            if (!module.submit || !module.move)
            {
                throw new System.InvalidOperationException("Menu navigation action references missing.");
            }
            if (!module.point || !module.leftClick)
            {
                throw new System.InvalidOperationException("Menu UI action references missing.");
            }
        }
    }
}
