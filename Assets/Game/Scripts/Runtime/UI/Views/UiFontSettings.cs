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
            Apply(text, Current);
        }

        public static void ApplyTo(Transform root)
        {
            var currentFont = Current;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                Apply(text, currentFont);
            }
        }

        private static void Apply(Text text, Font currentFont)
        {
            var scope = text.GetComponentInParent<UiFontScope>(true);
            if (scope && scope.font)
            {
                text.font = scope.font;
                text.fontStyle = scope.style;
                if (scope.style == FontStyle.Normal)
                {
                    var input = text.GetComponentInParent<InputField>(true);
                    var dropdown = text.GetComponentInParent<Dropdown>(true);
                    if (input || dropdown)
                    {
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                }

                return;
            }

            text.font = currentFont;
            text.fontStyle = Style;
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
