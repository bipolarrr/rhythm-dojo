using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SpeedTestLaneView : MonoBehaviour
    {
        public RectTransform rail;
        public RectTransform[] notes;
        public CanvasGroup feedback;
        public Text judgment;
        public Text timingLabel;

        public void Validate()
        {
            if (!rail || !feedback)
            {
                throw new InvalidOperationException("Speed test layout missing.");
            }

            if (!judgment || !timingLabel)
            {
                throw new InvalidOperationException("Speed test labels missing.");
            }

            if (notes == null || notes.Length != 32)
            {
                throw new InvalidOperationException("Speed test note pool missing.");
            }

            if (Array.Exists(notes, note => !note))
            {
                throw new InvalidOperationException("Speed test note reference missing.");
            }
        }
    }
}
