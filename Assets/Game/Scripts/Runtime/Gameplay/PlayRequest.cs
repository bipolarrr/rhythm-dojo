using RhythmDojo.Application;
using RhythmDojo.Content;

namespace RhythmDojo.Gameplay
{
    public sealed class PlayRequest
    {
        public PlayableSong Song { get; }
        public DifficultyProfile Difficulty { get; }
        public ScrollMode ScrollMode { get; }
        public double Multiplier { get; }
        public int TimingOffsetMs { get; }
        public PlayRequest(PlayableSong song, DifficultyProfile difficulty, ScrollMode scrollMode, double multiplier, int timingOffsetMs = 0)
        {
            if (song == null || !difficulty) throw new System.ArgumentNullException("Selected song/difficulty missing.");
            song.Validate(); difficulty.Validate();
            if (!System.Enum.IsDefined(typeof(ScrollMode), scrollMode) || !double.IsFinite(multiplier) || multiplier <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            Song = song; Difficulty = difficulty; ScrollMode = scrollMode; Multiplier = multiplier;
            TimingOffsetMs = UnityEngine.Mathf.Clamp(timingOffsetMs, -200, 200);
        }
    }
}

