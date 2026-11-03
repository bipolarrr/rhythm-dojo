using System;
using UnityEngine;

namespace RhythmDojo.Services
{
    public interface IAudioConfigurationBackend
    {
        AudioConfiguration Configuration { get; }
        AudioState Actual { get; }
        double DspTime { get; }
        double Realtime { get; }
        bool Paused { get; }
        event Action<bool> Changed;
        bool Reset(AudioConfiguration configuration);
    }

    public interface IAudioBufferValidation
    {
        bool IsBufferValidationPending { get; }
        bool HasBufferValidationResult { get; }
    }

    internal sealed class UnityAudioConfigurationBackend : IAudioConfigurationBackend, IDisposable
    {
        public AudioConfiguration Configuration => AudioSettings.GetConfiguration();
        public double DspTime => AudioSettings.dspTime;
        public double Realtime => Time.realtimeSinceStartupAsDouble;
        public bool Paused => AudioListener.pause;
        public event Action<bool> Changed;

        public AudioState Actual
        {
            get
            {
                AudioSettings.GetDSPBufferSize(out int size, out int count);
                return new AudioState(size, count, Configuration.sampleRate);
            }
        }

        public UnityAudioConfigurationBackend()
        {
            AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
        }

        public bool Reset(AudioConfiguration configuration) => AudioSettings.Reset(configuration);

        private void ConfigurationChanged(bool changed) => Changed?.Invoke(changed);

        public void Dispose() => AudioSettings.OnAudioConfigurationChanged -= ConfigurationChanged;
    }
}
