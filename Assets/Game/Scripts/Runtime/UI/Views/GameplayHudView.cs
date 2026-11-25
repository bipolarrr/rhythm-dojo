using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        public bool pixelTheme;
        public GameplayJudgmentMotion judgmentMotion;
        public Image album;
        public Sprite fallbackCover;
        public GameObject introduction;
        public Image healthDamage;
        public RectTransform healthGauge;
        public RectTransform laneReadout;
        public GameplayViewport viewport;
        public HudLaneLayout lanes;
        public Button returnButton;
        public Text songTitle;
        public Text songDetails;
        public Text sessionState;
        public Text combo;
        public Text judgment;
        public Text timing;
        public CanvasGroup judgmentGroup;
        public Image healthFill;
        public Text healthLabel;
        public GameObject gameOverPanel;
        public Text gameOverText;
        public Text gameOverHint;
        public GameObject gameOverFrame;
        public Slider railOpacity;
        public Button focusHudButton;
        public GameObject songInformationPanel;

        public void Validate()
        {
            if (!judgmentMotion || !laneReadout)
            {
                throw new InvalidOperationException("Gameplay judgment composition missing.");
            }
            judgmentMotion.Validate();

            if (!album || !fallbackCover || !introduction)
            {
                throw new InvalidOperationException("Gameplay song/introduction references missing.");
            }
            if (!healthDamage)
            {
                throw new InvalidOperationException("Gameplay health feedback missing.");
            }
            if (!healthGauge || !viewport)
            {
                throw new InvalidOperationException("Gameplay gauge positioning references missing.");
            }
            if (!lanes || !returnButton)
            {
                throw new InvalidOperationException("HUD view references missing.");
            }

            if ((!songTitle || !songDetails) || !sessionState)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if (!combo || !judgment)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if (!timing || !judgmentGroup)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if ((!healthFill || !healthLabel) || !gameOverPanel)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if (!gameOverText || !gameOverHint || !railOpacity)
            {
                throw new InvalidOperationException("Training HUD references missing.");
            }

            if (!focusHudButton || !songInformationPanel)
            {
                throw new InvalidOperationException("Gameplay focus HUD references missing.");
            }

            lanes.Validate();
        }

        public void SetFocusHud(bool enabled)
        {
            songInformationPanel.SetActive(true);
            var caption = focusHudButton.GetComponentInChildren<Text>(true);
            if (enabled)
            {
                caption.text = "HUD · 집중";
            }
            else
            {
                caption.text = "HUD · 기본";
            }
        }
    }
}
