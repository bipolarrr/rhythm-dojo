using System;
using UnityEngine;

namespace RhythmDojo.Audio
{
    public enum DojoSoundCue
    {
        Swing, KickSwing, Perfect, Good, DummyHit, Footstep, Creak, Transition, Complete, Failed
    }

    // Immutable recorded assets are shared. Selection history belongs to each player.
    public sealed class DojoSoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Sound
        {
            public DojoSoundCue cue;
            public AudioClip[] clips;
            [Range(0, 1)] public float gain = .25f;
            [Range(0, .1f)] public float pitchVariation = .025f;
        }

        public Sound[] sounds;
        public AudioClip ambience;

        public void Validate()
        {
            if (sounds == null || sounds.Length != Enum.GetValues(typeof(DojoSoundCue)).Length)
            {
                throw new InvalidOperationException("Recorded dojo sound bank is incomplete.");
            }
            for (int i = 0; i < sounds.Length; i++)
            {
                var sound = sounds[i];
                if (sound == null || (int)sound.cue != i)
                {
                    throw new InvalidOperationException("Recorded dojo cue order is invalid.");
                }
                if (sound.clips == null || sound.clips.Length == 0)
                {
                    throw new InvalidOperationException("Recorded dojo cue has no takes: " + sound.cue);
                }
                foreach (var clip in sound.clips)
                {
                    if (!clip)
                    {
                        throw new InvalidOperationException("Recorded dojo take is missing: " + sound.cue);
                    }
                }
            }
            if (!ambience)
            {
                throw new InvalidOperationException("Recorded courtyard ambience is missing.");
            }
        }
    }
}
