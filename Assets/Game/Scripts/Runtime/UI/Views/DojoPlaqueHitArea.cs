using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class DojoPlaqueHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, point, eventCamera, out var local))
            {
                return false;
            }

            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * .47f;
            return (local - rect.rect.center).sqrMagnitude <= radius * radius;
        }
    }
}
