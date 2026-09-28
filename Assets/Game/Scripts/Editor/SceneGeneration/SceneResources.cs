using System;
using UnityEditor;
using UnityEngine;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;

namespace RhythmDojo.EditorTools
{
    public static class SceneResources
    {
        public static GeneratedContent Load()
        {
            var content = new GeneratedContent(); content.ReloadAfterSceneChange();
            GameContentValidator.Validate(content.Settings); return content;
        }
        public static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing resource: " + path +
                ". Run Game Tools > Content > Prepare Default and Verification Content.");
            return asset;
        }
    }
}
