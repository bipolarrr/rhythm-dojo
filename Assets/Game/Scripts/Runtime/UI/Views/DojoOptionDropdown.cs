using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Native Dropdown owns submit, cancel and its data-dependent option list.
    public sealed class DojoOptionDropdown : Dropdown
    {
        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    value--;
                    break;
                case MoveDirection.Right:
                    value++;
                    break;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }
    }
}
