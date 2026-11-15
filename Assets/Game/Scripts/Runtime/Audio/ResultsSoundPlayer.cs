using System;
using UnityEngine;
using RhythmDojo.Services;

namespace RhythmDojo.Audio
{
    // Results owns three authored voices: paper, wooden details, and the final stamp.
    // Stop/replace explicit voices so skipping never leaves hidden one-shot tails.
    public sealed class ResultsSoundPlayer : MonoBehaviour
    {
        public AudioSource[] voices;
        public AudioClip arrival;
        public AudioClip brush;
        public AudioClip[] counts;
        public AudioClip statistic;
        public AudioClip complete;
        public AudioClip failed;
        public AudioClip menu;
        private readonly float[] gains = new float[3];
        private int count;
        private double confirmedAt = -1;

        public int PlayedCount { get; private set; }
        public AudioClip LastClip { get; private set; }

        public void Validate()
        {
            if (voices == null || voices.Length != 3)
            {
                throw new InvalidOperationException("Results requires three authored audio voices.");
            }
            foreach (var voice in voices)
            {
                if (!voice)
                {
                    throw new InvalidOperationException("Results audio voice missing.");
                }
            }
            if (!arrival || !brush || !statistic)
            {
                throw new InvalidOperationException("Results reveal Foley missing.");
            }
            if (!complete || !failed || !menu)
            {
                throw new InvalidOperationException("Results stamp or menu Foley missing.");
            }
            if (counts == null || counts.Length != 3)
            {
                throw new InvalidOperationException("Results wooden count takes missing.");
            }
            foreach (var clip in counts)
            {
                if (!clip)
                {
                    throw new InvalidOperationException("Results wooden count take missing.");
                }
            }
        }

        public void Begin()
        {
            StopAll();
            count = 0;
            Play(2, arrival, .24f);
        }

        public void Brush() => Play(0, brush, .18f);

        public void Count()
        {
            Play(1, counts[count % counts.Length], .17f);
            count++;
        }

        public void Statistic() => Play(1, statistic, .15f);

        public void Stamp(bool failure)
        {
            var clip = complete;
            if (failure)
            {
                clip = failed;
            }
            Play(2, clip, .27f);
        }

        public void Menu(bool confirm)
        {
            if (!confirm && Time.unscaledTimeAsDouble - confirmedAt < .08)
            {
                return;
            }
            float gain = .075f;
            if (confirm)
            {
                gain = .18f;
                confirmedAt = Time.unscaledTimeAsDouble;
            }
            Play(1, menu, gain);
        }

        private void Play(int slot, AudioClip clip, float gain)
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }
            if (!isActiveAndEnabled || !RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                return;
            }
            var source = voices[slot];
            source.Stop();
            source.clip = clip;
            gains[slot] = gain;
            ApplyVolumes();
            if (source.volume <= 0)
            {
                return;
            }
            source.Play();
            LastClip = clip;
            PlayedCount++;
        }

        private void ApplyVolumes()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].volume = gains[i] * DojoSoundPlayer.EffectsVolume;
            }
        }

        public void StopAll()
        {
            if (voices == null)
            {
                return;
            }
            foreach (var source in voices)
            {
                if (source)
                {
                    source.Stop();
                }
            }
        }

        private void OnEnable() => PresentationPreferences.Changed += ApplyVolumes;

        private void Update()
        {
            if (!RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                StopAll();
            }
        }

        private void OnDisable()
        {
            PresentationPreferences.Changed -= ApplyVolumes;
            StopAll();
        }
    }
}
