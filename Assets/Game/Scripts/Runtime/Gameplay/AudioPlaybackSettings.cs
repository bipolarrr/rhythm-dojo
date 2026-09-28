using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Audio Playback Settings")]
    public sealed class AudioPlaybackSettings : ScriptableObject
    {
        [SerializeField] private double scheduleLeadTime = .15;
        [SerializeField, Range(0, 1)] private float volume = .45f;
        public double ScheduleLeadTime => scheduleLeadTime;
        public float Volume => volume;
        public void Validate()
        {
            if (!double.IsFinite(scheduleLeadTime) || scheduleLeadTime < .1 || !float.IsFinite(volume) || volume < 0 || volume > 1)
                throw new InvalidOperationException("Invalid audio scheduling lead or volume.");
        }
        public void MigrateLeadTime(double lead) { scheduleLeadTime = lead; }
    }
}
