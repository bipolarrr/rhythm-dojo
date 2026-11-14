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
        internal static readonly string[] Folders =
        {
            "Assets/Game/Scenes", "Assets/Game/Settings", "Assets/Game/Audio",
            "Assets/Game/Materials", "Assets/Game/Prefabs", "Assets/Game/Resources",
        };
        internal static readonly string[] Paths =
        {
            SceneBuilder.TitlePath, SceneBuilder.ModeSelectionPath, SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath,
            SceneBuilder.GameplayPath, SceneBuilder.SettingsPath, SceneBuilder.ResultsPath,
            ChartEditorSceneBuilder.ScenePath, SceneBuilder.TutorialPath,
        };
        private readonly Dictionary<string, byte[]> originals;
        private readonly HashSet<string> directories;
        private readonly EditorBuildSettingsScene[] buildSettings;
        private readonly SceneSetup[] scenes;
        private readonly string[] selection;
        private readonly SceneAsset startScene;
        private bool disposed;

        internal static string[] Files() => Folders.Where(Directory.Exists)
            .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            .Concat(Folders.Select(folder => folder + ".meta").Where(File.Exists))
            .Select(path => path.Replace('\\', '/')).ToArray();

        public GeneratedSceneSnapshot()
        {
            originals = Files().ToDictionary(path => path, File.ReadAllBytes);
            directories = new HashSet<string>(Folders.Where(Directory.Exists)
                .SelectMany(folder => Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Prepend(folder)));
            buildSettings = EditorBuildSettings.scenes;
            scenes = EditorSceneManager.GetSceneManagerSetup();
            selection = Selection.objects.Select(obj => GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString()).ToArray();
            startScene = EditorSceneManager.playModeStartScene;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var path in Files().Where(path => !originals.ContainsKey(path)))
            {
                File.Delete(path);
            }

            foreach (var original in originals)
            {
                if (!File.Exists(original.Key) || !File.ReadAllBytes(original.Key).SequenceEqual(original.Value))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(original.Key));
                    File.WriteAllBytes(original.Key, original.Value);
                }
            }

            foreach (var folder in Folders.Where(Directory.Exists)
                .SelectMany(folder => Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).Prepend(folder))
                .OrderByDescending(path => path.Length))
            {
                if (!directories.Contains(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                {
                    Directory.Delete(folder);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorBuildSettings.scenes = buildSettings;
            EditorSceneManager.playModeStartScene = startScene;
            if (scenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            }

            Selection.objects = selection.Select(value =>
            {
                GlobalObjectId.TryParse(value, out var id);
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            }).Where(obj => obj).ToArray();
        }
    }
}
