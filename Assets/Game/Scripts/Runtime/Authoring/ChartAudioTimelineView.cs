using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class ChartAudioTimelineView : MonoBehaviour
    {
        public RectTransform panel;
        public RectTransform content;
        public GameObject advanced;
        public AudioTimelineGraphic overview;
        public AudioTimelineGraphic detail;
        public Text clock;
        public Text message;
        public Text tooltip;
        public Text ruler;
        public Text playLabel;
        public InputField bpm;
        public InputField origin;
        public InputField nudge;
        public InputField segmentTime;
        public InputField segmentBpm;
        public InputField numerator;
        public InputField denominator;
        public InputField firstBeat;
        public InputField secondBeat;
        public InputField beatCount;
        public Dropdown segments;
        public Dropdown nudgeUnit;
        public Button playAudioControl;
        public Button followAudioControl;
        public Button applyGridControl;
        public Button cursorOriginControl;
        public Dropdown nudgeUnitsControl;
        public Button nudgeEarlierControl;
        public Button nudgeLaterControl;
        public Button doubleBPMControl;
        public Button halfBPMControl;
        public Button gridLockControl;
        public Dropdown noteSnapControl;
        public Button closeAdvancedControl;
        public Dropdown tempoSegmentsControl;
        public Button addTempoSegmentControl;
        public Button applyTempoSegmentControl;
        public Button deleteTempoSegmentControl;
        public Button alignTempoSegmentControl;
        public Button captureFirstBeatControl;
        public Button captureSecondBeatControl;
        public Button calculateBPMControl;
        public Dropdown existingNotePolicyControl;
        public Dropdown displayDensityControl;
        public Button advancedGridControl;
        public RectTransform canvas;
        public Text lockLabel;
        public Selectable[] editControls;

        public void ValidateReferences()
        {
            var references = new Object[]
            {
                panel, content, advanced, overview, detail,
                clock, message, tooltip, ruler, playLabel,
                bpm, origin, nudge, segmentTime, segmentBpm,
                numerator, denominator, firstBeat, secondBeat, beatCount,
                segments, nudgeUnit, playAudioControl, followAudioControl, applyGridControl,
                cursorOriginControl, nudgeUnitsControl, nudgeEarlierControl, nudgeLaterControl, doubleBPMControl,
                halfBPMControl, gridLockControl, noteSnapControl, closeAdvancedControl, tempoSegmentsControl,
                addTempoSegmentControl, applyTempoSegmentControl, deleteTempoSegmentControl, alignTempoSegmentControl, captureFirstBeatControl,
                captureSecondBeatControl, calculateBPMControl, existingNotePolicyControl, displayDensityControl, advancedGridControl,
                canvas, lockLabel,
            };
            foreach (var reference in references)
            {
                if (!reference)
                {
                    throw new System.InvalidOperationException("Chart audio timeline has a missing scene reference. Rebuild Chart Editor.");
                }
            }
        }
    }
}
