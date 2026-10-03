using System;
using UnityEngine;

namespace RhythmDojo.Audio
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Title Music Settings")]
    public sealed class TitleMusicSettings : ScriptableObject
    {
        [Tooltip("Optional title music. Leave empty to bounce without music.")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Min(1f)] private float beatsPerMinute = 120f;
        [Tooltip("Time of the first beat in the clip, in seconds.")]
        [SerializeField, Min(0f)] private float firstBeatSeconds;
        [SerializeField, Range(0f, 1f)] private float volume = .7f;

        public AudioClip MusicClip => musicClip;
        public float Volume => volume;

        public void Validate()
        {
            if (!float.IsFinite(beatsPerMinute) || beatsPerMinute < 1f ||
                !float.IsFinite(firstBeatSeconds) || firstBeatSeconds < 0f ||
                !float.IsFinite(volume) || volume < 0f || volume > 1f)
                throw new InvalidOperationException("Invalid title music BPM, first beat or volume.");
            if (musicClip && (musicClip.samples <= 0 || musicClip.frequency <= 0 ||
                firstBeatSeconds >= (double)musicClip.samples / musicClip.frequency))
                throw new InvalidOperationException("Title music first beat must be inside the clip.");
        }

        public double BeatPositionAt(double elapsedSeconds)
        {
            // Restart the beat phase at each audio loop, including clips with an intro.
            double position = elapsedSeconds;
            if (musicClip && elapsedSeconds >= 0)
                position %= (double)musicClip.samples / musicClip.frequency;
            return (position - (musicClip ? firstBeatSeconds : 0f)) * beatsPerMinute / 60d;
        }
    }
}
