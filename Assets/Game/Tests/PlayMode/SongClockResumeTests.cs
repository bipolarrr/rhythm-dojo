using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Text;
using NUnit.Framework;
using RhythmDojo.Audio;
using RhythmDojo.UI;
using UnityEngine.InputSystem;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed class SongClockResumeTests : GameplayTestContext
    {
        private SongClock CreateClock(double offset, int timingOffsetMs = 0)
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var root = new GameObject("Resume clock verification");
            var source = root.AddComponent<AudioSource>();
            var clock = root.AddComponent<SongClock>();
            typeof(SongClock).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(clock, source);
            clock.Initialize(flow.Settings.catalog[0].AudioClip, offset, flow.Settings.audio, timingOffsetMs);
            return clock;
        }

        [UnityTest]
        public IEnumerator EditorStartPositionKeepsAudioAnchorAndDoesNotReplayPastEnd()
        {
            var clock = CreateClock(-.25, 200);
            try
            {
                var source = clock.GetComponent<AudioSource>();
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetEditorStartPosition(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetEditorStartPosition(-1));
                clock.SetEditorStartPosition(1.25);
                double before = AudioSettings.dspTime;
                clock.Schedule();
                Assert.That(clock.ScheduledStartDspTime + 1.25, Is.GreaterThan(before));
                Assert.That(source.timeSamples, Is.EqualTo((int)(1.25 * source.clip.frequency)));
                Assert.Throws<InvalidOperationException>(() => clock.SetEditorStartPosition(0));
                clock.Stop();
                clock.SetEditorStartPosition((double)source.clip.samples / source.clip.frequency + 1);
                clock.Schedule();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(source.isPlaying, Is.False, "A position beyond the clip must not replay its last sample.");
            }
            finally
            {
                Object.Destroy(clock.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ResumeWaitsForScheduledAnchorWithoutAdvancingLogicalTime()
        {
            var clock = CreateClock(.25, 200);
            try
            {
                clock.Schedule();
                yield return new WaitForSecondsRealtime(.4f);
                clock.Pause();
                double frozen = clock.SongTime;
                clock.Resume();
                Assert.That(clock.Running, Is.False, "Resume must reserve a shared future DSP anchor.");
                Assert.That(clock.SongTime, Is.EqualTo(frozen));
                yield return null;
                Assert.That(clock.SongTime, Is.EqualTo(frozen));
                yield return Await(() => clock.Running);
                Assert.That(clock.SongTime, Is.GreaterThanOrEqualTo(frozen));
            }
            finally
            {
                Object.Destroy(clock.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ResumeSeeksFromFrozenLogicalTimeInsteadOfPausedSourceCursor()
        {
            var clock = CreateClock(-.25, -200);
            try
            {
                clock.Schedule();
                yield return new WaitForSecondsRealtime(.5f);
                clock.Pause();
                double frozen = clock.SongTime;
                var source = clock.GetComponent<AudioSource>();
                source.timeSamples = source.clip.frequency * 2;
                clock.Resume();
                long expected = (long)Math.Round((frozen + .25) * source.clip.frequency);
                Assert.That(Math.Abs(source.timeSamples - expected), Is.LessThanOrEqualTo(1),
                    "User calibration and a stale source cursor must not change the resume sample.");
            }
            finally
            {
                Object.Destroy(clock.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TwentyResumesKeepSourceSamplesAlignedAcrossBuffersAndOffsets()
        {
            var report = new StringBuilder("requested,actual,buffers,rate,clipRate,offset,cycle,frame,dsp,realtime,anchor,frozen,seekSample,sourceSample,logical,audio,error,uncertainty,drift,dspElapsed,realtimeElapsed,dspRatio,rawDspAudioError\n");
            string failure = null;
            Directory.CreateDirectory("Logs/pr-quality-remediation");
            var validationReport = new StringBuilder("requested,applied,validationRatio,rejectedRatio,verified,count,totalSeconds,lower,upper,status\n");
            var appliedSizes = new System.Collections.Generic.HashSet<int>();
            using var validation = new AudioSettingsService(() => true);
            try
            {
                foreach (int size in new[] { 128, 1024 })
                {
                    for (int option = 1; option < validation.OptionCount; option++)
                    {
                        if (validation.GetBufferSize(option) == size)
                        {
                            validation.SelectBufferOption(option);
                            break;
                        }
                    }
                    Assert.That(validation.ApplyBufferSize(), Is.True);
                    double validationDeadline = Time.realtimeSinceStartupAsDouble + AudioSettingsService.ClockValidationTimeoutSeconds + 2;
                    while (validation.IsBufferValidationPending)
                    {
                        Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(validationDeadline));
                        validation.Tick();
                        yield return null;
                    }
                    AssertClockRateVerified(validation);
                    AudioSettings.GetDSPBufferSize(out int actual, out int count);
                    appliedSizes.Add(actual);
                    validationReport.AppendLine(FormattableString.Invariant(
                        $"{size},{actual},{validation.LastDspRealtimeRatio:R},{validation.LastRejectedDspRealtimeRatio:R},{validation.ClockRateVerified},{validation.ConsecutiveClockRateMeasurements},{validation.ValidationElapsedSeconds:R},{validation.LastClockRateMeasurement.Value.LowerBound:R},{validation.LastClockRateMeasurement.Value.UpperBound:R},{validation.BufferStatus}"));
                    foreach (double offset in new[] { -.25, .25 })
                    {
                        var clock = CreateClock(offset, 200);
                        try
                        {
                            var source = clock.GetComponent<AudioSource>();
                            clock.Schedule();
                            yield return new WaitForSecondsRealtime(.4f);
                            double firstError = 0;
                            double initialDsp = AudioSettings.dspTime;
                            double initialRealtime = Time.realtimeSinceStartupAsDouble;
                            double initialLogical = clock.SongTime - offset;
                            double initialAudio = source.timeSamples / (double)source.clip.frequency;
                            report.AppendLine(FormattableString.Invariant(
                                $"{size},{actual},{count},{AudioSettings.outputSampleRate},{source.clip.frequency},{offset},-1,-1,{initialDsp:R},{initialRealtime:R},{clock.ScheduledStartDspTime:R},{clock.SongTime:R},0,{source.timeSamples},{initialLogical:R},{initialAudio:R},{initialAudio - initialLogical:R},0,0,0,0,0,{initialAudio - (initialDsp - clock.ScheduledStartDspTime):R}"));
                            for (int cycle = 0; cycle < 20; cycle++)
                            {
                                clock.Pause();
                                double frozen = clock.SongTime;
                                clock.Resume();
                                Assert.That(clock.Running, Is.False);
                                Assert.That(clock.SongTime, Is.EqualTo(frozen));
                                int sample = (int)Math.Round((frozen - offset) * source.clip.frequency);
                                Assert.That(Math.Abs(source.timeSamples - sample), Is.LessThanOrEqualTo(1));
                                yield return Await(() => clock.Running);
                                for (int frameIndex = 0; frameIndex < 8; frameIndex++)
                                {
                                    double observed = Time.realtimeSinceStartupAsDouble;
                                    yield return null;
                                    double realtime = Time.realtimeSinceStartupAsDouble;
                                    double dsp = AudioSettings.dspTime;
                                    double frame = realtime - observed;
                                    double logical = clock.SongTime - offset;
                                    double audio = source.timeSamples / (double)source.clip.frequency;
                                    double error = audio - logical;
                                    double uncertainty = 2.0 * actual / AudioSettings.outputSampleRate
                                        + frame + 1.0 / source.clip.frequency;
                                    if (cycle == 0 && frameIndex == 0)
                                    {
                                        firstError = error;
                                    }
                                    double dspElapsed = dsp - initialDsp;
                                    double realtimeElapsed = realtime - initialRealtime;
                                    double rawError = audio - (dsp - clock.ScheduledStartDspTime);
                                    report.AppendLine(FormattableString.Invariant(
                                        $"{size},{actual},{count},{AudioSettings.outputSampleRate},{source.clip.frequency},{offset},{cycle},{frameIndex},{dsp:R},{realtime:R},{clock.ResumeDspTime:R},{frozen:R},{sample},{source.timeSamples},{logical:R},{audio:R},{error:R},{uncertainty:R},{error - firstError:R},{dspElapsed:R},{realtimeElapsed:R},{dspElapsed / realtimeElapsed:R},{rawError:R}"));
                                    if (Math.Abs(error) > uncertainty && failure == null)
                                    {
                                        failure = $"Requested {size}, applied {actual}, offset {offset}, cycle {cycle}, frame {frameIndex}: error {error:R}, uncertainty {uncertainty:R}, raw DSP error {rawError:R}.";
                                    }
                                }
                            }
                            double measuredDsp = AudioSettings.dspTime - initialDsp;
                            double measuredRealtime = Time.realtimeSinceStartupAsDouble - initialRealtime;
                            double rateUncertainty = 2.0 * actual / AudioSettings.outputSampleRate;
                            Assert.That(Math.Abs(measuredDsp - measuredRealtime),
                                Is.LessThanOrEqualTo(.01 * measuredRealtime + rateUncertainty),
                                "Independent raw DSP/realtime observation must be compatible with normal speed.");
                        }
                        finally
                        {
                            Object.Destroy(clock.gameObject);
                        }
                        yield return null;
                    }
                }
                Assert.That(appliedSizes.Count, Is.EqualTo(2), "Measure two distinct device-applied buffers.");
                Assert.That(failure, Is.Null);
            }
            finally
            {
                File.WriteAllText("Logs/pr-quality-remediation/task3-resume-measurements.csv", report.ToString());
                File.WriteAllText("Logs/pr-quality-remediation/task3-buffer-validation.csv", validationReport.ToString());
            }
        }

        [UnityTest]
        public IEnumerator BeforeAudioResumeWaitsForItsRemainingLeadAndPendingPauseCancels()
        {
            var clock = CreateClock(.25);
            try
            {
                clock.Schedule();
                clock.Pause();
                double frozen = clock.SongTime;
                var source = clock.GetComponent<AudioSource>();
                clock.Resume();
                yield return Await(() => clock.Running);
                Assert.That(source.timeSamples, Is.Zero, "Negative audio time must retain the remaining silent lead.");
                Assert.That(clock.SongTime, Is.GreaterThanOrEqualTo(frozen));
                yield return Await(() => source.timeSamples > 0);
                clock.Pause();
                frozen = clock.SongTime;
                clock.Resume();
                clock.Pause();
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(clock.Running, Is.False);
                Assert.That(clock.ResumePending, Is.False);
                Assert.That(clock.SongTime, Is.EqualTo(frozen));
                Assert.That(source.isPlaying, Is.False);
            }
            finally
            {
                Object.Destroy(clock.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ResumeAfterAudioEndDoesNotReplayTheFinalSample()
        {
            var clock = CreateClock(-.25);
            var clip = AudioClip.Create("Short resume edge", 480, 1, 48000, false);
            try
            {
                var flow = Object.FindFirstObjectByType<AppFlowController>();
                clock.Initialize(clip, -.25, flow.Settings.audio);
                clock.Schedule();
                yield return new WaitForSecondsRealtime(.3f);
                clock.Pause();
                Assert.That(clock.SongTime + .25, Is.GreaterThan(clip.length));
                clock.Resume();
                var source = clock.GetComponent<AudioSource>();
                Assert.That(source.isPlaying, Is.False);
                yield return Await(() => clock.Running);
                Assert.That(source.isPlaying, Is.False);
                Assert.That(source.timeSamples, Is.Zero);
            }
            finally
            {
                Object.Destroy(clock.gameObject);
                Object.Destroy(clip);
            }
        }

        [UnityTest]
        public IEnumerator ResetDuringScheduledResumeKeepsTimeFrozenUntilNewResume()
        {
            var clock = CreateClock(.25);
            try
            {
                clock.Schedule();
                yield return new WaitForSecondsRealtime(.4f);
                clock.Pause();
                double frozen = clock.SongTime;
                clock.Resume();
                Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
                yield return null;
                Assert.That(clock.ResumePending, Is.False);
                Assert.That(clock.Running, Is.False);
                Assert.That(clock.SongTime, Is.EqualTo(frozen));
                clock.Resume();
                yield return Await(() => clock.Running);
                Assert.That(clock.SongTime, Is.GreaterThanOrEqualTo(frozen));
            }
            finally
            {
                Object.Destroy(clock.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EscapeDuringResumeLeadKeepsSessionAndSourcePaused()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            game.StartSession();
            yield return new WaitForSecondsRealtime(.4f);
            game.PauseSession();
            game.ResumeSession();
            Assert.That(game.IsPaused, Is.True);
            yield return Press(Key.Escape);
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            Assert.That(menu.panel.activeSelf, Is.True);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Clock.Running, Is.False);
            double frozen = game.Clock.SongTime;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(game.Clock.SongTime, Is.EqualTo(frozen));
            Assert.That(((Component)game.Clock).GetComponent<AudioSource>().isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator ResetDuringMenuResumeLeadReopensPauseWithoutAdvancingSession()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            game.StartSession();
            yield return new WaitForSecondsRealtime(.4f);
            menu.Open();
            int resolved = game.Session.Resolved;
            menu.BeginResume();
            var clock = (SongClock)game.Clock;
            yield return Await(() => clock.ResumePending);
            Assert.That(AudioSettings.Reset(AudioSettings.GetConfiguration()), Is.True);
            yield return Await(() => menu.panel.activeSelf);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(game.Clock.Running, Is.False);
            Assert.That(game.Session.Resolved, Is.EqualTo(resolved));
            Assert.That(menu.CountingDown, Is.False);
        }
    }
}
