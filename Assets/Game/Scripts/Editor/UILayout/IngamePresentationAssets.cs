using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class IngamePresentationAssets
    {
        public static Sprite Background()
        {
            const string path = "Assets/Game/Art/DojoGameplay/TrainingHall.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return SceneResources.Require<Sprite>(path);
        }

        public static void PrepareAudio()
        {
            foreach (string name in new[] { "StrikeWhoosh", "StrikeImpact" })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath("Assets/Game/Audio/Gameplay/" + name + ".wav");
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }
    }
}
