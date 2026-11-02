using System;
using UnityEngine;

namespace RhythmDojo.Services
{
    public sealed class AudioSettingsService : IAudioSettingsService, IAudioBufferValidation, IDisposable
    {
        private static readonly int[] bufferSizes = { 0, 32, 64, 128, 256, 512, 1024, 2048 };

        public int OptionCount => bufferSizes.Length;

        public int DefaultBufferSize { get; private set; }
        public int SelectedBufferOption { get; private set; }
        public string BufferStatus { get; private set; } =
            "Select a buffer size, then choose Apply.";

        public int RequestedBufferSize
        {
            get
            {
                if (SelectedBufferOption == 0)
                {
                    return DefaultBufferSize;
                }
                else
                {
                    return bufferSizes[SelectedBufferOption];
                }
            }
        }

        public int GetBufferSize(int option) => bufferSizes[option];

        private readonly Func<bool> canApply;
        private readonly IAudioConfigurationBackend backend;
        private readonly bool ownsBackend;
        private AudioConfiguration previousConfiguration;
        private AudioConfiguration checkedConfiguration;
        private AudioConfiguration? lastHealthyConfiguration;
        private double warmupEnds;
        private double probeStartRealtime = -1;
        private double probeStartDsp;
        private int validationRequestedBuffer;
        private bool recoveryOccurred;
        private bool disposed;

        public bool IsBufferValidationPending { get; private set; }
        public bool HasBufferValidationResult { get; private set; }
        public double LastDspRealtimeRatio { get; private set; } = double.NaN;
        public double LastRejectedDspRealtimeRatio { get; private set; } = double.NaN;

        public event Action Changed;

        public AudioSettingsService(Func<bool> canApply)
            : this(canApply, new UnityAudioConfigurationBackend())
        {
            ownsBackend = true;
        }

        public AudioSettingsService(Func<bool> canApply, IAudioConfigurationBackend backend)
        {
            this.canApply = canApply ?? throw new ArgumentNullException(nameof(canApply));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            DefaultBufferSize = backend.Configuration.dspBufferSize;
            backend.Changed += ConfigurationChanged;
        }

        public AudioState Actual
        {
            get
            {
                return backend.Actual;
            }
        }

        private void ConfigurationChanged(bool changed) => Changed?.Invoke();

        public void Dispose()
        {
            disposed = true;
            IsBufferValidationPending = false;
            backend.Changed -= ConfigurationChanged;
            if (ownsBackend && backend is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public void SelectBufferOption(int option)
        {
            if (disposed || IsBufferValidationPending)
            {
                return;
            }
            if (!canApply())
            {
                return;
            }

            if (option < 0 || option >= bufferSizes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(option));
            }

            SelectedBufferOption = option;
        }

        public bool ApplyBufferSize()
        {
            if (disposed || !canApply())
            {
                BufferStatus = "오디오 버퍼는 설정창에서만 변경할 수 있습니다.";

                return false;
            }

            if (IsBufferValidationPending)
            {
                return false;
            }

            var previous = backend.Configuration;
            var requested = previous;
            requested.dspBufferSize = RequestedBufferSize;
            previousConfiguration = previous;
            validationRequestedBuffer = RequestedBufferSize;
            recoveryOccurred = false;
            LastRejectedDspRealtimeRatio = double.NaN;

            string error;

            try
            {
                if (backend.Reset(requested))
                {
                    BeginValidation();
                    return true;
                }

                error = "Audio reset failed.";
            }
            catch (Exception e)
            {
                error = "Audio reset failed: " + e.Message;
            }

            bool restored = false;

            try
            {
                restored = backend.Reset(previous);
            }
            catch (Exception e)
            {
                error += " Restore failed: " + e.Message;
            }

            if (restored)
            {
                BufferStatus =
                    error
                    + " Previous configuration restore accepted."
                    + $" Actual: {backend.Configuration.dspBufferSize} samples.";
            }
            else
            {
                BufferStatus =
                    error
                    + " Previous configuration restore failed."
                    + $" Actual: {backend.Configuration.dspBufferSize} samples.";
            }

            HasBufferValidationResult = true;
            SyncAppliedOption();
            Changed?.Invoke();
            return false;
        }

        private void BeginValidation()
        {
            checkedConfiguration = backend.Configuration;
            warmupEnds = backend.Realtime + .3;
            probeStartRealtime = -1;
            IsBufferValidationPending = true;
            HasBufferValidationResult = false;
            LastDspRealtimeRatio = double.NaN;
            BufferStatus = $"오디오 안정성 확인 중… Actual: {Actual.BufferSize} samples";
            Changed?.Invoke();
        }

        public void Tick()
        {
            if (disposed || !IsBufferValidationPending)
            {
                return;
            }
            if (!canApply())
            {
                IsBufferValidationPending = false;
                HasBufferValidationResult = true;
                BufferStatus = "오디오 확인이 중단되었습니다. 설정에서 다시 적용해 주세요.";
                Changed?.Invoke();
                return;
            }

            double realtime = backend.Realtime;
            if (backend.Paused)
            {
                warmupEnds = realtime + .3;
                probeStartRealtime = -1;
                return;
            }
            if (realtime < warmupEnds)
            {
                return;
            }
            if (probeStartRealtime < 0)
            {
                probeStartRealtime = realtime;
                probeStartDsp = backend.DspTime;
                return;
            }

            double elapsed = realtime - probeStartRealtime;
            double dspElapsed = backend.DspTime - probeStartDsp;
            if (dspElapsed < 0 || backend.Configuration.dspBufferSize != checkedConfiguration.dspBufferSize)
            {
                BeginValidation();
                return;
            }
            if (elapsed < 1)
            {
                return;
            }

            LastDspRealtimeRatio = dspElapsed / elapsed;
            if (LastDspRealtimeRatio >= .85 && LastDspRealtimeRatio <= 1.15)
            {
                lastHealthyConfiguration = backend.Configuration;
                IsBufferValidationPending = false;
                HasBufferValidationResult = true;
                if (recoveryOccurred || Actual.BufferSize != validationRequestedBuffer)
                {
                    SyncAppliedOption();
                }
                if (recoveryOccurred)
                {
                    BufferStatus = $"요청 {validationRequestedBuffer} 불안정 → Actual: {Actual.BufferSize} samples로 복구";
                }
                else
                {
                    BufferStatus = $"오디오 적용 완료 · Actual: {Actual.BufferSize} samples";
                }
                Changed?.Invoke();
                return;
            }

            LastRejectedDspRealtimeRatio = LastDspRealtimeRatio;
            int next = Math.Max(256, checkedConfiguration.dspBufferSize * 2);
            if (next <= 2048)
            {
                var requested = backend.Configuration;
                requested.dspBufferSize = next;
                try
                {
                    if (backend.Reset(requested) && backend.Configuration.dspBufferSize > checkedConfiguration.dspBufferSize)
                    {
                        recoveryOccurred = true;
                        BeginValidation();
                        return;
                    }
                }
                catch (Exception)
                {
                    // Restore below; a failed recovery must never be reported as successful.
                }
            }
            RestoreConfiguration("안정적인 오디오 설정을 확인하지 못했습니다.");
        }

        public void CancelBufferValidation()
        {
            if (IsBufferValidationPending)
            {
                RestoreConfiguration("오디오 확인을 취소했습니다.");
            }
        }

        private void RestoreConfiguration(string reason)
        {
            IsBufferValidationPending = false;
            HasBufferValidationResult = true;
            var previous = previousConfiguration;
            if (lastHealthyConfiguration.HasValue)
            {
                previous = lastHealthyConfiguration.Value;
            }
            else
            {
                previous.dspBufferSize = Math.Max(256, previous.dspBufferSize);
            }
            bool restored = false;
            try
            {
                restored = backend.Reset(previous);
            }
            catch (Exception)
            {
                // Keep the actual state visible if the device is unavailable.
            }
            BufferStatus = reason + $" Actual: {Actual.BufferSize} samples";
            if (!restored)
            {
                BufferStatus += " · 이전 설정 복원 실패";
            }
            SyncAppliedOption();
            Changed?.Invoke();
        }

        private void SyncAppliedOption()
        {
            if (Actual.BufferSize == DefaultBufferSize)
            {
                SelectedBufferOption = 0;
                return;
            }
            int option = Array.IndexOf(bufferSizes, Actual.BufferSize);
            if (option >= 1)
            {
                SelectedBufferOption = option;
            }
        }
    }
}
