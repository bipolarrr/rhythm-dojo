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
        private const string Art = "Assets/Game/Art/DojoGameplay/";

        public static GeneratedContent Prepare(GameSettings settings)
        {
            var presentation = settings.presentation;
            var content = new GeneratedContent
            {
                Settings = settings,
                NoteMaterial = Material("Note Base", new Color(.50f, .76f, .68f)),
                HighwayMaterial = Material("Highway", presentation.highwayColor),
                RailMaterial = Material("Rails", presentation.railColor),
                JudgmentMaterial = Material("Judgement", presentation.judgmentLineColor),
            };
            PrepareFieldTemplates(presentation);
            content.TapTemplate = Template("TapNote", false, content.NoteMaterial);
            content.HoldTemplate = Template("HoldNote", true, content.NoteMaterial);
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
            material.SetFloat("_Smoothness", .2f);
            if (name == "Highway")
            {
                material.SetTexture("_BaseMap", SceneResources.Require<Texture2D>(Art + "LaneTimber.png"));
                material.SetTextureScale("_BaseMap", new Vector2(1, 18));
            }
            if (name == "Note Base")
            {
                material.SetTexture("_BaseMap", SceneResources.Require<Texture2D>(Art + "SilkBinding.png"));
            }
            Dirty(material);

            return material;
        }

        private static NoteView Template(
            string name,
            bool hold,
            Material material
        )
        {
            string path = $"Assets/Game/Prefabs/{name}.prefab";
            var root = new GameObject(name);

            try
            {
                var view = root.AddComponent<NoteView>();
                var head = new GameObject("Head").transform;
                head.SetParent(root.transform, false);
                SceneDependencyAssembler.Wire(view, "head", head);
                if (hold)
                {
                    SceneDependencyAssembler.Wire(
                        view,
                        "body",
                        Part("Hold Body", root.transform, Vector3.one, material)
                    );
                }

                ConfigureNote(view, head, hold);
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
            PrepareFieldTemplates(settings.presentation);
            Material("Highway", settings.presentation.highwayColor);
            var material = Material("Note Base", new Color(.50f, .76f, .68f));
            Template("TapNote", false, material);
            Template("HoldNote", true, material);
        }

        private static void ConfigureNote(NoteView view, Transform head, bool hold)
        {
            var headSkin = new GameObject("Strike Face", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            headSkin.transform.SetParent(head, false);
            headSkin.transform.localRotation = Quaternion.Euler(28f, 0, 0);
            headSkin.transform.localPosition = new Vector3(0, -.08f, -.04f);
            string artwork = "IvoryStrike";
            if (hold)
            {
                artwork = "JadeHold";
            }
            headSkin.sprite = SceneResources.Require<Sprite>(Art + artwork + ".png");
            SceneDependencyAssembler.Wire(view, "headSkin", headSkin);

            if (hold)
            {
                var marker = new GameObject("Hold End Marker", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                marker.transform.SetParent(view.transform, false);
                marker.transform.localRotation = headSkin.transform.localRotation;
                marker.sprite = headSkin.sprite;
                SceneDependencyAssembler.Wire(view, "tailMarker", marker);
                view.Validate(NoteKind.Hold);
            }
            else
            {
                view.Validate(NoteKind.Tap);
            }
        }

        private static void PrepareFieldTemplates(GameplayPresentationSettings settings)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
                settings.beamTemplate = PrefabUtility.SaveAsPrefabAsset(beam,
                    "Assets/Game/Prefabs/TrainingBeam.prefab").GetComponent<MeshRenderer>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(beam);
            }

            const string path = "Assets/Game/Prefabs/StrikeShoe.prefab";
            var root = new GameObject("Strike Shoe", typeof(SpriteRenderer));
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = SceneResources.Require<Sprite>(Art + "StrikeShoe.png");
                root.transform.localRotation = Quaternion.Euler(68, 0, 0);
                settings.strikeShoe = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<SpriteRenderer>();
                Dirty(settings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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
