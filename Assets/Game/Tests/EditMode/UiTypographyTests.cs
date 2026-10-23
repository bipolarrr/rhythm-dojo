using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    public sealed class UiTypographyTests
    {
        [TestCase("Title")]
        [TestCase("SongSelection")]
        [TestCase("Settings")]
        [TestCase("Gameplay")]
        [TestCase("ChartEditor")]
        public void SavedSceneUsesMonaForAllTextIncludingHiddenControls(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(UiFontSettings.Current, Is.SameAs(font));
            string path = "Assets/Game/Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var text in root.GetComponentsInChildren<Text>(true))
                    {
                        Assert.That(text.font, Is.SameAs(font), name + "/" + text.name);
                        count++;
                    }
                }

                Assert.That(count, Is.GreaterThan(0));
            }
            finally
            {
                if (!alreadyLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
