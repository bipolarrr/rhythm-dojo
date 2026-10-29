using System;

namespace RhythmDojo.Gameplay
{
    public enum JudgmentProfile
    {
        Warmup,
        Sparring,
    }

    public enum PlayMode
    {
        Gauge,
        Challenge,
    }

    public enum HoldEndMode
    {
        Automatic,
        Release,
    }

    public enum GaugeFailureMode
    {
        Continue,
        Stop,
    }

    public enum FailureReason
    {
        None,
        GaugeEmpty,
        ChallengeLimit,
    }

    public readonly struct ChallengePresetDefinition
    {
        public int Id { get; }
        public int? GoodLimit { get; }
        public int? MissLimit { get; }

        public ChallengePresetDefinition(int id, int? goodLimit, int? missLimit)
        {
            Id = id;
            GoodLimit = goodLimit;
            MissLimit = missLimit;
        }
    }

    public sealed class PlayRules
    {
        public const int CurrentVersion = 3;
        public const double DefaultPerfectWeight = 1;
        public const double DefaultGoodWeight = .5;
        public const double DefaultMissWeight = 0;
        public const double ScoreScale = 1000000;
        public static readonly System.Collections.Generic.IReadOnlyList<ChallengePresetDefinition> ChallengePresets =
            Array.AsReadOnly(
                new[]
                {
                    new ChallengePresetDefinition(1, null, 1),
                    new ChallengePresetDefinition(2, 3, 0),
                    new ChallengePresetDefinition(3, 0, 0),
                }
            );

        public JudgmentProfile Profile { get; set; } = JudgmentProfile.Warmup;
        public PlayMode Mode { get; set; } = PlayMode.Gauge;
        public HoldEndMode HoldEnd { get; set; } = HoldEndMode.Automatic;
        public GaugeFailureMode GaugeFailure { get; set; } = GaugeFailureMode.Continue;
        public double WarmupPerfect { get; set; } = .050;
        public double WarmupGood { get; set; } = .100;
        public double SparringPerfect { get; set; } = .016;
        public double SparringGood { get; set; } = .050;
        public double HoldCheckBeats { get; set; } = .25;
        public int MaximumHealth { get; set; } = 100;
        public int StartingHealth { get; set; } = 100;
        public int MissDamage { get; set; } = 10;
        public int RecoveryCombo { get; set; } = 10;
        public int PerfectRecovery { get; set; } = 2;
        public double PerfectWeight { get; set; } = DefaultPerfectWeight;
        public double GoodWeight { get; set; } = DefaultGoodWeight;
        public double MissWeight { get; set; } = DefaultMissWeight;

        // null is unlimited; exactly the configured count is allowed.
        public int? PerfectLimit { get; set; }
        public int? GoodLimit { get; set; }
        public int? MissLimit { get; set; } = 1;
        public int ChallengePreset { get; private set; } = 1;

        public JudgmentSettings Timing
        {
            get
            {
                if (Profile == JudgmentProfile.Warmup)
                {
                    return new JudgmentSettings(WarmupPerfect, WarmupGood);
                }
                else
                {
                    return new JudgmentSettings(SparringPerfect, SparringGood);
                }
            }
        }

        public void SelectChallenge(int preset)
        {
            if (preset < 1 || preset > ChallengePresets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(preset));
            }

            ChallengePreset = preset;
            PerfectLimit = null;
            GoodLimit = ChallengePresets[preset - 1].GoodLimit;
            MissLimit = ChallengePresets[preset - 1].MissLimit;
        }

        public PlayRules Copy() => (PlayRules)MemberwiseClone();

        public void Validate()
        {
            Timing.Validate();
            new JudgmentSettings(WarmupPerfect, WarmupGood).Validate();
            new JudgmentSettings(SparringPerfect, SparringGood).Validate();

            if (
                !Enum.IsDefined(typeof(JudgmentProfile), Profile)
                || !Enum.IsDefined(typeof(PlayMode), Mode)
            )
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (
                !Enum.IsDefined(typeof(HoldEndMode), HoldEnd)
                || !Enum.IsDefined(typeof(GaugeFailureMode), GaugeFailure)
            )
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (!double.IsFinite(HoldCheckBeats) || HoldCheckBeats <= 0 || MaximumHealth <= 0)
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (StartingHealth <= 0 || StartingHealth > MaximumHealth || MissDamage < 0)
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (RecoveryCombo < 1 || PerfectRecovery < 0)
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (PerfectLimit < 0 || GoodLimit < 0 || MissLimit < 0)
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (
                !double.IsFinite(PerfectWeight)
                || !double.IsFinite(GoodWeight)
                || !double.IsFinite(MissWeight)
            )
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (PerfectWeight <= 0 || GoodWeight < 0 || GoodWeight > PerfectWeight)
            {
                throw new ArgumentException("Invalid play rules.");
            }

            if (MissWeight < 0 || MissWeight > GoodWeight)
            {
                throw new ArgumentException("Invalid play rules.");
            }
        }
    }

    public readonly struct FailureSnapshot
    {
        public bool Failed => Reason != FailureReason.None;

        public double Time { get; }
        public FailureReason Reason { get; }
        public bool Continues { get; }

        public FailureSnapshot(double time, FailureReason reason, bool continues)
        {
            Time = time;
            Reason = reason;
            Continues = continues;
        }
    }

    public static class JudgmentDisplay
    {
        public static string Label(Judgment grade)
        {
            if (grade == Judgment.Perfect)
            {
                return "Perfect";
            }
            else if (grade == Judgment.Good)
            {
                return "Hmm...";
            }
            else
            {
                return "Nope.";
            }
        }
    }

    public static class JudgmentEvaluator
    {
        public static Judgment Grade(double error, JudgmentSettings config)
        {
            if (Math.Abs(error) <= config.PerfectWindow + RhythmSession.TimingTolerance)
            {
                return Judgment.Perfect;
            }
            else if (Math.Abs(error) <= config.GoodWindow + RhythmSession.TimingTolerance)
            {
                return Judgment.Good;
            }
            else
            {
                return Judgment.Miss;
            }
        }
    }

    public static class ComboRules
    {
        public static int Apply(int combo, Judgment grade)
        {
            if (grade == Judgment.Miss)
            {
                return 0;
            }
            else
            {
                return combo + 1;
            }
        }
    }

    public static class GaugeRules
    {
        public static int Apply(int health, int combo, Judgment grade, PlayRules rules, bool failed)
        {
            if (failed)
            {
                return 0;
            }

            int change;

            if (grade == Judgment.Miss)
            {
                change = -rules.MissDamage;
            }
            else if (grade == Judgment.Perfect && combo >= rules.RecoveryCombo)
            {
                change = rules.PerfectRecovery;
            }
            else
            {
                change = 0;
            }

            return Math.Clamp(health + change, 0, rules.MaximumHealth);
        }
    }

    public static class ChallengeRules
    {
        public static bool Exceeded(int perfect, int good, int miss, PlayRules rules)
        {
            if (rules.PerfectLimit.HasValue && perfect > rules.PerfectLimit.Value)
            {
                return true;
            }

            if (rules.GoodLimit.HasValue && good > rules.GoodLimit.Value)
            {
                return true;
            }

            return rules.MissLimit.HasValue && miss > rules.MissLimit.Value;
        }
    }

    public static class ScoreRules
    {
        public static double Rate(int perfect, int good, int miss, int total, PlayRules rules)
        {
            if (total <= 0)
            {
                return 0;
            }
            else
            {
                return (
                        perfect * rules.PerfectWeight
                        + good * rules.GoodWeight
                        + miss * rules.MissWeight
                    )
                    / (total * rules.PerfectWeight)
                    * 100;
            }
        }

        public static int Score(double rate) =>
            (int)Math.Round(rate / 100 * PlayRules.ScoreScale, MidpointRounding.AwayFromZero);
    }
}
