using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public sealed class DojoAudioImporter : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            bool recorded = assetPath.StartsWith("Assets/Game/Audio/Foley/");
            recorded |= assetPath.StartsWith("Assets/Game/Audio/Gameplay/");
            recorded |= assetPath == "Assets/Game/Resources/UI/DojoClick.wav";
            if (!recorded)
            {
                switch (assetPath)
                {
                    case "Assets/Game/Audio/DojoKnock.wav":
                    case "Assets/Game/Audio/DojoBoardCrack.wav":
                    case "Assets/Game/Audio/DojoDoorSlide.wav":
                    case "Assets/Game/Audio/TrainingFootstep.wav":
                    case "Assets/Game/Audio/SummitGust.wav":
                        recorded = true;
                        break;
                }
            }
            if (!recorded)
            {
                return;
            }
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            // Offline masters are already mono; preserve their authored peak levels.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            // Web uses predecoded Web Audio buffers. Avoid compressed streaming on the hit path.
            importer.SetOverrideSampleSettings("WebGL", settings);
        }
    }
}
