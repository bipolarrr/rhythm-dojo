using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Presentation Settings")]
    public sealed class GameplayPresentationSettings : ScriptableObject
    {
        public Image fieldTemplate;
        public Sprite highway, divider, border, strikeShoe, pressedShoe, judgmentLine, beat, measure;
        public double flashDuration = .22;
        public double hudRefreshInterval = .05;
        public Color perfectColor = new Color(.64f, .85f, .72f);
        public Color goodColor = new Color(.97f, .75f, .39f);
        public Color missColor = new Color(.92f, .36f, .29f);
        public Color heldColor = new Color(.76f, .87f, .73f);
        public Color idleColor = new Color(.24f, .12f, .085f);

        public void Validate()
        {
            if (!double.IsFinite(flashDuration) || flashDuration <= 0)
            {
                throw new InvalidOperationException("Invalid input flash duration.");
            }

            if (!double.IsFinite(hudRefreshInterval) || hudRefreshInterval <= 0)
            {
                throw new InvalidOperationException("Invalid HUD refresh interval.");
            }
        }
    }
}
