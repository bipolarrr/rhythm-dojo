using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class SceneRegenerationTests
    {
        [Test]
        [Category("AssetGeneration")]
        public void AllFiveScenesCanBeRegeneratedWithoutReplacingTheirGuids()
        {
            using var generated = new GeneratedSceneSnapshot();
            var paths = GeneratedSceneSnapshot.Paths;
            var guids = GeneratedSceneSnapshot.Files().Where(path => path.EndsWith(".meta"))
                .ToDictionary(path => path, File.ReadAllBytes);
            var files = new[] { "Assets/Game/Settings", "Assets/Game/Audio", "Assets/Game/Materials" }
                .SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                .ToDictionary(path => path, File.ReadAllBytes);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (string path in paths)
            {
                File.Delete(path);
            }

            Assert.That(paths.All(path => !File.Exists(path)), Is.True);
            SceneBuilder.BuildAll();
            string first = Signature();
            var generatedFiles = GeneratedSceneSnapshot.Files()
                .Where(path => !path.EndsWith(".unity") && !path.EndsWith(".prefab"))
                .ToDictionary(path => path, File.ReadAllBytes);
            AssertContentPreserved(files);
            AssertContentPreserved(guids);

            SceneBuilder.BuildAll();
            Assert.That(Signature(), Is.EqualTo(first), "All eight scenes must regenerate consistently.");
            Assert.That(GeneratedSceneSnapshot.Files().Where(path => !path.EndsWith(".unity") && !path.EndsWith(".prefab")),
                Is.EquivalentTo(generatedFiles.Keys), "Repeated generation must not add assets.");
            AssertContentPreserved(generatedFiles);
            AssertContentPreserved(files);
            AssertContentPreserved(guids);
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(paths), "All eight scenes must be registered in startup order.");
        }

        private static void AssertContentPreserved(System.Collections.Generic.Dictionary<string, byte[]> files)
        {
            foreach (var pair in files)
            {
                Assert.That(File.Exists(pair.Key), Is.True, pair.Key);
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        private static string Signature()
        {
            var result = new System.Text.StringBuilder();
            var references = new System.Collections.Generic.Dictionary<string, string>();
            foreach (string path in GeneratedSceneSnapshot.Files().Where(path => path.EndsWith(".prefab")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    foreach (var obj in new UnityEngine.Object[] { transform.gameObject }.Concat(transform.GetComponents<Component>()))
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long localId);
                        references[guid + ":" + localId] = path + ":" + ObjectKey(obj);
                    }
                }
            }

            foreach (string path in GeneratedSceneSnapshot.Paths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                // ExecuteAlways fitting depends on the newly opened canvas dimensions.
                // Compare the settled presentation, not the previous scene's viewport state.
                Canvas.ForceUpdateCanvases();
                GameSceneValidator.Validate(scene);
                AppendSignature(result, path, scene.GetRootGameObjects(), references);
            }

            foreach (string path in GeneratedSceneSnapshot.Files().Where(path => path.EndsWith(".prefab")).OrderBy(path => path))
            {
                AppendSignature(result, path, new[] { AssetDatabase.LoadAssetAtPath<GameObject>(path) }, references);
            }

            return result.ToString();
        }

        private static void AppendSignature(System.Text.StringBuilder result, string path, GameObject[] roots,
            System.Collections.Generic.Dictionary<string, string> references)
        {
            var objects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SelectMany(transform => new UnityEngine.Object[] { transform.gameObject }
                    .Concat(transform.GetComponents<Component>())).ToArray();
            Assert.That(objects, Has.None.Null, "Missing serialized script in " + path);
            var ids = objects.ToDictionary(obj => obj.GetInstanceID(), ObjectKey);
            foreach (var obj in objects)
            {
                string json = EditorJsonUtility.ToJson(obj);
                json = System.Text.RegularExpressions.Regex.Replace(json, @"""instanceID"":(-?\d+)", match =>
                {
                    int id = int.Parse(match.Groups[1].Value);
                    if (ids.TryGetValue(id, out string key))
                    {
                        return "reference:" + key;
                    }

                    var asset = EditorUtility.InstanceIDToObject(id);
                    if (!asset)
                    {
                        return "reference:null";
                    }

                    if (asset is GameObject || asset is Component)
                    {
                        return "asset:" + AssetDatabase.GetAssetPath(asset) + ":" + ObjectKey(asset);
                    }

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId);
                    return "asset:" + guid + ":" + localId + ":" + asset?.name;
                });
                json = System.Text.RegularExpressions.Regex.Replace(json,
                    @"""fileID"":(-?\d+),""guid"":""([^""]+)""", match =>
                    {
                        if (references.TryGetValue(match.Groups[2].Value + ":" + match.Groups[1].Value, out string key))
                        {
                            return "prefab-reference:" + key;
                        }

                        return match.Value;
                    });
                result.AppendLine(path + ":" + ObjectKey(obj) + ":" + json);
            }
        }

        private static string ObjectKey(UnityEngine.Object obj)
        {
            Transform transform;
            if (obj is GameObject gameObject)
            {
                transform = gameObject.transform;
            }
            else
            {
                transform = ((Component)obj).transform;
            }

            string path = "";
            while (transform)
            {
                path = transform.GetSiblingIndex() + "/" + path;
                transform = transform.parent;
            }

            int componentIndex = 0;
            if (obj is Component component)
            {
                componentIndex = System.Array.IndexOf(component.GetComponents(component.GetType()), component);
            }

            return path + obj.GetType().FullName + ":" + componentIndex;
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("AssetGeneration")]
        public void SnapshotRestoresFilesAndRemovesNewAssetsAfterSuccessOrFailure(bool fail)
        {
            const string created = "Assets/Game/Scenes/SnapshotCleanupProbe.txt";
            Assert.That(File.Exists(created), Is.False);
            var original = File.ReadAllBytes(SceneBuilder.TitlePath);
            var meta = File.ReadAllBytes(SceneBuilder.TitlePath + ".meta");
            var scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray();
            void Mutate()
            {
                using var snapshot = new GeneratedSceneSnapshot();
                File.Delete(SceneBuilder.TitlePath);
                File.Delete(SceneBuilder.TitlePath + ".meta");
                File.WriteAllText(created, "owned by snapshot verification");
                File.WriteAllText(created + ".meta", "owned by snapshot verification");
                EditorBuildSettings.scenes = new EditorBuildSettingsScene[0];
                if (fail)
                {
                    throw new System.InvalidOperationException("Injected failure");
                }
            }

            if (fail)
            {
                Assert.Throws<System.InvalidOperationException>(Mutate);
            }
            else
            {
                Mutate();
            }

            Assert.That(File.ReadAllBytes(SceneBuilder.TitlePath), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(SceneBuilder.TitlePath + ".meta"), Is.EqualTo(meta));
            Assert.That(File.Exists(created), Is.False);
            Assert.That(File.Exists(created + ".meta"), Is.False);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Is.EqualTo(scenes));
        }

        [Test]
        [Category("Fast")]
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
