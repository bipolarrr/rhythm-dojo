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
            public event Action<int,double> Pressed;
            public event Action<int,double> Released;
            public event Action StartRequested;
            public void ResetHeld() => Resets++;
            public void Start() => StartRequested?.Invoke();
            public void Press(int lane, double time) => Pressed?.Invoke(lane,time);
            public void Release(int lane, double time) => Released?.Invoke(lane,time);
        }
        private sealed class FakeClock : ISongClock
        {
            public double SongTime { get; set; }
            public bool Running { get; private set; }
            public int Schedules;
            public void Schedule() { Running = true; SongTime = -.15; Schedules++; }
            public void Stop() { Running = false; SongTime = 0; }
            public double InputTimeToSongTime(double eventTime) => eventTime;
        }
        private sealed class FakePresenter : INotePresenter
        {
            public int Clears;
            public IScrollTimeline Scroll;
            public void Initialize(ChartData chart, GameModeRules mode, IScrollTimeline scroll) => Scroll = scroll;
            public void Render(RhythmSession session, double songTime) { }
            public void Reset() { }
            public void Clear() => Clears++;
        }
        private static RhythmSessionCoordinator Coordinator(FakeInput input, FakeClock clock, FakePresenter presenter,
            ScrollMode mode = ScrollMode.Constant, double multiplier = 1, double good = .11)
        {
            var tempo = new TempoMap(new[] { new TempoPoint(0,120), new TempoPoint(6,180) });
            IScrollTimeline scroll = mode == ScrollMode.Bpm ? new BpmScrollTimeline(tempo,8,120,multiplier) : new ConstantScrollTimeline(8,multiplier);
            return new RhythmSessionCoordinator(new ChartData(new[] { NoteData.Tap(0,1), NoteData.Hold(1,5.2,6.8) },8),
                new JudgmentSettings(.05,good),new GameModeRules(4),tempo,"Test","Standard",mode,multiplier,scroll,input,clock,presenter);
        }
        [TestCase(ScrollMode.Constant,.5)] [TestCase(ScrollMode.Constant,2)]
        [TestCase(ScrollMode.Bpm,.5)] [TestCase(ScrollMode.Bpm,2)]
        public void VisualPolicyNeverChangesJudgments(ScrollMode mode,double multiplier)
        {
            var input = new FakeInput(); var clock = new FakeClock(); var presenter = new FakePresenter();
            using var c = Coordinator(input,clock,presenter,mode,multiplier); input.Start();
            input.Press(0,1); input.Release(0,1.01); input.Press(1,5.2); input.Release(1,6.8);
            clock.SongTime = 8; c.Tick(); Assert.That(c.Snapshot.Session.Perfect, Is.EqualTo(2));
            Assert.That(c.Snapshot.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(c.Snapshot.SongTime, Is.EqualTo(8)); Assert.That(clock.Running, Is.False);
            c.Tick(); Assert.That(c.Snapshot.SongTime, Is.EqualTo(8));
        }
        [Test]
        public void RestartAndFocusLossClearDisplayAndLogicalState()
        {
            var input = new FakeInput(); var clock = new FakeClock(); var presenter = new FakePresenter();
            using var c = Coordinator(input,clock,presenter); int resets=0; c.ResetOccurred += () => resets++;
            input.Start(); input.Press(0,1); c.LoseFocus();
            Assert.That(c.Snapshot.ReadyReason, Is.EqualTo(ReadyReason.FocusLost));
            Assert.That(c.Session.State, Is.EqualTo(SessionState.Ready)); Assert.That(c.Session.Resolved, Is.Zero);
            Assert.That(c.IsHeld(0), Is.False); Assert.That(clock.Running, Is.False);
            input.Start(); Assert.That(clock.Schedules, Is.EqualTo(2)); Assert.That(resets, Is.EqualTo(3));
            Assert.That(c.Snapshot.ScrollMultiplier, Is.EqualTo(1));
        }
        [Test]
        public void DisposalUnsubscribesAndStopsDependencies()
        {
            var input = new FakeInput(); var clock = new FakeClock(); var presenter = new FakePresenter();
            var c = Coordinator(input,clock,presenter); input.Start(); c.Dispose(); c.Dispose();
            input.Start(); input.Press(0,1);
            Assert.That(clock.Schedules, Is.EqualTo(1)); Assert.That(clock.Running, Is.False);
            Assert.That(presenter.Clears, Is.EqualTo(1)); Assert.That(c.Session.Resolved, Is.Zero);
        }
        [Test]
        public void DifficultyChangesJudgmentButKeepsScrollDistance()
        {
            var i = new FakeInput(); var k = new FakeClock(); var p = new FakePresenter();
            using var easy = Coordinator(i,k,p,ScrollMode.Bpm,1,.16); i.Start(); i.Press(0,1.14);
            Assert.That(easy.Session.Good, Is.EqualTo(1)); double distance = p.Scroll.DistanceBetween(5,7);
            var i2 = new FakeInput(); var k2 = new FakeClock(); var p2 = new FakePresenter();
            using var standard = Coordinator(i2,k2,p2); i2.Start(); i2.Press(0,1.14);
            Assert.That(standard.Session.Miss, Is.EqualTo(1));
            using var sameScroll = Coordinator(new FakeInput(),new FakeClock(),p2,ScrollMode.Bpm,1,.11);
            Assert.That(p2.Scroll.DistanceBetween(5,7), Is.EqualTo(distance));
        }
    }
}
