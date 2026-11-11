using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    // Synthetic UI input must not compete with the user's physical pointer or keys.
    public sealed class NativeInputScope : IDisposable
    {
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private readonly List<InputDevice> addedDevices = new List<InputDevice>();
        private readonly Func<bool> previousFocusProvider;
        private readonly InputSettings input;
        private readonly InputSettings.BackgroundBehavior backgroundBehavior;
        private readonly InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private readonly Keyboard keyboard;
        private readonly Mouse mouse;
        private bool disposed;
        private static readonly List<NativeInputScope> activeScopes = new List<NativeInputScope>();

        public NativeInputScope()
        {
            input = InputSystem.settings;
            backgroundBehavior = input.backgroundBehavior;
            editorBehavior = input.editorInputBehaviorInPlayMode;
            keyboard = Keyboard.current;
            mouse = Mouse.current;
            previousFocusProvider = RhythmDojo.Input.ApplicationFocusState.Provider;
            RhythmDojo.Input.ApplicationFocusState.Provider = () => true;
            foreach (var device in InputSystem.devices)
            {
                if (device.native && device.enabled)
                {
                    devices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            }
            activeScopes.Add(this);
            InputSystem.onDeviceChange += TrackDevice;
            SaveRecovery();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            InputSystem.onDeviceChange -= TrackDevice;
            foreach (var device in addedDevices)
            {
                if (device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
            addedDevices.Clear();
            foreach (var device in devices)
            {
                if (device.added)
                {
                    InputSystem.EnableDevice(device);
                }
            }
            devices.Clear();
            input.backgroundBehavior = backgroundBehavior;
            input.editorInputBehaviorInPlayMode = editorBehavior;
            if (keyboard != null && keyboard.added)
            {
                keyboard.MakeCurrent();
            }
            if (mouse != null && mouse.added)
            {
                mouse.MakeCurrent();
            }
            RhythmDojo.Input.ApplicationFocusState.Provider = previousFocusProvider;
            activeScopes.Remove(this);
            SaveRecovery();
        }

        private void TrackDevice(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added || device.native)
            {
                return;
            }
            // Devices created inside an isolated test belong to that scope, even if setup aborts.
            addedDevices.Add(device);
            SaveRecovery();
        }

        internal static void RestoreAll()
        {
            while (activeScopes.Count > 0)
            {
                activeScopes[activeScopes.Count - 1].Dispose();
            }
        }

        private static void SaveRecovery()
        {
#if UNITY_EDITOR
            if (NativeInputRecovery.reloading)
            {
                return;
            }
            if (activeScopes.Count == 0)
            {
                UnityEditor.SessionState.EraseString(NativeInputRecovery.Key);
                return;
            }
            var snapshot = new NativeInputRecovery.Snapshot();
            snapshot.backgroundBehavior = activeScopes[0].backgroundBehavior;
            snapshot.editorBehavior = activeScopes[0].editorBehavior;
            foreach (var scope in activeScopes)
            {
                foreach (var device in scope.devices)
                {
                    snapshot.devices.Add(device.deviceId);
                }
                foreach (var device in scope.addedDevices)
                {
                    snapshot.addedDevices.Add(device.deviceId);
                }
            }
            UnityEditor.SessionState.SetString(NativeInputRecovery.Key, UnityEngine.JsonUtility.ToJson(snapshot));
#endif
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    internal static class NativeInputRecovery
    {
        internal const string Key = "RhythmDojo.Tests.NativeInputRecovery";
        internal static bool reloading;

        [Serializable]
        internal sealed class Snapshot
        {
            public List<int> devices = new List<int>();
            public List<int> addedDevices = new List<int>();
            public InputSettings.BackgroundBehavior backgroundBehavior;
            public InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        }

        static NativeInputRecovery()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
            UnityEditor.EditorApplication.delayCall += RestoreAfterReload;
        }

        private static void BeforeReload()
        {
            // Input System may already have serialized its disabled devices. Keep the journal
            // until its next domain has restored that state, then repair only our owned changes.
            reloading = true;
            NativeInputScope.RestoreAll();
        }

        private static void PlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                NativeInputScope.RestoreAll();
            }
        }

        private static void RestoreAfterReload()
        {
            string saved = UnityEditor.SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(saved))
            {
                return;
            }
            var snapshot = UnityEngine.JsonUtility.FromJson<Snapshot>(saved);
            foreach (int id in snapshot.addedDevices)
            {
                var device = InputSystem.GetDeviceById(id);
                if (device != null && !device.native)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
            foreach (int id in snapshot.devices)
            {
                var device = InputSystem.GetDeviceById(id);
                if (device != null && device.native)
                {
                    InputSystem.EnableDevice(device);
                    device.MakeCurrent();
                }
            }
            InputSystem.settings.backgroundBehavior = snapshot.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = snapshot.editorBehavior;
            UnityEditor.SessionState.EraseString(Key);
        }
    }
#endif
}
