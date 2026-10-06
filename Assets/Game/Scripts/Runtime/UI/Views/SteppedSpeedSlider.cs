using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace RhythmDojo.UI
{
    public sealed class SteppedSpeedSlider : Slider
    {
        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            if (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right)
            {
                float direction = eventData.moveDir == MoveDirection.Right ? .1f : -.1f;
                value = Mathf.Round((value + direction) * 10f) / 10f;
            }
            else base.OnMove(eventData);
        }
    }
}
