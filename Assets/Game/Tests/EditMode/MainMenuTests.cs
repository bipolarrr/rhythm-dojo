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
                Assert.That(view.startPrompt.text, Is.EqualTo("두드려 시작"));
                Assert.That(view.title.rectTransform.parent, Is.EqualTo(view.logo.transform));
                Assert.That(view.title.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(view.startPrompt.rectTransform.parent, Is.EqualTo(view.logo.transform));
                Assert.That(view.logo.GetComponent<UnityEngine.UI.Image>().sprite, Is.Not.Null);
                Assert.That(view.alwaysVisibleMenu, Is.False);
                Assert.That(view.title.enabled, Is.False);
                Assert.That(view.logo.targetGraphic.raycastTarget, Is.True);
                Assert.That(view.logo.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(view.menuPanel.gameObject.activeSelf, Is.False);
                Assert.That(view.menuPanel.blocksRaycasts, Is.False);
                Assert.That(view.entrance.leftDoor, Is.Not.Null);
                Assert.That(view.entrance.rightDoor, Is.Not.Null);
                Assert.That(view.entrance.logoClip.GetComponent<UnityEngine.UI.RectMask2D>(), Is.Not.Null);
                Assert.That(view.logo.transform.parent, Is.SameAs(view.entrance.logoClip));
                Assert.That(view.entrance.logoClip.GetSiblingIndex(),
                    Is.LessThan(view.entrance.frame.transform.GetSiblingIndex()));
                Assert.That(view.entrance.logoClip.anchorMin.x, Is.GreaterThan(0f));
                Assert.That(view.entrance.logoClip.anchorMax.x, Is.LessThan(1f));
                Assert.That(view.knock, Is.Not.Null);
                Assert.That(view.boardCrack, Is.Not.Null);
                Assert.That(view.doorSlide, Is.Not.Null);
                Assert.That(view.logo.GetComponent<DojoPlaqueHitArea>(), Is.Not.Null);
                Assert.That(view.quitQuestion.text, Is.EqualTo("게임을 종료 하시겠습니까?"));

                foreach (char glyph in "게임을 종료 하시겠습니까?예아니요")
                {
                    Assert.That(
                        view.quitQuestion.font.HasCharacter(glyph),
                        Is.True,
                        "Missing glyph: " + glyph
                    );
                }

                Assert.That(view.quitPopup.activeSelf, Is.False);

                foreach (var icon in view.GetComponentsInChildren<MainMenuIcon>())
                {
                    Assert.That(icon.GetComponent<CanvasRenderer>(), Is.Not.Null);
                }

                Assert.That(view.title.raycastTarget || view.startPrompt.raycastTarget, Is.False);

                var quitRect = (RectTransform)view.quit.transform;
                var settingsRect = (RectTransform)view.settings.transform;
                var playRect = (RectTransform)view.start.transform;
                Assert.That(playRect.parent, Is.EqualTo(view.menuPanel.transform));
                Assert.That(settingsRect.parent, Is.EqualTo(playRect.parent));
                Assert.That(quitRect.parent, Is.EqualTo(playRect.parent));
                Assert.That(
                    playRect.anchoredPosition.x,
                    Is.LessThan(settingsRect.anchoredPosition.x)
                );
                Assert.That(
                    settingsRect.anchoredPosition.x,
                    Is.LessThan(quitRect.anchoredPosition.x)
                );
                Assert.That(
                    settingsRect.anchoredPosition.y,
                    Is.EqualTo(quitRect.anchoredPosition.y)
                );
                var editorRect = (RectTransform)view.editor.transform;
                Assert.That(editorRect.parent, Is.EqualTo(playRect.parent));
                Assert.That(editorRect.anchoredPosition.x, Is.GreaterThan(settingsRect.anchoredPosition.x));
                Assert.That(editorRect.anchoredPosition.x, Is.LessThan(quitRect.anchoredPosition.x));
                Assert.That(editorRect.anchoredPosition.y, Is.EqualTo(playRect.anchoredPosition.y));
                foreach (var button in new[] { view.start, view.settings, view.editor, view.quit })
                {
                    var rect = (RectTransform)button.transform;
                    Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(80, 290)),
                        "Authored plaque size: Docs/pixel-dojo-title.md");
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
                    Assert.That(button.GetComponentInChildren<UnityEngine.UI.Text>().font,
                        Is.SameAs(Resources.Load<UiFontSettings>("UI/UiFontSettings").bold));
                    var graphic = button.GetComponent<UnityEngine.UI.Image>();
                    Assert.That(graphic, Is.Not.Null);
                    Assert.That(graphic.preserveAspect, Is.True);
                    Assert.That(graphic.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                }

                var interiorArt = view.trainingInterior.GetComponent<DojoTrainingGraphic>();
                Assert.That(interiorArt.mainTexture, Is.SameAs(interiorArt.artwork.texture));

                Assert.That(view.start.navigation.selectOnRight, Is.SameAs(view.settings));
                Assert.That(view.settings.navigation.selectOnRight, Is.SameAs(view.editor));
                Assert.That(view.editor.navigation.selectOnLeft, Is.SameAs(view.settings));
                Assert.That(view.editor.navigation.selectOnRight, Is.SameAs(view.quit));
                Assert.That(view.quit.navigation.selectOnLeft, Is.SameAs(view.editor));
                Assert.That(view.start.navigation.selectOnDown, Is.SameAs(view.settings));
                Assert.That(view.editor.navigation.selectOnUp, Is.SameAs(view.settings));
                Assert.That(view.menuPanel.transform.GetSiblingIndex(),
                    Is.GreaterThan(view.entrance.rightDoor.GetSiblingIndex()));
                Assert.That(view.menuPanel.transform.GetSiblingIndex(),
                    Is.LessThan(view.entrance.logoClip.GetSiblingIndex()),
                    "Wooden menu plaques must render behind the logo during opening and closing.");
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
