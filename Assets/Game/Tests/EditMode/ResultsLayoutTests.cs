using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    public sealed class ResultsLayoutTests
    {
        [Test]
        public void SavedResultsSceneHasSketchLayoutAndEmptyFeedback()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.ResultsPath, OpenSceneMode.Additive);

            try
            {
                GameSceneValidator.Validate(scene);
                var view = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ResultsView>())
                    .Single();
                view.Validate();
                Assert.That(view.GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
                Assert.That(view.review.gameObject.activeSelf, Is.False);
                view.review.Validate();
                Assert.That(view.feedback.GetComponentsInChildren<Text>(), Is.Empty);
                Assert.That(view.feedback.GetComponentsInChildren<Button>(), Is.Empty);
                Assert.That(view.moreButton.onClick.GetPersistentEventCount(), Is.Zero);
                Assert.That(view.cover.rectTransform.anchorMax.x, Is.LessThanOrEqualTo(1));
                Assert.That(view.feedback.anchorMax.y, Is.LessThan(.435f));
                Assert.That(view.outcome.rectTransform.anchorMin.x, Is.GreaterThan(.725f));
                Assert.That(view.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(view.GetComponent<CanvasScaler>().uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                    .Any(item => item.name.Contains("Character")), Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
