using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public sealed class RhythmSessionCoordinator : IGameplayReadModel, IFailureReadModel, IDisposable
    {
        private readonly ILaneInput input;
        private readonly ISongClock clock;
        private readonly INotePresenter presenter;
        private readonly TempoMap tempo;
        private readonly string title, difficulty;
        private readonly ScrollMode scrollMode;
        private readonly double multiplier;
        private double songTime;
        private ReadyReason reason;
        private bool disposed;
        public RhythmSession Session { get; }
        public int LaneCount => Session.LaneCount;
        public GameplaySnapshot Snapshot => new GameplaySnapshot(Session.Snapshot, title, difficulty, scrollMode,
            multiplier, songTime, tempo.BpmAt(songTime), reason);
        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public event Action ResetOccurred;
        public event Action<FailureSnapshot> Failed;
        public RhythmSessionCoordinator(ChartData chart, JudgmentSettings settings, GameModeRules mode,
            TempoMap tempo, string title, string difficulty, ScrollMode scrollMode, double multiplier,
            IScrollTimeline scroll, ILaneInput input, ISongClock clock, INotePresenter presenter)
            : this(chart, settings, mode, tempo, title, difficulty, scrollMode, multiplier, scroll, input, clock, presenter, null) { }
        public RhythmSessionCoordinator(ChartData chart, JudgmentSettings settings, GameModeRules mode,
            TempoMap tempo, string title, string difficulty, ScrollMode scrollMode, double multiplier,
            IScrollTimeline scroll, ILaneInput input, ISongClock clock, INotePresenter presenter, HardModeHealthRules healthRules, PlayRules rules = null)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            this.tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
            if (input.LaneCount != mode.LaneCount) throw new ArgumentException("Input and mode lane counts differ.");
            this.title = title; this.difficulty = difficulty; this.scrollMode = scrollMode; this.multiplier = multiplier;
            Session = rules == null ? new RhythmSession(chart, settings, mode, healthRules) : new RhythmSession(chart, mode, tempo, rules);
            presenter.Initialize(chart, mode, scroll ?? throw new ArgumentNullException(nameof(scroll)));
            presenter.Render(Session, songTime);
            Session.Judged += OnJudged; Session.HoldStarted += OnHoldStarted;
            Session.Failed += OnFailed;
            input.Pressed += Press; input.Released += Release; input.StartRequested += Start;
        }
        public bool IsHeld(int lane) => Session.IsHeld(lane);
        public NoteState GetNoteState(int index) => Session.GetState(index);
        private void OnJudged(JudgmentEvent result)
        {
            FreezeGameOver(); Judged?.Invoke(result);
        }
        private void FreezeGameOver()
        {
            if (Session.State != SessionState.GameOver || !clock.Running) return;
            songTime = clock.SongTime;
            if (clock is IPausableSongClock pausable) pausable.Pause(); else clock.Stop();
            input.ResetHeld();
        }
        private void OnHoldStarted(HoldStartedEvent result) => HoldStarted?.Invoke(result);
        private void OnFailed(FailureSnapshot result) => Failed?.Invoke(result);
        private void Press(int lane, double timestamp)
        { if (!disposed && clock.Running) Session.PressLane(lane, clock.InputTimeToSongTime(timestamp)); }
        private void Release(int lane, double timestamp)
        { if (!disposed && clock.Running) Session.ReleaseLane(lane, clock.InputTimeToSongTime(timestamp)); }
        public void Start()
        {
            if (disposed) return;
            clock.Stop(); Session.Reset(); input.ResetHeld(); presenter.Reset();
            reason = ReadyReason.Initial; songTime = 0; ResetOccurred?.Invoke();
            clock.Schedule(); Session.Start(); songTime = clock.SongTime;
            presenter.Render(Session, songTime);
        }
        public void Tick()
        {
            if (disposed) return;
            // Input System Dynamic dispatch precedes the Unity controller's timeout sweep.
            if (clock.Running) songTime = clock.SongTime;
            double sweepTime = songTime - (clock is IInputTimingClock calibrated ? Math.Max(0, calibrated.InputTimingOffsetSeconds) : 0);
            Session.Advance(sweepTime); FreezeGameOver(); presenter.Render(Session, songTime);
            if (Session.State == SessionState.Completed && clock.Running) clock.Stop();
        }
        public void LoseFocus()
        {
            if (disposed || Session.State != SessionState.Playing) return;
            clock.Stop(); Session.Reset(); input.ResetHeld(); presenter.Reset();
            songTime = 0; reason = ReadyReason.FocusLost; ResetOccurred?.Invoke();
            presenter.Render(Session, songTime);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            input.Pressed -= Press; input.Released -= Release; input.StartRequested -= Start;
            Session.Judged -= OnJudged; Session.HoldStarted -= OnHoldStarted;
            Session.Failed -= OnFailed;
            clock.Stop(); presenter.Clear();
        }
    }
}
