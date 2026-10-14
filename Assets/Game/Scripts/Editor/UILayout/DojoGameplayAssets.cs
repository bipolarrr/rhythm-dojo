using System;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class DojoGameplayAssets
    {
        private const string Root = "Assets/Game/Art/DojoGameplay/";

        public static Sprite Paper => SceneResources.Require<Sprite>(Root + "PaperPanel.asset");

        public static Sprite Scroll => SceneResources.Require<Sprite>(Root + "GameplayScroll.asset");

        public static Sprite Wood => SceneResources.Require<Sprite>(Root + "WoodPanel.asset");

        public static Sprite Tile => SceneResources.Require<Sprite>(Root + "StrikeTile.asset");

        [MenuItem("Game Tools/Art/Prepare Generated Gameplay Assets")]
        public static void Prepare()
        {
            Load("PaperPanel", new Rect(24, 72, 1488, 890), new Vector4(112, 100, 112, 100));
            Load("WoodPanel", new Rect(26, 94, 1484, 860), new Vector4(104, 100, 104, 100));
            Load("StrikeTile", new Rect(0, 173, 1983, 444), new Vector4(100, 80, 100, 80));
            Load("GameplayScroll", new Rect(0, 0, 1024, 1536), Vector4.zero);
            PrepareNoteMaterial();
            AssetDatabase.SaveAssets();
        }

        public static void PrepareNoteMaterial()
        {
            var sprite = Tile;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Note Base.mat");
            if (!material)
            {
                throw new InvalidOperationException("Note material missing.");
            }

            material.SetTexture("_BaseMap", sprite.texture);
            material.SetTextureScale("_BaseMap", new Vector2(
                sprite.rect.width / sprite.texture.width,
                sprite.rect.height / sprite.texture.height
            ));
            material.SetTextureOffset("_BaseMap", new Vector2(
                sprite.rect.x / sprite.texture.width,
                sprite.rect.y / sprite.texture.height
            ));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
        }

        private static Sprite Load(string name, Rect rect, Vector4 border)
        {
            string path = Root + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Generated dojo texture missing: " + path);
            }

            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + name + ".asset");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var generated = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
            generated.name = name;
            if (sprite)
            {
                EditorUtility.CopySerialized(generated, sprite);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(sprite);
                return sprite;
            }

            sprite = generated;
            AssetDatabase.CreateAsset(sprite, Root + name + ".asset");
            return sprite;
        }
    }
}
