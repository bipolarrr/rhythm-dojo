using System;
using UnityEngine;
using RhythmDojo.Application;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Scroll Settings")]
    public sealed class ScrollSettings : ScriptableObject
    {
        [SerializeField] private double baseSpeed = 8;
        [SerializeField] private double referenceBpm = 120;
        [SerializeField] private double[] multipliers = CreateDefaultMultipliers();
        private static double[] CreateDefaultMultipliers()
        {
            // Preserve the existing indices for integration callers, then add the slider values.
            var values = new System.Collections.Generic.List<double> { .5, .75, 1, 1.25, 1.5, 2 };
            for (int i = 10; i <= 100; i++)
                if (!values.Contains(i / 10.0)) values.Add(i / 10.0);
            return values.ToArray();
        }
        public int MultiplierCount => multipliers?.Length ?? 0;
        public double GetMultiplier(int index) => multipliers[index];
        public IScrollTimeline CreateTimeline(ScrollMode mode, TempoMap tempo, double multiplier)
        {
            Validate();
            if (!Enum.IsDefined(typeof(ScrollMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return mode == ScrollMode.Bpm ? new BpmScrollTimeline(tempo, baseSpeed, referenceBpm, multiplier) :
                new ConstantScrollTimeline(baseSpeed, multiplier);
        }
        public void Validate()
        {
            if (!double.IsFinite(baseSpeed) || baseSpeed <= 0 || !double.IsFinite(referenceBpm) || referenceBpm <= 0 || MultiplierCount == 0)
                throw new InvalidOperationException("Invalid base speed, reference BPM or multiplier list.");
            var distinct = new System.Collections.Generic.HashSet<double>();
            foreach (double value in multipliers)
                if (!double.IsFinite(value) || value <= 0 || !distinct.Add(value)) throw new InvalidOperationException("Invalid scroll multiplier.");
            if (!distinct.Contains(1)) throw new InvalidOperationException("Multiplier list needs the default 1x option.");
        }
        public void MigrateBaseSpeed(float speed) { baseSpeed = speed; }
    }
}
