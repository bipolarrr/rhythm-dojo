using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class IngamePresentationAssets
    {
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
