using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ResultsView : MonoBehaviour
    {
        public Text title;
        public Text artist;
        public Text difficulty;
        public Text outcome;
        public Text perfect;
        public Text good;
        public Text miss;
        public Text accuracy;
        public Text maxCombo;
        public Image cover;
        public Text coverPlaceholder;
        public RectTransform feedback;
        public Button backButton;
        public Button moreButton;
        public ResultsReviewView review;

        public void Validate()
        {
            if (!title || !artist || !difficulty)
            {
                throw new InvalidOperationException("Result song labels missing.");
            }

            if (!outcome || !perfect || !good)
            {
                throw new InvalidOperationException("Result judgment labels missing.");
            }

            if (!miss || !accuracy || !maxCombo)
            {
                throw new InvalidOperationException("Result statistics missing.");
            }

            if (!cover || !coverPlaceholder || !feedback)
            {
                throw new InvalidOperationException("Result panels missing.");
            }

            if (!backButton || !moreButton)
            {
                throw new InvalidOperationException("Result buttons missing.");
            }

            if (!review)
            {
                throw new InvalidOperationException("Results review missing.");
            }

            review.Validate();
        }
    }
}
