using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // The resource references the original font asset, including in player builds.
    public sealed class UiFontSettings : ScriptableObject
    {
        public Font font;
        public static FontStyle Style => FontStyle.Bold;

        public static void Apply(Text text)
        {
            text.font = Current;
            var scope = text.GetComponentInParent<UiFontScope>(true);
            if (scope && scope.font)
            {
                text.font = scope.font;
            }
            text.fontStyle = Style;
        }

        public static void ApplyTo(Transform root)
        {
            var currentFont = Current;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = currentFont;
                var scope = text.GetComponentInParent<UiFontScope>(true);
                if (scope && scope.font)
                {
                    text.font = scope.font;
                }
                text.fontStyle = Style;
            }
        }

        public static Font Current
        {
            get
            {
                var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
                if (!settings || !settings.font)
                {
                    throw new InvalidOperationException("UI font settings or Dokdo font missing.");
                }

                return settings.font;
            }
        }
    }
}
