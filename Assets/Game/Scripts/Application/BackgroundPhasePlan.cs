using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public interface IBackgroundJudgmentReadModel
    {
        Guid SessionId { get; }
        JudgmentSchedule Schedule { get; }
        event Action<JudgmentResolvedEvent> JudgmentResolved;
        event Action<InputResolutionEvent> InputResolved;
    }

    public sealed class BackgroundPhasePlan
    {
        private readonly int[] phases;
        private readonly double[] weights;
        private readonly double[] opportunities = new double[4];
        public double Duration { get; }
        public double AudioOffset { get; }
        public int UnitCount => phases.Length;
        public double PhaseDuration => Duration / 4;
        public double TransitionDuration => Math.Min(.7, PhaseDuration * .1);

        public BackgroundPhasePlan(ChartData chart, JudgmentSchedule schedule,
            double audioLength, double audioOffset)
        {
            Duration = Math.Min(audioLength, chart.CompletionTime - audioOffset);
            if (!double.IsFinite(Duration) || Duration <= 0)
            {
                throw new ArgumentException("Background music interval must be finite and positive.");
            }
            AudioOffset = audioOffset;
            phases = new int[schedule.Checks.Count];
            weights = new double[phases.Length];
            foreach (var unit in schedule.Checks)
            {
                int phase = PhaseAt(unit.ScheduledTime);
                phases[unit.Id] = phase;
                weights[unit.Id] = unit.Weight;
                opportunities[phase] += unit.Weight;
            }
        }

        public int PhaseAt(double songTime)
        {
            double musicTime = songTime - AudioOffset;
            if (musicTime < PhaseDuration)
            {
                return 0;
            }
            if (musicTime < PhaseDuration * 2)
            {
                return 1;
            }
            if (musicTime < PhaseDuration * 3)
            {
                return 2;
            }
            return 3;
        }

        public int PhaseOf(int id) => phases[id];
        public double WeightOf(int id) => weights[id];
        public double Opportunities(int phase) => opportunities[phase];
    }

    public sealed class BackgroundPerformanceState
    {
        private readonly BackgroundPhasePlan plan;
        private readonly bool[] resolved;
        private readonly double[] earned = new double[4];
        private int resolvedCount;
        public Guid SessionId { get; private set; }
        public int Mistakes { get; private set; }

        public BackgroundPerformanceState(BackgroundPhasePlan plan, Guid sessionId)
        {
            this.plan = plan;
            resolved = new bool[plan.UnitCount];
            Reset(sessionId);
        }

        public void Reset(Guid sessionId)
        {
            SessionId = sessionId;
            Array.Clear(resolved, 0, resolved.Length);
            Array.Clear(earned, 0, earned.Length);
            Mistakes = 0;
            resolvedCount = 0;
        }

        public bool Consume(JudgmentResolvedEvent result)
        {
            if (result.SessionId != SessionId)
            {
                return false;
            }
            int id = result.Unit.Id;
            if (id < 0 || id >= resolved.Length)
            {
                return false;
            }
            if (resolved[id])
            {
                return false;
            }
            resolved[id] = true;
            resolvedCount++;
            if (result.Grade == Judgment.Miss)
            {
                Mistakes++;
                return true;
            }
            if (result.Grade == Judgment.Perfect || result.Grade == Judgment.Good)
            {
                earned[plan.PhaseOf(id)] += plan.WeightOf(id);
            }
            return true;
        }

        public void Consume(InputResolutionEvent result)
        {
            if (result.SessionId != SessionId)
            {
                return;
            }
            if (result.Resolution == InputResolution.EmptyPress ||
                result.Resolution == InputResolution.HoldInterrupted)
            {
                Mistakes++;
            }
        }

        public double Progress(int phase)
        {
            double available = plan.Opportunities(phase);
            if (available <= 0)
            {
                return 0;
            }
            return Math.Clamp(earned[phase] / available, 0, 1);
        }

        public bool CanFinishFlawlessly(SessionSnapshot snapshot)
        {
            if (!snapshot.IsFullCombo || snapshot.Miss != 0)
            {
                return false;
            }
            if (Mistakes != 0 || plan.UnitCount == 0)
            {
                return false;
            }
            return resolvedCount == plan.UnitCount;
        }
    }
}
