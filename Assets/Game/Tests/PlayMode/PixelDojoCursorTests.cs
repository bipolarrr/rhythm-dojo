using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class PixelDojoCursorTests : GameplayTestContext
    {
        [UnitySetUp]
        public IEnumerator PrepareCursorInput()
        {
            // Native cursor input reads Mouse.current; keep the synthetic device selected even
            // when a real mouse moves while an automated test is running in the Editor.
            InputSystem.onAfterUpdate += MakeTestMouseCurrent;
            MakeTestMouseCurrent();
#if UNITY_EDITOR
            var gameViewType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            UnityEditor.EditorWindow.GetWindow(gameViewType).Focus();
#endif
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreCursorInput()
        {
            InputSystem.onAfterUpdate -= MakeTestMouseCurrent;
            yield return null;
        }

        private void MakeTestMouseCurrent()
        {
            if (mouse != null && mouse.added)
            {
                mouse.MakeCurrent();
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator NativeCursorUsesPressedArtworkOnlyWhileLeftButtonIsHeld()
        {
            var cursor = Object.FindFirstObjectByType<DojoCursor>();
            Assert.That(cursor, Is.Not.Null);
            var field = typeof(DojoCursor).GetField("currentTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            cursor.SendMessage("RefreshTexture");
            var normal = (Texture2D)field.GetValue(cursor);
            Assert.That(normal.name, Is.EqualTo("DojoCursorRest"));
            Assert.That(normal.width, Is.EqualTo(128));
            Assert.That(normal.height, Is.EqualTo(128));
            Assert.That(normal.format, Is.EqualTo(TextureFormat.RGBA32));
#if UNITY_EDITOR
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(
                UnityEditor.AssetDatabase.GetAssetPath(normal));
            Assert.That(importer.alphaIsTransparency, Is.True,
                "Unity rejects cursor textures without alpha transparency enabled.");
#endif
            Assert.That(normal.GetPixel(0, 0).a, Is.Zero);
            Assert.That(normal.GetPixel(64, 64).a, Is.GreaterThan(.9f));
            int redPixels = 0;

            foreach (var pixel in normal.GetPixels32())
            {
                if (pixel.a == 0)
                {
                    continue;
                }

                if (pixel.r > pixel.g * 1.4f && pixel.r > pixel.b * 1.4f)
                {
                    redPixels++;
                }
            }

            Assert.That(redPixels, Is.GreaterThan(1100));
            Assert.That(VisibleWidth(normal), Is.InRange(68, 72));
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            cursor.SendMessage("RefreshTexture");
            var punch = (Texture2D)field.GetValue(cursor);
            Assert.That(punch, Is.Not.SameAs(normal));
            yield return new WaitForSecondsRealtime(.25f);
            cursor.SendMessage("RefreshTexture");
            var pressed = (Texture2D)field.GetValue(cursor);
            Assert.That(pressed.name, Is.EqualTo("DojoCursorRest"));
            Assert.That(pressed, Is.SameAs(normal), "Identical idle and held artwork shares the baked texture.");
            Assert.That(pressed, Is.Not.SameAs(punch));
            yield return null;
            cursor.SendMessage("RefreshTexture");
            Assert.That(field.GetValue(cursor), Is.SameAs(pressed));

            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            cursor.SendMessage("RefreshTexture");
            Assert.That(field.GetValue(cursor), Is.SameAs(normal));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PunchFinishesAfterQuickReleaseWhilePaused()
        {
            var cursor = Object.FindFirstObjectByType<DojoCursor>();
            var field = typeof(DojoCursor).GetField("currentTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            var normalField = typeof(DojoCursor).GetField("normalTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            float previousScale = Time.timeScale;

            try
            {
                Time.timeScale = 0;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                InputSystem.Update();
                cursor.SendMessage("Update");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                cursor.SendMessage("RefreshTexture");
                Assert.That(field.GetValue(cursor), Is.Not.SameAs(normalField.GetValue(cursor)));
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(field.GetValue(cursor), Is.SameAs(normalField.GetValue(cursor)));
            }
            finally
            {
                Time.timeScale = previousScale;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator FistReachesForwardAndReturnsWithNoRandomImpactLayer()
        {
            var cursor = Object.FindFirstObjectByType<DojoCursor>();
            var field = typeof(DojoCursor).GetField("currentTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            var normalField = typeof(DojoCursor).GetField("normalTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            var elapsed = typeof(DojoCursor).GetField("punchElapsed", BindingFlags.NonPublic | BindingFlags.Instance);
            var hotspot = typeof(DojoCursor).GetField("Hotspot", BindingFlags.NonPublic | BindingFlags.Static);
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            var normal = (Texture2D)normalField.GetValue(cursor);
            var rest = VisibleCenter(normal);
            var originalHotspot = (Vector2)hotspot.GetValue(null);
            elapsed.SetValue(cursor, 0f);
            cursor.SendMessage("RefreshTexture");
            var retract = VisibleCenter((Texture2D)field.GetValue(cursor));
            Assert.That(retract.x, Is.GreaterThan(rest.x));
            Assert.That(retract.y, Is.LessThan(rest.y));
            elapsed.SetValue(cursor, .09f);
            cursor.SendMessage("RefreshTexture");
            var extended = VisibleCenter((Texture2D)field.GetValue(cursor));
            Assert.That(rest.x - extended.x, Is.InRange(10f, 15f));
            Assert.That(extended.y - rest.y, Is.InRange(10f, 15f));
            Assert.That(mouse.position.ReadValue(), Is.EqualTo(point));
            Assert.That((Vector2)hotspot.GetValue(null), Is.EqualTo(originalHotspot));
            elapsed.SetValue(cursor, .2f);
            cursor.SendMessage("RefreshTexture");
            Assert.That(field.GetValue(cursor), Is.SameAs(normal));
            Assert.That(Object.FindFirstObjectByType<DojoPunchImpact>(), Is.Not.Null);
            Assert.That(cursor.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            Assert.That(cursor.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true), Is.Empty);
            Assert.That(cursor.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), Is.Empty);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator RepeatedClicksReuseFramesAndFocusLossRestoresTheNativePointer()
        {
            var cursor = Object.FindFirstObjectByType<DojoCursor>();
            var field = typeof(DojoCursor).GetField("currentTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            var framesField = typeof(DojoCursor).GetField("punchFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            var elapsed = typeof(DojoCursor).GetField("punchElapsed", BindingFlags.NonPublic | BindingFlags.Instance);
            var frames = (Texture2D[])framesField.GetValue(cursor);
            foreach (var frame in frames)
            {
                for (int edge = 0; edge < frame.width; edge++)
                {
                    Assert.That(frame.GetPixel(edge, 0).a, Is.Zero);
                    Assert.That(frame.GetPixel(edge, frame.height - 1).a, Is.Zero);
                    Assert.That(frame.GetPixel(0, edge).a, Is.Zero);
                    Assert.That(frame.GetPixel(frame.width - 1, edge).a, Is.Zero);
                }
            }

            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            for (int i = 0; i < 24; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                InputSystem.Update();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
                InputSystem.Update();
                cursor.SendMessage("Update");
                Assert.That((float)elapsed.GetValue(cursor), Is.Zero,
                    $"focus={RhythmDojo.Input.ApplicationFocusState.IsFocused}, current={Mouse.current?.deviceId}, test={mouse.deviceId}");
                Assert.That(field.GetValue(cursor), Is.SameAs(frames[0]));
            }

            Assert.That(framesField.GetValue(cursor), Is.SameAs(frames));
            Assert.That(Object.FindFirstObjectByType<DojoPunchImpact>(), Is.Not.Null);
            var previousFocusProvider = RhythmDojo.Input.ApplicationFocusState.Provider;
            RhythmDojo.Input.ApplicationFocusState.Provider = null;
            try
            {
                cursor.SendMessage("OnApplicationFocus", false);
                Assert.That(field.GetValue(cursor), Is.Null);
                Assert.That((float)elapsed.GetValue(cursor), Is.GreaterThanOrEqualTo(.18f));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                InputSystem.Update();
                cursor.SendMessage("OnApplicationFocus", true);
                Assert.That(field.GetValue(cursor), Is.Not.Null);
            }
            finally
            {
                RhythmDojo.Input.ApplicationFocusState.Provider = previousFocusProvider;
            }
            yield return null;
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator FistMarkWaitsForContactStaysAtTheExtendedPoseAndFadesWhilePaused()
        {
            var cursor = Object.FindFirstObjectByType<DojoCursor>();
            var impact = Object.FindFirstObjectByType<DojoPunchImpact>();
            var elapsed = typeof(DojoCursor).GetField("punchElapsed", BindingFlags.NonPublic | BindingFlags.Instance);
            var framesField = typeof(DojoCursor).GetField("punchFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            var active = (bool[])typeof(DojoPunchImpact).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(impact);
            var positions = (Vector2[])typeof(DojoPunchImpact).GetField("positions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(impact);
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            impact.Clear();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            InputSystem.Update();
            cursor.SendMessage("Update");
            Assert.That(active, Has.All.False, "A mark must wait for the fist to reach contact.");
            elapsed.SetValue(cursor, .09f);
            cursor.SendMessage("RefreshTexture");
            Assert.That(active[0], Is.True);
            Assert.That(active[1], Is.False);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                impact.rectTransform, point, null, out var expected);
            Assert.That(positions[0].x - expected.x, Is.InRange(-30f, -8f));
            Assert.That(positions[0].y - expected.y, Is.InRange(8f, 30f));
            expected = positions[0];
            Assert.That(impact.raycastTarget, Is.False);
            Assert.That(impact.canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
            var frames = (Texture2D[])framesField.GetValue(cursor);
            var extendedPixels = frames[3].GetPixels32();
            var stampPixels = ((Texture2D)impact.mainTexture).GetPixels32();
            var artwork = Resources.Load<Texture2D>("UI/DojoFistImprint");
            Assert.That(artwork, Is.Not.Null);
            Assert.That(artwork.width, Is.GreaterThanOrEqualTo(512));
            Assert.That(stampPixels.Length, Is.EqualTo(artwork.width * artwork.height));
            Assert.That(stampPixels.Length, Is.GreaterThan(extendedPixels.Length));
            Assert.That(((Texture2D)impact.mainTexture).filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(stampPixels[0].a, Is.Zero);
            bool monochrome = true;
            int darkest = 255;
            int brightest = 0;
            foreach (var pixel in stampPixels)
            {
                if (pixel.a < 128)
                {
                    continue;
                }

                if (pixel.r != pixel.g || pixel.g != pixel.b)
                {
                    monochrome = false;
                }

                darkest = Mathf.Min(darkest, pixel.r);
                brightest = Mathf.Max(brightest, pixel.r);
            }

            Assert.That(monochrome, Is.True, "The imprint must be neutral gray, without the previous red tint.");
            Assert.That(brightest - darkest, Is.GreaterThan(80), "The detailed pixel highlights and shadows must survive.");

            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + new Vector2(100, 40) });
            InputSystem.Update();
            elapsed.SetValue(cursor, .2f);
            cursor.SendMessage("RefreshTexture");
            Assert.That(positions[0], Is.EqualTo(expected), "The mark must not follow the returning cursor.");
            for (int i = 0; i < 24; i++)
            {
                impact.PlayAt(point);
            }

            Assert.That(active, Has.Length.EqualTo(8));
            Assert.That(active, Has.All.True);
            Canvas.ForceUpdateCanvases();
            Assert.That(impact.canvasRenderer.GetMesh().vertexCount, Is.EqualTo(32));
            var stampTint = impact.canvasRenderer.GetMesh().colors32[0];
            Assert.That(stampTint.r, Is.EqualTo(stampTint.g));
            Assert.That(stampTint.g, Is.EqualTo(stampTint.b));
            Assert.That(stampTint.a, Is.InRange(104, 109), "The imprint should start at about 42 percent opacity.");
            var markBounds = impact.canvasRenderer.GetMesh().bounds;
            Assert.That(Mathf.Max(markBounds.size.x, markBounds.size.y), Is.InRange(38f, 42f),
                "The imprint must fit the leading knuckles rather than the entire wrapped hand.");
            Assert.That(impact.GetComponentsInChildren<UnityEngine.UI.RawImage>(true), Is.Empty);
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(.75f);
                Assert.That(active, Has.All.False);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator HoverDrawsOnceScalesOnlyArtworkAndRestoresAfterExit()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.library);
            var button = view.Rows.First(row => row.select.isActiveAndEnabled).favorite;
            var label = button.GetComponentInChildren<Text>();
            var focus = Object.FindFirstObjectByType<DojoButtonFocus>();
            var rect = (RectTransform)button.transform;
            var originalRect = rect.rect;
            var originalPosition = rect.position;
            var originalScale = rect.localScale;
            var circles = focus.GetComponentsInChildren<DojoBrushCircle>(true);
            var mesh = new Mesh();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-50, -50) });
                yield return new WaitForSecondsRealtime(.18f);
                Canvas.ForceUpdateCanvases();
                mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                mesh.RecalculateBounds();
                float width = mesh.bounds.size.x;

                var point = HoverPoint(button);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return null;
                yield return null;
                Assert.That(focus.Hovered, Is.SameAs(button));
                var circle = circles.Single(item => item.gameObject.activeSelf);
                Assert.That(circle.Reveal, Is.GreaterThan(0));
                yield return Await(() => circle.Reveal == 1, "Hover stroke did not finish.");
                Canvas.ForceUpdateCanvases();
                mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                mesh.RecalculateBounds();
                Assert.That(mesh.bounds.size.x / width, Is.EqualTo(1.14f).Within(.005f));
                Assert.That(rect.rect, Is.EqualTo(originalRect));
                Assert.That(rect.position, Is.EqualTo(originalPosition));
                Assert.That(rect.localScale, Is.EqualTo(originalScale));
                Assert.That(circle.raycastTarget, Is.False);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                var edge = new Vector2(rect.rect.xMax - .25f, rect.rect.center.y);
                var pointer = new PointerEventData(EventSystem.current);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(edge));
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                edge.x += .5f;
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(edge));
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.All(hit => hit.gameObject.GetComponentInParent<Button>() != button), Is.True,
                    "Expanded artwork must not expand the clickable rectangle.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Logs/hover-library.png");

                string previous = label.text;
                for (int i = 0; i < 2; i++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                    yield return null;
                    Assert.That(circle.Reveal, Is.EqualTo(1), "Pressing must not redraw the circle.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    yield return null;
                    Assert.That(circle.Reveal, Is.EqualTo(1), "Releasing must not redraw the circle.");
                    Assert.That(label.text, Is.Not.EqualTo(previous), "Each native click must toggle the favorite.");
                    previous = label.text;
                }

                InputSystem.QueueStateEvent(mouse, new MouseState { position = point + new Vector2(rect.rect.width, 0) });
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(focus.Hovered, Is.Not.SameAs(button));
                mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                mesh.RecalculateBounds();
                Assert.That(mesh.bounds.size.x, Is.EqualTo(width).Within(.01f));
            }
            finally
            {
                Object.Destroy(mesh);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator HoverRespectsKeyboardDisabledControlsModalShadeAndFocusLoss()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.practice.settings);
            var settings = Object.FindFirstObjectByType<SettingsView>();
            var focus = Object.FindFirstObjectByType<DojoButtonFocus>();
            var circles = focus.GetComponentsInChildren<DojoBrushCircle>(true);
            var button = settings.soundTab;
            var point = HoverPoint(button);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Await(() => focus.Hovered == button, "Settings tab did not hover.");
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Logs/hover-settings.png");

            yield return Press(Key.RightArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(settings.masterVolume.gameObject));
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(circles.All(item => !item.gameObject.activeSelf), Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.one * 3 });
            yield return Await(() => focus.Hovered == button);
            button.interactable = false;
            yield return null;
            yield return null;
            Assert.That(focus.Hovered, Is.Null);
            Assert.That(circles.All(item => !item.gameObject.activeSelf), Is.True);
            button.interactable = true;
            settings.panelOpacity.interactable = false;
            yield return null;
            yield return null;
            Assert.That(focus.Hovered, Is.Null, "A disabled parent group must suppress hover.");
            settings.panelOpacity.interactable = true;

            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(2, 2) });
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(focus.Hovered, Is.Null, "The modal background is not a visible action.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return Await(() => focus.Hovered == button);

            var previousFocus = RhythmDojo.Input.ApplicationFocusState.Provider;
            try
            {
                RhythmDojo.Input.ApplicationFocusState.Provider = () => false;
                yield return null;
                yield return null;
                Assert.That(circles.All(item => !item.gameObject.activeSelf), Is.True);
            }
            finally
            {
                RhythmDojo.Input.ApplicationFocusState.Provider = previousFocus;
            }
            yield return Press(Key.Escape);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator HoverReusesAuthoredCirclesAcrossRapidMovementAndSceneChanges()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            var focus = Object.FindFirstObjectByType<DojoButtonFocus>();
            var circles = focus.GetComponentsInChildren<DojoBrushCircle>(true);
            var buttons = new[] { view.practice.library, view.practice.settings, view.practice.preview };
            for (int i = 0; i < 9; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = HoverPoint(buttons[i % buttons.Length]) });
                yield return null;
                yield return null;
                Assert.That(focus.Hovered, Is.SameAs(buttons[i % buttons.Length]));
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-20, -20) });
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(circles.All(item => !item.gameObject.activeSelf), Is.True);
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Game/Scenes/ModeSelection.unity");
            yield return null;
            foreach (string name in new[] { "Settings", "Next Mode" })
            {
                var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Single(item => item.name == name && item.isActiveAndEnabled);
                var rect = (RectTransform)button.transform;
                var originalRect = rect.rect;
                var originalPosition = rect.position;
                var label = button.GetComponentInChildren<Text>();
                var ink = button.GetComponentInChildren<DojoInkGraphic>();
                var mesh = new Mesh();
                try
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-20, -20) });
                    yield return new WaitForSecondsRealtime(.2f);
                    Canvas.ForceUpdateCanvases();
                    mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                    mesh.RecalculateBounds();
                    float width = mesh.bounds.size.x;
                    mesh.vertices = ink.canvasRenderer.GetMesh().vertices;
                    mesh.RecalculateBounds();
                    float inkWidth = mesh.bounds.size.x;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = HoverPoint(button) });
                    yield return Await(() => focus.Hovered == button, "Visible mode menu button did not hover.");
                    yield return new WaitForSecondsRealtime(.25f);
                    Canvas.ForceUpdateCanvases();
                    mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                    mesh.RecalculateBounds();
                    Assert.That(mesh.bounds.size.x / width, Is.EqualTo(1.14f).Within(.005f), name);
                    mesh.vertices = ink.canvasRenderer.GetMesh().vertices;
                    mesh.RecalculateBounds();
                    Assert.That(mesh.bounds.size.x / inkWidth, Is.EqualTo(1.14f).Within(.005f), name + " ink");
                    Assert.That(rect.rect, Is.EqualTo(originalRect));
                    Assert.That(rect.position, Is.EqualTo(originalPosition));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-20, -20) });
                    yield return new WaitForSecondsRealtime(.2f);
                    Canvas.ForceUpdateCanvases();
                    mesh.vertices = label.canvasRenderer.GetMesh().vertices;
                    mesh.RecalculateBounds();
                    Assert.That(mesh.bounds.size.x, Is.EqualTo(width).Within(.01f));
                }
                finally
                {
                    Object.Destroy(mesh);
                }
            }
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            CollectionAssert.AreEquivalent(circles, focus.GetComponentsInChildren<DojoBrushCircle>(true));
            Assert.That(circles.All(item => !item.gameObject.activeSelf), Is.True);
        }

        private static Vector2 HoverPoint(Selectable control)
        {
            var rect = (RectTransform)control.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private static int VisibleWidth(Texture2D texture)
        {
            int left = texture.width;
            int right = -1;
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128)
                    {
                        continue;
                    }

                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }

            return right - left + 1;
        }

        private static Vector2 VisibleCenter(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var sum = Vector2.zero;
            int count = 0;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128)
                    {
                        continue;
                    }

                    sum += new Vector2(x, y);
                    count++;
                }
            }

            Assert.That(count, Is.GreaterThan(0));
            return sum / count;
        }
    }
}
