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
    public sealed class PupaResultsFlowTests : GameplayTestContext
    {
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
        public IEnumerator RealPupaAudioFinishesAndAutomaticallyLoadsResults()
        {
            string path = System.IO.Path.GetFullPath("LocalAudio/PUPA.wav");

            if (!System.IO.File.Exists(path))
            {
                Assert.Ignore("Requires the separately supplied LocalAudio/PUPA.wav.");
            }

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

            if (gameView)
            {
                gameView.Focus();
            }

            yield return null;
#endif
            double started = Time.realtimeSinceStartupAsDouble;
            double deadline = started + game.Chart.CompletionTime + 15;
            game.StartSession();

            while (!Object.FindFirstObjectByType<ResultsScreen>())
            {
                if (game && game.Session != null && game.Session.State == SessionState.Ready)
                {
                    Assert.Fail("PUPA playback was reset before completion: " + game.ReadModel.Snapshot.ReadyReason);
                }

                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline),
                    "PUPA audio ended without automatic result transition.");
                yield return null;
            }

            yield return null;
            Assert.That(Time.realtimeSinceStartupAsDouble - started,
                Is.GreaterThan(document.Notes.Max(note => note.EndTime)));
            Assert.That(Time.realtimeSinceStartupAsDouble - started,
                Is.LessThan(lease.Clip.length - 6), "Trailing audio must not delay Results.");
            Assert.That(flow.LastResult.Title, Is.EqualTo("PUPA"));
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(flow.LastResult.Session.Resolved, Is.EqualTo(flow.LastResult.Session.TotalNotes));
            Assert.That(flow.LastResult.Notes.Count, Is.EqualTo(929));
            Assert.That(Object.FindFirstObjectByType<SongClock>(), Is.Null);
        }

        [UnityTest]
        [Category("LocalAudio")]
        public IEnumerator CorrectedPupaClockKeepsAudioNotesAndTimestampedInputTogether()
        {
            string path = System.IO.Path.GetFullPath("LocalAudio/PUPA.wav");

            if (!System.IO.File.Exists(path))
            {
                Assert.Ignore("Requires the separately supplied LocalAudio/PUPA.wav.");
            }

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
        public IEnumerator PupaAudioTailTransitionsPromptlyWithEveryJudgmentPreserved()
        {
            string path = System.IO.Path.GetFullPath("LocalAudio/PUPA.wav");

            if (!System.IO.File.Exists(path))
            {
                Assert.Ignore("Requires the separately supplied LocalAudio/PUPA.wav.");
            }

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
