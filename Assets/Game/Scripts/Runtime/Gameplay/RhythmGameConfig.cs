using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    public sealed class RhythmGameConfig : ScriptableObject
    {
        [SerializeField] private double perfectWindow = .05;
        [SerializeField] private double goodWindow = .11;
        [SerializeField] private float scrollSpeed = 8;
        [Tooltip("Positive offset advances chart time relative to audio.")]
        [SerializeField] private double audioOffset;
        [SerializeField] private double scheduleLeadTime = .5;
        public double PerfectWindow => perfectWindow;
        public double GoodWindow => goodWindow;
        public float ScrollSpeed => scrollSpeed;
        public double AudioOffset => audioOffset;
        public double ScheduleLeadTime => scheduleLeadTime;
        public void Validate()
        {
            if (!double.IsFinite(perfectWindow) || !double.IsFinite(goodWindow) || perfectWindow <= 0 ||
                goodWindow < perfectWindow || !float.IsFinite(scrollSpeed) || scrollSpeed <= 0 ||
                !double.IsFinite(audioOffset) || !double.IsFinite(scheduleLeadTime) || scheduleLeadTime < .1)
                throw new InvalidOperationException("Malformed judgment windows, scroll speed or audio timing configuration.");
        }
    }
}
