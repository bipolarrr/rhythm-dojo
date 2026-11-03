using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Audio
{
    public sealed class SongClock : MonoBehaviour, ISongClock, IInputTimingClock, IPausableSongClock,
        IResumableSongClock, IPlaybackReadinessClock, IScheduledResumeClock
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
        private bool pausedAudioReset;
        private int pausedSamplePosition;
        private Func<bool> playbackReady;
        private double resumeDspTime;
        private double resumeRealtime;
        private double editorStartPosition;

        public void SetEditorStartPosition(double time)
        {
            if (!double.IsFinite(time) || time < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(time));
            }

            if (Running || ResumePending)
            {
                throw new InvalidOperationException("Stop playback before changing its start position.");
            }

            editorStartPosition = time;
        }


        public bool ResumePending { get; private set; }
        public double ResumeDspTime => resumeDspTime;

        public bool PlaybackReady => playbackReady?.Invoke() ?? true;

        public void SetPlaybackReadiness(Func<bool> playbackReady)
        {
            this.playbackReady = playbackReady;
        }

        public void ReplaceAudioClip(AudioClip clip)
        {
            if (Running)
            {
                throw new InvalidOperationException("Pause playback before replacing its audio.");
            }

            if (!clip)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            CancelResume();
            source.clip = clip;
            pausedAudioReset = true;
        }

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
            editorStartPosition = 0;
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
            if (!PlaybackReady)
            {
                throw new InvalidOperationException("Recover playback audio before starting.");
            }

            if (!initialized)
            {
                throw new InvalidOperationException("SongClock is not initialized.");
            }

            RefreshVolume();
            ResumePending = false;
            paused = false;
            source.Stop();
            double dsp = AudioSettings.dspTime;
            clockSampleRealtime = Time.realtimeSinceStartupAsDouble;
            clockSampleFrame = Time.frameCount;
            dspRealtimeOffset = dsp - clockSampleRealtime;
            double scheduledAudio = dsp + lead;
            ScheduledStartDspTime = scheduledAudio - editorStartPosition;
            int samplePosition = AudioResumeTiming.SamplePosition(
                editorStartPosition, 0, source.clip.frequency, source.clip.samples);
            if (samplePosition < source.clip.samples)
            {
                source.timeSamples = samplePosition;
                source.PlayScheduled(scheduledAudio);
            }
            Running = true;
        }

        public double InputTimeToSongTime(double eventTime)
        {
            double songTime = SongTime - (Time.realtimeSinceStartupAsDouble - eventTime);
            return InputTiming.Calibrate(songTime, InputTimingOffsetSeconds);
        }

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
            if (ResumePending)
            {
                CancelResume();
                return;
            }

            if (!Running)
            {
                return;
            }

            pausedSongTime = SongTime;
            paused = true;
            pausedAudioReset = false;

            if (source)
            {
                pausedSamplePosition = source.timeSamples;
                source.Pause();
            }

            Running = false;
        }

        public void Resume()
        {
            if (!PlaybackReady)
            {
                return;
            }

            if (!paused || !initialized)
            {
                return;
            }

            if (ResumePending)
            {
                return;
            }

            double dsp = AudioSettings.dspTime;
            clockSampleRealtime = Time.realtimeSinceStartupAsDouble;
            clockSampleFrame = Time.frameCount;
            dspRealtimeOffset = dsp - clockSampleRealtime;
            resumeDspTime = dsp + lead;
            resumeRealtime = clockSampleRealtime + lead;
            ScheduledStartDspTime = resumeDspTime + offset - pausedSongTime;
            RefreshVolume();
            source.Stop();
            pausedSamplePosition = AudioResumeTiming.SamplePosition(
                pausedSongTime, offset, source.clip.frequency, source.clip.samples);
            if (pausedSongTime < offset)
            {
                source.timeSamples = 0;
                source.PlayScheduled(ScheduledStartDspTime);
            }
            else if (pausedSamplePosition < source.clip.samples)
            {
                source.timeSamples = pausedSamplePosition;
                source.PlayScheduled(resumeDspTime);
            }

            ResumePending = true;
        }

        public bool IsInputAfterResume(double timestamp)
        {
            return timestamp >= resumeRealtime;
        }

        public bool TryCompleteResume()
        {
            if (!ResumePending)
            {
                return Running;
            }

            if (!PlaybackReady)
            {
                CancelResume();
                return false;
            }

            double dsp = AudioSettings.dspTime;
            if (dsp < resumeDspTime)
            {
                return false;
            }

            // A block update can cross the anchor before the old interpolation does.
            // Re-anchor here so the first resumed logical time never moves backwards.
            clockSampleRealtime = Time.realtimeSinceStartupAsDouble;
            clockSampleFrame = Time.frameCount;
            dspRealtimeOffset = dsp - clockSampleRealtime;
            ResumePending = false;
            pausedAudioReset = false;
            paused = false;
            Running = true;
            return true;
        }

        public void CancelResume()
        {
            if (!ResumePending)
            {
                return;
            }

            source.Stop();
            ResumePending = false;
        }

        private void Update()
        {
            if (ResumePending)
            {
                TryCompleteResume();
            }
        }

        public void Stop()
        {
            if (source)
            {
                source.Stop();
            }

            Running = false;
            ResumePending = false;
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
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            RhythmDojo.Services.PresentationPreferences.Changed += RefreshVolume;

            if (initialized)
            {
                RefreshVolume();
            }
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            RhythmDojo.Services.PresentationPreferences.Changed -= RefreshVolume;
            Stop();
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            if (Running && !PlaybackReady)
            {
                Pause();
            }

            if (paused)
            {
                CancelResume();
                pausedAudioReset = true;
            }
        }
    }
}
