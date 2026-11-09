using System.IO;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class DojoCursorAssetBuilder
    {
        public static void Build()
        {
            var baker = new CursorBaker();
            var frames = baker.BakeFrames();
            var root = new GameObject("Dojo Cursor");
            root.SetActive(false);
            try
            {
                var cursor = root.AddComponent<DojoCursor>();
                var focus = root.AddComponent<DojoButtonFocus>();
                var canvasObject = new GameObject("Fist Imprints", typeof(RectTransform), typeof(Canvas));
                canvasObject.transform.SetParent(root.transform, false);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32001;
                canvas.pixelPerfect = true;
                var impactObject = new GameObject("Fist Marks", typeof(RectTransform), typeof(DojoPunchImpact));
                impactObject.transform.SetParent(canvasObject.transform, false);
                var rect = (RectTransform)impactObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var impact = impactObject.GetComponent<DojoPunchImpact>();
                impact.raycastTarget = false;
                BakeImprint(impact, frames[3], new Vector2(48, 48));
                var feedback = root.AddComponent<DojoClickFeedback>();
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.volume = .32f;
                source.priority = 128;
                source.ignoreListenerPause = true;
                SceneDependencyAssembler.Wire(feedback, "clickSource", source);
                SceneDependencyAssembler.Wire(feedback, "clickSound",
                    SceneResources.Require<AudioClip>("Assets/Game/Resources/UI/DojoClick.wav"));
                SceneDependencyAssembler.Wire(cursor, "normalTexture", frames[1]);
                SceneDependencyAssembler.Wire(cursor, "hoverTexture", frames[1]);
                SceneDependencyAssembler.Wire(cursor, "impact", impact);
                SceneDependencyAssembler.Wire(cursor, "feedback", feedback);
                var serialized = new SerializedObject(cursor);
                var array = serialized.FindProperty("punchFrames");
                array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var border = new FocusBuilder().Build();
                border.SetParent(root.transform, false);
                border.gameObject.SetActive(false);
                SceneDependencyAssembler.Wire(focus, "border", border);
                var borderTemplate = PrefabUtility.SaveAsPrefabAsset(border.gameObject,
                    "Assets/Game/Prefabs/DojoFocusBorder.prefab").GetComponent<RectTransform>();
                SceneDependencyAssembler.Wire(focus, "borderTemplate", borderTemplate);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Game/Resources/UI/DojoCursor.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Texture2D SaveTexture(Texture2D texture, string name)
        {
            string path = "Assets/Game/Resources/UI/" + name + ".png";
            var bytes = texture.EncodeToPNG();
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            Object.DestroyImmediate(texture);
            return SceneResources.Require<Texture2D>(path);
        }

        private static void BakeImprint(DojoPunchImpact impact, Texture2D texture, Vector2 hotspot)
        {
            var sourcePixels = texture.GetPixels32();
            int left = texture.width;
            int right = -1;
            int bottom = texture.height;
            int top = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (sourcePixels[y * texture.width + x].a < 128)
                    {
                        continue;
                    }

                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }

            if (right < left || top < bottom)
            {
                throw new System.InvalidOperationException("Cursor silhouette is empty.");
            }

            var artwork = SceneResources.Require<Texture2D>("Assets/Game/Resources/UI/DojoFistImprint.png");
            if (!artwork)
            {
                throw new System.InvalidOperationException("Gray frontal fist imprint artwork missing.");
            }

            var pixels = artwork.GetPixels32();
            int imprintLeft = artwork.width;
            int imprintRight = -1;
            int imprintBottom = artwork.height;
            int imprintTop = -1;
            for (int y = 0; y < artwork.height; y++)
            {
                for (int x = 0; x < artwork.width; x++)
                {
                    int index = y * artwork.width + x;
                    var pixel = pixels[index];
                    byte gray = (byte)((pixel.r * 77 + pixel.g * 150 + pixel.b * 29) >> 8);
                    pixels[index] = new Color32(gray, gray, gray, pixel.a);
                    if (pixel.a < 128)
                    {
                        continue;
                    }

                    imprintLeft = Mathf.Min(imprintLeft, x);
                    imprintRight = Mathf.Max(imprintRight, x);
                    imprintBottom = Mathf.Min(imprintBottom, y);
                    imprintTop = Mathf.Max(imprintTop, y);
                }
            }

            if (imprintRight < imprintLeft || imprintTop < imprintBottom)
            {
                throw new System.InvalidOperationException("Gray frontal fist imprint artwork is empty.");
            }

            // Preserve the generated pixel shading, with strictly neutral gray and genuine alpha.
            var stampTexture = new Texture2D(artwork.width, artwork.height, TextureFormat.RGBA32, false);
            stampTexture.name = "DojoFistImprint";
            stampTexture.filterMode = FilterMode.Point;
            stampTexture.wrapMode = TextureWrapMode.Clamp;
            stampTexture.SetPixels32(pixels);
            stampTexture.Apply();
            float width = right - left + 1;
            float height = top - bottom + 1;
            float imprintWidth = imprintRight - imprintLeft + 1;
            float imprintHeight = imprintTop - imprintBottom + 1;
            var stampUv = new Rect(imprintLeft / (float)artwork.width, imprintBottom / (float)artwork.height,
                imprintWidth / artwork.width, imprintHeight / artwork.height);
            var stampSize = new Vector2(imprintWidth, imprintHeight)
                * (Mathf.Round(Mathf.Max(width, height) * .5f) / Mathf.Max(imprintWidth, imprintHeight));
            // Place the print at the leading knuckles of the extended diagonal cursor.
            // Cursor hotspots use top-left coordinates; canvas positions use bottom-left coordinates.
            var stampOffset = new Vector2(left + width * .28f - hotspot.x,
                bottom + height * .78f - texture.height + hotspot.y);
            SceneDependencyAssembler.Wire(impact, "stampTexture", SaveTexture(stampTexture, "DojoFistImprintBaked"));
            var serialized = new SerializedObject(impact);
            serialized.FindProperty("stampUv").rectValue = stampUv;
            serialized.FindProperty("stampSize").vector2Value = stampSize;
            serialized.FindProperty("stampOffset").vector2Value = stampOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class CursorBaker
        {
            private Color32[] cursorPixels;
            private RectInt cursorBounds;
            private int cursorSourceWidth;

            public Texture2D[] BakeFrames()
            {
                var texture = SceneResources.Require<Texture2D>("Assets/Game/Resources/UI/DojoFistCursor.png");
                cursorPixels = texture.GetPixels32();
                cursorSourceWidth = texture.width;
                cursorBounds = GetVisibleBounds(cursorPixels, texture.width, texture.height);
                var normalTexture = SaveTexture(CreateCursor(72), "DojoCursorRest");
                var extended = SaveTexture(CreateCursor(78, new Vector2Int(-9, 9)), "DojoCursorExtended");
                return new[]
                {
                    SaveTexture(CreateCursor(69, new Vector2Int(6, -6)), "DojoCursorWindup"),
                    normalTexture,
                    extended,
                    SaveTexture(CreateCursor(81, new Vector2Int(-12, 12)), "DojoCursorContact"),
                    extended,
                    SaveTexture(CreateCursor(75, new Vector2Int(-3, 3)), "DojoCursorReturn"),
                    normalTexture,
                };
            }

            private Texture2D CreateCursor(int size)
            {
                return CreateCursor(size, Vector2Int.zero);
            }

            private Texture2D CreateCursor(int size, Vector2Int offset)
            {
                // Fit the complete v5 silhouette, including its loose tail, without filtering the pixels.
                // Extra transparent padding fits the longer punch; the matching hotspot shift
                // preserves the resting artwork's screen position and its visible size.
                const int canvasSize = 128;
                var output = new Color32[canvasSize * canvasSize];
                float scale = (float)size / Mathf.Max(cursorBounds.width, cursorBounds.height);
                int width = Mathf.Max(1, Mathf.RoundToInt(cursorBounds.width * scale));
                int height = Mathf.Max(1, Mathf.RoundToInt(cursorBounds.height * scale));
                int left = (canvasSize - width) / 2 + offset.x;
                int bottom = (canvasSize - height) / 2 + offset.y;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sourceX = cursorBounds.x + Mathf.Min(cursorBounds.width - 1,
                            Mathf.FloorToInt((x + .5f) * cursorBounds.width / width));
                        int sourceY = cursorBounds.y + Mathf.Min(cursorBounds.height - 1,
                            Mathf.FloorToInt((y + .5f) * cursorBounds.height / height));
                        output[(bottom + y) * canvasSize + left + x] = cursorPixels[sourceY * cursorSourceWidth + sourceX];
                    }
                }

                var texture = new Texture2D(canvasSize, canvasSize, TextureFormat.RGBA32, false);
                texture.name = "DojoHandrapV5Cursor";
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels32(output);
                texture.Apply();
                return texture;
            }

            private static RectInt GetVisibleBounds(Color32[] pixels, int width, int height)
            {
                int left = width;
                int bottom = height;
                int right = -1;
                int top = -1;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[y * width + x].a < 128)
                        {
                            continue;
                        }

                        left = Mathf.Min(left, x);
                        bottom = Mathf.Min(bottom, y);
                        right = Mathf.Max(right, x);
                        top = Mathf.Max(top, y);
                    }
                }

                if (right < left || top < bottom)
                {
                    throw new System.InvalidOperationException("Hand-wrap cursor artwork is empty.");
                }

                return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            }

        }

        private sealed class FocusBuilder
        {
            private RectTransform border;

            public RectTransform Build()
            {
                BuildBorder();
                return border;
            }

            private void BuildBorder()
            {
                border = new GameObject("Dojo Focus Border", typeof(RectTransform)).GetComponent<RectTransform>();
                AddEdge("Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
                AddEdge("Bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
                AddEdge("Left", Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
                AddEdge("Right", new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
            }

            private void AddEdge(string name, Vector2 minimum, Vector2 maximum, Vector2 inset, Vector2 outset)
            {
                var edge = new GameObject(name, typeof(Image), typeof(Shadow)).GetComponent<Image>();
                var rect = edge.rectTransform;
                rect.SetParent(border, false);
                rect.anchorMin = minimum;
                rect.anchorMax = maximum;
                rect.offsetMin = inset;
                rect.offsetMax = outset;
                edge.color = new Color(1f, .94f, .70f);
                edge.raycastTarget = false;
                var shadow = edge.GetComponent<Shadow>();
                shadow.effectColor = new Color(.13f, .05f, .025f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }
}
