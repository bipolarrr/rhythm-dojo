using System;
using UnityEngine;
namespace RhythmDojo.Services
{
    public sealed class AudioSettingsService : IAudioSettingsService, IDisposable
    {
        private static readonly int[] bufferSizes = { 0, 32, 64, 128, 256, 512, 1024, 2048 };
        public int OptionCount => bufferSizes.Length;
        public int DefaultBufferSize { get; private set; }
        public int SelectedBufferOption { get; private set; }
        public string BufferStatus { get; private set; } = "Select a buffer size, then choose Apply.";
        public int RequestedBufferSize => SelectedBufferOption == 0 ? DefaultBufferSize : bufferSizes[SelectedBufferOption];
        public int GetBufferSize(int option) => bufferSizes[option];
        private readonly Func<bool> canApply;
        public event Action Changed;
        public AudioSettingsService(Func<bool> canApply)
        {
            this.canApply = canApply;
            DefaultBufferSize = AudioSettings.GetConfiguration().dspBufferSize;
            AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
        }
        public AudioState Actual
        {
            get
            {
                AudioSettings.GetDSPBufferSize(out int size, out int count);
                return new AudioState(size, count, AudioSettings.GetConfiguration().sampleRate);
            }
        }
        private void ConfigurationChanged(bool changed) => Changed?.Invoke();
        public void Dispose() => AudioSettings.OnAudioConfigurationChanged -= ConfigurationChanged;
        public void SelectBufferOption(int option)
        {
            if (!canApply()) return;
            if (option < 0 || option >= bufferSizes.Length) throw new ArgumentOutOfRangeException(nameof(option));
            SelectedBufferOption = option;
        }
        public bool ApplyBufferSize()
        {
            if (!canApply())
            {
                BufferStatus = "Audio buffer can only be applied in Settings before playing.";
                return false;
            }
            var previous = AudioSettings.GetConfiguration();
            var requested = previous; requested.dspBufferSize = RequestedBufferSize;
            string error;
            try
            {
                if (AudioSettings.Reset(requested))
                {
                    BufferStatus = $"Applied request: {RequestedBufferSize} samples. Actual: {AudioSettings.GetConfiguration().dspBufferSize} samples.";
                    return true;
                }
                error = "Audio reset failed.";
            }
            catch (Exception e) { error = "Audio reset failed: " + e.Message; }
            bool restored = false;
            try { restored = AudioSettings.Reset(previous); }
            catch (Exception e) { error += " Restore failed: " + e.Message; }
            BufferStatus = error + (restored ? " Previous configuration restore accepted." : " Previous configuration restore failed.") +
                $" Actual: {AudioSettings.GetConfiguration().dspBufferSize} samples.";
            return false;
        }
    }
}
