using System;
using UnityEngine;
using RhythmDojo.Services;

namespace RhythmDojo.Audio
{
    public sealed class TitleMusicPlayer : MonoBehaviour, IMainMenuBeatClock
    {
        private TitleMusicSettings settings;
        private AudioSource source;
        private double startDspTime;
        private bool initialized;

        public double BeatPosition => settings.BeatPositionAt(AudioSettings.dspTime - startDspTime);

        public void Initialize(TitleMusicSettings musicSettings)
        {
            if (!musicSettings) throw new ArgumentNullException(nameof(musicSettings));
            musicSettings.Validate();
            settings = musicSettings;
            if (!source) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.volume = settings.Volume * (PresentationPreferences.Current ?? PresentationPreferences.Load()).backgroundVolume;
            source.clip = settings.MusicClip;
            initialized = true;
            if (isActiveAndEnabled) Restart();
        }

        private void Restart()
        {
            source.Stop();
            startDspTime = AudioSettings.dspTime + (settings.MusicClip ? .15d : 0d);
            if (settings.MusicClip) source.PlayScheduled(startDspTime);
        }

        private void RefreshVolume() { if (source && settings) source.volume = settings.Volume * PresentationPreferences.Current.backgroundVolume; }
        private void OnEnable() { PresentationPreferences.Changed += RefreshVolume; if (initialized) { RefreshVolume(); Restart(); } }
        private void OnDisable() { PresentationPreferences.Changed -= RefreshVolume; if (source) source.Stop(); }
    }
}
