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
    [Category("UI")]
    public sealed class UiTypographyTests
    {
        [Test]
        [Category("Fast")]
        public void SharedPopupAndRuntimeControlsMatchStartButtonTypography()
        {
            var popup = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/SettingsPopup.prefab");
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            Assert.That(popup.GetComponent<UiFontScope>().font, Is.SameAs(font));
            foreach (var text in popup.GetComponentsInChildren<Text>(true))
            {
                AssertRoleFont(text);
                Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), text.name);
            }

            var root = new GameObject("Runtime typography test");
            try
            {
                UiElements.Label("Label", root.transform, "글씨 123", Vector2.zero, new Vector2(200, 40));
                UiElements.Button("Button", root.transform, "시작", Vector2.zero, new Vector2(200, 40));
                UiElements.Dropdown("Dropdown", root.transform, Vector2.zero, new Vector2(200, 40));
                RhythmDojo.Authoring.AudioTimelineUi.Field(root.transform, "Input", "입력", 0, 0, 200, "123");
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.font, Is.SameAs(UiFontSettings.Current), text.name);
                    Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), text.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Fast")]
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
                    Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), text.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Fast")]
        public void IndividualAndTreeFontApplicationUseTheSameInactiveScope()
        {
            var root = new GameObject("Font resolver", typeof(UiFontScope));
            var font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
            root.GetComponent<UiFontScope>().font = font;
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
        [Category("SceneIntegration")]
        public void SavedSceneUsesMonaForAllTextIncludingHiddenControls(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(UiFontSettings.Current, Is.SameAs(font));
            if (name == "SongSelection" || name == "Results")
            {
                font = AssetDatabase.LoadAssetAtPath<Font>(SongSelectionLayoutBuilder.BrushFontPath);
                Assert.That(font, Is.Not.Null);
            }
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
                        if (name == "ChartEditor")
                        {
                            Assert.That(text.font, Is.SameAs(AssetDatabase.LoadAssetAtPath<Font>(
                                "Assets/Game/Fonts/NotoSansKR-Regular.otf")), text.name);
                            Assert.That(text.material, Is.SameAs(text.defaultMaterial),
                                name + "/" + text.name + " must not inherit the game text effect.");
                        }
                        else
                        {
                            AssertRoleFont(text);
                        }

                        Assert.That(text.fontStyle, Is.EqualTo(FontStyle.Normal), name + "/" + text.name);
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
        private static void AssertRoleFont(Text text)
        {
            var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
            var style = text.GetComponent<UiTextStyle>();
            Assert.That(style, Is.Not.Null, text.name);
            Font expected = settings.font;
            switch (style.role)
            {
                case UiTextRole.Title:
                    expected = settings.bold;
                    break;
                case UiTextRole.Help:
                    expected = settings.regular;
                    break;
            }

            var composition = text.GetComponentInParent<DojoModeTypography>(true);
            var menu = text.GetComponentInParent<MainMenuView>(true);
            if (composition && text == composition.heading)
            {
                expected = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            }
            if (menu && text == menu.title)
            {
                expected = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            }

            Assert.That(text.font, Is.SameAs(expected), text.name);
        }

        [Test]
        [Category("Fast")]
        public void ExplicitRolesIgnoreNamesAndSurviveTreeReapplication()
        {
            var root = new GameObject("Play", typeof(UiFontScope));
            try
            {
                var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
                var title = UiElements.Label("Help", root.transform, "제목", Vector2.zero,
                    Vector2.one, role: UiTextRole.Title);
                var body = UiElements.Label("Title", root.transform, "본문", Vector2.zero, Vector2.one);
                var help = UiElements.Label("Score Value", root.transform, "도움말", Vector2.zero,
                    Vector2.one, role: UiTextRole.Help);
                help.gameObject.SetActive(false);
                root.GetComponent<UiFontScope>().font = settings.font;
                UiFontSettings.ApplyTo(root.transform);
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(title.font, Is.SameAs(settings.bold));
                Assert.That(body.font, Is.SameAs(settings.font));
                Assert.That(help.font, Is.SameAs(settings.regular));
                title.name = "Renamed";
                UiFontSettings.Apply(title);
                Assert.That(title.font, Is.SameAs(settings.bold));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Fast")]
        public void ScopeOrderAndPixelSmoothRoundTripsPreserveCustomMaterials(bool configureFirst)
        {
            var root = new GameObject("Rendering scope", typeof(UiFontScope));
            var scope = root.GetComponent<UiFontScope>();
            var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
            var custom = new Material(settings.pixelText);
            try
            {
                scope.useSmoothRendering = configureFirst;
                var text = UiElements.Label("Label", root.transform, "표시", Vector2.zero, Vector2.one,
                    role: UiTextRole.Title);
                scope.useSmoothRendering = true;
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(text.material, Is.SameAs(text.defaultMaterial));
                scope.useSmoothRendering = false;
                UiFontSettings.Apply(text);
                Assert.That(text.material, Is.SameAs(settings.pixelText));
                scope.useSmoothRendering = true;
                UiFontSettings.Apply(text);
                Assert.That(text.material, Is.SameAs(text.defaultMaterial));
                text.material = custom;
                UiFontSettings.Apply(text);
                scope.useSmoothRendering = false;
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(text.material, Is.SameAs(custom));
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/NotoSansKR-Regular.otf");
                scope.font = font;
                scope.style = FontStyle.Normal;
                UiFontSettings.Apply(text);
                Assert.That(text.font, Is.SameAs(font));
                scope.font = settings.font;
                UiFontSettings.Apply(text);
                Assert.That(text.font, Is.SameAs(settings.bold));
                text.font = font;
                UiFontSettings.Apply(text);
                UiFontSettings.ApplyTo(root.transform);
                Assert.That(text.font, Is.SameAs(font), "Explicit per-text font takes precedence.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(custom);
            }
        }
    }
}
