using System;
using System.Collections.Generic;
using NUnit.Framework;
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
                Configuration = configuration;
                DspTime = 0;
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
                double rate = 1;
                if (SlowAllBuffers || (SlowSmallBuffers && Configuration.dspBufferSize < 256))
                {
                    rate = .2;
                }
                DspTime += seconds * rate;
            }
        }

        private static void Measure(AudioSettingsService service, FakeBackend backend)
        {
            backend.Advance(.31);
            service.Tick();
            backend.Advance(1.01);
            service.Tick();
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
            Assert.That(service.IsBufferValidationPending, Is.True);
            Assert.That(service.LastRejectedDspRealtimeRatio, Is.EqualTo(.2).Within(.001));
            Assert.That(backend.Configuration.dspBufferSize, Is.EqualTo(256));
            Measure(service, backend);
            Assert.That(service.IsBufferValidationPending, Is.False);
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
            for (int i = 0; i < 5; i++)
            {
                Measure(service, backend);
            }
            Assert.That(service.IsBufferValidationPending, Is.False);
            Assert.That(service.BufferStatus, Does.Contain("못했습니다"));
            Assert.That(service.Actual.BufferSize, Is.EqualTo(1024));
            Assert.That(backend.Resets.Count, Is.EqualTo(6));
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
        }
    }
}
