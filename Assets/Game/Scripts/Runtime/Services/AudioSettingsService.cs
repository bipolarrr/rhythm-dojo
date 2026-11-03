using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RhythmDojo.Application;
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
        private double validationStartedRealtime;
        private double lastProbeDsp;
        private bool resettingConfiguration;
        private readonly List<DspClockRateMeasurement> clockRateMeasurements =
            new List<DspClockRateMeasurement>();
        private readonly ReadOnlyCollection<DspClockRateMeasurement> clockRateMeasurementView;

        public const double ClockValidationTimeoutSeconds = 20;
        public bool ClockRateVerified { get; private set; }
        public int ConsecutiveClockRateMeasurements { get; private set; }
        public DspClockRateMeasurement? LastClockRateMeasurement { get; private set; }
        public IReadOnlyList<DspClockRateMeasurement> ClockRateMeasurements => clockRateMeasurementView;
        public double ValidationElapsedSeconds { get; private set; }

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
            clockRateMeasurementView = clockRateMeasurements.AsReadOnly();
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

        private void ConfigurationChanged(bool changed)
        {
            ClockRateVerified = false;
            if (IsBufferValidationPending && !resettingConfiguration)
            {
                BeginValidation();
            }
            else if (!resettingConfiguration)
            {
                ConsecutiveClockRateMeasurements = 0;
                HasBufferValidationResult = false;
                BufferStatus = "오디오 설정 변경으로 DSP 시계 진행률 확인이 무효화되었습니다. 다시 적용해 주세요.";
            }
            Changed?.Invoke();
        }

        public void Dispose()
        {
            disposed = true;
            IsBufferValidationPending = false;
            ClockRateVerified = false;
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
            ClockRateVerified = false;
            validationStartedRealtime = backend.Realtime;
            ValidationElapsedSeconds = 0;
            clockRateMeasurements.Clear();

            string error;

            try
            {
                if (ResetConfiguration(requested))
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
                restored = ResetConfiguration(previous);
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
            ConsecutiveClockRateMeasurements = 0;
            LastClockRateMeasurement = null;
            IsBufferValidationPending = true;
            HasBufferValidationResult = false;
            LastDspRealtimeRatio = double.NaN;
            BufferStatus = $"DSP 시계 진행률 확인 중 (±1%, 최대 20초)… Actual: {Actual.BufferSize} samples";
            Changed?.Invoke();
        }

        public void Tick()
        {
            if (disposed || !IsBufferValidationPending)
            {
                return;
            }

            double realtime = backend.Realtime;
            ValidationElapsedSeconds = realtime - validationStartedRealtime;
            if (ValidationElapsedSeconds >= ClockValidationTimeoutSeconds)
            {
                if (LastClockRateMeasurement.HasValue)
                {
                    clockRateMeasurements.Add(LastClockRateMeasurement.Value);
                }
                RestoreConfiguration("DSP 시계 진행률 확인 시간 초과 (20초). 정상 여부를 확인하지 못했습니다.");
                return;
            }
            if (ValidationElapsedSeconds < 0 || !double.IsFinite(realtime))
            {
                RestoreConfiguration("DSP 시계 진행률 확인 실패: 기준 시각이 변경되었습니다.");
                return;
            }
            if (!canApply())
            {
                IsBufferValidationPending = false;
                HasBufferValidationResult = true;
                BufferStatus = "DSP 시계 진행률 확인이 중단되었습니다. 설정에서 다시 적용해 주세요.";
                Changed?.Invoke();
                return;
            }

            if (backend.Paused)
            {
                BeginValidation();
                return;
            }
            if (!SameConfiguration(backend.Configuration, checkedConfiguration))
            {
                BeginValidation();
                return;
            }
            if (realtime < warmupEnds)
            {
                return;
            }

            double dsp = backend.DspTime;
            if (!double.IsFinite(dsp))
            {
                RestoreConfiguration("DSP 시계 진행률 확인 실패: 장치 시각을 읽을 수 없습니다.");
                return;
            }
            if (probeStartRealtime < 0)
            {
                probeStartRealtime = realtime;
                probeStartDsp = dsp;
                lastProbeDsp = dsp;
                return;
            }
            if (dsp < lastProbeDsp)
            {
                BeginValidation();
                return;
            }
            lastProbeDsp = dsp;

            double elapsed = realtime - probeStartRealtime;
            double dspElapsed = dsp - probeStartDsp;
            if (elapsed <= 0)
            {
                return;
            }
            if (checkedConfiguration.dspBufferSize <= 0 || checkedConfiguration.sampleRate <= 0)
            {
                RestoreConfiguration("DSP 시계 진행률 확인 실패: 장치 설정이 유효하지 않습니다.");
                return;
            }

            var measurement = new DspClockRateMeasurement(elapsed, dspElapsed,
                checkedConfiguration.dspBufferSize, checkedConfiguration.sampleRate);
            LastClockRateMeasurement = measurement;
            LastDspRealtimeRatio = measurement.Ratio;
            if (measurement.State == DspClockRateState.Undetermined)
            {
                return;
            }

            clockRateMeasurements.Add(measurement);
            if (measurement.State == DspClockRateState.Healthy)
            {
                ConsecutiveClockRateMeasurements++;
                if (ConsecutiveClockRateMeasurements < DspClockRateMeasurement.RequiredConsecutiveMeasurements)
                {
                    // The next observation starts here; it does not reuse the previous interval.
                    probeStartRealtime = realtime;
                    probeStartDsp = dsp;
                    return;
                }
                CompleteValidation();
                return;
            }

            ConsecutiveClockRateMeasurements = 0;
            LastRejectedDspRealtimeRatio = LastDspRealtimeRatio;
            RecoverLargerBuffer();
        }

        private void CompleteValidation()
        {
            lastHealthyConfiguration = backend.Configuration;
            IsBufferValidationPending = false;
            HasBufferValidationResult = true;
            ClockRateVerified = true;
            if (recoveryOccurred || Actual.BufferSize != validationRequestedBuffer)
            {
                SyncAppliedOption();
            }
            if (recoveryOccurred)
            {
                BufferStatus = $"요청 {validationRequestedBuffer} 진행률 이상 → Actual: {Actual.BufferSize} samples로 복구 · DSP 시계 ±1% 확인";
            }
            else
            {
                BufferStatus = $"DSP 시계 진행률 ±1% 확인 완료 · Actual: {Actual.BufferSize} samples";
            }
            Changed?.Invoke();
        }

        private void RecoverLargerBuffer()
        {
            int next = NextLargerBuffer(checkedConfiguration.dspBufferSize);
            if (next > 0)
            {
                var requested = backend.Configuration;
                requested.dspBufferSize = next;
                int previousBuffer = checkedConfiguration.dspBufferSize;
                try
                {
                    if (ResetConfiguration(requested))
                    {
                        if (backend.Configuration.dspBufferSize > previousBuffer)
                        {
                            recoveryOccurred = true;
                            BeginValidation();
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    // Restore below; an accepted reset is not a verified clock rate.
                }
            }
            RestoreConfiguration("정상 DSP 시계 진행률을 확인하지 못했습니다.");
        }

        private static int NextLargerBuffer(int current)
        {
            foreach (int size in bufferSizes)
            {
                if (size > current)
                {
                    return size;
                }
            }
            return 0;
        }

        private bool ResetConfiguration(AudioConfiguration configuration)
        {
            resettingConfiguration = true;
            try
            {
                return backend.Reset(configuration);
            }
            finally
            {
                resettingConfiguration = false;
            }
        }

        private static bool SameConfiguration(AudioConfiguration first, AudioConfiguration second)
        {
            if (first.dspBufferSize != second.dspBufferSize || first.sampleRate != second.sampleRate)
            {
                return false;
            }
            return first.speakerMode == second.speakerMode
                && first.numRealVoices == second.numRealVoices
                && first.numVirtualVoices == second.numVirtualVoices;
        }

        public void CancelBufferValidation()
        {
            if (IsBufferValidationPending)
            {
                RestoreConfiguration("DSP 시계 진행률 확인을 취소했습니다.");
            }
        }

        private void RestoreConfiguration(string reason)
        {
            IsBufferValidationPending = false;
            HasBufferValidationResult = true;
            ClockRateVerified = false;
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
                restored = ResetConfiguration(previous);
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
