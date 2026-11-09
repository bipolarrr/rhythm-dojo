using System.IO;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class PixelDojoFrameTests
    {
        [Test]
        [Category("Fast")]
        public void ArtworkAtNormalAndSmallSizesPreservesTintMaskAndClickTargets()
        {
            var root = new GameObject("Frame capture", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Frame capture camera", typeof(Camera));
            var target = new RenderTexture(1200, 700, 24);
            var texture = new Texture2D(1200, 700, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            root.hideFlags = HideFlags.DontSave;
            cameraObject.hideFlags = HideFlags.DontSave;
            root.layer = 31;
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 350;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .18f, .22f);
            camera.cullingMask = 1 << 31;
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1200, 700);

            try
            {
                var panel = SettingsArtAssets.Load("SettingsPanel");
                var control = SettingsArtAssets.Load("SettingsControl");
                Frame(root.transform, "Normal panel", panel, new Vector2(-260, 160),
                    new Vector2(620, 300), Color.white, 24);
                Frame(root.transform, "Tinted panel", panel, new Vector2(-260, -170),
                    new Vector2(620, 300), new Color(.49f, .14f, .10f), 24);
                Frame(root.transform, "Control", control, new Vector2(350, 250),
                    new Vector2(350, 72), Color.white, 16);
                Frame(root.transform, "Tinted control", control, new Vector2(350, 150),
                    new Vector2(350, 72), new Color(.49f, .14f, .10f), 16);
                Frame(root.transform, "Small control", control, new Vector2(230, 50),
                    new Vector2(36, 22), new Color(.49f, .14f, .10f), 24);
                Frame(root.transform, "Small panel", panel, new Vector2(340, 50),
                    new Vector2(72, 36), Color.white, 24);
                Frame(root.transform, "Procedural", null, new Vector2(350, -230),
                    new Vector2(350, 110), new Color(.79f, .63f, .42f), 16);

                var mask = new GameObject("Clipped area", typeof(RectTransform), typeof(RectMask2D));
                mask.layer = 31;
                mask.transform.SetParent(root.transform, false);
                var maskRect = (RectTransform)mask.transform;
                maskRect.anchoredPosition = new Vector2(350, -80);
                maskRect.sizeDelta = new Vector2(220, 90);
                var clipped = Frame(mask.transform, "Clipped control", control, Vector2.zero,
                    new Vector2(350, 72), Color.white, 16);
                Canvas.ForceUpdateCanvases();
                foreach (var frame in root.GetComponentsInChildren<PixelDojoFrameGraphic>())
                {
                    Assert.That(frame.raycastTarget, Is.False, frame.name);
                    var mesh = frame.canvasRenderer.GetMesh();
                    if (mesh)
                    {
                        foreach (var vertex in mesh.vertices)
                        {
                            Assert.That(float.IsFinite(vertex.x) && float.IsFinite(vertex.y), Is.True, frame.name);
                        }
                    }
                }
                var inside = camera.WorldToScreenPoint(maskRect.TransformPoint(Vector3.zero));
                var outside = camera.WorldToScreenPoint(maskRect.TransformPoint(new Vector3(150, 0, 0)));
                Assert.That(clipped.Raycast(inside, camera), Is.True);
                Assert.That(clipped.Raycast(outside, camera), Is.False);
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);
                texture.Apply();
                Assert.That(texture.GetPixel(340, 510), Is.Not.EqualTo(camera.backgroundColor));
                Directory.CreateDirectory("Logs/pr-quality-remediation");
                File.WriteAllBytes("Logs/pr-quality-remediation/task6-frames-current.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        [Category("Fast")]
        public void ArtworkLayersFollowDisableReenableClearAndDestroy()
        {
            var root = new GameObject("Frame lifecycle", typeof(RectTransform), typeof(Canvas));
            try
            {
                var frame = Frame(root.transform, "Frame", SettingsArtAssets.Load("SettingsPanel"),
                    Vector2.zero, new Vector2(620, 300), Color.red, 24);
                var border = frame.GetComponentInChildren<Image>();
                Assert.That(border, Is.Not.Null);
                Assert.That(border.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(border.fillCenter, Is.False);
                Assert.That(border.color, Is.EqualTo(Color.white));
                Assert.That(border.raycastTarget, Is.False);
                Assert.That(border.transform.GetSiblingIndex(), Is.Zero);
                frame.enabled = false;
                Assert.That(border.enabled, Is.False);
                frame.enabled = true;
                Assert.That(border.enabled, Is.True);
                frame.artwork = null;
                frame.RefreshArtwork();
                Assert.That(border.enabled, Is.False);
                frame.artwork = SettingsArtAssets.Load("SettingsControl");
                frame.RefreshArtwork();
                Assert.That(border.enabled, Is.True);
                Object.DestroyImmediate(frame);
                Assert.That(border == null, Is.True, "Removing the owner must remove its artwork layer.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        [Category("Fast")]
        public void InspectorUpdatesDeferHierarchyChangesAndReuseTheBorderLayer()
        {
            var root = new GameObject("Inspector frame", typeof(RectTransform), typeof(Canvas));
            try
            {
                var frame = Frame(root.transform, "Frame", null, Vector2.zero,
                    new Vector2(620, 300), Color.white, 24);
                var serialized = new SerializedObject(frame);
                serialized.FindProperty("artwork").objectReferenceValue = SettingsArtAssets.Load("SettingsPanel");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(frame.transform.childCount, Is.Zero,
                    "OnValidate must not mutate the hierarchy during native consistency checks.");
                frame.RefreshArtwork();
                var border = frame.GetComponentInChildren<Image>();
                Assert.That(border, Is.Not.Null);
                for (int i = 0; i < 3; i++)
                {
                    serialized.Update();
                    serialized.FindProperty("borderWidth").floatValue = 16 + i;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    frame.RefreshArtwork();
                    Assert.That(frame.transform.childCount, Is.EqualTo(1));
                    Assert.That(frame.GetComponentInChildren<Image>(), Is.SameAs(border));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(2, 4, 8, 6)]
        [Category("Fast")]
        public void NativeBorderPolicySupportsZeroAndAsymmetricSourceBorders(float left, float bottom,
            float right, float top)
        {
            var root = new GameObject("Border policy", typeof(RectTransform), typeof(Canvas));
            var texture = new Texture2D(32, 32);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(left, bottom, right, top));
            try
            {
                var frame = Frame(root.transform, "Frame", sprite, Vector2.zero,
                    new Vector2(200, 100), Color.red, 16);
                var border = frame.GetComponentInChildren<Image>();
                if (right == 0)
                {
                    Assert.That(border.enabled, Is.False);
                }
                else
                {
                    Assert.That(border.enabled, Is.True);
                    Assert.That(border.pixelsPerUnitMultiplier, Is.EqualTo(.5f).Within(.0001));
                }
                Canvas.ForceUpdateCanvases();
                var mesh = frame.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                float scale = 0;
                if (right > 0)
                {
                    scale = 2;
                }
                Assert.That(mesh.vertices[0].x, Is.EqualTo(-100 + left * scale).Within(.001));
                Assert.That(mesh.vertices[0].y, Is.EqualTo(-50 + bottom * scale).Within(.001));
                Assert.That(mesh.vertices[2].x, Is.EqualTo(100 - right * scale).Within(.001));
                Assert.That(mesh.vertices[2].y, Is.EqualTo(50 - top * scale).Within(.001));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        private static PixelDojoFrameGraphic Frame(Transform parent, string name, Sprite artwork,
            Vector2 position, Vector2 size, Color color, float borderWidth)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(PixelDojoFrameGraphic));
            item.layer = 31;
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var frame = item.GetComponent<PixelDojoFrameGraphic>();
            frame.artwork = artwork;
            frame.color = color;
            frame.borderWidth = borderWidth;
            frame.raycastTarget = false;
            frame.RefreshArtwork();
            frame.SetAllDirty();
            return frame;
        }
    }
}
