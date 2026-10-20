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
            Assert.That(normal.name, Is.EqualTo("DojoDotCursor"));
            Assert.That(normal.width, Is.EqualTo(32));
            Assert.That(normal.height, Is.EqualTo(32));
            Assert.That(normal.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(normal.GetPixel(0, 0).a, Is.Zero);
            Assert.That(normal.GetPixel(16, 16).a, Is.Zero);

            foreach (var pixel in normal.GetPixels32())
            {
                if (pixel.a == 0)
                {
                    continue;
                }

                Assert.That(pixel, Is.EqualTo(new Color32(255, 241, 213, 255)));
            }

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            cursor.SendMessage("RefreshTexture");
            var punch = (Texture2D)field.GetValue(cursor);
            Assert.That(punch, Is.Not.SameAs(normal));
            yield return new WaitForSecondsRealtime(.25f);
            cursor.SendMessage("RefreshTexture");
            var pressed = (Texture2D)field.GetValue(cursor);
            Assert.That(pressed.name, Is.EqualTo("DojoDotCursor"));
            Assert.That(pressed, Is.Not.SameAs(normal));
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
        public IEnumerator ImpactStaysAtClickPositionUsesBoundedMeshAndExpiresWhilePaused()
        {
            var impact = Object.FindFirstObjectByType<DojoPunchImpact>();
            Assert.That(impact, Is.Not.Null);
            Assert.That(impact.raycastTarget, Is.False);
            Assert.That(impact.canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
            var positions = typeof(DojoPunchImpact).GetField("positions", BindingFlags.NonPublic | BindingFlags.Instance);
            var active = typeof(DojoPunchImpact).GetField("active", BindingFlags.NonPublic | BindingFlags.Instance);
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            float previousScale = Time.timeScale;

            try
            {
                Time.timeScale = 0;

                for (int i = 0; i < 24; i++)
                {
                    impact.PlayAt(point);
                }

                var stored = (Vector2[])positions.GetValue(impact);
                Assert.That(stored.Length, Is.EqualTo(8));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    impact.rectTransform, point, null, out var expected);
                Assert.That(stored[0], Is.EqualTo(expected));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point + new Vector2(100, 40) });
                yield return null;
                Assert.That(stored[0], Is.EqualTo(expected));
                Assert.That(impact.GetComponentsInChildren<UnityEngine.UI.RawImage>().Length, Is.EqualTo(8));
                yield return new WaitForSecondsRealtime(.11f);
                Canvas.ForceUpdateCanvases();
                var mesh = impact.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.InRange(1, 320));

                yield return new WaitForSecondsRealtime(.3f);
                Assert.That((bool[])active.GetValue(impact), Has.All.False);
            }
            finally
            {
                Time.timeScale = previousScale;
            }
        }

        [UnityTest]
        public IEnumerator PunchesApproachClickFromDifferentDirectionsAndFlashOnlyAtContact()
        {
            var impact = Object.FindFirstObjectByType<DojoPunchImpact>();
            impact.Clear();
            var point = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var ages = (float[])typeof(DojoPunchImpact)
                .GetField("ages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(impact);
            var origins = (Vector2[])typeof(DojoPunchImpact)
                .GetField("origins", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(impact);
            var fists = (UnityEngine.UI.RawImage[])typeof(DojoPunchImpact)
                .GetField("fists", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(impact);

            for (int i = 0; i < 8; i++)
            {
                impact.PlayAt(point);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                impact.rectTransform, point, null, out var expected);
            var directions = new System.Collections.Generic.HashSet<Vector2>();

            for (int i = 0; i < 8; i++)
            {
                Assert.That(Vector2.Distance(origins[i], expected), Is.InRange(69, 111));
                Assert.That(fists[i].rectTransform.anchoredPosition, Is.EqualTo(origins[i]));
                Assert.That(fists[i].raycastTarget, Is.False);
                directions.Add((origins[i] - expected).normalized);
            }

            Assert.That(directions.Count, Is.GreaterThan(1));
            Canvas.ForceUpdateCanvases();
            Assert.That(impact.canvasRenderer.GetMesh().vertexCount, Is.Zero,
                "The flash must wait until a fist reaches the click point.");

            // Advance exactly to contact without a wall-clock scheduling assumption.
            for (int i = 0; i < 8; i++)
            {
                ages[i] = .1f;
                typeof(DojoPunchImpact).GetMethod("UpdateFist",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(impact, new object[] { i });
                Assert.That(fists[i].rectTransform.anchoredPosition, Is.EqualTo(expected));
            }

            impact.SetVerticesDirty();
            Canvas.ForceUpdateCanvases();
            Assert.That(impact.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
            impact.SendMessage("OnApplicationFocus", false);
            Assert.That(impact.GetComponentsInChildren<UnityEngine.UI.RawImage>(), Is.Empty);
            yield return null;
        }
    }
}
