using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace RhythmDojo.EditorTools
{
    public static class ContentAssets
    {
        public static T Asset<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path); created = !asset;
            if (asset) return asset;
            if (File.Exists(path)) throw new InvalidOperationException($"Cannot load {path} as {typeof(T).Name}. Repair its script reference.");
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        public static void Dirty(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            // Later asset creation/import can reload this object, including after regeneration.
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
