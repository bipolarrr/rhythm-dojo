using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RhythmDojo.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RhythmDojo.Tests
{
    internal sealed class GeneratedSceneSnapshot : IDisposable
    {
        private readonly Dictionary<string, byte[]> originals;
        private readonly EditorBuildSettingsScene[] buildSettings;
        private bool disposed;

        public GeneratedSceneSnapshot()
        {
            var paths = new[]
            {
                SceneBuilder.TitlePath,
                SceneBuilder.BootstrapPath,
                SceneBuilder.SelectionPath,
                SceneBuilder.GameplayPath,
                SceneBuilder.SettingsPath,
                SceneBuilder.ResultsPath,
                ChartEditorSceneBuilder.ScenePath,
                "Assets/Game/Resources/UI/SettingsPopup.prefab",
            };
            originals = paths.SelectMany(path => new[] { path, path + ".meta" })
                .ToDictionary(path => path, File.ReadAllBytes);
            buildSettings = EditorBuildSettings.scenes;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var original in originals)
            {
                File.WriteAllBytes(original.Key, original.Value);
            }

            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = buildSettings;
            EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
        }
    }
}
