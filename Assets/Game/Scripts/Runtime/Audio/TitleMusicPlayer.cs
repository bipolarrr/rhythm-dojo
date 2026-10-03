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
            source.volume = settings.Volume;
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

        private void OnEnable() { if (initialized) Restart(); }
        private void OnDisable() { if (source) source.Stop(); }
    }
}
