using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmDojo.EditorTools;

namespace RhythmDojo.Tests
{
    public sealed class SceneRegenerationTests
    {
        [Test]
        public void AllFiveScenesCanBeRegeneratedWithoutReplacingTheirGuids()
        {
            var paths = new[] { SceneBuilder.TitlePath, SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath,
                SceneBuilder.GameplayPath, SceneBuilder.SettingsPath };
            var originals = paths.SelectMany(path => new[] { path, path + ".meta" })
                .ToDictionary(path => path, File.ReadAllBytes);
            var guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
            var buildSettings = EditorBuildSettings.scenes;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Keep .meta files: consumers of scene GUIDs must keep working after regeneration.
                foreach (string path in paths) File.Delete(path);
                Assert.That(paths.All(path => !File.Exists(path)), Is.True);
                SceneBuilder.BuildAll();
                foreach (string path in paths)
                {
                    Assert.That(File.Exists(path), Is.True, path);
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[path]), path);
                    GameSceneValidator.Validate(EditorSceneManager.OpenScene(path));
                }
                Assert.That(EditorBuildSettings.scenes.First(scene => scene.enabled).path, Is.EqualTo(SceneBuilder.TitlePath));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (var original in originals) File.WriteAllBytes(original.Key, original.Value);
                AssetDatabase.Refresh();
                EditorBuildSettings.scenes = buildSettings;
                EditorSceneManager.OpenScene(SceneBuilder.TitlePath);
            }
        }

        [Test]
        public void TitleStartupDoesNotOverrideActiveFoundationVerification()
        {
            bool active = SessionState.GetBool(FoundationVerification.ActiveKey, false);
            var previous = EditorSceneManager.playModeStartScene;
            try
            {
                SessionState.SetBool(FoundationVerification.ActiveKey, true);
                EditorSceneManager.playModeStartScene = null;
                MainMenuEditorStartup.Apply();
                Assert.That(EditorSceneManager.playModeStartScene, Is.Null);
            }
            finally
            {
                SessionState.SetBool(FoundationVerification.ActiveKey, active);
                EditorSceneManager.playModeStartScene = previous;
            }
        }
    }
}
