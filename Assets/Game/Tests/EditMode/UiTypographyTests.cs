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
        [Test]
        public void ScopedControlsKeepBrushFontWhenCreatedAtRuntime()
        {
            var root = new GameObject("Scoped typography test", typeof(UiFontScope));
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            Assert.That(font, Is.Not.Null);
            root.GetComponent<UiFontScope>().font = font;
            try
            {
                foreach (char character in "곡선택수련난이도스코어랭크레이트최대콤보기록시작설정뒤로0123456789")
                {
                    Assert.That(font.HasCharacter(character), Is.True, "Missing brush glyph: " + character);
                }
                var importer = AssetImporter.GetAtPath(SongSelectionLayoutBuilder.BrushFontPath) as TrueTypeFontImporter;
                Assert.That(importer.includeFontData, Is.True);
                Assert.That(importer.fontReferences, Does.Contain(
                    AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Mona12TextKR.ttf")));
                UiElements.Label("Label", root.transform, "곡 선택 123", Vector2.zero, new Vector2(300, 50));
                UiElements.Button("Button", root.transform, "시작", Vector2.zero, new Vector2(200, 50));
                UiElements.Dropdown("Dropdown", root.transform, Vector2.zero, new Vector2(200, 50));
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.font, Is.SameAs(font), text.name);
                    Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold), text.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Title")]
        [TestCase("SongSelection")]
        [TestCase("Settings")]
        [TestCase("Gameplay")]
        [TestCase("ChartEditor")]
        [TestCase("Results")]
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
                        var expectedFont = font;
                        var scope = text.GetComponentInParent<UiFontScope>(true);
                        if (scope && scope.font)
                        {
                            expectedFont = scope.font;
                        }
                        Assert.That(text.font, Is.SameAs(expectedFont), name + "/" + text.name);
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
