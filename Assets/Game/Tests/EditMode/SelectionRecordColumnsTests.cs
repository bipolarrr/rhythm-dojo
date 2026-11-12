using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SelectionRecordColumnsTests
    {
        [TestCase(1920)]
        [TestCase(1728)]
        [TestCase(1440)]
        [Category("SceneIntegration")]
        public void ScoreAndComboColumnsKeepLargeTextInsideSeparateBounds(int width)
        {
            var scene = SceneManager.GetSceneByPath(SceneBuilder.SelectionPath);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(SceneBuilder.SelectionPath, OpenSceneMode.Additive);
            }

            try
            {
                var view = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SongSelectionView>()).Single();
                view.Validate();
                Assert.That(view.record.rectTransform.parent, Is.SameAs(view.comboRecord.rectTransform.parent));
                Assert.That(view.record.rectTransform.anchoredPosition.y - view.record.rectTransform.sizeDelta.y / 2,
                    Is.GreaterThan(view.comboRecord.rectTransform.anchoredPosition.y + view.comboRecord.rectTransform.sizeDelta.y / 2));
                Assert.That(view.record.font, Is.SameAs(view.comboRecord.font));
                Assert.That(view.comboRecord.fontStyle, Is.EqualTo(UiFontSettings.Style));
                Assert.That(view.record.resizeTextMinSize, Is.GreaterThanOrEqualTo(18));
                Assert.That(view.comboRecord.resizeTextMinSize, Is.GreaterThanOrEqualTo(18));
                AssertFits(view.record, "스코어  1,000,000\n랭크  SSS\n레이트  100.00%", width);
                AssertFits(view.comboRecord,
                    "최대 콤보  1,000,000\nGauge CLEAR\nChallenge 1 / 2 / 3\n실패 완주 1,000,000 · 100.00%", width);
            }
            finally
            {
                if (!alreadyLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssertFits(Text label, string text, int width)
        {
            var ancestors = new Stack<RectTransform>();
            var canvas = label.GetComponentInParent<Canvas>().transform;
            for (var rect = label.rectTransform; rect && rect != canvas; rect = rect.parent as RectTransform)
            {
                ancestors.Push(rect);
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            float scale = Mathf.Pow(width / scaler.referenceResolution.x, 1 - scaler.matchWidthOrHeight)
                * Mathf.Pow(1080 / scaler.referenceResolution.y, scaler.matchWidthOrHeight);
            var size = new Vector2(width, 1080) / scale;
            foreach (var rect in ancestors)
            {
                size = Vector2.Scale(rect.anchorMax - rect.anchorMin, size) + rect.sizeDelta;
            }

            var settings = label.GetGenerationSettings(size);
            settings.fontSize = label.resizeTextMinSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using var generator = new TextGenerator();
            float height = generator.GetPreferredHeight(text, settings) / label.pixelsPerUnit;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float textWidth = generator.GetPreferredWidth(text, settings) / label.pixelsPerUnit;
            Assert.That(textWidth, Is.LessThanOrEqualTo(size.x + 1), label.name + " horizontal overflow at " + width);
            Assert.That(height, Is.LessThanOrEqualTo(size.y + 1), label.name + " at width " + width);
        }
    }
}
