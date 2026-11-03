using System;
using System.IO;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class UiArtworkAssetsTests
    {
        [Test]
        public void ExplicitCropAndBorderRefreshKeepSpriteIdentityAndMeta()
        {
            string folder = "Assets/Game/Tests/Artwork-" + Guid.NewGuid().ToString("N");
            string texturePath = folder + "/Texture.png";
            string path = folder + "/Sprite.asset";
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            AssetDatabase.CreateFolder("Assets/Game/Tests", Path.GetFileName(folder));
            try
            {
                var pixels = new Color32[32 * 32];
                for (int y = 7; y < 17; y++)
                {
                    for (int x = 4; x < 20; x++)
                    {
                        pixels[y * 32 + x] = new Color32(200, 100, 50, 255);
                    }
                }
                pixels[0] = new Color32(255, 255, 255, 31);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath);
                var sprite = UiArtworkAssets.Load(texturePath, path, "Sprite", UiArtworkCrop.Alpha,
                    new Vector4(2, 3, 4, 1));
                Assert.That(sprite.rect, Is.EqualTo(new Rect(4, 7, 16, 10)));
                Assert.That(sprite.border, Is.EqualTo(new Vector4(2, 3, 4, 1)));
                string guid = AssetDatabase.AssetPathToGUID(path);
                string meta = File.ReadAllText(path + ".meta");
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                Assert.That(importer.isReadable, Is.True);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

                var refreshed = UiArtworkAssets.Load(texturePath, path, "Sprite", UiArtworkCrop.Full,
                    Vector4.zero, true);
                Assert.That(refreshed, Is.SameAs(sprite));
                Assert.That(refreshed.rect, Is.EqualTo(new Rect(0, 0, 32, 32)));
                Assert.That(refreshed.border, Is.EqualTo(Vector4.zero));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo(meta));
            }
            finally
            {
                Object.DestroyImmediate(texture);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase("EntranceFrame", UiArtworkCrop.Full)]
        [TestCase("RoundPlaque", UiArtworkCrop.Alpha)]
        public void ExistingEntranceAssetsKeepTheirGuidAndSpriteReference(string name, UiArtworkCrop crop)
        {
            string path = DojoEntranceAssets.Root + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            string meta = File.ReadAllText(path + ".meta");
            Assert.That(DojoEntranceAssets.Load(name, crop), Is.SameAs(sprite));
            Assert.That(DojoEntranceAssets.Load(name), Is.SameAs(sprite));
            Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo(meta));
        }
    }
}
