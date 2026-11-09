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

        public static void Apply(Text text, UiTextRole role)
        {
            var style = text.GetComponent<UiTextStyle>() ?? text.gameObject.AddComponent<UiTextStyle>();
            style.role = role;
            Apply(text);
        }

        private static void Apply(Text text, UiFontSettings settings)
        {
            var scope = text.GetComponentInParent<UiFontScope>(true);
            var style = text.GetComponent<UiTextStyle>() ?? text.gameObject.AddComponent<UiTextStyle>();
            if (scope && scope.useSmoothRendering)
            {
                if (text.material == settings.pixelText)
                {
                    text.material = null;
                }
            }
            else if (text.material == text.defaultMaterial)
            {
                text.material = settings.pixelText;
            }

            // Preserve explicitly assigned fonts, but never capture our last application as an override.
            if (text.font && text.font != style.appliedFont && !IsSharedFont(text.font, settings))
            {
                if (text.font != Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"))
                {
                    style.customFont = text.font;
                    style.customStyle = text.fontStyle;
                }
            }

            if (style.customFont)
            {
                text.font = style.customFont;
                text.fontStyle = style.customStyle;
            }
            else if (scope && scope.font && !IsSharedFont(scope.font, settings))
            {
                text.font = scope.font;
                text.fontStyle = scope.style;
            }
            else
            {
                text.font = ResolveFont(style.role, settings);
                text.fontStyle = Style;
            }

            style.appliedFont = text.font;
            if (text.fontStyle == FontStyle.Normal)
            {
                var input = text.GetComponentInParent<InputField>(true);
                var dropdown = text.GetComponentInParent<Dropdown>(true);
                if (input || dropdown)
                {
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }
        }

        private static bool IsSharedFont(Font font, UiFontSettings settings) =>
            font == settings.font || font == settings.bold || font == settings.regular;

        private static Font ResolveFont(UiTextRole role, UiFontSettings settings)
        {
            switch (role)
            {
                case UiTextRole.Title:
                    return settings.bold;
                case UiTextRole.Help:
                    return settings.regular;
                default:
                    return settings.font;
            }
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
