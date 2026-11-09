using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class PupaResultsFlowTests : GameplayTestContext
    {
        [System.Serializable]
        private sealed class PlaybackTimingMeasurement
        {
            public double startedRealtime;
            public double startedDsp;
            public double scheduledStartDsp;
            public double chartCompletionTime;
            public double chartOffset;
            public double clipDurationSeconds;
            public int sampleCount;
            public int frequency;
            public int dspBufferLength;
            public int dspBufferCount;
            public int outputSampleRate;
            public double lastPlayingRealtime;
            public double completedRealtime = -1;
            public double completedDsp = -1;
            public double resultsRealtime = -1;
            public double resultsDsp = -1;
            public double maximumFrameGapSeconds;
            public double wallElapsedSeconds;
            public double audioElapsedSeconds;
            public double outcomeDelayLowerSeconds;
            public double outcomeDelayUpperSeconds;
            public bool clockStoppedAtCompletion;
        }

        private static SongDocument PupaDocument(SongDefinition source)
        {
            var project = new SongProjectJson().Deserialize(Resources.Load<TextAsset>("Authoring/Pupa").text);
            var document = SongAssetAdapter.ToDocument(source);
            document.SongId = "pupa-results-regression";
            document.Title = project.Title;
            document.Artist = project.Artist;
            document.Notes = project.Charts.Single().Notes;
            document.CompletionTime = project.Charts.Single().CompletionTimeSeconds;
            document.TempoPoints = project.TempoPoints;
            document.AudioOffset = project.AudioOffsetSeconds;

            return document;
        }

        private sealed class PupaProvider : ISongProvider
        {
            private readonly SongDocument document;
            private readonly SongDefinition source;
            private readonly AudioClip audio;

            public PupaProvider(SongDocument document, SongDefinition source, AudioClip audio = null)
            {
                this.document = document;
                this.source = source;
                this.audio = audio;

                if (!this.audio)
                {
                    this.audio = source.AudioClip;
                }
            }

            public IReadOnlyList<SongEntry> GetEntries() => new[]
            {
                new SongEntry(document.SongId, document.Title, document.Artist,
                    document.CompletionTime, document.Notes.Length, 4, 202, 202),
            };

            public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken) =>
                Task.FromResult(new PlayableSong(document, audio, source.Mode));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SongSelectionPupaCompletesAndKeepsTheWholeChart()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            flow.Library.Register(new PupaProvider(document, source));
            flow.Selection.UpdateSelection(document.SongId, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.Chart.Count, Is.EqualTo(929));
            Assert.That(game.Chart.CompletionTime, Is.LessThan(document.CompletionTime - 7),
                "PUPA must not wait through the long audio tail after its last judgment.");
            game.StartSession();
            double lastEnd = document.Notes.Max(note => note.EndTime);
            game.Session.Advance(lastEnd - .001);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            game.Session.Advance(lastEnd + flow.Rules.Snapshot.Timing.GoodWindow + .001);
            Assert.That(game.Session.Resolved, Is.EqualTo(game.Session.Snapshot.TotalNotes));
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            Assert.That(flow.LastResult.Title, Is.EqualTo("PUPA"));
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(flow.LastResult.Notes.Count, Is.EqualTo(929));
            Assert.That(flow.LastResult.Notes.Sum(note => note.Judgments.Count),
                Is.EqualTo(flow.LastResult.Notes.Count + flow.LastResult.Notes.Count(note => note.Note.Kind == NoteKind.Hold)));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator EditorPupaShowsResultsBeforeReturningTheUnsavedSession()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            var loader = new DocumentSongLoader(_ => source.Mode,
                (_, token) => Task.FromResult(new SongAudioLease(source.AudioClip)));
            string returned = null;
            string preferences = PlayerPrefs.GetString(Services.SelectionPreferences.StorageKey, "");
            var task = flow.PlayAsync(document, loader, "pupa-unsaved-session", AppFlowController.SelectionPath,
                id => returned = id, CancellationToken.None);
            yield return Await(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            game.StartSession();
            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>(),
                "PUPA editor playtest completed but did not open Results.");
            yield return null;
            Assert.That(flow.IsEditorPlaytest, Is.True);
            Assert.That(returned, Is.Null);
            Assert.That(flow.LastResult.Title, Is.EqualTo("PUPA"));
            Assert.That(flow.LastResult.Notes.Count, Is.EqualTo(929));
            Assert.That(flow.CurrentRequest, Is.Null);
            Assert.That(PlayerPrefs.GetString(Services.SelectionPreferences.StorageKey, ""), Is.EqualTo(preferences));
            var view = Object.FindFirstObjectByType<ResultsView>();
            yield return Click(view.moreButton);
            Assert.That(view.review.GetComponent<ResultsChartReview>().Result.Notes.Count, Is.EqualTo(929));
            yield return Click(view.review.closeButton);
            yield return Click(view.backButton);
            yield return Await(() => returned != null);
            Assert.That(returned, Is.EqualTo("pupa-unsaved-session"));
            Assert.That(flow.IsEditorPlaytest, Is.False);
            Assert.That(source.AudioClip, Is.Not.Null);
        }

        [UnityTest]
        [Category("LocalAudio")]
        [Category("Endurance")]
        public IEnumerator RealPupaAudioFinishesAndAutomaticallyLoadsResults()
        {
            // Legacy test names retain the bundled chart; audio is an independent external fixture.
            using var wave = new TemporaryWaveFile(131.54, .5033333333333334);
            string path = wave.Path;
            var load = ChartEditorCompositionRoot.LoadAudio(path, CancellationToken.None);
            yield return Await(() => load.IsCompleted);
            Assert.That(load.IsFaulted, Is.False, load.Exception?.ToString());
            using var lease = load.Result;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            var rules = flow.Rules.Snapshot;
            rules.Profile = JudgmentProfile.Sparring;
            rules.Mode = RhythmDojo.Gameplay.PlayMode.Gauge;
            rules.GaugeFailure = GaugeFailureMode.Continue;
            flow.Rules.Update(rules);
            flow.Library.Register(new PupaProvider(document, source, lease.Clip));
            flow.Selection.UpdateSelection(document.SongId, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(lease.Clip.length, Is.GreaterThan(130));
            Assert.That(game.Chart.CompletionTime, Is.LessThan(document.CompletionTime - 7));
#if UNITY_EDITOR
            var gameView = Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>()
                .FirstOrDefault(window => window.GetType().Name == "GameView");

            if (gameView && !UnityEngine.Application.isBatchMode)
            {
                gameView.Focus();
            }

            yield return null;
#endif
            var clock = Object.FindFirstObjectByType<SongClock>();
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            double started = Time.realtimeSinceStartupAsDouble;
            double deadline = started + game.Chart.CompletionTime + 15;
            var measurement = new PlaybackTimingMeasurement
            {
                startedRealtime = started,
                startedDsp = AudioSettings.dspTime,
                chartCompletionTime = game.Chart.CompletionTime,
                chartOffset = document.AudioOffset,
                clipDurationSeconds = (double)lease.Clip.samples / lease.Clip.frequency,
                sampleCount = lease.Clip.samples,
                frequency = lease.Clip.frequency,
                dspBufferLength = bufferLength,
                dspBufferCount = bufferCount,
                outputSampleRate = AudioSettings.outputSampleRate,
                lastPlayingRealtime = started,
            };
            double previousFrame = started;
            game.StartSession();
            measurement.scheduledStartDsp = clock.ScheduledStartDspTime;

            try
            {
                while (!Object.FindFirstObjectByType<ResultsScreen>())
                {
                    double observedRealtime = Time.realtimeSinceStartupAsDouble;
                    measurement.maximumFrameGapSeconds = System.Math.Max(
                        measurement.maximumFrameGapSeconds, observedRealtime - previousFrame);
                    previousFrame = observedRealtime;
                    if (game && game.Session != null)
                    {
                        if (game.Session.State == SessionState.Ready)
                        {
                            Assert.Fail("PUPA playback was reset before completion: " + game.ReadModel.Snapshot.ReadyReason);
                        }
                        if (game.Session.State == SessionState.Playing)
                        {
                            measurement.lastPlayingRealtime = observedRealtime;
                        }
                        else if (game.Session.State == SessionState.Completed && measurement.completedRealtime < 0)
                        {
                            measurement.completedRealtime = observedRealtime;
                            measurement.completedDsp = AudioSettings.dspTime;
                            measurement.clockStoppedAtCompletion = !clock.Running;
                        }
                    }

                    Assert.That(observedRealtime, Is.LessThan(deadline),
                        "PUPA audio ended without automatic result transition.");
                    yield return null;
                }

                measurement.resultsRealtime = Time.realtimeSinceStartupAsDouble;
                measurement.resultsDsp = AudioSettings.dspTime;
                measurement.maximumFrameGapSeconds = System.Math.Max(
                    measurement.maximumFrameGapSeconds, measurement.resultsRealtime - previousFrame);
                measurement.wallElapsedSeconds = measurement.resultsRealtime - started;
                measurement.audioElapsedSeconds = measurement.resultsDsp - measurement.scheduledStartDsp;
                measurement.outcomeDelayLowerSeconds = measurement.resultsRealtime - measurement.completedRealtime;
                measurement.outcomeDelayUpperSeconds = measurement.resultsRealtime - measurement.lastPlayingRealtime;
            }
            finally
            {
                System.IO.Directory.CreateDirectory("Logs/pr-quality-remediation");
                System.IO.File.WriteAllText("Logs/pr-quality-remediation/final-pupa-timing.json",
                    JsonUtility.ToJson(measurement, true));
            }

            Assert.That(measurement.completedRealtime, Is.GreaterThanOrEqualTo(started));
            Assert.That(measurement.clockStoppedAtCompletion, Is.True);
            Assert.That(measurement.audioElapsedSeconds,
                Is.GreaterThan(document.Notes.Max(note => note.EndTime) - document.AudioOffset));
            Assert.That(measurement.audioElapsedSeconds,
                Is.LessThan(measurement.clipDurationSeconds - 6),
                "Trailing audio must not delay Results, measured from the scheduled audio DSP anchor.");
            // Completion lies between the last Playing and first Completed observations.
            // The interval must be consistent with the one-second outcome display contract.
            Assert.That(measurement.outcomeDelayUpperSeconds, Is.GreaterThanOrEqualTo(1));
            yield return null;
            Assert.That(flow.LastResult.Title, Is.EqualTo("PUPA"));
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(flow.LastResult.Session.Resolved, Is.EqualTo(flow.LastResult.Session.TotalNotes));
            Assert.That(flow.LastResult.Notes.Count, Is.EqualTo(929));
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
        }

        [UnityTest]
        [Category("LocalAudio")]
        [Category("Hardware")]
        public IEnumerator CorrectedPupaClockKeepsAudioNotesAndTimestampedInputTogether()
        {
            // Legacy test names retain the bundled chart; audio is an independent external fixture.
            using var wave = new TemporaryWaveFile(131.54, .5033333333333334);
            string path = wave.Path;
            var load = ChartEditorCompositionRoot.LoadAudio(path, CancellationToken.None);
            yield return Await(() => load.IsCompleted);
            Assert.That(load.IsFaulted, Is.False, load.Exception?.ToString());
            using var lease = load.Result;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            flow.Library.Register(new PupaProvider(document, source, lease.Clip));
            flow.Selection.UpdateSelection(document.SongId, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var clock = Object.FindFirstObjectByType<SongClock>();
            var audio = clock.GetComponent<AudioSource>();
            var first = document.Notes[0];
            Assert.That(document.AudioOffset, Is.EqualTo(-.02));
            double firstAudioTime = first.StartTime - document.AudioOffset;
            Assert.That(firstAudioTime, Is.EqualTo(.5033333333333334).Within(.000001));

            // Check the actual first attack, rather than using the chart clock as an audio oracle.
            var samples = new float[lease.Clip.frequency * lease.Clip.channels];
            Assert.That(lease.Clip.GetData(samples, 0), Is.True);
            int attack = System.Array.FindIndex(samples, sample => System.Math.Abs(sample) > .02);
            double attackTime = (double)(attack / lease.Clip.channels) / lease.Clip.frequency;
            Assert.That(attackTime, Is.EqualTo(firstAudioTime).Within(.008));

            game.StartSession();
            yield return Await(() => clock.SongTime > first.StartTime + .03);
            double timestamp = Time.realtimeSinceStartupAsDouble - (clock.SongTime - first.StartTime);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F), timestamp);
            yield return null;
            Assert.That(game.Session.Perfect, Is.EqualTo(1));
            Assert.That(game.Session.LastTimingError, Is.EqualTo(0).Within(.008),
                "Dispatching a key later must retain the corrected musical event time.");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;

            foreach (double musicTime in new[] { 30.0, 70.0, 115.0 })
            {
                audio.time = (float)musicTime;
                typeof(SongClock).GetProperty(nameof(SongClock.ScheduledStartDspTime))
                    .SetValue(clock, AudioSettings.dspTime - musicTime);
                double songTime = clock.SongTime;
                double eventTime = Time.realtimeSinceStartupAsDouble - .03;
                Assert.That(songTime, Is.EqualTo(musicTime + document.AudioOffset).Within(.03));
                Assert.That(clock.InputTimeToSongTime(eventTime),
                    Is.EqualTo(songTime - .03).Within(.003),
                    "Chart offset must apply once to both presentation and input throughout the song.");
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ShortenedOutroStillPreservesTheFinalHoldReleaseWindow()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            var last = document.Notes[document.Notes.Length - 1];
            document.Notes[document.Notes.Length - 1] = NoteData.Hold(last.Lane, last.StartTime, 130);
            document.CompletionTime = 150;
            var rules = flow.Rules.Snapshot;
            rules.HoldEnd = HoldEndMode.Release;
            rules.GaugeFailure = GaugeFailureMode.Continue;
            flow.Rules.Update(rules);
            flow.Library.Register(new PupaProvider(document, source));
            flow.Selection.UpdateSelection(document.SongId, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(game.Chart.CompletionTime, Is.InRange(131, 131.2));
            game.StartSession();
            game.Session.Advance(130 + rules.Timing.GoodWindow);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            Assert.That(game.Session.Resolved, Is.LessThan(game.Session.Snapshot.TotalNotes));
            game.Session.Advance(130 + rules.Timing.GoodWindow + .001);
            Assert.That(game.Session.Resolved, Is.EqualTo(game.Session.Snapshot.TotalNotes));
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing));
            game.Session.Advance(game.Chart.CompletionTime + .001);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            Assert.That(flow.LastResult.Session.Resolved, Is.EqualTo(flow.LastResult.Session.TotalNotes));
        }

        [UnityTest]
        [Category("LocalAudio")]
        [Category("Hardware")]
        public IEnumerator PupaAudioTailTransitionsPromptlyWithEveryJudgmentPreserved()
        {
            // Legacy test names retain the bundled chart; audio is an independent external fixture.
            using var wave = new TemporaryWaveFile(131.54, .5033333333333334);
            string path = wave.Path;
            var load = ChartEditorCompositionRoot.LoadAudio(path, CancellationToken.None);
            yield return Await(() => load.IsCompleted);
            Assert.That(load.IsFaulted, Is.False, load.Exception?.ToString());
            using var lease = load.Result;
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var source = flow.Settings.catalog[0];
            var document = PupaDocument(source);
            var rules = flow.Rules.Snapshot;
            rules.Profile = JudgmentProfile.Sparring;
            rules.GaugeFailure = GaugeFailureMode.Continue;
            flow.Rules.Update(rules);
            flow.Library.Register(new PupaProvider(document, source, lease.Clip));
            flow.Selection.UpdateSelection(document.SongId, flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
            flow.PlaySelected();
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var clock = Object.FindFirstObjectByType<SongClock>();
            var audio = clock.GetComponent<AudioSource>();
            game.StartSession();
            yield return Await(() => clock.SongTime > .01 && audio.isPlaying);

            // Seek the real audio and its DSP epoch together; only the outro behavior is under test.
            double lastEnd = document.Notes.Max(note => note.EndTime);
            double seekTime = lastEnd - 2;
            audio.time = (float)seekTime;
            typeof(SongClock).GetProperty(nameof(SongClock.ScheduledStartDspTime))
                .SetValue(clock, AudioSettings.dspTime - seekTime);
            game.Session.Advance(clock.SongTime);
            double started = Time.realtimeSinceStartupAsDouble;

            while (!Object.FindFirstObjectByType<ResultsScreen>())
            {
                Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.LessThan(5),
                    "PUPA must not wait for its long audio tail after the last note.");
                yield return null;
            }

            yield return null;
            Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.GreaterThan(2));
            Assert.That(flow.LastResult.Chart.CompletionTime, Is.EqualTo(lastEnd + rules.Timing.GoodWindow + 1).Within(.001));
            Assert.That(flow.LastResult.Session.Resolved, Is.EqualTo(flow.LastResult.Session.TotalNotes));
            Assert.That(flow.LastResult.Notes.Count, Is.EqualTo(929));
            Assert.That(flow.LastResult.Notes.Sum(note => note.Judgments.Count),
                Is.EqualTo(flow.LastResult.Notes.Count + flow.LastResult.Notes.Count(note => note.Note.Kind == NoteKind.Hold)));
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
        }
    }
}
