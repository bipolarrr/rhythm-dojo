using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Input
{
    public sealed class LaneKeyboardInput : MonoBehaviour, ILaneInput
    {
        private InputAction[] lanes;
        private InputAction restart, back;
        private bool[] held, blockedUntilRelease;
        public int LaneCount => lanes?.Length ?? 0;
        public event Action<int, double> Pressed;
        public event Action<int, double> Released;
        public event Action StartRequested;
        public event Action ReturnRequested;
        public bool IsHeld(int lane) => held[lane];
        public void Initialize(GameModeDefinition mode)
        {
            Shutdown(); mode.Validate();
            lanes = new InputAction[mode.LaneCount]; held = new bool[mode.LaneCount];
            blockedUntilRelease = new bool[mode.LaneCount];
            for (int i = 0; i < lanes.Length; i++)
            {
                int lane = i;
                lanes[i] = new InputAction(mode.GetLane(i).displayName, InputActionType.Button, mode.GetLane(i).binding);
                lanes[i].performed += c =>
                {
                    if (!blockedUntilRelease[lane] && !held[lane])
                    { held[lane] = true; Pressed?.Invoke(lane, c.time); }
                };
                lanes[i].canceled += c =>
                { blockedUntilRelease[lane] = false; held[lane] = false; Released?.Invoke(lane, c.time); };
            }
            restart = new InputAction("Start / Restart", InputActionType.Button, mode.StartBinding);
            back = new InputAction("Song List", InputActionType.Button, mode.ReturnBinding);
            restart.performed += OnStart; back.performed += OnReturn;
        }
        private void OnStart(InputAction.CallbackContext context) => StartRequested?.Invoke();
        private void OnReturn(InputAction.CallbackContext context) => ReturnRequested?.Invoke();
        public void Activate()
        {
            if (lanes == null) throw new InvalidOperationException("Lane input needs a mode.");
            foreach (var lane in lanes) lane.Enable();
            restart.Enable(); back.Enable(); ResetHeld();
        }
        public void ResetHeld()
        {
            if (lanes == null) return;
            for (int i = 0; i < lanes.Length; i++)
            { held[i] = false; blockedUntilRelease[i] = lanes[i].IsPressed(); }
        }
        public void Shutdown()
        {
            if (lanes == null) return;
            foreach (var lane in lanes) { lane.Disable(); lane.Dispose(); }
            restart.Disable(); back.Disable(); restart.Dispose(); back.Dispose();
            lanes = null; held = blockedUntilRelease = null;
        }
        private void OnDisable()
        {
            if (lanes == null) return;
            foreach (var lane in lanes) lane.Disable();
            restart.Disable(); back.Disable();
        }
        private void OnDestroy() => Shutdown();
    }
}
