using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Audio
{
    public sealed class SongClock : MonoBehaviour, ISongClock
    {
        [SerializeField] private AudioSource source;
        private double offset, lead;
        private bool initialized;
        public double ScheduledStartDspTime { get; private set; }
        public bool Running { get; private set; }
        public double SongTime => Running ? AudioSettings.dspTime - ScheduledStartDspTime + offset : 0;
        public void Initialize(AudioClip clip, double chartOffset, AudioPlaybackSettings settings)
        {
            if (!source || !clip || !settings || !double.IsFinite(chartOffset))
                throw new InvalidOperationException("SongClock dependencies missing.");
            Stop(); settings.Validate(); source.clip = clip; source.volume = settings.Volume;
            source.playOnAwake = false; source.spatialBlend = 0;
            offset = chartOffset; lead = settings.ScheduleLeadTime; initialized = true;
        }
        public void Validate()
        { if (!source) throw new InvalidOperationException("SongClock needs an AudioSource."); }
        public void Schedule()
        {
            if (!initialized) throw new InvalidOperationException("SongClock is not initialized.");
            source.Stop(); ScheduledStartDspTime = AudioSettings.dspTime + lead;
            source.PlayScheduled(ScheduledStartDspTime); Running = true;
        }
        public double InputTimeToSongTime(double eventTime) =>
            SongTime - (Time.realtimeSinceStartupAsDouble - eventTime);
        public void Stop() { if (source) source.Stop(); Running = false; }
        private void OnDisable() => Stop();
    }
}
