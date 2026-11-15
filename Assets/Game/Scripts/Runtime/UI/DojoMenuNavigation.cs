using System.Collections.Generic;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Navigation data for the existing InputSystemUIInputModule, not a second input loop.
    public static class DojoMenuNavigation
    {
        public static void Link(Selectable control, Selectable up, Selectable down,
            Selectable left = null, Selectable right = null)
        {
            control.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right,
            };
        }

        public static void Chain(List<Selectable> controls, bool horizontal = false)
        {
            for (int i = 0; i < controls.Count; i++)
            {
                var previous = controls[(i + controls.Count - 1) % controls.Count];
                var next = controls[(i + 1) % controls.Count];
                Link(controls[i], previous, next);
                if (horizontal && controls[i] is Button)
                {
                    Link(controls[i], previous, next, previous, next);
                }
            }
        }
    }
}
