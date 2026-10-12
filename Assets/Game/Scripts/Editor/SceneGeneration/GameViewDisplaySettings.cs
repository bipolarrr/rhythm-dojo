using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RhythmDojo.Services;

namespace RhythmDojo.EditorTools
{
    [InitializeOnLoad]
    public static class GameViewDisplaySettings
    {
        private static readonly Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static readonly MethodInfo setCustomResolution = gameViewType?.GetMethod("SetCustomResolution", BindingFlags.Instance | BindingFlags.NonPublic);
        static GameViewDisplaySettings()
        {
            PresentationPreferences.DisplaySizeRequested -= Apply;
            PresentationPreferences.DisplaySizeRequested += Apply;
        }
        private static void Apply(Vector2Int size)
        {
            if (setCustomResolution == null)
                throw new InvalidOperationException("Unity Game View resolution API is unavailable. Update the editor adapter for this Unity version.");
            var windows = Resources.FindObjectsOfTypeAll(gameViewType);
            if (windows.Length == 0) windows = new UnityEngine.Object[] { EditorWindow.GetWindow(gameViewType) };
            foreach (EditorWindow window in windows)
            {
                // Reuse one fixed-resolution preset rather than appending options on every Apply.
                setCustomResolution.Invoke(window, new object[] { new Vector2(size.x,size.y), "Rhythm Dojo Settings" });
                window.Repaint();
            }
        }
    }
}
