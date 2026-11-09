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
                Assert.That(view.GetComponentsInChildren<Button>().Length, Is.EqualTo(3));
                Assert.That(view.review.gameObject.activeSelf, Is.False);
                view.review.Validate();
                Assert.That(view.scoreSummary.transform.parent, Is.EqualTo(view.feedback));
                Assert.That(view.scoreValue.resizeTextMaxSize, Is.GreaterThan(view.scoreSummary.resizeTextMaxSize));
                Assert.That(view.newRecordBadge.activeSelf, Is.False);
                Assert.That(view.feedback.GetComponentsInChildren<Button>(), Is.EqualTo(new[] { view.retryButton }));
                Assert.That(view.recommendationButton, Is.Not.Null);
                Assert.That(view.recommendationButton.gameObject.activeSelf, Is.False);
                Assert.That(view.moreButton.onClick.GetPersistentEventCount(), Is.Zero);
                foreach (var button in new[] { view.backButton, view.moreButton })
                {
                    var label = button.GetComponentInChildren<Text>();
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width), label.text);
                }
                Assert.That(view.cover.rectTransform.anchorMax.x, Is.LessThanOrEqualTo(1));
                var summary = (RectTransform)view.title.transform.parent;
                Assert.That(view.feedback.anchorMin.y, Is.GreaterThan(summary.anchorMax.y));
                Assert.That(view.progressNote, Is.Not.Null);
                Assert.That(view.retryButton.GetComponentInChildren<PixelDojoFrameGraphic>().color,
                    Is.Not.EqualTo(view.recommendationButton.GetComponentInChildren<PixelDojoFrameGraphic>().color));
                Assert.That(((RectTransform)view.outcome.transform.parent).anchorMin.x, Is.GreaterThan(.69f));
                Assert.That(view.feedback.GetComponentInChildren<PixelDojoFrameGraphic>().artwork.name,
                    Is.EqualTo("SettingsPanel"));
                Assert.That(view.moreButton.GetComponentInChildren<PixelDojoFrameGraphic>().artwork.name,
                    Is.EqualTo("SettingsControl"));
                Assert.That(view.outcome.transform.parent.Find("Wooden Result Seal").GetComponent<Image>().sprite.name,
                    Is.EqualTo("RoundPlaque"));
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
