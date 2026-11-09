using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    [Serializable]
    public sealed class TimingCalibrationLayout
    {
        public Vector2 panelSize;
        public Vector2 headingPosition;
        public Vector2 headingSize;
        public RectTransform statusParent;
        public Vector2 statusPosition;
        public Vector2 statusSize;
        public Vector2 startPosition;
        public Vector2 startSize;
        public Vector2 closePosition;
        public Vector2 closeSize;

        public void Apply(RectTransform panelRect, Text heading, Text status, Button start, Button close)
        {
            panelRect.sizeDelta = panelSize;
            UiElements.Position(heading.rectTransform, headingPosition, headingSize);
            status.transform.SetParent(statusParent, false);
            UiElements.Position(status.rectTransform, statusPosition, statusSize);
            UiElements.Position((RectTransform)start.transform, startPosition, startSize);
            UiElements.Position((RectTransform)close.transform, closePosition, closeSize);
        }
    }

    public sealed class TimingCalibrationView : MonoBehaviour
    {
        public GameObject overlay;
        public RectTransform panelRect;
        public Button entry;
        public Button speedEntry;
        public Button start;
        public Button apply;
        public Button close;
        public Text heading;
        public Text status;
        public AudioSource source;
        public SpeedTestLaneView speedLane;
        public TimingCalibrationLayout timingLayout;
        public TimingCalibrationLayout speedLayout;

        public void Validate()
        {
            if (!overlay || !panelRect || !source)
            {
                throw new InvalidOperationException("Timing calibration panel missing.");
            }

            if (!entry || !speedEntry || !start)
            {
                throw new InvalidOperationException("Timing calibration entry controls missing.");
            }

            if (!apply || !close || !heading)
            {
                throw new InvalidOperationException("Timing calibration controls missing.");
            }

            if (!status || !speedLane)
            {
                throw new InvalidOperationException("Timing calibration feedback missing.");
            }

            if (timingLayout == null || speedLayout == null)
            {
                throw new InvalidOperationException("Timing calibration layouts missing.");
            }

            if (!timingLayout.statusParent || !speedLayout.statusParent)
            {
                throw new InvalidOperationException("Timing calibration layout parents missing.");
            }

            speedLane.Validate();
        }
    }
}
