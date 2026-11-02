using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public sealed class RhythmSessionCoordinator
        : IGameplayReadModel,
            IFailureReadModel,
            IDisposable
    {
        private readonly ILaneInput input;
        private readonly ISongClock clock;
        private readonly INotePresenter presenter;
        private readonly TempoMap tempo;
        private readonly string title,
            difficulty;
        private readonly ScrollMode scrollMode;
        private readonly double multiplier;
        private double songTime;
        private double pausedInputTime;
        private bool resumeRequested;
        private bool resumeInputCutoff;
        private ReadyReason reason;
        private bool disposed;
        public bool IsPaused { get; private set; }
        private readonly System.Collections.Generic.HashSet<int> pausedReleases =
            new System.Collections.Generic.HashSet<int>();

        public RhythmSession Session { get; }

        public int LaneCount => Session.LaneCount;

        public GameplaySnapshot Snapshot =>
            new GameplaySnapshot(
                Session.Snapshot,
                title,
                difficulty,
                scrollMode,
                multiplier,
                songTime,
                tempo.BpmAt(songTime),
                reason
            );

        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public event Action ResetOccurred;
        public event Action<FailureSnapshot> Failed;

        public RhythmSessionCoordinator(
            ChartData chart,
            JudgmentSettings settings,
            GameModeRules mode,
            TempoMap tempo,
            string title,
            string difficulty,
            ScrollMode scrollMode,
            double multiplier,
            IScrollTimeline scroll,
            ILaneInput input,
            ISongClock clock,
            INotePresenter presenter
        )
            : this(
                chart,
                settings,
                mode,
                tempo,
                title,
                difficulty,
                scrollMode,
                multiplier,
                scroll,
                input,
                clock,
                presenter,
                null
            ) { }

        public RhythmSessionCoordinator(
            ChartData chart,
            JudgmentSettings settings,
            GameModeRules mode,
            TempoMap tempo,
            string title,
            string difficulty,
            ScrollMode scrollMode,
            double multiplier,
            IScrollTimeline scroll,
            ILaneInput input,
            ISongClock clock,
            INotePresenter presenter,
            HardModeHealthRules healthRules,
            PlayRules rules = null
        )
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            this.tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));

            if (input.LaneCount != mode.LaneCount)
            {
                throw new ArgumentException("Input and mode lane counts differ.");
            }

            this.title = title;
            this.difficulty = difficulty;
            this.scrollMode = scrollMode;
            this.multiplier = multiplier;

            if (rules == null)
            {
                Session = new RhythmSession(chart, settings, mode, healthRules);
            }
            else
            {
                Session = new RhythmSession(chart, mode, tempo, rules);
            }

            presenter.Initialize(
                chart,
                mode,
                scroll ?? throw new ArgumentNullException(nameof(scroll))
            );
            presenter.Render(Session, songTime);
            Session.Judged += OnJudged;
            Session.HoldStarted += OnHoldStarted;
            Session.Failed += OnFailed;
            input.Pressed += Press;
            input.Released += Release;
            input.StartRequested += Start;
        }

        public bool IsHeld(int lane) => Session.IsHeld(lane);

        public NoteState GetNoteState(int index) => Session.GetState(index);

        private void OnJudged(JudgmentEvent result)
        {
            FreezeGameOver();
            Judged?.Invoke(result);
        }

        private void FreezeGameOver()
        {
            if (Session.State != SessionState.GameOver || !clock.Running)
            {
                return;
            }

            songTime = clock.SongTime;

            if (clock is IPausableSongClock pausable)
            {
                pausable.Pause();
            }
            else
            {
                clock.Stop();
            }

            input.ResetHeld();
        }

        private void OnHoldStarted(HoldStartedEvent result) => HoldStarted?.Invoke(result);

        private void OnFailed(FailureSnapshot result) => Failed?.Invoke(result);

        private void Press(int lane, double timestamp)
        {
            if (disposed)
            {
                return;
            }

            CompleteResumeForInput(timestamp);
            if (disposed)
            {
                return;
            }

            if (IsPaused)
            {
                pausedReleases.Remove(lane);
                return;
            }

            if (clock.Running)
            {
                if (resumeInputCutoff && !((IScheduledResumeClock)clock).IsInputAfterResume(timestamp))
                {
                    return;
                }
                Session.PressLane(lane, clock.InputTimeToSongTime(timestamp));
            }
        }

        private void Release(int lane, double timestamp)
        {
            if (disposed)
            {
                return;
            }

            CompleteResumeForInput(timestamp);
            if (disposed)
            {
                return;
            }

            if (IsPaused)
            {
                pausedReleases.Add(lane);
                return;
            }

            if (clock.Running)
            {
                if (resumeInputCutoff && !((IScheduledResumeClock)clock).IsInputAfterResume(timestamp))
                {
                    Session.ReleaseLane(lane, pausedInputTime);
                    return;
                }
                Session.ReleaseLane(lane, clock.InputTimeToSongTime(timestamp));
            }
        }

        public void Start()
        {
            if (disposed || IsPaused)
            {
                return;
            }

            if (clock is IPlaybackReadinessClock readiness && !readiness.PlaybackReady)
            {
                return;
            }

            clock.Stop();
            pausedReleases.Clear();
            resumeRequested = false;
            resumeInputCutoff = false;
            Session.Reset();
            input.ResetHeld();
            presenter.Reset();
            reason = ReadyReason.Initial;
            songTime = 0;
            ResetOccurred?.Invoke();
            clock.Schedule();
            Session.Start();
            songTime = clock.SongTime;
            presenter.Render(Session, songTime);
        }

        public void Tick()
        {
            if (disposed)
            {
                return;
            }

            if (IsPaused)
            {
                if (!TryCompleteScheduledResume())
                {
                    return;
                }
            }

            // Input System Dynamic dispatch precedes the Unity controller's timeout sweep.
            if (clock.Running)
            {
                songTime = clock.SongTime;
            }

            double sweepTime;

            if (clock is IInputTimingClock calibrated)
            {
                sweepTime = songTime - Math.Max(0, calibrated.InputTimingOffsetSeconds);
            }
            else
            {
                sweepTime = songTime - 0;
            }

            Session.Advance(sweepTime);
            FreezeGameOver();
            presenter.Render(Session, songTime);

            if (Session.State == SessionState.Completed && clock.Running)
            {
                clock.Stop();
            }
        }

        public void LoseFocus()
        {
            if (disposed || IsPaused || Session.State != SessionState.Playing)
            {
                return;
            }

            clock.Stop();
            Session.Reset();
            input.ResetHeld();
            presenter.Reset();
            songTime = 0;
            reason = ReadyReason.FocusLost;
            ResetOccurred?.Invoke();
            presenter.Render(Session, songTime);
        }

        public void Pause()
        {
            if (disposed || Session.State != SessionState.Playing)
            {
                return;
            }

            if (IsPaused)
            {
                TryCompleteScheduledResume();
            }

            if (disposed)
            {
                return;
            }

            if (IsPaused)
            {
                resumeRequested = false;
                if (clock is IScheduledResumeClock scheduled)
                {
                    scheduled.CancelResume();
                }
                return;
            }

            if (clock is IResumableSongClock pausable)
            {
                resumeInputCutoff = false;
                songTime = clock.SongTime;
                double timingOffsetSeconds = 0;

                if (clock is IInputTimingClock calibrated)
                {
                    timingOffsetSeconds = calibrated.InputTimingOffsetSeconds;
                }

                pausedInputTime = InputTiming.Calibrate(songTime, timingOffsetSeconds);
                pausable.Pause();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (disposed || !IsPaused)
            {
                return;
            }

            if (clock is IPlaybackReadinessClock readiness && !readiness.PlaybackReady)
            {
                return;
            }

            if (resumeRequested)
            {
                return;
            }

            ((IResumableSongClock)clock).Resume();
            if (clock is IScheduledResumeClock scheduled && scheduled.ResumePending)
            {
                resumeRequested = true;
                return;
            }

            if (!clock.Running)
            {
                return;
            }

            CompleteResume();
        }

        private void CompleteResume()
        {
            resumeInputCutoff = resumeRequested;
            resumeRequested = false;
            IsPaused = false;
            while (pausedReleases.Count > 0)
            {
                int lane;

                using (var pending = pausedReleases.GetEnumerator())
                {
                    pending.MoveNext();
                    lane = pending.Current;
                }

                // Remove before notifying judgment subscribers, which may restart or dispose us.
                pausedReleases.Remove(lane);

                if (Session.IsHeld(lane))
                {
                    Session.ReleaseLane(lane, pausedInputTime);
                }

                if (disposed || IsPaused)
                {
                    return;
                }
            }
        }

        private void CompleteResumeForInput(double timestamp)
        {
            if (!IsPaused || !resumeRequested)
            {
                return;
            }

            if (((IScheduledResumeClock)clock).IsInputAfterResume(timestamp))
            {
                TryCompleteScheduledResume();
            }
        }

        private bool TryCompleteScheduledResume()
        {
            if (!resumeRequested)
            {
                return false;
            }

            var scheduled = (IScheduledResumeClock)clock;
            if (clock is IPlaybackReadinessClock readiness && !readiness.PlaybackReady)
            {
                if (clock.Running)
                {
                    ((IResumableSongClock)clock).Pause();
                }
                scheduled.CancelResume();
                resumeRequested = false;
                return false;
            }

            if (!scheduled.TryCompleteResume())
            {
                if (!scheduled.ResumePending)
                {
                    resumeRequested = false;
                }
                return false;
            }

            CompleteResume();
            return !disposed && !IsPaused;
        }

        public void Restart()
        {
            if (disposed)
            {
                return;
            }

            if (clock is IPlaybackReadinessClock readiness && !readiness.PlaybackReady)
            {
                return;
            }

            IsPaused = false;
            resumeRequested = false;
            pausedReleases.Clear();
            Start();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            IsPaused = false;
            resumeRequested = false;
            pausedReleases.Clear();
            input.Pressed -= Press;
            input.Released -= Release;
            input.StartRequested -= Start;
            Session.Judged -= OnJudged;
            Session.HoldStarted -= OnHoldStarted;
            Session.Failed -= OnFailed;
            clock.Stop();
            presenter.Clear();
        }
    }
}
