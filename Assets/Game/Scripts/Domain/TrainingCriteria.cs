using System;

namespace RhythmDojo.Gameplay
{
    // Evaluates the existing engine's final aggregate; it never judges input or notes.
    public sealed class TrainingCriteria
    {
        public int MaximumMisses { get; }
        public double MinimumAccuracy { get; }

        public TrainingCriteria(int maximumMisses, double minimumAccuracy)
        {
            if (maximumMisses < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumMisses));
            }
            if (!double.IsFinite(minimumAccuracy) || minimumAccuracy < 0 || minimumAccuracy > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAccuracy));
            }

            MaximumMisses = maximumMisses;
            MinimumAccuracy = minimumAccuracy;
        }

        public bool IsSatisfied(SessionSnapshot snapshot)
        {
            if (snapshot.State != SessionState.Completed || snapshot.Failure.Failed)
            {
                return false;
            }
            if (snapshot.TotalNotes <= 0 || snapshot.Resolved != snapshot.TotalNotes)
            {
                return false;
            }

            return snapshot.Miss <= MaximumMisses && snapshot.AccuracyRate >= MinimumAccuracy;
        }
    }
}
