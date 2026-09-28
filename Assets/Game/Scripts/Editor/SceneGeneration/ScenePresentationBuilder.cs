using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Presentation;
using RhythmDojo.UI;

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
    }
    public static class ScenePresentationBuilder
    {
        public static Scene Bootstrap(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();
            var bootstrap = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            SceneDependencyAssembler.Wire(bootstrap, "settings", content.Settings); return scene;
        }
        public static GameplaySceneParts Gameplay(GeneratedContent content)
        {
            var parts = new GameplaySceneParts { Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single) };
            content.ReloadAfterSceneChange();
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 10, -10); camera.transform.LookAt(new Vector3(0, 0, 9));
            camera.fieldOfView = 52; camera.farClipPlane = 180; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f,.035f,.065f); camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Key Light").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(50, -25, 0);
            RenderSettings.ambientLight = new Color(.4f,.45f,.55f);
            parts.Playfield = new GameObject("Playfield").AddComponent<PlayfieldPresenter>();
            parts.Notes = new GameObject("Notes").AddComponent<NotePresenter>();
            parts.Audio = new GameObject("Song Audio").AddComponent<AudioSource>(); parts.Audio.playOnAwake = false;
            parts.Audio.spatialBlend = 0; parts.Audio.volume = content.Settings.audio.Volume;
            parts.Audio.clip = content.Settings.catalog[0].AudioClip;
            parts.Clock = parts.Audio.gameObject.AddComponent<SongClock>();
            parts.Input = new GameObject("Lane Input").AddComponent<LaneKeyboardInput>();
            var game = new GameObject("Rhythm Game"); parts.Controller = game.AddComponent<RhythmGameController>();
            parts.Root = game.AddComponent<GameplayCompositionRoot>();
            parts.HudView = GameplayHudLayoutBuilder.Build();
            parts.Hud = parts.HudView.gameObject.AddComponent<RhythmHud>();
            parts.Ui = parts.HudView.gameObject.AddComponent<GameplayUiController>();
            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions")); return parts;
        }
        public static void Preview(GameplaySceneParts parts, GeneratedContent content)
        {
            var song = content.Settings.catalog[0]; var chart = song.Chart.ToChartData();
            parts.Playfield.Build(song.Mode, content.Settings.presentation);
            parts.Notes.Configure(song.Mode, content.Settings.presentation);
            parts.Notes.Initialize(chart, song.Mode.ToRules(), content.Settings.scroll.CreateTimeline(ScrollMode.Constant, song.Timing.ToTempoMap(), 1));
            EditorUtility.SetDirty(parts.Notes);
            parts.HudView.lanes.Build(song.Mode, content.Settings.presentation);
        }
        public static Scene Selection(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.065f);
            SceneDependencyAssembler.Assemble(SongSelectionLayoutBuilder.Build(), content.Settings);
            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions")); return scene;
        }
        public static Scene Settings(GeneratedContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            content.ReloadAfterSceneChange();
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.065f);
            SceneDependencyAssembler.Assemble(SettingsLayoutBuilder.Build(), content.Settings);
            UiElements.EventSystem(AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
            return scene;
        }
    }
}

