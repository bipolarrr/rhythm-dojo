using System.Linq;
using NUnit.Framework;
using RhythmDojo.Input;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class NativeInputScopeTests
    {
        [Test]
        [Category("Fast")]
        public void InterruptedScopesRestoreDevicesAndFocusWithoutRemovingPreexistingDevices()
        {
            var input = InputSystem.settings;
            var backgroundBehavior = input.backgroundBehavior;
            var editorBehavior = input.editorInputBehaviorInPlayMode;
            var previousFocusProvider = ApplicationFocusState.Provider;
            var devices = InputSystem.devices.Where(device => device.native).ToArray();
            var enabled = devices.Select(device => device.enabled).ToArray();
            var existing = InputSystem.AddDevice<Keyboard>();
            InputSystem.DisableDevice(existing);
            NativeInputScope outer = null;
            NativeInputScope inner = null;

            try
            {
                outer = new NativeInputScope();
                input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                var keyboard = InputSystem.AddDevice<Keyboard>();
                inner = new NativeInputScope();
                input.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
                var mouse = InputSystem.AddDevice<Mouse>();

                // The Editor lifecycle calls this when NUnit teardown cannot finish.
                NativeInputScope.RestoreAll();
                inner.Dispose();
                outer.Dispose();

                Assert.That(keyboard.added, Is.False);
                Assert.That(mouse.added, Is.False);
                Assert.That(existing.added, Is.True);
                Assert.That(existing.enabled, Is.False, "Do not enable a device the test did not disable.");
                Assert.That(input.backgroundBehavior, Is.EqualTo(backgroundBehavior));
                Assert.That(input.editorInputBehaviorInPlayMode, Is.EqualTo(editorBehavior));
                Assert.That(ApplicationFocusState.Provider, Is.SameAs(previousFocusProvider));
                for (int i = 0; i < devices.Length; i++)
                {
                    Assert.That(devices[i].enabled, Is.EqualTo(enabled[i]), devices[i].name);
                }
            }
            finally
            {
                inner?.Dispose();
                outer?.Dispose();
                if (existing.added)
                {
                    InputSystem.RemoveDevice(existing);
                }
                input.backgroundBehavior = backgroundBehavior;
                input.editorInputBehaviorInPlayMode = editorBehavior;
                ApplicationFocusState.Provider = previousFocusProvider;
            }
        }
    }
}
