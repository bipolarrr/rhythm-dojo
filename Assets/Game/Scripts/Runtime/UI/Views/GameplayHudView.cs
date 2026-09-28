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
        public void Validate()
        {
            if (!status || !lanes || !returnButton) throw new InvalidOperationException("HUD view references missing.");
            lanes.Validate();
        }
    }
}
