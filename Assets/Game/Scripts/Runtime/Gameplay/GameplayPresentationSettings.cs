using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Presentation Settings")]
    public sealed class GameplayPresentationSettings : ScriptableObject
    {
        public Vector3 headSize = new Vector3(1.05f, .22f, .32f);
        public float headHeight = .28f;
        public float bodyWidth = .42f;
        public float bodyHeight = .16f;
        public float bodyElevation = .2f;
        public float minimumBodyLength = .01f;
        public float highwayLength = 100;
        public float highwayCenter = 48;
        public float laneGap = .08f;
        public float railWidth = .045f;
        public float railHeight = .06f;
        public float judgmentLineDepth = .13f;
        public float judgmentLineHeight = .1f;
        public double flashDuration = .22;
        public double hudRefreshInterval = .05;
        public Color perfectColor = Color.cyan;
        public Color goodColor = Color.yellow;
        public Color missColor = new Color(1, .22f, .3f);
        public Color heldColor = new Color(.3f, .85f, 1);
        public Color idleColor = new Color(.12f, .18f, .26f);
        public Color highwayColor = new Color(.055f, .075f, .11f);
        public Color railColor = new Color(.2f, .27f, .35f);
        public Color judgmentLineColor = Color.white;
        public void Validate()
        {
            foreach (float value in new[] { headSize.x, headSize.y, headSize.z, bodyWidth, bodyHeight,
                minimumBodyLength, highwayLength, railWidth, railHeight, judgmentLineDepth, judgmentLineHeight })
                if (!float.IsFinite(value) || value <= 0) throw new InvalidOperationException("Invalid presentation dimensions.");
            if (!double.IsFinite(flashDuration) || flashDuration <= 0 || !double.IsFinite(hudRefreshInterval) || hudRefreshInterval <= 0 ||
                !float.IsFinite(headHeight) || !float.IsFinite(bodyElevation) || !float.IsFinite(highwayCenter) ||
                !float.IsFinite(laneGap) || laneGap < 0) throw new InvalidOperationException("Invalid presentation timing or position.");
        }
    }
}
