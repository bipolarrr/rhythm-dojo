using System;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.UI
{
    // Notes, timing marks and receptors share drawing units independent of texture resolution.
    [ExecuteAlways]
    public sealed class GameplayViewport : MonoBehaviour
    {
        public const float LaneWidth = 32;
        public const float Height = 240;
        public const float StrikeY = 18;
        public const float PixelsPerDistance = 10;

        public RectTransform field;

        public static Vector2 Position(int lane, int count, double distance)
        {
            return new Vector2(GameModeDefinition.CenteredPosition(lane, count, LaneWidth),
                StrikeY + (float)distance * PixelsPerDistance);
        }

        public void Fit(int count)
        {
            if (!field)
            {
                throw new InvalidOperationException("Missing authored lane drawing.");
            }

            field.sizeDelta = new Vector2(count * LaneWidth + 6, Height);
            OnRectTransformDimensionsChange();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!field || field.sizeDelta.x <= 0)
            {
                return;
            }

            var size = ((RectTransform)transform).rect.size;
            float verticalScale = size.y / Height;
            float scale = Mathf.Min(size.x / field.sizeDelta.x, verticalScale);
            var fitted = new Vector3(scale, verticalScale, 1);
            if (field.localScale != fitted)
            {
                field.localScale = fitted;
            }
        }
    }
}
