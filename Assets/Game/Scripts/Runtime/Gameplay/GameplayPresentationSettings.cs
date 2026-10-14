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
        public Color perfectColor = new Color(0.64f, 0.85f, 0.72f);
        public Color goodColor = new Color(0.97f, 0.75f, 0.39f);
        public Color missColor = new Color(0.92f, 0.36f, 0.29f);
        public Color heldColor = new Color(0.76f, 0.87f, 0.73f);
        public Color idleColor = new Color(0.24f, 0.12f, 0.085f);
        public Color highwayColor = new Color(0.085f, 0.068f, 0.058f);
        public Color railColor = new Color(0.39f, 0.29f, 0.19f);
        public Color judgmentLineColor = new Color(0.96f, 0.83f, 0.58f);

        public void Validate()
        {
            foreach (
                float value in new[]
                {
                    headSize.x,
                    headSize.y,
                    headSize.z,
                    bodyWidth,
                    bodyHeight,
                    minimumBodyLength,
                    highwayLength,
                    railWidth,
                    railHeight,
                    judgmentLineDepth,
                    judgmentLineHeight,
                }
            )
            {
                if (!float.IsFinite(value) || value <= 0)
                {
                    throw new InvalidOperationException("Invalid presentation dimensions.");
                }
            }

            if (
                (!double.IsFinite(flashDuration) || flashDuration <= 0)
                || !double.IsFinite(hudRefreshInterval)
            )
            {
                throw new InvalidOperationException("Invalid presentation timing or position.");
            }

            if (
                (hudRefreshInterval <= 0 || !float.IsFinite(headHeight))
                || !float.IsFinite(bodyElevation)
            )
            {
                throw new InvalidOperationException("Invalid presentation timing or position.");
            }

            if ((!float.IsFinite(highwayCenter) || !float.IsFinite(laneGap)) || laneGap < 0)
            {
                throw new InvalidOperationException("Invalid presentation timing or position.");
            }
        }
    }
}
