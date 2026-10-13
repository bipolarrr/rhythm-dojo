using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        public Text status;
        public HudLaneLayout lanes;
        public Button returnButton;
        public Text songTitle;
        public Text songDetails;
        public Text sessionState;
        public Text combo;
        public Text bestCombo;
        public Text judgment;
        public Text timing;
        public Text counts;
        public Text accuracy;
        public Text progress;
        public Image progressFill;
        public CanvasGroup judgmentGroup;
        public Image healthFill;
        public Text healthLabel;
        public GameObject gameOverPanel;
        public Text gameOverText;

        public void Validate()
        {
            if (!status || !lanes || !returnButton)
            {
                throw new InvalidOperationException("HUD view references missing.");
            }

            if ((!songTitle || !songDetails) || !sessionState)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if ((!combo || !bestCombo) || !judgment)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if ((!timing || !counts) || !accuracy)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if ((!progress || !progressFill) || !judgmentGroup)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if ((!healthFill || !healthLabel) || !gameOverPanel)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if (!gameOverText)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            lanes.Validate();
        }
    }
}
