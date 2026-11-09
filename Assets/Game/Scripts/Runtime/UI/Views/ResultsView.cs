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
        }

        public void EnsureScoreActions()
        {
            if (recommendationButton)
            {
                return;
            }

            PlaceScoreAction((RectTransform)retryButton.transform, new Vector2(.71f, .52f), new Vector2(.97f, .92f));
            PlaceScoreAction(retryStatus.rectTransform, new Vector2(.39f, .035f), new Vector2(.69f, .29f));

            recommendationButton = Instantiate(retryButton, feedback);
            recommendationButton.name = "Result Timing Recommendation";
            recommendationButton.onClick = new Button.ButtonClickedEvent();
            recommendationButton.navigation = Navigation.defaultNavigation;
            PlaceScoreAction(
                (RectTransform)recommendationButton.transform,
                new Vector2(.71f, .08f),
                new Vector2(.97f, .46f)
            );

            var label = recommendationButton.GetComponentInChildren<Text>(true);
            var frame = recommendationButton.GetComponentInChildren<PixelDojoFrameGraphic>(true);
            if (frame)
            {
                frame.color = Color.white;
            }

            label.color = new Color(.24f, .12f, .085f);
            UiFontSettings.Apply(label, UiTextRole.Title);
            label.text = "";
            label.fontSize = 26;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 17;
            label.resizeTextMaxSize = 26;
            recommendationButton.gameObject.SetActive(false);
        }

        private static void PlaceScoreAction(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
