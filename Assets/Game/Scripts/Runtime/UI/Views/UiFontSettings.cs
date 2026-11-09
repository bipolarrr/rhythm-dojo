using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // The resource references the original font asset, including in player builds.
    public sealed class UiFontSettings : ScriptableObject
    {
        public Font font;
        public Font bold;
        public Font regular;
        public Material pixelText;

        public static FontStyle Style => FontStyle.Normal;

        public static void Apply(Text text)
        {
            Apply(text, Load());
        }

        public static void ApplyTo(Transform root)
        {
            var settings = Load();
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                Apply(text, settings);
            }
        }

        private static void Apply(Text text, UiFontSettings settings)
        {
            var scope = text.GetComponentInParent<UiFontScope>(true);
            if ((!scope || !scope.useSmoothRendering) && text.material == text.defaultMaterial)
            {
                text.material = settings.pixelText;
            }

            if (scope && scope.font)
            {
                if (scope.style == FontStyle.Normal)
                {
                    var input = text.GetComponentInParent<InputField>(true);
                    var dropdown = text.GetComponentInParent<Dropdown>(true);
                    if (input || dropdown)
                    {
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                }

                if (scope.font != settings.font)
                {
                    text.font = scope.font;
                    text.fontStyle = scope.style;
                    return;
                }
            }

            text.font = ResolveFont(text, settings);
            text.fontStyle = Style;
        }

        private static Font ResolveFont(Text text, UiFontSettings settings)
        {
            switch (text.name)
            {
                case "Dojo Brand":
                case "Mode Title":
                case "Combo Value":
                case "Countdown Number":
                case "ON AIR Countdown":
                case "Judgment Grade":
                case "Outcome":
                case "Score Value":
                case "New Record Text":
                case "Start Prompt":
                    return settings.bold;
                case "Placeholder":
                case "Instructions":
                case "Shortcut":
                case "Result Note":
                case "Review Legend":
                case "Training Caption":
                    return settings.regular;
                case "Title":
                    if (!text.GetComponentInParent<SongRowView>(true))
                    {
                        return settings.bold;
                    }
                    break;
            }

            if (text.name.EndsWith("Heading", StringComparison.Ordinal)
                || text.name == "Game Over Title")
            {
                return settings.bold;
            }

            var parent = text.transform.parent;
            if (parent)
            {
                var group = parent.parent;
                if (parent.name == "Play" || parent.name == "Retry")
                {
                    return settings.bold;
                }

                if (group && group.name == "Menu Buttons")
                {
                    return settings.bold;
                }
            }

            if (text.name.EndsWith(" Help", StringComparison.Ordinal)
                || text.name.EndsWith(" Hint", StringComparison.Ordinal))
            {
                return settings.regular;
            }

            if (text.name == "Help")
            {
                return settings.regular;
            }

            return settings.font;
        }

        public static Font Current => Load().font;

        private static UiFontSettings Load()
        {
            var settings = Resources.Load<UiFontSettings>("UI/UiFontSettings");
            if (!settings || !settings.font)
            {
                throw new InvalidOperationException("UI font settings or Maru Buri font missing.");
            }

            if (!settings.bold || !settings.regular)
            {
                throw new InvalidOperationException("UI font settings or Maru Buri font missing.");
            }

            if (!settings.pixelText)
            {
                throw new InvalidOperationException("UI pixel text material missing.");
            }

            return settings;
        }
    }
}
