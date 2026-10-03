using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using RhythmDojo.Audio;
using RhythmDojo.Core;

namespace RhythmDojo.Tests
{
    public sealed class MainMenuTests
    {
        [Test]
        public void GeneratedMenuHasRequiredReferencesAnchorsAndKoreanGlyphs()
        {
            var original = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(original.path))
            {
                System.IO.Directory.CreateDirectory("Assets/CodexIdeRepairTemp");
                EditorSceneManager.SaveScene(original, "Assets/CodexIdeRepairTemp/MainMenuTestHost.unity");
            }
            var scene = MainMenuSceneBuilder.Create();
            try
            {
                GameSceneValidator.Validate(scene);
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuView>()).Single();
                Assert.That(view.title.text, Is.EqualTo("리듬 도장"));
                Assert.That(view.startPrompt.text, Is.EqualTo("Click to Start"));
                Assert.That(view.title.rectTransform.parent, Is.EqualTo(view.logo.transform));
                Assert.That(view.title.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(view.startPrompt.rectTransform.parent, Is.EqualTo(view.logo.transform));
                Assert.That(view.logo.GetComponent<LogoCircleGraphic>(), Is.Not.Null);
                Assert.That(view.logo.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(view.menuPanel.gameObject.activeSelf, Is.False);
                Assert.That(view.menuPanel.blocksRaycasts, Is.False);
                Assert.That(view.quitQuestion.text, Is.EqualTo("게임을 종료 하시겠습니까?"));
                foreach (char glyph in "게임을 종료 하시겠습니까?예아니요")
                    Assert.That(view.quitQuestion.font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
                Assert.That(view.quitPopup.activeSelf, Is.False);
                foreach (var icon in view.GetComponentsInChildren<MainMenuIcon>())
                    Assert.That(icon.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(view.title.raycastTarget || view.startPrompt.raycastTarget, Is.False);
                var quitRect = (RectTransform)view.quit.transform;
                var settingsRect = (RectTransform)view.settings.transform;
                var playRect = (RectTransform)view.start.transform;
                Assert.That(playRect.parent, Is.EqualTo(view.menuPanel.transform));
                Assert.That(settingsRect.parent, Is.EqualTo(playRect.parent));
                Assert.That(quitRect.parent, Is.EqualTo(playRect.parent));
                Assert.That(playRect.anchoredPosition.x, Is.LessThan(settingsRect.anchoredPosition.x));
                Assert.That(settingsRect.anchoredPosition.x, Is.LessThan(quitRect.anchoredPosition.x));
                Assert.That(settingsRect.anchoredPosition.y, Is.EqualTo(quitRect.anchoredPosition.y));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()).Count(), Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single().backgroundColor, Is.EqualTo(Color.black));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>()).Count(), Is.EqualTo(1));
                var root = view.GetComponent<MainMenuCompositionRoot>();
                Assert.That(new SerializedObject(root).FindProperty("titleMusic").objectReferenceValue,
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<TitleMusicSettings>(MainMenuSceneBuilder.TitleMusicPath)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
        }

        [Test]
        public void TitleIsFirstEnabledBuildSceneAndBootstrapRemainsRegistered()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            Assert.That(scenes[0].path, Is.EqualTo(SceneBuilder.TitlePath));
            Assert.That(scenes.Count(scene => scene.path == SceneBuilder.TitlePath), Is.EqualTo(1));
            Assert.That(scenes.Any(scene => scene.path == SceneBuilder.BootstrapPath), Is.True);
        }

        [Test]
        public void EmptyMusicUsesConfiguredBpmWithoutAnIntroDelay()
        {
            var settings = ScriptableObject.CreateInstance<TitleMusicSettings>();
            try
            {
                SetMusic(settings, null, 60f, 3f);
                settings.Validate();
                Assert.That(settings.BeatPositionAt(.5d), Is.EqualTo(.5d).Within(.00001d));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
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
            finally { Object.DestroyImmediate(settings); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void FirstBeatOutsideMusicIsRejected()
        {
            var settings = ScriptableObject.CreateInstance<TitleMusicSettings>();
            var clip = AudioClip.Create("Title validation test", 8000, 1, 8000, false);
            try
            {
                SetMusic(settings, clip, 120f, 1f);
                Assert.Throws<System.InvalidOperationException>(() => settings.Validate());
            }
            finally { Object.DestroyImmediate(settings); Object.DestroyImmediate(clip); }
        }

        private static void SetMusic(TitleMusicSettings settings, AudioClip clip, float bpm, float firstBeat)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("musicClip").objectReferenceValue = clip;
            serialized.FindProperty("beatsPerMinute").floatValue = bpm;
            serialized.FindProperty("firstBeatSeconds").floatValue = firstBeat;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
