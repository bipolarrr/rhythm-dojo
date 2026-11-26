using System;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TitleMusicPlayer : MonoBehaviour, IMainMenuBeatClock
    {
        private const double FadeSeconds = 1d;
        private TitleMusicSettings settings;
        [SerializeField] private AudioSource source;
        private double startDspTime;
        private bool initialized;
        private bool restartRequested;
        private int resumeSample;
        private bool menuActive;
        private bool previewActive;
        private bool calibrationMuted;
        private bool playing;
        private float fade;

        public double BeatPosition
        {
            get
            {
                if (settings.MusicClip)
                {
                    int sample = resumeSample;
                    if (playing && source.isPlaying)
                    {
                        sample = source.timeSamples;
                    }
                    return settings.BeatPositionAt((double)sample / settings.MusicClip.frequency);
                }
                return settings.BeatPositionAt(AudioSettings.dspTime - startDspTime);
            }
        }

        public void SetMenuActive(bool active)
        {
            menuActive = active;
            UpdatePlayback();
        }

        public void SetPreviewActive(bool active)
        {
            previewActive = active;
            UpdatePlayback();
        }

        public void SetCalibrationMuted(bool muted)
        {
            calibrationMuted = muted;
            UpdatePlayback();
        }

        public void CopyPlaybackFrom(TitleMusicPlayer previous)
        {
            if (!previous || !previous.initialized || previous.source.clip != source.clip)
            {
                return;
            }

            PausePlayback();
            resumeSample = previous.resumeSample;
            if (previous.source.isPlaying)
            {
                resumeSample = previous.source.timeSamples;
            }
            UpdatePlayback();
        }

        public void Initialize(TitleMusicSettings musicSettings, bool active = true)
        {
            if (!musicSettings)
            {
                throw new ArgumentNullException(nameof(musicSettings));
            }
            musicSettings.Validate();
            if (!source)
            {
                source = GetComponent<AudioSource>();
            }

            PausePlayback();
            settings = musicSettings;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.clip = settings.MusicClip;
            resumeSample = 0;
            startDspTime = AudioSettings.dspTime;
            menuActive = active;
            initialized = true;
            UpdatePlayback();
        }

        private void UpdatePlayback()
        {
            if (!initialized || !isActiveAndEnabled)
            {
                return;
            }

            if (!menuActive || previewActive || calibrationMuted)
            {
                PausePlayback();
                return;
            }

            if (playing || !settings.MusicClip)
            {
                return;
            }

            source.timeSamples = resumeSample;
            startDspTime = AudioSettings.dspTime;
            fade = 0f;
            RefreshVolume();
            source.Play();
            playing = true;
        }

        private void PausePlayback()
        {
            if (!playing)
            {
                return;
            }

            if (source.isPlaying)
            {
                resumeSample = source.timeSamples;
            }
            source.Stop();
            playing = false;
        }

        private void RefreshVolume()
        {
            if (source && settings)
            {
                var presentation = PresentationPreferences.Current ?? PresentationPreferences.Load();
                source.volume = settings.Volume * presentation.backgroundVolume * fade;
            }
        }

        private void OnEnable()
        {
            PresentationPreferences.Changed += RefreshVolume;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            UpdatePlayback();
        }

        private void OnDisable()
        {
            PresentationPreferences.Changed -= RefreshVolume;
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            restartRequested = false;
            PausePlayback();
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            // Unity can invalidate the source before this callback. Keep the last observed sample.
            restartRequested = playing;
        }

        private void LateUpdate()
        {
            if (restartRequested)
            {
                restartRequested = false;
                source.Stop();
                playing = false;
                UpdatePlayback();
            }

            if (!playing || !source.isPlaying)
            {
                return;
            }

            resumeSample = source.timeSamples;
            if (fade < 1f)
            {
                fade = Mathf.Clamp01((float)((AudioSettings.dspTime - startDspTime) / FadeSeconds));
                RefreshVolume();
            }
        }
    }
}
