using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class WoodenDummyRotationAssets
    {
        private const string Root = "Assets/Game/Art/PracticeSelection/";
        public const float Scale = 1.4f;
        public const int JointHeight = 356;

        public static Sprite LoadBase()
        {
            var sheet = UiArtworkAssets.Load(Root + "WoodenDummyRotation.png",
                Root + "WoodenDummyRotation.asset", "Wooden Dummy Turnaround", UiArtworkCrop.Full, Vector4.zero);
            int width = sheet.texture.width / 4;
            int height = sheet.texture.height / 2;
            var rect = new Rect(0, height, width, height - JointHeight + 6);
            var pivot = new Vector2(181.5f / width, (height - 504f) / rect.height);
            return SaveSprite("DummyFixedBase.asset", sheet.texture, rect, pivot, "Wooden Dummy Fixed Base");
        }

        public static WoodenDummyRig.RotationFrame[] Load()
        {
            var sheet = UiArtworkAssets.Load(Root + "WoodenDummyRotation.png",
                Root + "WoodenDummyRotation.asset", "Wooden Dummy Turnaround", UiArtworkCrop.Full, Vector4.zero);
            var texture = sheet.texture;
            int width = texture.width / 4;
            int height = texture.height / 2;
            // Crop above the bearing; never include the source atlas's rotating feet.
            // All cells retain their pixel scale and register on the same column centre.
            var pivots = new[]
            {
                new Vector2(181.5f, JointHeight), new Vector2(182, JointHeight),
                new Vector2(181, JointHeight), new Vector2(180, JointHeight),
                new Vector2(181.5f, JointHeight + 1), new Vector2(181.5f, JointHeight + 1),
                new Vector2(181, JointHeight + 1), new Vector2(182, JointHeight + 1)
            };
            var left = new[]
            {
                new Vector2(98, 274), new Vector2(122, 277),
                new Vector2(144, 267), new Vector2(112, 211),
                new Vector2(96, 211), new Vector2(102, 211),
                new Vector2(140, 224), new Vector2(118, 263)
            };
            var right = new[]
            {
                new Vector2(274, 224), new Vector2(270, 220),
                new Vector2(221, 222), new Vector2(254, 262),
                new Vector2(264, 264), new Vector2(239, 264),
                new Vector2(223, 263), new Vector2(221, 223)
            };
            var frames = new WoodenDummyRig.RotationFrame[8];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = "DummyRotationFrame" + i + ".asset";
                float top = texture.height - i / 4 * height;
                var rect = new Rect(i % 4 * width, top - pivots[i].y, width, JointHeight);
                var pivot = new Vector2(pivots[i].x / width, 0);
                var sprite = SaveSprite(path, texture, rect, pivot, "Wooden Dummy Rotation " + i * 45);

                var panel = new Vector2(180, 114);
                var panelSize = new Vector2(84, 74);
                float panelTilt = 0;
                if (i == 1)
                {
                    panel = new Vector2(205, 114);
                    panelSize = new Vector2(70, 74);
                    panelTilt = 2;
                }
                else if (i == 7)
                {
                    panel = new Vector2(151, 114);
                    panelSize = new Vector2(63, 74);
                    panelTilt = -3;
                }

                frames[i] = new WoodenDummyRig.RotationFrame
                {
                    sprite = sprite,
                    panelPosition = Point(panel, pivots[i]),
                    panelSize = panelSize * Scale,
                    panelTilt = panelTilt,
                    leftContact = Point(left[i], pivots[i]),
                    rightContact = Point(right[i], pivots[i]),
                    leftKickContact = new Vector2(-30, 37) * Scale,
                    rightKickContact = new Vector2(30, 37) * Scale
                };
            }
            return frames;
        }

        private static Sprite SaveSprite(string path, Texture2D texture, Rect rect, Vector2 pivot, string name)
        {
            path = Root + path;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite && sprite.rect == rect)
            {
                if (Vector2.Distance(sprite.pivot / sprite.rect.size, pivot) < .0001f)
                {
                    return sprite;
                }
            }
            var generated = Sprite.Create(texture, rect, pivot, 100, 0, SpriteMeshType.FullRect);
            generated.name = name;
            if (!sprite)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, sprite);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(sprite);
            AssetDatabase.SaveAssetIfDirty(sprite);
            return sprite;
        }

        private static Vector2 Point(Vector2 point, Vector2 pivot)
        {
            return new Vector2(point.x - pivot.x, pivot.y - point.y) * Scale;
        }
    }
}
