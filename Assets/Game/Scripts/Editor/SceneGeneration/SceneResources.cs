using System;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class SceneResources
    {
        public const string ReadableFontPath = MainMenuLayoutBuilder.FontPath;

        public static void ApplyReadableFont(params Text[] labels)
        {
            var font = Require<Font>(ReadableFontPath);
            foreach (var label in labels)
            {
                var scope = label.GetComponent<UiFontScope>();
                if (!scope)
                {
                    scope = label.gameObject.AddComponent<UiFontScope>();
                }

                scope.font = font;
                scope.style = FontStyle.Normal;
                UiFontSettings.Apply(label);
            }
        }

        public static GeneratedContent Load()
        {
            var content = new GeneratedContent();
            content.ReloadAfterSceneChange();
            GameContentValidator.Validate(content.Settings);

            return content;
        }

        public static T Require<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (!asset)
            {
                throw new InvalidOperationException(
                    "Missing resource: "
                        + path
                        + ". Run Game Tools > Content > Prepare Default and Verification Content."
                );
            }

            return asset;
        }
    }
}
