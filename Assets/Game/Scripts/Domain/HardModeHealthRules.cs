using System;

namespace RhythmDojo.Gameplay
{
    public enum HealthRuleAction { Preserve, Damage, GameOver }

    public readonly struct HardModeHealthRule
    {
        public const int MaximumLossCount = 1000;
        public HealthRuleAction Action { get; }
        public int LossCount { get; }
        public HardModeHealthRule(HealthRuleAction action, int lossCount)
        {
            if (!Enum.IsDefined(typeof(HealthRuleAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (lossCount < 1 || lossCount > MaximumLossCount) throw new ArgumentOutOfRangeException(nameof(lossCount));
            Action = action; LossCount = lossCount;
        }
        public void Validate() { _ = new HardModeHealthRule(Action, LossCount); }
    }

    // Immutable per-run rules. Null rules in the session retain the normal recovery policy.
    public sealed class HardModeHealthRules
    {
        public HardModeHealthRule Perfect { get; }
        public HardModeHealthRule Good { get; }
        public HardModeHealthRule Miss { get; }
        public HardModeHealthRules(HardModeHealthRule perfect, HardModeHealthRule good, HardModeHealthRule miss)
        {
            perfect.Validate(); good.Validate(); miss.Validate();
            Perfect = perfect; Good = good; Miss = miss;
        }
        public HardModeHealthRule GetRule(Judgment grade) => grade switch
        {
            Judgment.Perfect => Perfect,
            Judgment.Good => Good,
            Judgment.Miss => Miss,
            _ => throw new ArgumentOutOfRangeException(nameof(grade))
        };
    }
}
