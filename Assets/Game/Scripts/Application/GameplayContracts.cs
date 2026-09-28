using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public interface ILaneInput
    {
        int LaneCount { get; }
        event Action<int, double> Pressed;
        event Action<int, double> Released;
        event Action StartRequested;
        void ResetHeld();
    }
    public interface ISongClock
    {
        double SongTime { get; }
        bool Running { get; }
        void Schedule();
        void Stop();
        double InputTimeToSongTime(double eventTime);
    }
    public interface INotePresenter
    {
        void Initialize(ChartData chart, GameModeRules mode, IScrollTimeline scroll);
        void Render(RhythmSession session, double songTime);
        void Reset();
        void Clear();
    }
    public enum ReadyReason { Initial, FocusLost }
    public readonly struct GameplaySnapshot
    {
        public SessionSnapshot Session { get; }
        public string SongTitle { get; }
        public string DifficultyName { get; }
        public ScrollMode ScrollMode { get; }
        public double ScrollMultiplier { get; }
        public double SongTime { get; }
        public double CurrentBpm { get; }
        public ReadyReason ReadyReason { get; }
        public GameplaySnapshot(SessionSnapshot session, string songTitle, string difficultyName, ScrollMode scrollMode,
            double multiplier, double time, double bpm, ReadyReason reason)
        {
            Session = session; SongTitle = songTitle; DifficultyName = difficultyName; ScrollMode = scrollMode;
            ScrollMultiplier = multiplier; SongTime = time; CurrentBpm = bpm; ReadyReason = reason;
        }
    }
    public interface IGameplayReadModel
    {
        GameplaySnapshot Snapshot { get; }
        int LaneCount { get; }
        bool IsHeld(int lane);
        NoteState GetNoteState(int index);
        event Action<JudgmentEvent> Judged;
        event Action<HoldStartedEvent> HoldStarted;
        event Action ResetOccurred;
    }
}
