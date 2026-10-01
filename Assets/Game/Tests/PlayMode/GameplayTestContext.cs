using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Tests
{
    [PrebuildSetup(typeof(TestRunStartup))]
    [PostBuildCleanup(typeof(TestRunStartup))]
    public abstract class GameplayTestContext
    {

        protected InputSettings original;
        protected InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        protected InputSettings.BackgroundBehavior priorBackgroundBehavior;
        protected Keyboard keyboard;
        protected Mouse mouse;
        protected AudioConfiguration originalAudio;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            System.IO.Directory.CreateDirectory("Logs");
            originalAudio = AudioSettings.GetConfiguration();
            original = InputSystem.settings;
            priorEditorBehavior = original.editorInputBehaviorInPlayMode; priorBackgroundBehavior = original.backgroundBehavior;
            original.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Bootstrap.unity");
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            bool audioRestored = AudioSettings.Reset(originalAudio);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            original.editorInputBehaviorInPlayMode = priorEditorBehavior; original.backgroundBehavior = priorBackgroundBehavior;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>(); if (flow) UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
            Assert.That(audioRestored, Is.True, "Restore original audio configuration.");
        }
        protected static IEnumerator Await(System.Func<bool> condition)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "Scene transition timed out.");
                yield return null;
            }
        }
        protected static Button ButtonNamed(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == name);
        protected IEnumerator Click(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True, "Button must accept input.");
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new System.Collections.Generic.List<RaycastResult>();
            var system = EventSystem.current;
            system.RaycastAll(new PointerEventData(system) { position = point }, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No raycast target for " + button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button), "Button is obscured.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }
        protected IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        protected static void CaptureSelection(string imageName = "selection")
        {
            var camera = Camera.main; var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var target = new RenderTexture(1280,720,24); var previous = RenderTexture.active;
            var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target; canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=1; Canvas.ForceUpdateCanvases();
                camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes($"Logs/{imageName}.png",texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
                RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }

    }
}
