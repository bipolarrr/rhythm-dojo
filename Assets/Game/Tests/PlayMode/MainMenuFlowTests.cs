using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RhythmDojo.Core;
using RhythmDojo.Services;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [PrebuildSetup(typeof(MainMenuTestStartup))]
    [PostBuildCleanup(typeof(MainMenuTestStartup))]
    [Category("UI")]
    public sealed class MainMenuFlowTests
    {
        private MainMenuView view;
        private MainMenuScreen screen;
        private Mouse mouse;
        private InputSettings input;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private InputSettings.BackgroundBehavior backgroundBehavior;

        private sealed class Actions : IMainMenuActions
        {
            public bool Transitioning { get; set; }
            public int Starts, Quits;
            public void StartGame() { Starts++; Transitioning = true; }
            public void QuitGame() { Quits++; }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input = InputSystem.settings;
            editorBehavior = input.editorInputBehaviorInPlayMode;
            backgroundBehavior = input.backgroundBehavior;
            input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            view = UnityEngine.Object.FindFirstObjectByType<MainMenuView>();
            screen = UnityEngine.Object.FindFirstObjectByType<MainMenuScreen>();
            Assert.That(view, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            input.editorInputBehaviorInPlayMode = editorBehavior;
            input.backgroundBehavior = backgroundBehavior;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            if (flow) UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
        }

        private IEnumerator ClickAt(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return null;
        }

        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            var events = UnityEngine.EventSystems.EventSystem.current;
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No raycast hit for " + button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button), "Wrong raycast target for " + button.name);
            yield return ClickAt(point);
        }

        [UnityTest]
        public IEnumerator SettingsDoesNotStartOrOpenAnyScreen()
        {
            var actions = new Actions(); screen.Initialize(actions);
            yield return Click(view.settings);
            Assert.That(actions.Starts, Is.Zero);
            Assert.That(actions.Quits, Is.Zero);
            Assert.That(view.quitPopup.activeSelf, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/Title.unity"));
        }

        [UnityTest]
        public IEnumerator QuitPopupBlocksBackgroundAndNoRestoresMenu()
        {
            var actions = new Actions(); screen.Initialize(actions);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                yield return Click(view.quit);
                Assert.That(view.quitPopup.activeSelf, Is.True);
                Assert.That(view.start.interactable || view.settings.interactable || view.quit.interactable, Is.False);
                yield return ClickAt(new Vector2(Screen.width * .2f, Screen.height * .8f));
                Assert.That(actions.Starts, Is.Zero);
                Assert.That(view.quitPopup.activeSelf, Is.True);
                yield return Click(view.cancelQuit);
                Assert.That(view.quitPopup.activeSelf, Is.False);
                Assert.That(view.start.interactable && view.settings.interactable && view.quit.interactable, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator YesRequestsQuitExactlyOnceWithoutStarting()
        {
            var actions = new Actions(); screen.Initialize(actions);
            yield return Click(view.quit);
            Assert.That(view.quitPopup.activeSelf, Is.True, "Quit popup must open before confirming.");
            yield return Click(view.confirmQuit);
            view.confirmQuit.onClick.Invoke();
            Assert.That(actions.Quits, Is.EqualTo(1));
            Assert.That(actions.Starts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BackgroundClickStartsOnceAndReinitializationDoesNotDuplicateListeners()
        {
            var oldActions = new Actions(); screen.Initialize(oldActions);
            var actions = new Actions(); screen.Initialize(actions);
            yield return ClickAt(new Vector2(Screen.width * .3f, Screen.height * .7f));
            view.start.onClick.Invoke();
            Assert.That(actions.Starts, Is.EqualTo(1));
            Assert.That(oldActions.Starts, Is.Zero);
            Assert.That(view.start.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator StartLoadsBootstrapThenExistingSongSelection()
        {
            bool bootstrapLoaded = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) => { if (scene.path == MainMenuActions.BootstrapScenePath) { bootstrapLoaded = true; AssertHasDisplayCamera(); } };
            SceneManager.sceneLoaded += loaded;
            try
            {
                yield return ClickAt(new Vector2(Screen.width * .3f, Screen.height * .7f));
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (!UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>())
                {
                    AssertHasDisplayCamera();
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Song selection transition timed out.");
                    yield return null;
                }
                AssertHasDisplayCamera();
                Assert.That(bootstrapLoaded, Is.True);
                Assert.That(SceneManager.GetActiveScene().path, Does.EndWith("/SongSelection.unity"));
            }
            finally { SceneManager.sceneLoaded -= loaded; }
        }

        [UnityTest]
        public IEnumerator CaptureMenuAndKoreanQuitPopup()
        {
            Capture("Logs/main-menu.png");
            Capture("Logs/main-menu-1920.png", 1920, 1080);
            Capture("Logs/main-menu-4x3.png", 1024, 768);
            yield return Click(view.quit);
            Assert.That(view.quitPopup.activeSelf, Is.True);
            Capture("Logs/main-menu-quit.png");
        }

        private static void AssertHasDisplayCamera()
        {
            Assert.That(System.Array.Exists(Camera.allCameras, camera =>
                camera.isActiveAndEnabled && camera.targetDisplay == 0 && !camera.targetTexture),
                Is.True, "Display 1 must have a rendering camera throughout the transition.");
        }

        private void Capture(string path, int width = 1280, int height = 720)
        {
            var camera = Camera.main;
            var canvas = view.GetComponent<Canvas>();
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var priorScale = canvas.scaleFactor;
            var priorScaler = scaler.enabled;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                scaler.enabled = false; canvas.scaleFactor = Mathf.Sqrt(width / 1280f * height / 720f);
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                camera.targetTexture = null; RenderTexture.active = previous;
                canvas.scaleFactor = priorScale; scaler.enabled = priorScaler;
                target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}