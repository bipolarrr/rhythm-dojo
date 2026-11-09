using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

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
