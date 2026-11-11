using System.Linq;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class MainMenuTests
    {
        [Test]
        [Category("SceneIntegration")]
        public void GeneratedMenuHasRequiredReferencesAnchorsAndKoreanGlyphs()
        {
            var original = SceneManager.GetActiveScene();

            var scene = EditorSceneManager.OpenScene(SceneBuilder.TitlePath, OpenSceneMode.Additive);

            try
            {
                GameSceneValidator.Validate(scene);

                var view = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MainMenuView>())
                    .Single();
                Assert.That(view.title.text, Is.EqualTo("리듬 도장"));
                Assert.That(view.title.enabled, Is.True);
                Assert.That(view.menuPanel.gameObject.activeSelf, Is.False);
                Assert.That(view.menuPanel.blocksRaycasts, Is.False);
                Assert.That(view.modeSelection, Is.Not.Null);
                view.modeSelection.Validate();
                foreach (char glyph in "비급 전수자유 연습실력 검정채보 공방리듬 도장")
                {
                    Assert.That(view.title.font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
                }
                foreach (var button in new[] { view.start, view.settings, view.editor, view.quit })
                {
                    var rect = (RectTransform)button.transform;
                    Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(44));
                    Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(44));
                    Assert.That(button.targetGraphic.raycastTarget, Is.True);
                    var others = new[] { view.start, view.settings, view.editor, view.quit }.Where(other => other != button);
                    foreach (var other in others)
                    {
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.menuPanel.transform, rect);
                        var otherBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.menuPanel.transform, other.transform);
                        Assert.That(bounds.Intersects(otherBounds), Is.False, "Menu click targets must not overlap.");
                    }
                }
                Assert.That(
                    scene
                        .GetRootGameObjects()
                        .SelectMany(root =>
                            root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()
                        )
                        .Count(),
                    Is.EqualTo(1)
                );
                Assert.That(
                    scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Camera>())
                        .Single()
                        .backgroundColor,
                    Is.EqualTo(Color.black)
                );
                Assert.That(
                    scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<AudioListener>())
                        .Count(),
                    Is.EqualTo(1)
                );

                var root = view.GetComponent<MainMenuCompositionRoot>();
                Assert.That(
                    new SerializedObject(root).FindProperty("titleMusic").objectReferenceValue,
                    Is.EqualTo(
                        AssetDatabase.LoadAssetAtPath<TitleMusicSettings>(
                            MainMenuSceneBuilder.TitleMusicPath
                        )
                    )
                );
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);

                if (original.IsValid() && original.isLoaded)
                {
                    SceneManager.SetActiveScene(original);
                }
            }
        }

        [Test]
        [Category("Fast")]
        public void TitleIsFirstEnabledBuildSceneAndBootstrapRemainsRegistered()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes[0].path, Is.EqualTo(SceneBuilder.TitlePath));
            Assert.That(scenes.Count(scene => scene.path == SceneBuilder.TitlePath), Is.EqualTo(1));
            Assert.That(scenes.Any(scene => scene.path == SceneBuilder.BootstrapPath), Is.True);
        }

        [Test]
        [Category("Fast")]
        public void EmptyMusicUsesConfiguredBpmWithoutAnIntroDelay()
        {
            var settings = ScriptableObject.CreateInstance<TitleMusicSettings>();

            try
            {
                SetMusic(settings, null, 60f, 3f);
                settings.Validate();
                Assert.That(settings.BeatPositionAt(.5d), Is.EqualTo(.5d).Within(.00001d));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        [Category("Fast")]
        public void MusicBeatRestartsAtTheClipLoopAndWaitsForTheFirstBeat()
        {
            var settings = ScriptableObject.CreateInstance<TitleMusicSettings>();
            var clip = AudioClip.Create("Title loop test", 18000, 1, 8000, false);

            try
            {
                SetMusic(settings, clip, 120f, .25f);
                settings.Validate();
                Assert.That(settings.BeatPositionAt(0d), Is.LessThan(0d));
                Assert.That(settings.BeatPositionAt(.25d), Is.EqualTo(0d));
                Assert.That(settings.BeatPositionAt(.75d), Is.EqualTo(1d));

                // A 2.25-second clip is deliberately not a whole number of beats.
                Assert.That(settings.BeatPositionAt(2.25d), Is.LessThan(0d));
                Assert.That(settings.BeatPositionAt(2.5d), Is.EqualTo(0d));
                Assert.That(settings.BeatPositionAt(3d), Is.EqualTo(1d));
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Fast")]
        public void FirstBeatOutsideMusicIsRejected()
        {
            var settings = ScriptableObject.CreateInstance<TitleMusicSettings>();
            var clip = AudioClip.Create("Title validation test", 8000, 1, 8000, false);

            try
            {
                SetMusic(settings, clip, 120f, 1f);
                Assert.Throws<System.InvalidOperationException>(() => settings.Validate());
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(clip);
            }
        }

        private static void SetMusic(
            TitleMusicSettings settings,
            AudioClip clip,
            float bpm,
            float firstBeat
        )
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("musicClip").objectReferenceValue = clip;
            serialized.FindProperty("beatsPerMinute").floatValue = bpm;
            serialized.FindProperty("firstBeatSeconds").floatValue = firstBeat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
