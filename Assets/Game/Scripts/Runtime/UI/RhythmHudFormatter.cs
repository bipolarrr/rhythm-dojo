using RhythmDojo.Application;

namespace RhythmDojo.UI
{
    public static class RhythmHudFormatter
    {
        public static string Format(GameplaySnapshot snapshot, string recent)
        {
            var s = snapshot.Session;
            string mode = snapshot.ScrollMode == ScrollMode.Bpm ? "BPM" : "Constant";
            string prompt = s.State == Gameplay.SessionState.Ready ?
                (snapshot.ReadyReason == ReadyReason.FocusLost ? "Focus lost. Press SPACE to restart." : "Press SPACE to start.") : recent;
            return $"RHYTHM DOJO / {s.State}\n{snapshot.SongTitle}\n" +
                $"Judgment: {snapshot.DifficultyName} | Scroll: {mode} {snapshot.ScrollMultiplier:0.##}x\n\n" +
                $"COMBO {s.Combo}    {prompt}\nPerfect {s.Perfect} / Good {s.Good} / Miss {s.Miss}\n" +
                $"Resolved {s.Resolved}/{s.TotalNotes}  Song {snapshot.SongTime:F2}s  BPM {snapshot.CurrentBpm:0.##}\n\n" +
                "SPACE start / restart | ESC song list\nHold the long notes; release at the tail.";
        }
    }
}
