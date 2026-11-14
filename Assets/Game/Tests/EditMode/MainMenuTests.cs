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
        [TestCase(false)]
        [TestCase(true)]
        [Category("SceneIntegration")]
        public void GeneratedMenuHasRequiredReferencesAnchorsAndKoreanGlyphs(bool modeSelection)
        {
            var original = SceneManager.GetActiveScene();

            string path = SceneBuilder.TitlePath;
            if (modeSelection)
            {
                path = SceneBuilder.ModeSelectionPath;
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                GameSceneValidator.Validate(scene);

                var view = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MainMenuView>())
                    .Single();
                if (!modeSelection)
                {
                    Assert.That(view.title.text, Is.EqualTo("리듬 도장"));
                    Assert.That(view.title.enabled, Is.True);
                    Assert.That(view.modeSelection, Is.Null);
                    Assert.That(view.menuPanel, Is.Null);
                    Assert.That(view.entrance.Opening, Is.Zero);
                    AssertTitleLandscapeFits(view);
                    return;
                }
                Assert.That(view.entrance, Is.Null);
                Assert.That(view.menuPanel.gameObject.activeSelf, Is.True);
                Assert.That(view.menuPanel.blocksRaycasts, Is.True);
                Assert.That(view.modeSelection, Is.Not.Null);
                view.modeSelection.Validate();
                foreach (char glyph in "비급 전수자유 연습실력 검정율보 편찬리듬 도장")
                {
                    Assert.That(view.modeSelection.heading.font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
                }
                Assert.That(view.transform.Find("Menu Buttons/Mode Stages/Play"), Is.Null,
                    "The retired start button must not remain in the serialized scene.");
                foreach (var button in new[] { view.settings, view.editor, view.quit })
                {
                    var rect = (RectTransform)button.transform;
                    Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(44));
                    Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(44));
                    Assert.That(button.targetGraphic.raycastTarget, Is.True);
                    var others = new[] { view.settings, view.editor, view.quit }.Where(other => other != button);
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
            Assert.That(scenes.Count(scene => scene.path == SceneBuilder.ModeSelectionPath), Is.EqualTo(1));
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

        private static void AssertTitleLandscapeFits(MainMenuView view)
        {
            var sky = view.GetComponent<TitleSkyView>();
            var canvas = view.GetComponent<Canvas>();
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var rect = (RectTransform)canvas.transform;
            var originalMode = canvas.renderMode;
            var originalSize = rect.sizeDelta;
            bool originalScaler = scaler.enabled;
            float originalDescent = sky.Descent;
            var upper = (RectTransform)sky.clouds[0].parent;
            var lower = (RectTransform)view.entrance.frame.transform;
            var topCorners = new Vector3[4];
            var bottomCorners = new Vector3[4];
            var sizes = new[] { new Vector2(1280, 720), new Vector2(1920, 1080),
                new Vector2(1024, 768), new Vector2(1920, 1200), new Vector2(2560, 1080),
                new Vector2(720, 1280) };

            try
            {
                scaler.enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                foreach (var size in sizes)
                {
                    rect.sizeDelta = size;
                    Canvas.ForceUpdateCanvases();
                    view.entrance.GetComponent<DojoCompositionFit>().Fit();
                    var entrance = (RectTransform)view.entrance.transform;
                    entrance.GetWorldCorners(topCorners);
                    var minimum = rect.InverseTransformPoint(topCorners[0]);
                    var maximum = rect.InverseTransformPoint(topCorners[2]);
                    Assert.That(minimum.x, Is.LessThanOrEqualTo(rect.rect.xMin + .01f), "Outdoor scene must cover the viewport.");
                    Assert.That(minimum.y, Is.LessThanOrEqualTo(rect.rect.yMin + .01f));
                    Assert.That(maximum.x, Is.GreaterThanOrEqualTo(rect.rect.xMax - .01f));
                    Assert.That(maximum.y, Is.GreaterThanOrEqualTo(rect.rect.yMax - .01f));

                    ((RectTransform)sky.logo.transform).GetWorldCorners(topCorners);
                    Assert.That(rect.InverseTransformPoint(topCorners[0]).x, Is.GreaterThanOrEqualTo(rect.rect.xMin));
                    Assert.That(rect.InverseTransformPoint(topCorners[2]).x, Is.LessThanOrEqualTo(rect.rect.xMax));
                    foreach (float progress in new[] { 0f, .5f, 1f })
                    {
                        sky.SetDescent(progress);
                        upper.GetWorldCorners(topCorners);
                        lower.GetWorldCorners(bottomCorners);
                        Assert.That(Vector3.Distance(topCorners[0], bottomCorners[1]), Is.LessThan(.01f),
                            "Sky and roof must remain connected while moving or resizing.");
                        Assert.That(Vector3.Distance(topCorners[3], bottomCorners[2]), Is.LessThan(.01f));
                    }
                }
            }
            finally
            {
                sky.SetDescent(originalDescent);
                rect.sizeDelta = originalSize;
                canvas.renderMode = originalMode;
                scaler.enabled = originalScaler;
                Canvas.ForceUpdateCanvases();
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
