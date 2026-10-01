using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;

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
                Assert.That(view.title.text, Is.EqualTo("rythm-dojo"));
                Assert.That(view.startPrompt.text, Is.EqualTo("Click to Start"));
                Assert.That(view.title.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .6f)));
                Assert.That(view.startPrompt.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .2f)));
                Assert.That(view.quitQuestion.text, Is.EqualTo("게임을 종료 하시겠습니까?"));
                foreach (char glyph in "게임을 종료 하시겠습니까?예아니요")
                    Assert.That(view.quitQuestion.font.HasCharacter(glyph), Is.True, "Missing glyph: " + glyph);
                Assert.That(view.quitPopup.activeSelf, Is.False);
                foreach (var icon in view.GetComponentsInChildren<MainMenuIcon>())
                    Assert.That(icon.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(view.title.raycastTarget || view.startPrompt.raycastTarget, Is.False);
                var quitRect = (RectTransform)view.quit.transform;
                var settingsRect = (RectTransform)view.settings.transform;
                Assert.That(quitRect.anchorMin, Is.EqualTo(new Vector2(1, 0)));
                Assert.That(settingsRect.anchoredPosition.y, Is.GreaterThan(quitRect.anchoredPosition.y + quitRect.sizeDelta.y));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()).Count(), Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single().backgroundColor, Is.EqualTo(Color.black));
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
    }
}