using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class ResultsLayoutTests
    {
        [Test]
        [Category("SceneIntegration")]
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
                Canvas.ForceUpdateCanvases();
                var buttons = new[] { view.backButton, view.moreButton, view.retryButton };
                foreach (var button in buttons)
                {
                    Assert.That(button.GetComponentsInChildren<Graphic>().Any(graphic => graphic.raycastTarget), Is.True);
                    var rect = (RectTransform)button.transform;
                    Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(44));
                    Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(44));
                    foreach (var other in buttons.Where(other => other != button))
                    {
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, rect);
                        var otherBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.transform, other.transform);
                        Assert.That(bounds.Intersects(otherBounds), Is.False, "Result controls must not overlap.");
                    }
                }
                Assert.That(view.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(view.GetComponent<CanvasScaler>().uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
