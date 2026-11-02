using System;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class SettingsArtAssets
    {
        public static Sprite Load(string name)
        {
            string root = DojoEntranceAssets.Root;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(root + name + ".asset");
            if (sprite)
            {
                return sprite;
            }

            string path = root + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Settings artwork missing: " + path);
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
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(128, 128, 128, 128));
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, root + name + ".asset");
            return sprite;
        }
    }
}
