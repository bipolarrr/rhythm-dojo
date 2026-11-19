using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    internal sealed class ActionStageArtwork
    {
        private const string Root = "Assets/Game/Art/ActionStages/";
        public readonly Sprite[] poses;
        public readonly Sprite[] partner;
        public readonly Sprite[] locations = Sheet("Locations", 2, 2);
        public readonly Sprite[] equipment = Sheet("Equipment", 4, 2);
        public readonly Sprite[] flags = Sheet("Pennant", 6, 1);
        public readonly Sprite[] cascade = Sheet("Cascade", 6, 2);
        public readonly Sprite[] effects = Sheet("Motion", 6, 7, true);
        public readonly Sprite shadow = Sheet("Shadow", 1, 1, true)[0];
        public readonly Sprite trail = Sheet("Trail", 1, 1)[0];
        public readonly Sprite branch = Sheet("Bough", 1, 1)[0];

        public ActionStageArtwork()
        {
            var footwork = Sheet("Footwork", 4, 2);
            poses = Sheet("Trainee", 5, 4).Concat(footwork.Take(4)).ToArray();
            partner = Sheet("Partner", 7, 1).Take(6).Concat(footwork.Skip(4)).ToArray();
        }

        public Material StageMaterial(int index)
        {
            string path = Root + "Stage" + index + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(SceneResources.Require<Shader>(Root + "StageSprite.shader"));
                material.SetFloat("_StageIndex", index);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        // Store sliced drawings as sub-assets so a saved scene and a rebuilt scene share the same sprites.
        private static Sprite[] Sheet(string name, int columns, int rows, bool center = false)
        {
            string path = Root + name + ".png";
            var texture = SceneResources.Require<Texture2D>(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                texture = SceneResources.Require<Texture2D>(path);
            }

            path = Root + name + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path))
            {
                var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
                if (saved.Length != columns * rows)
                {
                    throw new InvalidOperationException("Incomplete action sprite sheet: " + path);
                }
                return saved;
            }

            int width = texture.width / columns;
            int height = texture.height / rows;
            var pivot = new Vector2(.5f, 0);
            if (center)
            {
                pivot.y = .5f;
            }
            var sprites = new Sprite[columns * rows];
            for (int i = 0; i < sprites.Length; i++)
            {
                var rect = new Rect(i % columns * width, (rows - 1 - i / columns) * height, width, height);
                var sprite = Sprite.Create(texture, rect, pivot, 1, 0, SpriteMeshType.FullRect);
                sprite.name = name + i.ToString("D2");
                if (i == 0)
                {
                    AssetDatabase.CreateAsset(sprite, path);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(sprite, path);
                }
                sprites[i] = sprite;
            }
            AssetDatabase.SaveAssets();
            return sprites;
        }
    }
}
