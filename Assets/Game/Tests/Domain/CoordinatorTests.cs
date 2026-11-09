using System;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class CoordinatorTests
    {
        private sealed class FakeInput : ILaneInput
        {
            public int LaneCount => 4;

            public int Resets;

            public event Action<int, double> Pressed;
            public event Action<int, double> Released;
            public event Action StartRequested;

            public void ResetHeld() => Resets++;

            public void Start() => StartRequested?.Invoke();

            public void Press(int lane, double time) => Pressed?.Invoke(lane, time);

            public void Release(int lane, double time) => Released?.Invoke(lane, time);
        }

        private sealed class FakeClock : ISongClock, IInputTimingClock, IResumableSongClock,
            IPlaybackReadinessClock, IScheduledResumeClock
        {
            public bool ScheduleResume;
            public bool ResumeReady;
            public double ResumeTimestamp = 100;
            public bool ResumePending { get; private set; }
            public bool PlaybackReady { get; set; } = true;
            public double SongTime { get; set; }
            public bool Running { get; private set; }

            public int Schedules;

            public void Pause()
            {
                Running = false;
                ResumePending = false;
            }

            public void Resume()
            {
                if (ScheduleResume)
                {
                    ResumePending = true;
                    return;
                }
                Running = true;
            }

            public bool IsInputAfterResume(double timestamp) => timestamp >= ResumeTimestamp;

            public bool TryCompleteResume()
            {
                if (ResumePending && ResumeReady)
                {
                    ResumePending = false;
                    Running = true;
                }
                return Running;
            }

            public void CancelResume()
            {
                ResumePending = false;
            }

            public double InputTimingOffsetSeconds { get; set; }

            public void Schedule()
            {
                Running = true;
                SongTime = -.15;
                Schedules++;
            }

            public void Stop()
            {
                Running = false;
                ResumePending = false;
                SongTime = 0;
            }

            public double InputTimeToSongTime(double eventTime) =>
                eventTime - InputTimingOffsetSeconds;
        }

        private sealed class FakePresenter : INotePresenter
        {
            public int Clears;
            public IScrollTimeline Scroll;

            public void Initialize(ChartData chart, GameModeRules mode, IScrollTimeline scroll) =>
                Scroll = scroll;

            public void Render(RhythmSession session, double songTime) { }

            public void Reset() { }

            public void Clear() => Clears++;
        }

        [Test]
        public void GameOverStopsClockAndKeepsPresentationTimeUntilRestart()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            var presenter = new FakePresenter();
            var notes = new NoteData[12];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = NoteData.Tap(i % 4, .5 + i * .5);
            }

            var tempo = new TempoMap(new[] { new TempoPoint(0, 120) });
            using var c = new RhythmSessionCoordinator(
                new ChartData(notes, 8),
                new JudgmentSettings(.05, .11),
                new GameModeRules(4),
                tempo,
                "Test",
                "Normal",
                ScrollMode.Constant,
                1,
                new ConstantScrollTimeline(8, 1),
                input,
                clock,
                presenter
            );
            input.Start();
            clock.SongTime = 5.2;
            c.Tick();
            Assert.That(c.Session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(clock.Running, Is.False);

            double frozen = c.Snapshot.SongTime;
            clock.SongTime = 9;
            c.Tick();
            input.Press(0, 6);
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(frozen));
            Assert.That(c.Session.Miss, Is.EqualTo(10));
            input.Start();
            Assert.That(c.Session.Health, Is.EqualTo(100));
            Assert.That(clock.Running, Is.True);
        }

        [Test]
        public void UnavailableAudioBlocksInputStartResumeAndRestartUntilRecovered()
        {
            var input = new FakeInput();
            var clock = new FakeClock { PlaybackReady = false };
            using var c = Coordinator(input, clock, new FakePresenter());
            input.Start();
            Assert.That(clock.Schedules, Is.Zero);
            Assert.That(c.Session.State, Is.EqualTo(SessionState.Ready));
            clock.PlaybackReady = true;
            input.Start();
            clock.SongTime = 2;
            c.Pause();
            clock.PlaybackReady = false;
            c.Resume();
            c.Restart();
            input.Start();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(clock.Running, Is.False);
            Assert.That(clock.Schedules, Is.EqualTo(1));
            clock.PlaybackReady = true;
            c.Resume();
            Assert.That(c.IsPaused, Is.False);
            Assert.That(clock.Running, Is.True);
        }

        [Test]
        public void ScheduledResumeDefersReleaseAndSweepUntilAnchor()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(1, 5.2);
            clock.SongTime = 6.8;
            c.Pause();
            input.Release(1, 50);
            c.Resume();
            clock.SongTime = 100;
            c.Tick();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(c.Session.GetNoteJudgments(1).End.HasValue, Is.False);
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(6.8));
            clock.SongTime = 6.8;
            clock.ResumeReady = true;
            c.Tick();
            Assert.That(c.IsPaused, Is.False);
            Assert.That(c.Session.GetNoteJudgments(1).End.Value.ErrorSeconds, Is.Zero.Within(1e-9));
        }

        [Test]
        public void FirstPostAnchorInputCompletesResumeBeforeTheFrameTick()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true, ResumeTimestamp = 1 };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            clock.SongTime = .9;
            c.Pause();
            c.Resume();
            clock.ResumeReady = true;
            input.Press(0, .99);
            Assert.That(c.IsPaused, Is.True, "A delayed pre-anchor input remains paused input.");
            Assert.That(c.Session.Resolved, Is.Zero);
            input.Press(0, 1);
            Assert.That(c.IsPaused, Is.False);
            Assert.That(c.Session.Perfect, Is.EqualTo(1));
        }

        [Test]
        public void PauseCancelsPendingResumeAndCanPauseAClockThatCompletedBeforeCoordinator()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            clock.SongTime = .9;
            c.Pause();
            c.Resume();
            c.Pause();
            Assert.That(clock.ResumePending, Is.False);
            clock.ResumeReady = true;
            c.Tick();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(clock.Running, Is.False);
            c.Resume();
            clock.TryCompleteResume();
            Assert.That(clock.Running, Is.True);
            c.Pause();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(clock.Running, Is.False);
        }

        [Test]
        public void AudioInvalidationAfterClockAnchorKeepsCoordinatorAndClockPaused()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            clock.SongTime = .9;
            c.Pause();
            c.Resume();
            clock.ResumeReady = true;
            clock.TryCompleteResume();
            clock.PlaybackReady = false;
            c.Tick();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(clock.Running, Is.False);
            Assert.That(c.Session.Resolved, Is.Zero);
        }

        [Test]
        public void LatePreAnchorReleaseAfterFrameTickUsesFrozenPauseTime()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true, ResumeTimestamp = 6.8 };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(1, 5.2);
            clock.SongTime = 6.8;
            c.Pause();
            c.Resume();
            clock.ResumeReady = true;
            c.Tick();
            input.Release(1, 6.7);
            Assert.That(c.Session.GetNoteJudgments(1).End.Value.ErrorSeconds, Is.Zero.Within(1e-9));
        }

        [Test]
        public void TwentyScheduledPausesPreserveTheSameHoldJudgment()
        {
            var input = new FakeInput();
            var clock = new FakeClock { ScheduleResume = true, InputTimingOffsetSeconds = .2, ResumeTimestamp = 5.7 };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(0, 1.2);
            input.Press(1, 5.4);
            clock.SongTime = 5.7;
            for (int i = 0; i < 20; i++)
            {
                c.Pause();
                clock.ResumeReady = false;
                c.Resume();
                c.Tick();
                Assert.That(c.IsPaused, Is.True);
                clock.ResumeReady = true;
                c.Tick();
                Assert.That(c.IsPaused, Is.False);
            }
            input.Release(1, 7);
            Assert.That(c.Session.Perfect, Is.EqualTo(2));
        }

        [TestCase(.2, 1.2, JudgmentProfile.Warmup)]
        [TestCase(-.2, .8, JudgmentProfile.Warmup)]
        [TestCase(.2, 1.2, JudgmentProfile.Sparring)]
        [TestCase(-.2, .8, JudgmentProfile.Sparring)]
        public void CalibrationCorrectsInputAndDefersMissSweep(
            double offset,
            double inputTime,
            JudgmentProfile profile
        )
        {
            var input = new FakeInput();
            var clock = new FakeClock { InputTimingOffsetSeconds = offset };
            using var coordinator = Coordinator(
                input,
                clock,
                new FakePresenter(),
                rules: new PlayRules { Profile = profile }
            );
            input.Start();

            if (offset > 0)
            {
                clock.SongTime = 1.15;
            }
            else
            {
                clock.SongTime = .75;
            }

            coordinator.Tick();
            Assert.That(coordinator.Session.Miss, Is.Zero);
            input.Press(0, inputTime);
            Assert.That(coordinator.Session.Perfect, Is.EqualTo(1));
        }

        private static RhythmSessionCoordinator Coordinator(
            FakeInput input,
            FakeClock clock,
            FakePresenter presenter,
            ScrollMode mode = ScrollMode.Constant,
            double multiplier = 1,
            double good = .11,
            PlayRules rules = null
        )
        {
            var tempo = new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(6, 180) });
            IScrollTimeline scroll;

            if (mode == ScrollMode.Bpm)
            {
                scroll = new BpmScrollTimeline(tempo, 8, 120, multiplier);
            }
            else
            {
                scroll = new ConstantScrollTimeline(8, multiplier);
            }

            return new RhythmSessionCoordinator(
                new ChartData(new[] { NoteData.Tap(0, 1), NoteData.Hold(1, 5.2, 6.8) }, 8),
                new JudgmentSettings(.05, good),
                new GameModeRules(4),
                tempo,
                "Test",
                "Standard",
                mode,
                multiplier,
                scroll,
                input,
                clock,
                presenter,
                null,
                rules
            );
        }

        [Test]
        public void PausePreservesProgressAndHeldNotesWhileBlockingJudgmentsAndRestartInput()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(0, 1);
            clock.SongTime = 5.2;
            input.Press(1, 5.2);
            c.Tick();
            c.Pause();
            int resolved = c.Session.Resolved;
            input.Release(1, 6);
            input.Press(1, 6.1);
            input.Start();
            c.LoseFocus();
            c.Tick();
            Assert.That(c.IsPaused, Is.True);
            Assert.That(c.Session.IsHeld(1), Is.True);
            Assert.That(c.Session.Resolved, Is.EqualTo(resolved));
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(5.2));
            Assert.That(clock.Schedules, Is.EqualTo(1));
            c.Resume();
            input.Release(1, 6.8);
            Assert.That(c.Session.Perfect, Is.EqualTo(2));
            Assert.That(clock.Running, Is.True);
            c.Pause();
            c.Restart();
            Assert.That(c.IsPaused, Is.False);
            Assert.That(c.Session.Resolved, Is.Zero);
            Assert.That(clock.Schedules, Is.EqualTo(2));
        }

        [TestCase(.2, true)]
        [TestCase(-.2, true)]
        [TestCase(-.2, false)]
        public void AutomaticHoldResolvesAtTheUncalibratedSongTail(double offset, bool keyboardInput)
        {
            var input = new FakeInput();
            var clock = new FakeClock { InputTimingOffsetSeconds = offset };
            using var coordinator = Coordinator(input, clock, new FakePresenter(), rules: new PlayRules());
            coordinator.Start();

            if (keyboardInput)
            {
                input.Press(1, 5.2 + offset);
            }
            else
            {
                coordinator.Session.PressLane(1, 5.2);
            }

            clock.SongTime = 6.79;
            coordinator.Tick();
            Assert.That(coordinator.Session.GetNoteJudgments(1).End, Is.Null);

            clock.SongTime = 6.8;
            if (keyboardInput)
            {
                input.Release(1, clock.SongTime);
            }
            else
            {
                coordinator.Session.ReleaseLane(1, clock.SongTime);
            }

            coordinator.Tick();


            var end = coordinator.Session.GetNoteJudgments(1).End;
            Assert.That(end.HasValue, Is.True);
            Assert.That(end.Value.Grade, Is.EqualTo(Judgment.Perfect));
            Assert.That(end.Value.ErrorSeconds, Is.Zero);
            Assert.That(coordinator.Session.GetState(1), Is.EqualTo(NoteState.Completed));
        }

        [Test]
        public void PausedReleaseMatchesOrdinaryInputAtFrozenCalibratedTime(
            [Values(0, .2, -.2)] double offset,
            [Values(HoldEndMode.Release, HoldEndMode.Automatic)] HoldEndMode holdEnd,
            [Values(-.100001, -.1, -.05, 0, .05, .1, .100001)] double error
        )
        {
            var input = new FakeInput();
            var clock = new FakeClock { InputTimingOffsetSeconds = offset };
            using var c = Coordinator(input, clock, new FakePresenter(),
                rules: new PlayRules { HoldEnd = holdEnd });
            var ordinaryInput = new FakeInput();
            var ordinaryClock = new FakeClock { InputTimingOffsetSeconds = offset };
            using var ordinary = Coordinator(ordinaryInput, ordinaryClock, new FakePresenter(),
                rules: new PlayRules { HoldEnd = holdEnd });
            c.Start();
            ordinary.Start();
            input.Press(0, 1 + offset);
            ordinaryInput.Press(0, 1 + offset);
            input.Press(1, 5.2 + offset);
            ordinaryInput.Press(1, 5.2 + offset);
            clock.SongTime = 6.8 + error + offset;
            ordinaryInput.Release(1, clock.SongTime);

            c.Pause();
            int resolved = c.Session.Resolved;
            double frozen = c.Snapshot.SongTime;
            clock.InputTimingOffsetSeconds = .07 - offset;
            clock.SongTime = 900;
            input.Release(1, 300);
            input.Release(1, 600);
            c.Tick();
            Assert.That(c.Session.Resolved, Is.EqualTo(resolved));
            Assert.That(c.Session.IsHeld(1), Is.True);
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(frozen));

            c.Resume();
            int resumedResolved = c.Session.Resolved;
            c.Resume();
            Assert.That(c.Session.Resolved, Is.EqualTo(resumedResolved));
            Assert.That(c.Session.IsHeld(1), Is.False);
            clock.InputTimingOffsetSeconds = offset;
            clock.SongTime = 9;
            ordinaryClock.SongTime = 9;
            c.Tick();
            ordinary.Tick();

            Assert.That(c.Session.Perfect, Is.EqualTo(ordinary.Session.Perfect));
            Assert.That(c.Session.Good, Is.EqualTo(ordinary.Session.Good));
            Assert.That(c.Session.Miss, Is.EqualTo(ordinary.Session.Miss));
            Assert.That(c.Session.GetState(1), Is.EqualTo(ordinary.Session.GetState(1)));
            var expected = ordinary.Session.GetNoteJudgments(1).End;
            var actual = c.Session.GetNoteJudgments(1).End;
            Assert.That(expected.HasValue, Is.True);
            Assert.That(actual.HasValue, Is.True);
            Assert.That(actual.Value.Grade, Is.EqualTo(expected.Value.Grade));
            Assert.That(actual.Value.ErrorSeconds, Is.EqualTo(expected.Value.ErrorSeconds).Within(1e-9));
        }

        [TestCase(0)]
        [TestCase(.2)]
        [TestCase(-.2)]
        public void PressDuringPauseCancelsPendingReleaseAndNextPauseUsesNewTime(double offset)
        {
            var input = new FakeInput();
            var clock = new FakeClock { InputTimingOffsetSeconds = offset };
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(0, 1 + offset);
            input.Press(1, 5.2 + offset);
            clock.SongTime = 5.5 + offset;
            c.Pause();
            input.Release(1, 100);
            input.Press(1, 200);
            c.Resume();
            Assert.That(c.Session.IsHeld(1), Is.True);
            Assert.That(c.Session.GetNoteJudgments(1).End.HasValue, Is.False);

            clock.SongTime = 6.8 + offset;
            c.Pause();
            input.Release(1, 300);
            c.Resume();
            Assert.That(c.Session.Perfect, Is.EqualTo(2));
            Assert.That(c.Session.IsHeld(1), Is.False);
        }

        [Test]
        public void RestartDiscardsPendingReleaseBeforeAnotherHoldAndPause()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            using var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(1, 5.2);
            clock.SongTime = 5.5;
            c.Pause();
            input.Release(1, 100);
            c.Restart();
            input.Press(1, 5.2);
            clock.SongTime = 5.5;
            c.Pause();
            c.Resume();
            Assert.That(c.Session.IsHeld(1), Is.True);
            Assert.That(c.Session.GetNoteJudgments(1).End.HasValue, Is.False);
        }

        [Test]
        public void DisposalDiscardsPendingReleaseAndCannotResumeOrRestart()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            var c = Coordinator(input, clock, new FakePresenter());
            c.Start();
            input.Press(1, 5.2);
            clock.SongTime = 6.8;
            c.Pause();
            input.Release(1, 100);
            c.Dispose();
            int resolved = c.Session.Resolved;
            input.Release(1, 200);
            c.Resume();
            c.Restart();
            c.Tick();
            Assert.That(c.Session.Resolved, Is.EqualTo(resolved));
            Assert.That(c.Session.GetNoteJudgments(1).End.HasValue, Is.False);
            Assert.That(clock.Running, Is.False);
            Assert.That(clock.Schedules, Is.EqualTo(1));
        }

        [TestCase(ScrollMode.Constant, .5)]
        [TestCase(ScrollMode.Constant, 2)]
        [TestCase(ScrollMode.Bpm, .5)]
        [TestCase(ScrollMode.Bpm, 2)]
        public void VisualPolicyNeverChangesJudgments(ScrollMode mode, double multiplier)
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            var presenter = new FakePresenter();
            using var c = Coordinator(input, clock, presenter, mode, multiplier);
            input.Start();
            input.Press(0, 1);
            input.Release(0, 1.01);
            input.Press(1, 5.2);
            input.Release(1, 6.8);
            clock.SongTime = 8;
            c.Tick();
            Assert.That(c.Snapshot.Session.Perfect, Is.EqualTo(2));
            Assert.That(c.Snapshot.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(8));
            Assert.That(clock.Running, Is.False);
            c.Tick();
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(8));
        }

        [Test]
        public void RestartAndFocusLossClearDisplayAndLogicalState()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            var presenter = new FakePresenter();
            using var c = Coordinator(input, clock, presenter);
            int resets = 0;
            c.ResetOccurred += () => resets++;
            input.Start();
            input.Press(0, 1);
            c.LoseFocus();
            Assert.That(c.Snapshot.ReadyReason, Is.EqualTo(ReadyReason.FocusLost));
            Assert.That(c.Session.State, Is.EqualTo(SessionState.Ready));
            Assert.That(c.Session.Resolved, Is.Zero);
            Assert.That(c.IsHeld(0), Is.False);
            Assert.That(clock.Running, Is.False);
            input.Start();
            Assert.That(clock.Schedules, Is.EqualTo(2));
            Assert.That(resets, Is.EqualTo(3));
            Assert.That(c.Snapshot.ScrollMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void DisposalUnsubscribesAndStopsDependencies()
        {
            var input = new FakeInput();
            var clock = new FakeClock();
            var presenter = new FakePresenter();
            var c = Coordinator(input, clock, presenter);
            input.Start();
            c.Dispose();
            c.Dispose();
            input.Start();
            input.Press(0, 1);
            Assert.That(clock.Schedules, Is.EqualTo(1));
            Assert.That(clock.Running, Is.False);
            Assert.That(presenter.Clears, Is.EqualTo(1));
            Assert.That(c.Session.Resolved, Is.Zero);
        }

        [Test]
        public void DifficultyChangesJudgmentButKeepsScrollDistance()
        {
            var i = new FakeInput();
            var k = new FakeClock();
            var p = new FakePresenter();
            using var easy = Coordinator(i, k, p, ScrollMode.Bpm, 1, .16);
            i.Start();
            i.Press(0, 1.14);
            Assert.That(easy.Session.Good, Is.EqualTo(1));

            double distance = p.Scroll.DistanceBetween(5, 7);
            var i2 = new FakeInput();
            var k2 = new FakeClock();
            var p2 = new FakePresenter();
            using var standard = Coordinator(i2, k2, p2);
            i2.Start();
            i2.Press(0, 1.14);
            Assert.That(standard.Session.Miss, Is.EqualTo(1));

            using var sameScroll = Coordinator(
                new FakeInput(),
                new FakeClock(),
                p2,
                ScrollMode.Bpm,
                1,
                .11
            );
            Assert.That(p2.Scroll.DistanceBetween(5, 7), Is.EqualTo(distance));
        }
    }
}
