using System;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class TitleMusicPlayer : MonoBehaviour, IMainMenuBeatClock
    {
        private TitleMusicSettings settings;
        [SerializeField]
        private AudioSource source;
        private double startDspTime;
        private bool initialized;
        private bool restartRequested;
        private bool resumeRequested;
        private int resumeSample;

        public double BeatPosition => settings.BeatPositionAt(AudioSettings.dspTime - startDspTime);

        public void SetCalibrationMuted(bool muted)
        {
            if (source)
            {
                source.mute = muted;
            }
        }

        public void CopyPlaybackFrom(TitleMusicPlayer previous)
        {
            if (!previous || !previous.initialized || previous.source.clip != source.clip)
            {
                return;
            }
            startDspTime = previous.startDspTime;
            resumeSample = previous.source.timeSamples;
            resumeRequested = true;
        }

        public void Initialize(TitleMusicSettings musicSettings)
        {
            if (!musicSettings)
            {
                throw new ArgumentNullException(nameof(musicSettings));
            }

            musicSettings.Validate();
            settings = musicSettings;

            if (!source)
            {
                source = GetComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.volume =
                settings.Volume
                * (
                    PresentationPreferences.Current ?? PresentationPreferences.Load()
                ).backgroundVolume;
            source.clip = settings.MusicClip;
            initialized = true;

            if (isActiveAndEnabled)
            {
                Restart();
            }
        }

        private void Restart()
        {
            source.Stop();

            if (settings.MusicClip)
            {
                startDspTime = AudioSettings.dspTime + .15d;
            }
            else
            {
                startDspTime = AudioSettings.dspTime + 0d;
            }

            if (settings.MusicClip)
            {
                source.PlayScheduled(startDspTime);
            }
        }

        private void RefreshVolume()
        {
            if (source && settings)
            {
                source.volume = settings.Volume
                    * (PresentationPreferences.Current ?? PresentationPreferences.Load()).backgroundVolume;
            }
        }

        private void OnEnable()
        {
            PresentationPreferences.Changed += RefreshVolume;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;

            if (initialized)
            {
                RefreshVolume();
                if (resumeRequested)
                {
                    resumeRequested = false;
                    if (source.clip)
                    {
                        source.timeSamples = resumeSample;
                        source.Play();
                    }
                }
                else
                {
                    Restart();
                }
            }
        }

        private void OnDisable()
        {
            restartRequested = false;
            PresentationPreferences.Changed -= RefreshVolume;
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;

            if (source)
            {
                source.Stop();
            }
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!isActiveAndEnabled || !initialized)
            {
                return;
            }
            if (source && settings)
            {
                // Reset may still be stopping sources while this event is raised.
                restartRequested = true;
            }
        }

        private void LateUpdate()
        {
            if (!restartRequested)
            {
                return;
            }

            restartRequested = false;
            RefreshVolume();
            Restart();
        }
    }
}
