using System;
using System.Collections.Generic;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Tests
{
    public sealed class AudioSettingsServiceTests
    {
        private sealed class FakeBackend : IAudioConfigurationBackend
        {
            public AudioConfiguration Configuration { get; private set; } =
                new AudioConfiguration { dspBufferSize = 1024, sampleRate = 48000 };
            public AudioState Actual => new AudioState(Configuration.dspBufferSize, 4, Configuration.sampleRate);
            public double DspTime { get; private set; }
            public double Realtime { get; private set; }
            public bool Paused { get; set; }
            public bool SlowSmallBuffers;
            public bool SlowAllBuffers;
            public bool ClampSmallBuffers;
            public double Rate = 1;
            public bool QuantizeDsp;
            public int ForcedBuffer;
            private double continuousDsp;
            public readonly HashSet<int> Rejected = new HashSet<int>();
            public readonly List<int> Resets = new List<int>();
            public event Action<bool> Changed;

            public bool Reset(AudioConfiguration configuration)
            {
                Resets.Add(configuration.dspBufferSize);
                if (Rejected.Contains(configuration.dspBufferSize))
                {
                    return false;
                }
                if (ClampSmallBuffers && configuration.dspBufferSize < 256)
                {
                    configuration.dspBufferSize = 256;
                }
                if (ForcedBuffer > 0)
                {
                    configuration.dspBufferSize = ForcedBuffer;
                }
                Configuration = configuration;
                DspTime = 0;
                continuousDsp = 0;
                Changed?.Invoke(false);
                return true;
            }

            public void Advance(double seconds)
            {
                Realtime += seconds;
                if (Paused)
                {
                    return;
                }
                double rate = Rate;
                if (SlowAllBuffers || (SlowSmallBuffers && Configuration.dspBufferSize < 256))
                {
                    rate = .2;
                }
                continuousDsp += seconds * rate;
                DspTime = continuousDsp;
                if (QuantizeDsp)
                {
                    double block = (double)Configuration.dspBufferSize / Configuration.sampleRate;
                    DspTime = Math.Floor(continuousDsp / block) * block;
                }
            }

            public void ResetClockWithoutNotification()
            {
                DspTime = 0;
                continuousDsp = 0;
            }

            public void NotifyConfigurationChanged()
            {
                Changed?.Invoke(true);
            }
        }

        private static void Measure(AudioSettingsService service, FakeBackend backend)
        {
            for (int i = 0; i < 210 && service.IsBufferValidationPending; i++)
            {
                backend.Advance(.1);
                service.Tick();
            }
            Assert.That(service.IsBufferValidationPending, Is.False);
        }

        private static void AssertVerified(AudioSettingsService service)
        {
            Assert.That(service.ClockRateVerified, Is.True, service.BufferStatus);
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.EqualTo(2));
            Assert.That(service.ClockRateMeasurements.Count, Is.GreaterThanOrEqualTo(2));
            for (int i = service.ClockRateMeasurements.Count - 2; i < service.ClockRateMeasurements.Count; i++)
            {
                var measurement = service.ClockRateMeasurements[i];
                Assert.That(measurement.State, Is.EqualTo(DspClockRateState.Healthy));
                Assert.That(measurement.LowerBound, Is.GreaterThanOrEqualTo(.99));
                Assert.That(measurement.UpperBound, Is.LessThanOrEqualTo(1.01));
            }
        }

        [Test]
        public void TwoIndependentIntervalsMustContainTheEntireUncertaintyRange()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            backend.Advance(.31);
            service.Tick();
            backend.Advance(4.3);
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.EqualTo(1));
            Assert.That(service.ClockRateVerified, Is.False);
            backend.Advance(.1);
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.EqualTo(1));
            Assert.That(service.LastClockRateMeasurement.Value.State, Is.EqualTo(DspClockRateState.Undetermined));
            backend.Advance(4.2);
            service.Tick();
            AssertVerified(service);
        }

        [TestCase(1, 1, DspClockRateState.Undetermined)]
        [TestCase(5, 5, DspClockRateState.Healthy)]
        [TestCase(5, 4.9, DspClockRateState.Unhealthy)]
        [TestCase(5, 5.1, DspClockRateState.Unhealthy)]
        public void MeasurementIncludesBothEndpointBlockUncertainty(double real, double dsp, DspClockRateState expected)
        {
            var measurement = new DspClockRateMeasurement(real, dsp, 1024, 48000);
            double error = 2.0 * 1024 / 48000;
            Assert.That(measurement.LowerBound, Is.EqualTo((dsp - error) / real).Within(1e-12));
            Assert.That(measurement.UpperBound, Is.EqualTo((dsp + error) / real).Within(1e-12));
            Assert.That(measurement.State, Is.EqualTo(expected));
        }

        [Test]
        public void RejectedLargerBufferReportsFailureAndRestoresPreviousConfiguration()
        {
            var backend = new FakeBackend { SlowAllBuffers = true };
            backend.Rejected.Add(64);
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(1);
            service.ApplyBufferSize();
            Measure(service, backend);
            Assert.That(backend.Resets, Is.EqualTo(new[] { 32, 64, 1024 }));
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(1024));
        }

        [Test]
        public void PauseInvalidatesCompletedFirstInterval()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            backend.Advance(.31);
            service.Tick();
            backend.Advance(4.3);
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.EqualTo(1));
            backend.Paused = true;
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.Zero);
            backend.Paused = false;
            Measure(service, backend);
            AssertVerified(service);
        }

        [Test]
        public void QuantizedClockAndDelayedFramesStillRequireContainedIntervals()
        {
            var backend = new FakeBackend { QuantizeDsp = true };
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            for (int i = 0; i < 40 && service.IsBufferValidationPending; i++)
            {
                backend.Advance(.173 + i % 3 * .391);
                service.Tick();
            }
            AssertVerified(service);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PauseOrRepeatedConfigurationChangesCannotRestartOverallDeadline(bool paused)
        {
            var backend = new FakeBackend { Paused = paused };
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            for (int i = 0; i < 20; i++)
            {
                backend.Advance(1);
                if (!paused)
                {
                    backend.NotifyConfigurationChanged();
                }
                service.Tick();
            }
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.ValidationElapsedSeconds, Is.EqualTo(20));
            Assert.That(service.BufferStatus, Does.Contain("시간 초과"));
        }

        [Test]
        public void ClockResetDiscardsPriorSuccessfulObservation()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            backend.Advance(.31);
            service.Tick();
            backend.Advance(4.3);
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.EqualTo(1));
            backend.ResetClockWithoutNotification();
            service.Tick();
            Assert.That(service.ConsecutiveClockRateMeasurements, Is.Zero);
            Measure(service, backend);
            AssertVerified(service);
        }

        [Test]
        public void DeviceRefusingLargerAppliedBufferCannotReportRecovery()
        {
            var backend = new FakeBackend { ForcedBuffer = 32, SlowAllBuffers = true };
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(1);
            service.ApplyBufferSize();
            Measure(service, backend);
            Assert.That(backend.Resets, Is.EqualTo(new[] { 32, 64, 1024 }));
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.BufferStatus, Does.Contain("못했습니다"));
            Assert.That(service.Actual.BufferSize, Is.EqualTo(32));
        }

        [Test]
        public void UnresolvedUncertaintyTimesOutWithoutClaimingHealthy()
        {
            var backend = new FakeBackend();
            backend.Reset(new AudioConfiguration { dspBufferSize = 2048, sampleRate = 8000 });
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            Measure(service, backend);
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.BufferStatus, Does.Contain("시간 초과"));
            Assert.That(service.LastClockRateMeasurement.Value.State, Is.EqualTo(DspClockRateState.Undetermined));
        }

        [Test]
        public void ExternalResetInvalidatesVerifiedStatus()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.ApplyBufferSize();
            Measure(service, backend);
            AssertVerified(service);
            backend.NotifyConfigurationChanged();
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.HasBufferValidationResult, Is.False);
            Assert.That(service.BufferStatus, Does.Contain("무효화"));
        }

        [TestCase(.98)]
        [TestCase(1.02)]
        public void TwoPercentClockErrorCannotPassAsHealthy(double rate)
        {
            var backend = new FakeBackend { Rate = rate };
            using var service = new AudioSettingsService(() => true, backend);
            Assert.That(service.ApplyBufferSize(), Is.True);
            backend.Advance(.31);
            service.Tick();
            backend.Advance(1.01);
            service.Tick();
            Assert.That(service.IsBufferValidationPending, Is.True,
                "One short observation must not accept a persistent two-percent clock error.");
            Measure(service, backend);
            Assert.That(service.ClockRateVerified, Is.False);
            Assert.That(service.BufferStatus, Does.Not.Contain("확인 완료"));
        }
        [TestCase(1, 32)]
        [TestCase(2, 64)]
        [TestCase(3, 128)]
        public void SlowAcceptedConfigurationRecoversBeforeReportingSuccess(int option, int requested)
        {
            var backend = new FakeBackend { SlowSmallBuffers = true };
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(option);
            Assert.That(service.ApplyBufferSize(), Is.True);
            Assert.That(service.IsBufferValidationPending, Is.True);
            Assert.That(backend.Configuration.dspBufferSize, Is.EqualTo(requested));
            Measure(service, backend);
            Assert.That(service.LastRejectedDspRealtimeRatio, Is.EqualTo(.2).Within(.001));
            Assert.That(backend.Configuration.dspBufferSize, Is.EqualTo(256));
            Assert.That(service.IsBufferValidationPending, Is.False);
            AssertVerified(service);
            Assert.That(service.LastDspRealtimeRatio, Is.EqualTo(1).Within(.001));
            Assert.That(service.RequestedBufferSize, Is.EqualTo(256));
            Assert.That(service.BufferStatus, Does.Contain("복구"));
        }

        [Test]
        public void HealthyLowBufferRemainsAvailableWithoutForcedMinimum()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(2);
            service.ApplyBufferSize();
            Assert.That(service.ApplyBufferSize(), Is.False);
            service.SelectBufferOption(7);
            Assert.That(service.SelectedBufferOption, Is.EqualTo(2));
            Measure(service, backend);
            Assert.That(service.IsBufferValidationPending, Is.False);
            AssertVerified(service);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(64));
            Assert.That(backend.Resets, Has.Count.EqualTo(1));
        }

        [Test]
        public void FailedRecoveryStopsRetryingAndReportsFailureInsteadOfSuccess()
        {
            var backend = new FakeBackend { SlowAllBuffers = true };
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(1);
            service.ApplyBufferSize();
            Measure(service, backend);
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.BufferStatus, Does.Contain("못했습니다"));
            Assert.That(service.Actual.BufferSize, Is.EqualTo(1024));
            Assert.That(backend.Resets, Is.EqualTo(new[] { 32, 64, 128, 256, 512, 1024, 2048, 1024 }));
            Assert.That(service.ClockRateVerified, Is.False);
        }

        [Test]
        public void RejectedResetRestoresPreviousConfigurationAndPublishesResult()
        {
            var backend = new FakeBackend();
            backend.Rejected.Add(32);
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(1);
            Assert.That(service.ApplyBufferSize(), Is.False);
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.HasBufferValidationResult, Is.True);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(1024));
            Assert.That(service.SelectedBufferOption, Is.Zero);
        }

        [Test]
        public void ForcedNavigationCanCancelValidationAndRestoreBeforeGameplay()
        {
            bool allowed = true;
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => allowed, backend);
            service.SelectBufferOption(1);
            service.ApplyBufferSize();
            service.CancelBufferValidation();
            allowed = false;
            service.Tick();
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(1024));
            Assert.That(backend.Resets.Count, Is.EqualTo(2));
            Assert.That(service.ApplyBufferSize(), Is.False);
        }

        [Test]
        public void PausedAudioDoesNotGetMisdiagnosedAsSlowHardware()
        {
            var backend = new FakeBackend();
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(2);
            service.ApplyBufferSize();
            backend.Paused = true;
            backend.Advance(10);
            service.Tick();
            Assert.That(service.IsBufferValidationPending, Is.True);
            Assert.That(backend.Resets.Count, Is.EqualTo(1));
            backend.Paused = false;
            Measure(service, backend);
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(64));
            AssertVerified(service);
        }

        [Test]
        public void DriverClampingIsReflectedInTheSelectedAndActualBuffer()
        {
            var backend = new FakeBackend { ClampSmallBuffers = true };
            using var service = new AudioSettingsService(() => true, backend);
            service.SelectBufferOption(1);
            service.ApplyBufferSize();
            Measure(service, backend);
            Assert.That(service.Actual.BufferSize, Is.EqualTo(256));
            Assert.That(service.RequestedBufferSize, Is.EqualTo(256));
            Assert.That(service.SelectedBufferOption, Is.EqualTo(4));
            AssertVerified(service);
        }
    }
}
