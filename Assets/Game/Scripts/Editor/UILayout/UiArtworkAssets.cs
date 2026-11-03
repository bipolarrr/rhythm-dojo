using System;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public enum UiArtworkCrop
    {
        Full,
        Alpha,
    }

    public static class UiArtworkAssets
    {
        public static Sprite Load(string texturePath, string path, string name,
            UiArtworkCrop crop, Vector4 border, bool refresh = false)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite && !refresh)
            {
                return sprite;
            }

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("UI artwork missing: " + texturePath);
            }

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var bounds = new Rect(0, 0, texture.width, texture.height);
            switch (crop)
            {
                case UiArtworkCrop.Alpha:
                    bounds = OpaqueBounds(texture);
                    break;
                case UiArtworkCrop.Full:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(crop));
            }

            var generated = Sprite.Create(texture, bounds, new Vector2(.5f, .5f), 100f,
                0, SpriteMeshType.FullRect, border);
            generated.name = name;
            if (sprite)
            {
                EditorUtility.CopySerialized(generated, sprite);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(sprite);
                AssetDatabase.SaveAssetIfDirty(sprite);
                return sprite;
            }

            sprite = generated;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Rect OpaqueBounds(Texture2D texture)
        {
            int left = texture.width;
            int bottom = texture.height;
            int right = 0;
            int top = 0;
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 32)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y);
                    top = Math.Max(top, y);
                }
            }

            if (right < left || top < bottom)
            {
                throw new InvalidOperationException("UI artwork is empty: " + texture.name);
            }

            return new Rect(left, bottom, right - left + 1, top - bottom + 1);
        }
    }
}
