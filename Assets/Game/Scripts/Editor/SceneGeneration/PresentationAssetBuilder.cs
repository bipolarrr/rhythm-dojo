using System;
using UnityEditor;
using UnityEngine;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using static RhythmDojo.EditorTools.ContentAssets;
namespace RhythmDojo.EditorTools
{
    public static class PresentationAssetBuilder
    {
        public static GeneratedContent Prepare(GameSettings settings)
        {
            var presentation = settings.presentation;
            var content = new GeneratedContent { Settings = settings,
                NoteMaterial = Material("Note Base", Color.white),
                HighwayMaterial = Material("Highway", presentation.highwayColor),
                RailMaterial = Material("Rails", presentation.railColor),
                JudgmentMaterial = Material("Judgement", presentation.judgmentLineColor) };
            content.TapTemplate = Template("TapNote", false, content.NoteMaterial, presentation);
            content.HoldTemplate = Template("HoldNote", true, content.NoteMaterial, presentation);
            AssetDatabase.SaveAssets(); return content;
        }
        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader unavailable.");
            string path = $"Assets/Game/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .3f); Dirty(material); return material;
        }
        private static NoteView Template(string name, bool hold, Material material, GameplayPresentationSettings settings)
        {
            string path = $"Assets/Game/Prefabs/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) { existing.GetComponent<NoteView>().Validate(hold ? NoteKind.Hold : NoteKind.Tap); return existing.GetComponent<NoteView>(); }
            var root = new GameObject(name);
            try
            {
                var view = root.AddComponent<NoteView>();
                SceneDependencyAssembler.Wire(view, "head", Part("Head", root.transform, settings.headSize, material));
                if (hold) SceneDependencyAssembler.Wire(view, "body", Part("Hold Body", root.transform, Vector3.one, material));
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NoteView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Transform Part(string name, Transform parent, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
            go.transform.localScale = size; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material; return go.transform;
        }
    }
}
