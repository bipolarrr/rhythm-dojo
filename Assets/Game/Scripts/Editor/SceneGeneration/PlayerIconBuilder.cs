using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class PlayerIconBuilder
    {
        public static void Prepare()
        {
            const string sourcePath = "Assets/Game/Art/PixelDojo/PlayerIconText.png";
            const string iconPath = "Assets/Game/Art/PixelDojo/PlayerIcon.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D icon = null;

            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                {
                    throw new InvalidOperationException("Cannot read the title logo.");
                }

                // Pad the original pixels to a square so platform icon resizing preserves their aspect ratio.
                int size = Math.Max(source.width, source.height);
                icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
                icon.SetPixels32(new Color32[size * size]);
                icon.SetPixels((size - source.width) / 2, (size - source.height) / 2,
                    source.width, source.height, source.GetPixels());
                icon.Apply();
                File.WriteAllBytes(iconPath, icon.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (icon)
                {
                    UnityEngine.Object.DestroyImmediate(icon);
                }
            }

            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { texture }, IconKind.Any);
            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone,
                sizes.Select(size => texture).ToArray(), IconKind.Any);
            AssetDatabase.SaveAssets();
        }
    }
}
