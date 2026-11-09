using System.Linq;
using NUnit.Framework;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.Tests
{
    public sealed class AuthoredCompositionTests
    {
        [Test]
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
        public void NotePrefabContainsItsCompleteAppearance(string name, NoteKind kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/" + name + ".prefab");
            var view = prefab.GetComponent<NoteView>();
            view.Validate(kind);
            var skin = prefab.transform.Find("Head/Songpan Skin").GetComponent<SpriteRenderer>();
            Assert.That(AssetDatabase.Contains(skin.sprite), Is.True);
            Assert.That(skin.transform.localRotation, Is.EqualTo(Quaternion.Euler(28, 0, 0)));
            if (kind == NoteKind.Hold)
            {
                var tail = prefab.transform.Find("Hold End Marker").GetComponent<MeshRenderer>();
                Assert.That(tail.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                Assert.That(tail.sharedMaterial, Is.SameAs(prefab.transform.Find("Hold Body").GetComponent<Renderer>().sharedMaterial));
            }
        }

        [Test]
        public void CursorPrefabReferencesBakedFramesAndImprintInsteadOfOwnedTextures()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/DojoCursor.prefab");
            var cursor = new SerializedObject(prefab.GetComponent<DojoCursor>());
            var normal = cursor.FindProperty("normalTexture").objectReferenceValue;
            Assert.That(AssetDatabase.Contains(normal), Is.True);
            Assert.That(cursor.FindProperty("hoverTexture").objectReferenceValue, Is.SameAs(normal));
            var frames = cursor.FindProperty("punchFrames");
            Assert.That(frames.arraySize, Is.EqualTo(7));
            for (int i = 0; i < frames.arraySize; i++)
            {
                var frame = (Texture2D)frames.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(AssetDatabase.Contains(frame), Is.True);
                Assert.That(frame.isReadable, Is.True);
                Assert.That(frame.width, Is.EqualTo(128));
                Assert.That(frame.height, Is.EqualTo(128));
            }

            var impact = prefab.GetComponentInChildren<DojoPunchImpact>(true);
            Assert.That(AssetDatabase.Contains(impact.mainTexture), Is.True);
        }
    }
}
