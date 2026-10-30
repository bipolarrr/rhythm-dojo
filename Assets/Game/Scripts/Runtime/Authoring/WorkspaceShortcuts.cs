using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RhythmDojo.Authoring
{
    public static class WorkspaceShortcuts
    {
        public readonly struct Binding
        {
            public WorkspaceAction Action { get; }
            public Key Key { get; }
            public string Label { get; }
            public bool Control { get; }
            public bool Shift { get; }

            public Binding(WorkspaceAction action, Key key, string label, bool control = false, bool shift = false)
            {
                Action = action;
                Key = key;
                Label = label;
                Control = control;
                Shift = shift;
            }
        }

        public static IReadOnlyList<Binding> Bindings { get; } = Array.AsReadOnly(new[]
        {
            new Binding(WorkspaceAction.New, Key.N, "Ctrl+N", true),
            new Binding(WorkspaceAction.Save, Key.S, "Ctrl+S", true),
            new Binding(WorkspaceAction.SaveAs, Key.S, "Ctrl+Shift+S", true, true),
            new Binding(WorkspaceAction.ImportPupa, Key.I, "Ctrl+Shift+I", true, true),
            new Binding(WorkspaceAction.Exit, Key.Escape, "Esc"),
            new Binding(WorkspaceAction.ApplySettings, Key.Enter, "Ctrl+Enter", true),
            new Binding(WorkspaceAction.Undo, Key.Z, "Ctrl+Z", true),
            new Binding(WorkspaceAction.Redo, Key.Y, "Ctrl+Y", true),
            new Binding(WorkspaceAction.Copy, Key.C, "Ctrl+C", true),
            new Binding(WorkspaceAction.Paste, Key.V, "Ctrl+V", true),
            new Binding(WorkspaceAction.Delete, Key.Delete, "Delete"),
            new Binding(WorkspaceAction.SelectAll, Key.A, "Ctrl+A", true),
            new Binding(WorkspaceAction.SelectTaps, Key.Digit1, "Ctrl+1", true),
            new Binding(WorkspaceAction.SelectHolds, Key.Digit2, "Ctrl+2", true),
            new Binding(WorkspaceAction.Deselect, Key.A, "Ctrl+Shift+A", true, true),
            new Binding(WorkspaceAction.Mirror, Key.M, "Ctrl+M", true),
            new Binding(WorkspaceAction.Randomize, Key.R, "Ctrl+Shift+R", true, true),
            new Binding(WorkspaceAction.Select, Key.V, "V"),
            new Binding(WorkspaceAction.Pencil, Key.P, "P"),
            new Binding(WorkspaceAction.Eraser, Key.E, "E"),
            new Binding(WorkspaceAction.Position, Key.G, "G"),
            new Binding(WorkspaceAction.PlayPause, Key.Space, "Space"),
            new Binding(WorkspaceAction.Stop, Key.Digit0, "0"),
            new Binding(WorkspaceAction.Test, Key.F5, "F5"),
            new Binding(WorkspaceAction.TestHere, Key.F5, "Shift+F5", false, true),
            new Binding(WorkspaceAction.AutoTest, Key.F5, "Ctrl+F5", true),
            new Binding(WorkspaceAction.AutoTestHere, Key.F5, "Ctrl+Shift+F5", true, true)
        });

        public static string Label(WorkspaceAction action)
        {
            foreach (var binding in Bindings)
            {
                if (binding.Action == action)
                {
                    return binding.Label;
                }
            }
            return "";
        }

        public static bool TryGetAction(Keyboard keyboard, out WorkspaceAction action)
        {
            action = default;
            if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
            {
                return false;
            }
            bool control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            foreach (var binding in Bindings)
            {
                if (binding.Control != control || binding.Shift != shift)
                {
                    continue;
                }
                if (keyboard[binding.Key].wasPressedThisFrame)
                {
                    action = binding.Action;
                    return true;
                }
            }
            return false;
        }

        public static bool TryGetSlot(Keyboard keyboard, out int slot)
        {
            slot = -1;
            bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            bool control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (!alt || control || shift)
            {
                return false;
            }
            var keys = new[] { Key.Digit1, Key.Digit2, Key.Digit3 };
            for (int i = 0; i < keys.Length; i++)
            {
                if (keyboard[keys[i]].wasPressedThisFrame)
                {
                    slot = i;
                    return true;
                }
            }
            return false;
        }
    }
}
