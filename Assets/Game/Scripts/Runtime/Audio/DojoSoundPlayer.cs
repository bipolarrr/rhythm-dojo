using System;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Audio
{
    // Three contacts, two movements and one detail. No one-shot subvoices or runtime sources.
    public sealed class DojoSoundPlayer : MonoBehaviour
    {
        public DojoSoundBank bank;
        public AudioSource[] voices;
        public AudioSource ambience;
        public int PlayedCount { get; private set; }
        public DojoSoundCue LastCue { get; private set; }
        public AudioClip LastClip { get; private set; }
        public int ContactCount { get; private set; }
        public int MotionCount { get; private set; }
        public static float EffectsVolume
        {
            get
            {
                if (PresentationPreferences.Current != null)
                {
                    return PresentationPreferences.Current.gameplayVolume;
                }
                return 1f;
            }
        }

        private int[] lastTakes;
        private int[] lastFrames;
        private readonly double[] started = new double[6];
        private readonly float[] gains = new float[6];
        private uint variation = 19373;
        private float ambienceTarget;
        private float ambienceLevel;

        public void Validate()
        {
            if (!bank || voices == null)
            {
                throw new InvalidOperationException("Authored dojo audio references are missing.");
            }
            bank.Validate();
            if (voices.Length != 6 || !ambience)
            {
                throw new InvalidOperationException("Authored dojo audio pool requires six voices and ambience.");
            }
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i] || voices[i] == ambience)
                {
                    throw new InvalidOperationException("Authored dojo audio voice is missing or shared.");
                }
                for (int j = 0; j < i; j++)
                {
                    if (voices[i] == voices[j])
                    {
                        throw new InvalidOperationException("Authored dojo audio voices must be distinct.");
                    }
                }
            }
        }

        private void Awake()
        {
            Validate();
            lastTakes = new int[bank.sounds.Length];
            lastFrames = new int[bank.sounds.Length];
            for (int i = 0; i < lastTakes.Length; i++)
            {
                lastTakes[i] = -1;
                lastFrames[i] = -1;
            }
        }

        private void OnEnable()
        {
            PresentationPreferences.Changed += ApplyVolumes;
        }

        public bool Play(DojoSoundCue cue, float strength = 1f, float pan = 0f)
        {
            if (!isActiveAndEnabled || !RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                return false;
            }
            int index = (int)cue;
            if (lastFrames[index] == Time.frameCount || strength <= 0)
            {
                return false;
            }
            var preferences = PresentationPreferences.Current;
            if (preferences != null && preferences.gameplayVolume <= 0)
            {
                return false;
            }
            int first = 5;
            int count = 1;
            switch (cue)
            {
                case DojoSoundCue.Perfect:
                case DojoSoundCue.Good:
                case DojoSoundCue.DummyHit:
                    first = 0;
                    count = 3;
                    break;
                case DojoSoundCue.Swing:
                case DojoSoundCue.KickSwing:
                case DojoSoundCue.Footstep:
                    first = 3;
                    count = 2;
                    break;
            }

            double now = Time.unscaledTimeAsDouble;
            int slot = first;
            int active = 0;
            bool available = false;
            for (int i = first; i < first + count; i++)
            {
                if (!voices[i].isPlaying)
                {
                    if (!available)
                    {
                        slot = i;
                        available = true;
                    }
                    continue;
                }
                active++;
                if (!available && started[i] < started[slot])
                {
                    slot = i;
                }
            }
            if (!available && now - started[slot] < .028)
            {
                return false;
            }

            var sound = bank.sounds[index];
            int take = (int)(NextVariation() * sound.clips.Length) % sound.clips.Length;
            if (sound.clips.Length > 1 && take == lastTakes[index])
            {
                take = (take + 1) % sound.clips.Length;
            }
            var source = voices[slot];
            source.Stop();
            source.clip = sound.clips[take];
            source.pitch = 1f + (NextVariation() * 2 - 1) * sound.pitchVariation;
            source.panStereo = Mathf.Clamp(pan, -.35f, .35f);
            // Reduce energy during dense patterns; a new attack never changes another voice's pitch.
            gains[slot] = sound.gain * Mathf.Clamp01(strength) * Mathf.Lerp(.94f, 1f, NextVariation());
            gains[slot] /= Mathf.Sqrt(1f + active * .65f);
            started[slot] = now;
            ApplyVolume(slot);
            source.Play();
            lastFrames[index] = Time.frameCount;
            lastTakes[index] = take;
            LastCue = cue;
            LastClip = source.clip;
            PlayedCount++;
            if (first == 0)
            {
                ContactCount++;
            }
            if (first == 3)
            {
                MotionCount++;
            }
            return true;
        }

        public void SetAmbience(float level)
        {
            ambienceTarget = Mathf.Clamp(level, 0, .06f);
        }

        private float NextVariation()
        {
            variation = unchecked(variation * 1664525 + 1013904223);
            return (variation >> 8) / 16777216f;
        }

        private void ApplyVolume(int index)
        {
            float volume = 1f;
            if (PresentationPreferences.Current != null)
            {
                volume = PresentationPreferences.Current.gameplayVolume;
            }
            voices[index].volume = gains[index] * volume;
        }

        private void ApplyVolumes()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                ApplyVolume(i);
            }
        }

        private void Update()
        {
            if (!RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                StopAll();
                return;
            }
            ambienceLevel = Mathf.MoveTowards(ambienceLevel, ambienceTarget, Time.unscaledDeltaTime * .1f);
            float volume = 1f;
            if (PresentationPreferences.Current != null)
            {
                volume = PresentationPreferences.Current.gameplayVolume;
            }
            ambience.volume = ambienceLevel * volume;
            if (ambience.volume > .0001f)
            {
                if (!ambience.isPlaying)
                {
                    ambience.Play();
                }
            }
            else
            {
                ambience.Stop();
            }
        }

        public void StopAll()
        {
            if (voices != null)
            {
                foreach (var source in voices)
                {
                    if (source)
                    {
                        source.Stop();
                    }
                }
            }
            if (ambience)
            {
                ambience.Stop();
            }
            ambienceTarget = ambienceLevel = 0;
        }

        private void OnDisable()
        {
            PresentationPreferences.Changed -= ApplyVolumes;
            StopAll();
        }
    }
}
