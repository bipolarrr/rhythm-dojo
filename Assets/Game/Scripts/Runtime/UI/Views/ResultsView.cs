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
        public Text progressNote;
        public Text perfect;
        public Text good;
        public Text miss;
        public Text accuracy;
        public Text maxCombo;
        public Image cover;
        public Sprite fallbackCover;
        public Sprite legacyFallbackCover;
        public Text coverPlaceholder;
        public RectTransform feedback;
        public Text scoreSummary;
        public Text scoreValue;
        public GameObject newRecordBadge;
        public Text retryStatus;
        public Button retryButton;
        public Button recommendationButton;
        public Button moreButton;
        public ResultsReviewView review;
        public ResultsPresentation presentation;

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

            if (!moreButton)
            {
                throw new InvalidOperationException("Result buttons missing.");
            }

            if (!recommendationButton)
            {
                throw new InvalidOperationException("Result timing recommendation button missing.");
            }

            if (!scoreSummary || !retryStatus || !retryButton)
            {
                throw new InvalidOperationException("Result score and retry controls missing.");
            }

            if (!scoreValue || !newRecordBadge)
            {
                throw new InvalidOperationException("Result score hierarchy is missing.");
            }

            if (!review)
            {
                throw new InvalidOperationException("Results review missing.");
            }

            review.Validate();
            if (!presentation)
            {
                throw new InvalidOperationException("Results presentation missing.");
            }
            presentation.Validate();
        }
    }
}
