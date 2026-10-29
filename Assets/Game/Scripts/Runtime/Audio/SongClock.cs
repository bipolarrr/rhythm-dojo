using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Audio
{
    public sealed class SongClock : MonoBehaviour, ISongClock, IInputTimingClock, IPausableSongClock
    {
        [SerializeField]
        private AudioSource source;
        private double offset,
            lead;
        private bool initialized;
        private bool paused;
        private double pausedSongTime;
        private float playbackVolume;
        private double dspRealtimeOffset;
        private double clockSampleRealtime;
        private int clockSampleFrame = -1;

        public double ScheduledStartDspTime { get; private set; }
        public bool Running { get; private set; }
        public double InputTimingOffsetSeconds { get; private set; }

        public double SongTime
        {
            get
            {
                if (Running)
                {
                    return InterpolatedDspTime() - ScheduledStartDspTime + offset;
                }
                else if (paused)
                {
                    return pausedSongTime;
                }
                else
                {
                    return 0;
                }
            }
        }

        public void Initialize(
            AudioClip clip,
            double chartOffset,
            AudioPlaybackSettings settings,
            int timingOffsetMs = 0
        )
        {
            if ((!source || !clip) || !settings)
            {
                throw new InvalidOperationException("SongClock dependencies missing.");
            }

            if (!double.IsFinite(chartOffset))
            {
                throw new InvalidOperationException("SongClock dependencies missing.");
            }

            Stop();
            settings.Validate();
            source.clip = clip;
            playbackVolume = settings.Volume;
            RefreshVolume();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            offset = chartOffset;
            InputTimingOffsetSeconds = Mathf.Clamp(timingOffsetMs, -200, 200) / 1000.0;
            lead = settings.ScheduleLeadTime;
            initialized = true;
        }

        public void Validate()
        {
            if (!source)
            {
                throw new InvalidOperationException("SongClock needs an AudioSource.");
            }
        }

        public void Schedule()
        {
            if (!initialized)
            {
                throw new InvalidOperationException("SongClock is not initialized.");
            }

            RefreshVolume();
            paused = false;
            source.Stop();
            double dsp = AudioSettings.dspTime;
            clockSampleRealtime = Time.realtimeSinceStartupAsDouble;
            clockSampleFrame = Time.frameCount;
            dspRealtimeOffset = dsp - clockSampleRealtime;
            ScheduledStartDspTime = dsp + lead;
            source.PlayScheduled(ScheduledStartDspTime);
            Running = true;
        }

        public double InputTimeToSongTime(double eventTime) =>
            SongTime - (Time.realtimeSinceStartupAsDouble - eventTime) - InputTimingOffsetSeconds;

        private double InterpolatedDspTime()
        {
            double realtime = Time.realtimeSinceStartupAsDouble;

            if (clockSampleFrame != Time.frameCount)
            {
                // DSP time advances in audio blocks. Follow its long-term rate without
                // passing block-sized jumps into presentation or timestamped judgments.
                double elapsed = Math.Max(0, realtime - clockSampleRealtime);
                double target = AudioSettings.dspTime - realtime;
                dspRealtimeOffset += (target - dspRealtimeOffset) * (1 - Math.Exp(-elapsed));
                clockSampleRealtime = realtime;
                clockSampleFrame = Time.frameCount;
            }

            return realtime + dspRealtimeOffset;
        }

        public void Pause()
        {
            if (!Running)
            {
                return;
            }

            pausedSongTime = SongTime;
            paused = true;

            if (source)
            {
                source.Pause();
            }

            Running = false;
        }

        public void Stop()
        {
            if (source)
            {
                source.Stop();
            }

            Running = false;
            paused = false;
            pausedSongTime = 0;
        }

        private void RefreshVolume()
        {
            if (source)
            {
                source.volume =
                    playbackVolume
                    * (
                        RhythmDojo.Services.PresentationPreferences.Current
                        ?? RhythmDojo.Services.PresentationPreferences.Load()
                    ).gameplayVolume;
            }
        }

        private void OnEnable()
        {
            RhythmDojo.Services.PresentationPreferences.Changed += RefreshVolume;

            if (initialized)
            {
                RefreshVolume();
            }
        }

        private void OnDisable()
        {
            RhythmDojo.Services.PresentationPreferences.Changed -= RefreshVolume;
            Stop();
        }
    }
}
