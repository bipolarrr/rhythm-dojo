using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ResultsReviewView : MonoBehaviour
    {
        public ResultsChartGraphic chart;
        public Text heading;
        public Text lanes;
        public Text range;
        public Text selected;
        public Text events;
        public Slider position;
        public Dropdown zoom;
        public Button closeButton;
        public Button previousButton;
        public Button nextButton;

        public void Validate()
        {
            if (!chart || !heading || !lanes)
            {
                throw new InvalidOperationException("Result review chart references missing.");
            }

            if (!range || !selected || !events)
            {
                throw new InvalidOperationException("Result review labels missing.");
            }

            if (!position || !zoom)
            {
                throw new InvalidOperationException("Result review navigation missing.");
            }

            if (!closeButton || !previousButton || !nextButton)
            {
                throw new InvalidOperationException("Result review buttons missing.");
            }
        }
    }
}
