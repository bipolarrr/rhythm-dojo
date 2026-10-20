using System;
using UnityEngine;

namespace RhythmDojo.UI
{
    // The resource references the original font asset, including in player builds.
    public sealed class UiFontSettings : ScriptableObject
    {
        public Font font;

        public static Font Current
        {
            get
            {
                var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
                if (!settings || !settings.font)
                {
                    throw new InvalidOperationException("UI font settings or Mona font missing.");
                }

                return settings.font;
            }
        }
    }
}
