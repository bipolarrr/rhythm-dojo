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
        public void SharedPopupAndRuntimeControlsMatchStartButtonTypography()
        {
            var popup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/SettingsPopup.prefab");
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            Assert.That(popup.GetComponent<UiFontScope>().font, Is.SameAs(font));
            foreach (var text in popup.GetComponentsInChildren<Text>(true))
            {
                AssertSavedTypography(text, text.name);
                Assert.That(text.material.GetFloat("_PixelSize"), Is.GreaterThan(1), text.name);
            }

            var root = new GameObject("Runtime typography test");
            try
            {
                var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
                var label = UiElements.Label("Label", root.transform, "글씨 123", Vector2.zero, new Vector2(200, 40));
                var play = UiElements.Button("Play", root.transform, "시작", Vector2.zero, new Vector2(200, 40));
                var help = UiElements.Label("Help", root.transform, "도움말", Vector2.zero, new Vector2(200, 40));
                UiElements.Dropdown("Dropdown", root.transform, Vector2.zero, new Vector2(200, 40));
                RhythmDojo.Authoring.AudioTimelineUi.Field(root.transform, "Input", "입력", 0, 0, 200, "123");
                Assert.That(label.font, Is.SameAs(settings.font));
                Assert.That(label.material, Is.SameAs(settings.pixelText));
                Assert.That(play.GetComponentInChildren<Text>().font, Is.SameAs(settings.bold));
                Assert.That(help.font, Is.SameAs(settings.regular));
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    AssertSavedTypography(text, text.name);
                }

                root.AddComponent<UiFontScope>().useSmoothRendering = true;
                var editorLabel = UiElements.Label("Editor Label", root.transform, "편집", Vector2.zero,
                    new Vector2(200, 40));
                Assert.That(editorLabel.material, Is.SameAs(editorLabel.defaultMaterial));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScopedControlsKeepBrushFontWhenCreatedAtRuntime()
        {
            var root = new GameObject("Scoped typography test", typeof(UiFontScope));
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            Assert.That(font, Is.Not.Null);
            try
            {
                var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
                foreach (var weight in new[] { font, settings.bold, settings.regular })
                {
                    foreach (char character in "곡선택수련난이도정확콤보기록시작설정뒤로0123456789+.%DFJK")
                    {
                        Assert.That(weight.HasCharacter(character), Is.True, weight.name + "/" + character);
                    }
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(weight)) as TrueTypeFontImporter;
                    Assert.That(importer.includeFontData, Is.True);
                    Assert.That(importer.fontReferences, Does.Contain(
                        AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Mona12TextKR.ttf")));
                }
                root.GetComponent<UiFontScope>().font = settings.bold;
                UiElements.Label("Label", root.transform, "곡 선택 123", Vector2.zero, new Vector2(300, 50));
                UiElements.Button("Button", root.transform, "시작", Vector2.zero, new Vector2(200, 50));
                UiElements.Dropdown("Dropdown", root.transform, Vector2.zero, new Vector2(200, 50));
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.font, Is.SameAs(settings.bold), text.name);
                    Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Bold), text.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IndividualAndTreeFontApplicationUseTheSameInactiveScope()
        {
            var root = new GameObject("Font resolver", typeof(UiFontScope));
            var font = Resources.Load<UiFontSettings>("UI/UiFontSettings").regular;
            var scope = root.GetComponent<UiFontScope>();
            scope.font = font;
            scope.style = UiFontSettings.Style;
            try
            {
                var text = UiElements.Label("Hidden label", root.transform, "Test", Vector2.zero,
                    new Vector2(200, 40));
                text.gameObject.SetActive(false);
                UiFontSettings.Apply(text);
                Assert.That(text.font, Is.SameAs(font));
                text.font = null;
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(text.font, Is.SameAs(font));
                Assert.That(text.fontStyle, Is.EqualTo(UiFontSettings.Style));
                root.GetComponent<UiFontScope>().font = null;
                UiFontSettings.Apply(text);
                Assert.That(text.font, Is.SameAs(UiFontSettings.Current));
                text.font = null;
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(text.font, Is.SameAs(UiFontSettings.Current));
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
                Font editorFont = null;
                if (name == "ChartEditor")
                {
                    editorFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansKR-Regular.otf");
                    Assert.That(editorFont, Is.Not.Null);
                }

                int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var text in root.GetComponentsInChildren<Text>(true))
                    {
                        if (editorFont)
                        {
                            Assert.That(text.font, Is.SameAs(editorFont), text.name);
                            Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), text.name);
                            Assert.That(text.material, Is.SameAs(text.defaultMaterial), text.name);
                        }
                        else
                        {
                            AssertSavedTypography(text, name + "/" + text.name);
                            Assert.That(text.material.GetFloat("_PixelSize"), Is.GreaterThan(1), text.name);
                        }
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

        private static void AssertSavedTypography(Text text, string context)
        {
            var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
            var font = text.font;
            var style = text.fontStyle;
            var overflow = text.verticalOverflow;
            try
            {
                Assert.That(new[] { settings.font, settings.bold, settings.regular }, Does.Contain(font), context);
                Assert.That(style, Is.EqualTo(FontStyle.Normal), context);
                UiFontSettings.Apply(text);
                Assert.That(font, Is.SameAs(text.font), context);
            }
            finally
            {
                text.font = font;
                text.fontStyle = style;
                text.verticalOverflow = overflow;
            }
        }
    }
}
