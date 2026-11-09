using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    // Synthetic UI input must not compete with the user's physical pointer or keys.
    public sealed class NativeInputScope : IDisposable
    {
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private readonly Func<bool> previousFocusProvider;

        public NativeInputScope()
        {
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
        }

        public void Dispose()
        {
            foreach (var device in devices)
            {
                if (device.added)
                {
                    InputSystem.EnableDevice(device);
                }
            }
            devices.Clear();
            RhythmDojo.Input.ApplicationFocusState.Provider = previousFocusProvider;
        }
    }
}
