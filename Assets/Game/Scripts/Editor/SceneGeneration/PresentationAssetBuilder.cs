using System;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using UnityEditor;
using UnityEngine;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    public static class PresentationAssetBuilder
    {
        public static GeneratedContent Prepare(GameSettings settings)
        {
            var presentation = settings.presentation;
            var content = new GeneratedContent
            {
                Settings = settings,
                NoteMaterial = Material("Note Base", Color.white),
                HighwayMaterial = Material("Highway", presentation.highwayColor),
                RailMaterial = Material("Rails", presentation.railColor),
                JudgmentMaterial = Material("Judgement", presentation.judgmentLineColor),
            };
            content.TapTemplate = Template("TapNote", false, content.NoteMaterial, presentation);
            content.HoldTemplate = Template("HoldNote", true, content.NoteMaterial, presentation);
            AssetDatabase.SaveAssets();

            return content;
        }

        private static Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            if (!shader)
            {
                throw new InvalidOperationException("URP Lit shader unavailable.");
            }

            string path = $"Assets/Game/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .3f);
            Dirty(material);

            return material;
        }

        private static NoteView Template(
            string name,
            bool hold,
            Material material,
            GameplayPresentationSettings settings
        )
        {
            string path = $"Assets/Game/Prefabs/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureNote(contents.GetComponent<NoteView>(), hold);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                return existing.GetComponent<NoteView>();
            }

            var root = new GameObject(name);

            try
            {
                var view = root.AddComponent<NoteView>();
                SceneDependencyAssembler.Wire(
                    view,
                    "head",
                    Part("Head", root.transform, settings.headSize, material)
                );

                if (hold)
                {
                    SceneDependencyAssembler.Wire(
                        view,
                        "body",
                        Part("Hold Body", root.transform, Vector3.one, material)
                    );
                }

                ConfigureNote(view, hold);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<NoteView>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void PrepareNoteTemplates()
        {
            var settings = SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath);
            var material = SceneResources.Require<Material>("Assets/Game/Materials/Note Base.mat");
            Template("TapNote", false, material, settings.presentation);
            Template("HoldNote", true, material, settings.presentation);
        }

        private static void ConfigureNote(NoteView view, bool hold)
        {
            var serialized = new SerializedObject(view);
            var head = (Transform)serialized.FindProperty("head").objectReferenceValue;
            var body = (Transform)serialized.FindProperty("body").objectReferenceValue;
            var headSkin = (SpriteRenderer)serialized.FindProperty("headSkin").objectReferenceValue;
            if (!headSkin)
            {
                var skin = new GameObject("Songpan Skin", typeof(SpriteRenderer));
                skin.transform.SetParent(head, false);
                headSkin = skin.GetComponent<SpriteRenderer>();
            }

            SceneDependencyAssembler.Wire(view, "headRenderer", head.GetComponent<Renderer>());
            headSkin.gameObject.layer = head.gameObject.layer;
            headSkin.transform.localRotation = Quaternion.Euler(28f, 0, 0);
            headSkin.sprite = SceneResources.Require<Sprite>("Assets/Game/Resources/UI/SongpanNote.png");
            headSkin.enabled = false;
            SceneDependencyAssembler.Wire(view, "headSkin", headSkin);
            if (hold)
            {
                var tailMarker = (MeshRenderer)serialized.FindProperty("tailMarker").objectReferenceValue;
                if (!tailMarker)
                {
                    var marker = new GameObject("Hold End Marker", typeof(MeshFilter), typeof(MeshRenderer));
                    marker.transform.SetParent(view.transform, false);
                    tailMarker = marker.GetComponent<MeshRenderer>();
                }

                tailMarker.gameObject.layer = body.gameObject.layer;
                tailMarker.GetComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
                tailMarker.sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;
                tailMarker.enabled = false;
                SceneDependencyAssembler.Wire(view, "tailMarker", tailMarker);
            }

            if (hold)
            {
                view.Validate(NoteKind.Hold);
            }
            else
            {
                view.Validate(NoteKind.Tap);
            }
        }

        private static Transform Part(
            string name,
            Transform parent,
            Vector3 size,
            Material material
        )
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;

            return go.transform;
        }
    }
}
