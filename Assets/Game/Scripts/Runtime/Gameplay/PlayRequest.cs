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
        public HardModeHealthRules HardModeRules { get; }

        public bool IsHardMode => HardModeRules != null;

        private readonly PlayRules rules;

        public PlayRules Rules => rules?.Copy();

        public PlayRequest(
            PlayableSong song,
            DifficultyProfile difficulty,
            ScrollMode scrollMode,
            double multiplier,
            int timingOffsetMs = 0
        )
            : this(song, difficulty, scrollMode, multiplier, timingOffsetMs, null) { }

        public PlayRequest(
            PlayableSong song,
            DifficultyProfile difficulty,
            ScrollMode scrollMode,
            double multiplier,
            int timingOffsetMs,
            HardModeHealthRules healthRules,
            PlayRules rules = null
        )
        {
            rules?.Validate();
            this.rules = rules?.Copy();
            HardModeRules = healthRules;

            if (song == null || !difficulty)
            {
                throw new System.ArgumentNullException("Selected song/difficulty missing.");
            }

            song.Validate();
            difficulty.Validate();

            if (
                !System.Enum.IsDefined(typeof(ScrollMode), scrollMode)
                || !double.IsFinite(multiplier)
            )
            {
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            }

            if (multiplier <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            }

            Song = song;
            Difficulty = difficulty;
            ScrollMode = scrollMode;
            Multiplier = multiplier;
            TimingOffsetMs = UnityEngine.Mathf.Clamp(timingOffsetMs, -200, 200);
        }
    }
}
