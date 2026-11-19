using System;
using System.Collections.Generic;
using System.IO;
using RhythmDojo.Core;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.EditorTools
{
    public static class GameplayBackgroundBuilder
    {
        public const string ArtPath = "Assets/Game/Art/DojoRoom/";
        private const int RoomLayer = 30;
        private const string PixelPath = ArtPath + "Pixel/";

        public static void Build(Canvas background, GameplayAtmosphere atmosphere, Camera camera)
        {
            PrepareArt();
            var fallback = new GameObject("Legacy Phase Scenery", typeof(RectTransform));
            fallback.transform.SetParent(background.transform, false);
            Stretch((RectTransform)fallback.transform);
            var children = new List<Transform>();
            foreach (Transform child in background.transform)
            {
                if (child != fallback.transform)
                {
                    children.Add(child);
                }
            }
            foreach (var child in children)
            {
                child.SetParent(fallback.transform, false);
            }

            var room = new GameObject("Dojo Room Spatial Stage").AddComponent<DojoRoomPhaseView>();
            room.transform.position = new Vector3(1000, 0, 0);
            var roomCamera = new GameObject("Dojo Perspective Camera").AddComponent<Camera>();
            roomCamera.transform.SetParent(room.transform, false);
            roomCamera.cullingMask = 1 << RoomLayer;
            roomCamera.clearFlags = CameraClearFlags.SolidColor;
            roomCamera.backgroundColor = new Color(.035f, .044f, .07f);
            roomCamera.fieldOfView = 58;
            roomCamera.nearClipPlane = .06f;
            roomCamera.farClipPlane = 120;
            roomCamera.depth = -90;
            roomCamera.allowHDR = false;
            roomCamera.allowMSAA = false;
            room.roomCamera = roomCamera;
            camera.cullingMask &= ~(1 << RoomLayer);

            var picture = new GameObject("Living Dojo Projection", typeof(RectTransform), typeof(RawImage),
                typeof(CanvasGroup));
            picture.transform.SetParent(background.transform, false);
            Stretch((RectTransform)picture.transform);
            picture.GetComponent<RawImage>().raycastTarget = false;
            room.picture = picture.GetComponent<CanvasGroup>();
            room.picture.blocksRaycasts = false;
            // The existing adapter owns only the device-size-dependent render target.
            picture.AddComponent<GameplayViewport>().gameplayCamera = roomCamera;

            var surfaces = new List<Renderer>();
            // Painted silhouettes and shadows replace the former architectural primitives.
            // Depth is used only to project separate art layers during the threshold crossing.
            float height = 2 * Mathf.Tan(29 * Mathf.Deg2Rad) * 8;
            float width = height * 16 / 9;
            var chamber = Material("Chamber pigment", "Chamber.png", Color.white);
            Quad(room, surfaces, "Painted chamber and threshold", new Vector3(0, 1.28f, 8),
                new Vector2(width, height), chamber);

            // These UV bounds follow the actual rectangular opening in Chamber.png.
            // Both leaves sit behind the painted jambs, so they disappear into the wall.
            float doorWidth = width * .198f;
            float doorHeight = height * .492f;
            float doorY = 1.28f - height * .002f;
            room.leftDoorClosed = new Vector3(-doorWidth * .5f + .008f, doorY, 8.035f);
            room.rightDoorClosed = new Vector3(doorWidth * .5f - .008f, doorY, 8.035f);
            room.doorDistance = doorWidth + .12f;
            room.leftDoor = Quad(room, surfaces, "Left sliding leaf", room.leftDoorClosed,
                new Vector2(doorWidth, doorHeight), Material("Left leaf pigment", "LeftLeaf.png", Color.white));
            room.rightDoor = Quad(room, surfaces, "Right sliding leaf", room.rightDoorClosed,
                new Vector2(doorWidth, doorHeight), Material("Right leaf pigment", "RightLeaf.png", Color.white));

            var sky = Material("Valley pigment", "Valley.png", new Color(.89f, .94f, .96f), 0);
            Quad(room, surfaces, "Mountain and garden distance", new Vector3(0, 1.28f, 46),
                new Vector2(width * 5.85f, height * 5.85f), sky);

            // Foreground columns use the exact source pixels at a nearer depth. Their
            // larger parallax is independent of the doors and distant painted valley.
            for (int side = 0; side < 2; side++)
            {
                float x = -.5f + .045f;
                float u = 0;
                if (side == 1)
                {
                    x = .5f - .045f;
                    u = .91f;
                }
                var edge = Material("Near silhouette " + side, "Chamber.png", Color.white);
                edge.mainTextureScale = new Vector2(.09f, 1);
                edge.mainTextureOffset = new Vector2(u, 0);
                float depth = 5.2f / 8;
                Quad(room, surfaces, "Foreground ink column " + side,
                    new Vector3(x * width * depth, 1.28f, 5.2f),
                    new Vector2(width * .09f * depth, height * depth), edge);
            }

            room.hanging = new Transform[4];
            var cloth = Material("Banner pigment", "HangingBanner.png", Color.white, .65f, 1);
            var paper = Material("Lantern pigment", "HangingLantern.png", Color.white, .12f);
            for (int side = 0; side < 2; side++)
            {
                float direction = -1;
                if (side == 1)
                {
                    direction = 1;
                }
                room.hanging[side] = Hanging(room, surfaces, "Suspended linen " + side,
                    new Vector3(direction * 4.30f, 3.50f, 7.5f), new Vector2(.79f, 1.87f), cloth);
                room.hanging[side + 2] = Hanging(room, surfaces, "Paper lantern " + side,
                    new Vector3(direction * 4.05f, 3.86f, 5.8f), new Vector2(.55f, 1.48f), paper);
            }

            room.dust = new Transform[12];
            // A tiny warm fleck sampled from painted paper, never a generated noise map.
            var dustMaterial = Material("Dust pigment", "HangingLantern.png", new Color(.89f, .78f, .58f), 0);
            dustMaterial.mainTextureScale = Vector2.zero;
            dustMaterial.mainTextureOffset = new Vector2(.5f, .45f);
            for (int i = 0; i < room.dust.Length; i++)
            {
                room.dust[i] = Quad(room, surfaces, "Sunlit dust " + i, Vector3.zero,
                    Vector2.one, dustMaterial);
            }
            room.surfaces = surfaces.ToArray();
            room.ResetPresentation();

            var cover = new GameObject("Background Ink Passage", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(background.transform, false);
            Stretch((RectTransform)cover.transform);
            var transition = cover.GetComponent<Image>();
            transition.color = Color.clear;
            transition.raycastTarget = false;
            var director = background.gameObject.AddComponent<GameplayBackgroundDirector>();
            director.room = room;
            director.fallback = fallback;
            director.atmosphere = atmosphere;
            director.transition = transition;
            director.fallbackLights = fallback.GetComponentsInChildren<GameplayWindowLight>(true);
            TrainingJourneyBuilder.Build(background, director);
            SceneDependencyAssembler.Wire(Object.FindFirstObjectByType<GameplayCompositionRoot>(),
                "backgroundDirector", director);
            fallback.SetActive(false);
            atmosphere.enabled = false;
            director.Validate();
            AssetDatabase.SaveAssets();
        }

        private static Transform Hanging(DojoRoomPhaseView room, List<Renderer> surfaces, string name,
            Vector3 position, Vector2 size, Material material)
        {
            var hanging = new GameObject(name).transform;
            hanging.SetParent(room.transform, false);
            hanging.localPosition = position;
            var art = Quad(room, surfaces, name + " art", Vector3.zero, size, material);
            art.SetParent(hanging, false);
            art.localPosition = new Vector3(0, -size.y * .5f, 0);
            if (material.GetFloat("_Cloth") > 0)
            {
                art.GetComponent<MeshFilter>().sharedMesh = ClothMesh();
            }
            return hanging;
        }

        private static Transform Quad(DojoRoomPhaseView room, List<Renderer> surfaces, string name,
            Vector3 position, Vector2 size, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.name = name;
            item.layer = RoomLayer;
            item.transform.SetParent(room.transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surfaces.Add(renderer);
            return item.transform;
        }

        private static Material Material(string name, string texture, Color color, float unlit = 1, float cloth = 0)
        {
            string path = PixelPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("RhythmDojo/Dojo Pigment"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PixelPath + texture);
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.SetColor("_Color", color);
            material.SetFloat("_RoomShade", unlit);
            material.SetFloat("_Cloth", cloth);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh ClothMesh()
        {
            string path = PixelPath + "SuspendedLinen.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh)
            {
                return mesh;
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = 0; y <= 16; y++)
            {
                for (int x = 0; x <= 4; x++)
                {
                    float u = x / 4f;
                    float v = y / 16f;
                    vertices.Add(new Vector3(u - .5f, v - .5f, 0));
                    uv.Add(new Vector2(u, v));
                    if (y == 16 || x == 4)
                    {
                        continue;
                    }
                    int i = y * 5 + x;
                    triangles.AddRange(new[] { i, i + 5, i + 1, i + 1, i + 5, i + 6 });
                }
            }
            mesh = new Mesh { name = "Painted cloth deformation grid" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void PrepareArt()
        {
            ConfigureTexture(PixelPath + "Chamber.png");
            ConfigureTexture(PixelPath + "Valley.png");
            PrepareDetails();
        }

        private static void PrepareDetails()
        {
            string path = PixelPath + "MovingArt.png";
            ConfigureTexture(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.SaveAndReimport();
            try
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string[] names = { "LeftLeaf", "RightLeaf", "HangingBanner", "HangingLantern" };
                for (int i = 0; i < names.Length; i++)
                {
                    int x = i % 2 * atlas.width / 2;
                    // The hanging cords extend a little above the atlas midpoint.
                    // Keep their silhouettes out of the door bounds.
                    int y = Mathf.RoundToInt(atlas.height * .54f);
                    int height = atlas.height - y;
                    if (i >= 2)
                    {
                        y = 0;
                        height = Mathf.RoundToInt(atlas.height * .52f);
                    }
                    int width = atlas.width / 2;
                    var pixels = atlas.GetPixels(x, y, width, height);
                    int x0 = width - 1;
                    int x1 = 0;
                    int y0 = height - 1;
                    int y1 = 0;
                    for (int py = 0; py < height; py++)
                    {
                        for (int px = 0; px < width; px++)
                        {
                            if (pixels[py * width + px].a < .8f)
                            {
                                continue;
                            }
                            x0 = Mathf.Min(x0, px);
                            x1 = Mathf.Max(x1, px);
                            y0 = Mathf.Min(y0, py);
                            y1 = Mathf.Max(y1, py);
                        }
                    }
                    if (x1 < x0 || y1 < y0)
                    {
                        throw new InvalidOperationException("Empty moving dojo art: " + names[i]);
                    }
                    var tile = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
                    try
                    {
                        tile.SetPixels(atlas.GetPixels(x + x0, y + y0, tile.width, tile.height));
                        tile.Apply();
                        string output = PixelPath + names[i] + ".png";
                        File.WriteAllBytes(output, tile.EncodeToPNG());
                        AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
                        ConfigureTexture(output);
                    }
                    finally
                    {
                        Object.DestroyImmediate(tile);
                    }
                }
            }
            finally
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Painted dojo art missing: " + path);
            }
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
