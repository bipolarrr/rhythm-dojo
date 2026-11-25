using System.Linq;
using NUnit.Framework;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class AuthoredCompositionTests
    {
        [Test]
        [Category("Fast")]
        public void SettingsPrefabContainsCalibrationBeforeAnyControllerInitializes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/SettingsPopup.prefab");
            var view = prefab.GetComponent<SettingsView>();
            view.Validate();
            var calibration = view.calibration;
            Assert.That(calibration.overlay.activeSelf, Is.False);
            Assert.That(calibration.speedLane.rail.gameObject.activeSelf, Is.False);
            Assert.That(calibration.source.playOnAwake, Is.False);
            Assert.That(calibration.speedLane.notes.Distinct().Count(), Is.EqualTo(32));
            Assert.That(calibration.speedLayout.statusParent, Is.SameAs(calibration.speedLane.rail));
            Assert.That(calibration.timingLayout.statusParent, Is.SameAs(calibration.panelRect));
        }

        [TestCase("TapNote", NoteKind.Tap)]
        [TestCase("HoldNote", NoteKind.Hold)]
        [Category("Fast")]
        public void NotePrefabContainsItsCompleteAppearance(string name, NoteKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/" + name + ".prefab");
            var view = prefab.GetComponent<NoteView>();
            view.Validate(kind);
            var skin = view.transform.Find("Head").GetComponent<Image>();
            Assert.That(AssetDatabase.Contains(skin.sprite), Is.True);
            Assert.That(skin.sprite.texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(skin.sprite.rect.width, Is.GreaterThan(skin.rectTransform.rect.width));
            var clone = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var cloneSkin = clone.transform.Find("Head").GetComponent<Image>();
                Assert.That(cloneSkin.sprite, Is.SameAs(skin.sprite));
                UnityEngine.Object.DestroyImmediate(clone);
                clone = null;
                Assert.That(skin.sprite, Is.Not.Null, "Destroying a note must not destroy shared artwork.");
            }
            finally
            {
                if (clone)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
            if (kind == NoteKind.Hold)
            {
                var tail = view.transform.Find("Hold End Marker").GetComponent<Image>();
                Assert.That(AssetDatabase.Contains(tail.sprite), Is.True);
                Assert.That(tail.sprite, Is.Not.SameAs(skin.sprite));
            }
        }

        [Test]
        [Category("Fast")]
        public void CursorPrefabReferencesBakedFramesAndImprintInsteadOfOwnedTextures()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/DojoCursor.prefab");
            var cursor = new SerializedObject(prefab.GetComponent<DojoCursor>());
            var normal = cursor.FindProperty("normalTexture").objectReferenceValue;
            Assert.That(AssetDatabase.Contains(normal), Is.True);
            Assert.That(cursor.FindProperty("hoverTexture").objectReferenceValue, Is.SameAs(normal));
            var frames = cursor.FindProperty("punchFrames");
            Assert.That(frames.arraySize, Is.GreaterThan(1), "Punch animation needs authored frames.");
            for (int i = 0; i < frames.arraySize; i++)
            {
                var frame = (Texture2D)frames.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(AssetDatabase.Contains(frame), Is.True);
                Assert.That(frame.isReadable, Is.True);
                Assert.That(frame.width, Is.EqualTo(((Texture2D)normal).width));
                Assert.That(frame.height, Is.EqualTo(((Texture2D)normal).height));
            }

            var impact = prefab.GetComponentInChildren<DojoPunchImpact>(true);
            Assert.That(AssetDatabase.Contains(impact.mainTexture), Is.True);
        }
    }
}
