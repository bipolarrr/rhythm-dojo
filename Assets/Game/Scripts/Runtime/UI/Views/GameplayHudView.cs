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
        public void Validate()
        {
            if (!status || !lanes || !returnButton) throw new InvalidOperationException("HUD view references missing.");
            if (!songTitle || !songDetails || !sessionState || !combo || !bestCombo || !judgment ||
                !timing || !counts || !accuracy || !progress || !progressFill || !judgmentGroup)
                throw new InvalidOperationException("Training HUD references missing.");
            lanes.Validate();
        }
    }
}
