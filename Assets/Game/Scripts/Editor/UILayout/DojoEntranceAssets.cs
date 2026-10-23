using System;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class DojoEntranceAssets
    {
        public const string Root = "Assets/Game/Art/DojoEntrance/";

        public static Sprite Load(string name)
        {
            string path = Root + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }

            string texturePath = Root + name + ".png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Dojo entrance artwork missing: " + texturePath);
            }

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var bounds = new Rect(0f, 0f, texture.width, texture.height);
            if (name != "EntranceFrame" && name != "DoorPanel")
            {
                bounds = OpaqueBounds(texture);
            }

            sprite = Sprite.Create(texture, bounds, new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
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
                throw new InvalidOperationException("Dojo artwork is empty: " + texture.name);
            }

            return new Rect(left, bottom, right - left + 1, top - bottom + 1);
        }
    }
}
