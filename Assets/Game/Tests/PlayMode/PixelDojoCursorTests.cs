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
            Assert.That(normal.name, Is.EqualTo("PixelDojoCursor"));
            Assert.That(normal.width, Is.EqualTo(64));
            Assert.That(normal.height, Is.EqualTo(64));
            Assert.That(normal.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(normal.GetPixel(0, 0).a, Is.Zero);

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            cursor.SendMessage("RefreshTexture");
            var pressed = (Texture2D)field.GetValue(cursor);
            Assert.That(pressed.name, Is.EqualTo("PixelDojoCursorPressed"));
            Assert.That(pressed, Is.Not.SameAs(normal));
            yield return null;
            cursor.SendMessage("RefreshTexture");
            Assert.That(field.GetValue(cursor), Is.SameAs(pressed));

            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            cursor.SendMessage("RefreshTexture");
            Assert.That(field.GetValue(cursor), Is.SameAs(normal));
        }
    }
}
