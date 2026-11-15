using UnityEngine;

namespace RhythmDojo.EditorTools
{
    // Foley must come from the licensed offline recording recipe, never a silent synthesis fallback.
    public static class DojoPresentationAudio
    {
        public static AudioClip Load(bool wind)
        {
            string name = "TrainingFootstep";
            if (wind)
            {
                name = "SummitGust";
            }
            string path = "Assets/Game/Audio/" + name + ".wav";
            return SceneResources.Require<AudioClip>(path);
        }
    }
}
