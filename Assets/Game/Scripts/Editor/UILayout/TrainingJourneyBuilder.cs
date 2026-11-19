using System;
using System.Collections.Generic;
using System.IO;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.EditorTools
{
    public static class TrainingJourneyBuilder
    {
        public const string ArtPath = "Assets/Game/Art/TrainingJourney/";
        private const int SceneryLayer = 30;

        public static void Build(Canvas background, GameplayBackgroundDirector director)
        {
            int insertion = director.room.picture.transform.GetSiblingIndex();
            PrepareArt();
            director.mountain = BuildMountain(background);
            director.run = BuildRun(background);
            director.dummy = BuildDummy(background);
            // The preserved dojo projection stays over the arriving mountain during
            // its existing continuous threshold crossing.
            director.mountain.picture.transform.SetSiblingIndex(insertion);
            director.run.picture.transform.SetSiblingIndex(insertion + 1);
            director.dummy.picture.transform.SetSiblingIndex(insertion + 2);
            director.room.picture.transform.SetSiblingIndex(insertion + 3);
            var passage = new GameObject("Wind Through The Training Journey", typeof(RectTransform),
                typeof(JourneyPassageGraphic));
            passage.transform.SetParent(background.transform, false);
            passage.transform.SetSiblingIndex(insertion + 4);
            Stretch((RectTransform)passage.transform);
            director.passage = passage.GetComponent<JourneyPassageGraphic>();
            director.passage.raycastTarget = false;
        }

        private static MountainPhaseView BuildMountain(Canvas background)
        {
            var view = Stage<MountainPhaseView>(background, "Mountain Ascent", 2000);
            var surfaces = new List<Renderer>();
            var scenery = new List<JourneyScenery>();
            var distance = Material("Mountain distance", "MountainDistance", Color.white, 0, 0, 1);
            view.horizon = Card(view.transform, surfaces, "Valley below the summit", new Vector3(-6, 30, 190),
                new Vector2(520, 292.5f), distance);
            view.horizonPosition = view.horizon.localPosition;
            var cliff = Material("Granite crags", "Cliff", new Color(.77f, .82f, .81f));
            var pine = Material("Living pines", "Pine", new Color(.72f, .81f, .75f), 1);
            var brush = Material("Roots and ferns", "Brush", new Color(.8f, .85f, .78f), .6f);
            var ground = Material("Worn trail", "TrailSurface", new Color(.76f, .78f, .73f));
            var gate = Material("Training gateway", "Gate", new Color(.82f, .86f, .81f));
            for (int i = 0; i < 17; i++)
            {
                float z = i * 4 - 4;
                Trail(view.transform, surfaces, "Climbing path " + i, z, 4, true, ground);
            }
            for (int i = 0; i < 22; i++)
            {
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                float z = 4 + i / 2 * 5 + Mathf.Sin(i * 2.3f) * 1.7f;
                float y = MountainPhaseView.TrailHeight(z);
                float height = 9 - i / 2 * .35f;
                var rock = Card(view.transform, surfaces, "Passing granite " + i,
                    new Vector3(side * (7.5f + Mathf.Sin(i * 1.9f) * 1.7f), y + height * .5f - 1.1f, z),
                    new Vector2(height * (.7f + i % 3 * .13f), height), cliff);
                scenery.Add(Motion(rock, 0));
                if (i < 10 || i % 4 == 0)
                {
                    float treeHeight = 6.2f + Mathf.Sin(i * 2.7f) * 1.6f;
                    var tree = Card(view.transform, surfaces, "Wind-shaped pine " + i,
                        new Vector3(side * (6.9f + i % 3), y + treeHeight * .45f, z + 2),
                        new Vector2(treeHeight * 1.3f * side, treeHeight), pine);
                    scenery.Add(Motion(tree, .32f));
                }
                var shrub = Card(view.transform, surfaces, "Near trail roots " + i,
                    new Vector3(side * (4.2f + i % 3 * .3f), y + .45f, z - 1),
                    new Vector2((2.8f + i % 3 * .25f) * side, 1.9f), brush);
                scenery.Add(Motion(shrub, .6f));
            }
            for (int i = 0; i < 2; i++)
            {
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                var ledge = Card(view.transform, surfaces, "Summit overlook ledge " + i,
                    new Vector3(side * 4.1f, 12.7f, 63),
                    new Vector2(side * 4, 1.8f), brush);
                scenery.Add(Motion(ledge, .15f));
            }
            Card(view.transform, surfaces, "Departure from the mountain dojo", new Vector3(0, 4.5f, 14),
                new Vector2(14, 10), gate);
            view.scenery = scenery.ToArray();
            view.mist = Atmosphere(view.transform, surfaces, true);
            view.surfaces = surfaces.ToArray();
            view.ResetPresentation();
            view.SetVisible(false);
            return view;
        }

        private static RunPhaseView BuildRun(Canvas background)
        {
            var view = Stage<RunPhaseView>(background, "Ridge Run", 3000);
            var surfaces = new List<Renderer>();
            var scenery = new List<JourneyScenery>();
            var roadside = new List<JourneyScenery>();
            var road = new List<JourneyScenery>();
            Card(view.transform, surfaces, "Ridge horizon", new Vector3(0, 18, 180), new Vector2(390, 220),
                Material("Ridge distance", "MountainDistance", new Color(.78f, .83f, .77f), 0, 0, 1));
            var pine = Material("Running pines", "Pine", new Color(.58f, .71f, .66f), 1);
            var brush = Material("Running roots", "Brush", new Color(.71f, .78f, .67f), .7f);
            var ground = Material("Ridge trail", "TrailSurface", new Color(.68f, .72f, .65f));
            for (int i = 0; i < 24; i++)
            {
                var tile = Trail(view.transform, surfaces, "Ridge trail section " + i, 0, 4, false, ground);
                tile.localPosition = new Vector3(0, 0, i * 4);
                road.Add(Motion(tile, 0));
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                float height = 6.5f + Mathf.Sin(i * 2.1f) * 1.5f;
                var tree = Card(view.transform, surfaces, "Passing pine " + i,
                    new Vector3(side * (7.8f + Mathf.Sin(i * 2.9f) * 2), height * .48f - .6f, i * 4),
                    new Vector2(height * 1.2f * side, height), pine);
                var motion = Motion(tree, .5f);
                scenery.Add(motion);
                roadside.Add(motion);
                var shrub = Card(view.transform, surfaces, "Passing undergrowth " + i,
                    new Vector3(side * (5.4f + i % 3 * .7f), .25f, i * 4 + 2),
                    new Vector2(side * (3.3f + i % 3 * .3f), 2.2f), brush);
                motion = Motion(shrub, 1);
                scenery.Add(motion);
                roadside.Add(motion);
            }
            var wind = AirMaterial("Ridge wind", new Color(.66f, .73f, .64f, .22f), 1);
            for (int i = 0; i < 16; i++)
            {
                float side = -1;
                if (i % 2 == 0)
                {
                    side = 1;
                }
                var streak = Card(view.transform, surfaces, "Pine wind wake " + i,
                    new Vector3(side * (3.6f + i % 4 * .9f), .6f + i % 3 * .7f, i * 6),
                    new Vector2(.075f, 5 + i % 3), wind);
                streak.localRotation = Quaternion.Euler(90, 0, 0);
                roadside.Add(Motion(streak, 0));
            }
            view.gateway = Card(view.transform, surfaces, "Return to the training court", new Vector3(0, 5, 110),
                new Vector2(19, 14), Material("Ridge gateway", "Gate", new Color(.68f, .77f, .72f)));
            view.scenery = scenery.ToArray();
            view.roadside = roadside.ToArray();
            view.road = road.ToArray();
            view.mist = Atmosphere(view.transform, surfaces, false);
            view.surfaces = surfaces.ToArray();
            view.ResetPresentation();
            view.SetVisible(false);
            return view;
        }

        private static DummyPhaseView BuildDummy(Canvas background)
        {
            var view = Stage<DummyPhaseView>(background, "Wooden Dummy Court", 4000);
            var surfaces = new List<Renderer>();
            var scenery = new List<JourneyScenery>();
            var pieces = new List<JourneyScenery>();
            Card(view.transform, surfaces, "Mountain training court", new Vector3(0, 1.8f, 24),
                new Vector2(48.4f, 27.25f), Material("Court pigment", "TrainingCourt", new Color(.75f, .8f, .79f)));
            var pine = Material("Court pine", "Pine", new Color(.42f, .54f, .46f), 1);
            for (int i = 0; i < 2; i++)
            {
                float side = -1;
                if (i == 1)
                {
                    side = 1;
                }
                var tree = Card(view.transform, surfaces, "Court living branches " + i,
                    new Vector3(side * 11.4f, 3.6f, 17), new Vector2(9, 7.8f), pine);
                scenery.Add(Motion(tree, .3f));
            }

            view.axis = new GameObject("Wooden pivot at the anchored base").transform;
            view.axis.SetParent(view.transform, false);
            view.axis.localPosition = new Vector3(0, .2f, 5.5f);
            var wood = new Color(.83f, .78f, .65f);
            Card(view.transform, surfaces, "Anchored dummy base", new Vector3(0, .28f, 5.6f),
                new Vector2(2.7f, .95f), Material("Dummy base", "Base", wood));
            AddLog(view, surfaces, pieces, "Lower", .95f, 1.25f, 1.5f, wood);
            AddLog(view, surfaces, pieces, "Upper", 2.28f, 1.25f, 1.4f, wood);
            var head = Card(view.axis, surfaces, "Dummy crown", new Vector3(0, 3.48f, -.01f),
                new Vector2(1.25f, 1.1f), Material("Dummy head", "Head", wood, 0, 1));
            pieces.Add(Motion(head, 0));
            view.joints = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = -1;
                string part = "LeftArm";
                if (i == 1)
                {
                    side = 1;
                    part = "RightArm";
                }
                var joint = new GameObject("Rotating striking peg " + i).transform;
                joint.SetParent(view.axis, false);
                joint.localPosition = new Vector3(side * .45f, 2.4f, -.12f);
                Card(joint, surfaces, "Hand carved arm " + i, new Vector3(side * .95f, -.12f, -.03f),
                    new Vector2(2.25f, 1.3f), Material("Dummy " + part, part, wood, 0, 1));
                view.joints[i] = joint;
                pieces.Add(Motion(joint, 0));
            }
            view.pieces = pieces.ToArray();
            view.hands = new Transform[2];
            view.handRest = new[] { new Vector3(-2.65f, .25f, 3.4f), new Vector3(2.65f, .25f, 3.4f) };
            view.handContact = new[] { new Vector3(-2.65f, 1.9f, 4.45f), new Vector3(2.65f, 1.9f, 4.45f) };
            for (int i = 0; i < 2; i++)
            {
                string name = "LeftHand";
                float side = -1;
                if (i == 1)
                {
                    name = "RightHand";
                    side = 1;
                }
                view.hands[i] = Card(view.transform, surfaces, name, view.handRest[i], new Vector2(1.85f, 1.85f),
                    Material(name + " pigment", name, new Color(.85f, .82f, .76f)));
                var sleeve = Material(name + " extended sleeve", name, new Color(.85f, .82f, .76f));
                sleeve.mainTextureScale = new Vector2(1, .2f);
                EditorUtility.SetDirty(sleeve);
                var continuation = Card(view.hands[i], surfaces, "Continuous first-person sleeve " + i,
                    new Vector3(0, -.49f, .015f), Vector2.one, sleeve);
                continuation.GetComponent<MeshFilter>().sharedMesh = SleeveMesh(name, side);
            }
            view.chips = new Transform[8];
            var chipMaterial = Material("Wood chips", "Splinters", new Color(.8f, .7f, .5f));
            for (int i = 0; i < view.chips.Length; i++)
            {
                view.chips[i] = Card(view.transform, surfaces, "Fixed wood chip " + i, Vector3.zero,
                    new Vector2(.10f, .14f), chipMaterial);
            }
            view.scenery = scenery.ToArray();
            view.mist = Atmosphere(view.transform, surfaces, false);
            view.surfaces = surfaces.ToArray();
            view.ResetPresentation();
            view.SetVisible(false);
            return view;
        }

        private static void AddLog(DummyPhaseView view, List<Renderer> surfaces,
            List<JourneyScenery> pieces, string name, float y, float width, float height, Color tint)
        {
            var material = Material("Dummy " + name, name, tint, 0, 1);
            for (int i = 0; i < 4; i++)
            {
                var part = Card(view.axis, surfaces, name + " grain fragment " + i,
                    new Vector3(0, y, 0), new Vector2(width, height), material);
                part.GetComponent<MeshFilter>().sharedMesh = FragmentMesh(i);
                pieces.Add(Motion(part, 0));
            }
        }

        private static T Stage<T>(Canvas background, string name, float x) where T : TrainingJourneyView
        {
            var view = new GameObject(name + " Spatial Stage").AddComponent<T>();
            view.transform.position = new Vector3(x, 0, 0);
            var camera = new GameObject(name + " Perspective Camera").AddComponent<Camera>();
            camera.transform.SetParent(view.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.8f, 0);
            camera.cullingMask = 1 << SceneryLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.2f, .26f, .25f);
            camera.fieldOfView = 58;
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 250;
            camera.depth = -89;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            view.sceneryCamera = camera;
            var picture = new GameObject(name + " Projection", typeof(RectTransform), typeof(RawImage), typeof(CanvasGroup));
            picture.transform.SetParent(background.transform, false);
            Stretch((RectTransform)picture.transform);
            picture.GetComponent<RawImage>().raycastTarget = false;
            view.picture = picture.GetComponent<CanvasGroup>();
            view.picture.blocksRaycasts = false;
            picture.AddComponent<GameplayViewport>().gameplayCamera = camera;
            return view;
        }

        private static JourneyScenery[] Atmosphere(Transform parent, List<Renderer> surfaces, bool rising)
        {
            var items = new List<JourneyScenery>();
            var material = AirMaterial("Valley air", new Color(.67f, .77f, .75f, .23f));
            for (int i = 0; i < 8; i++)
            {
                float z = 12 + i * 18;
                float y = 1.4f + i % 3 * 4;
                if (rising)
                {
                    y += MountainPhaseView.TrailHeight(z) * .8f;
                }
                var item = Card(parent, surfaces, "Moving mist bank " + i,
                    new Vector3(Mathf.Sin(i * 2.4f) * 13, y, z), new Vector2(26 + i * 3, 3 + i % 3), material);
                var motion = Motion(item, 0);
                motion.drift = 6 + i;
                items.Add(motion);
            }
            if (rising)
            {
                var cloud = AirMaterial("High moving clouds", new Color(.92f, .86f, .72f, .3f));
                for (int i = 0; i < 3; i++)
                {
                    var item = Card(parent, surfaces, "High sky cloud " + i,
                        new Vector3(-30 + i * 28, 48 + i * 6, 155 + i * 10), new Vector2(85, 12), cloud);
                    var motion = Motion(item, 0);
                    motion.drift = 12 + i * 5;
                    items.Add(motion);
                }
            }
            return items.ToArray();
        }

        private static Material AirMaterial(string name, Color color, float rush = 0)
        {
            string path = ArtPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("RhythmDojo/Journey Air"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Rush", rush);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Trail(Transform parent, List<Renderer> surfaces, string name,
            float z, float length, bool rising, Material material)
        {
            string meshPath = ArtPath + "Trail";
            float y0 = 0;
            float y1 = 0;
            float x0 = 0;
            float x1 = 0;
            if (rising)
            {
                y0 = MountainPhaseView.TrailHeight(z);
                y1 = MountainPhaseView.TrailHeight(z + length);
                x0 = Mathf.Sin(z * .075f) * 1.1f;
                x1 = Mathf.Sin((z + length) * .075f) * 1.1f;
                meshPath += " climb " + z.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            meshPath += ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (!mesh)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            mesh.Clear();
            float[] widths = { -16, -8, -4.2f, 4.2f, 8, 16 };
            float[] heights = { -6, -1.5f, 0, 0, -1.5f, -6 };
            if (!rising)
            {
                // The running ridge has broad, grounded shoulders for trees even
                // when the successful run opens the sides of the view.
                widths = new[] { -28f, -18, -8, -4.2f, 4.2f, 8, 18, 28 };
                heights = new[] { -3f, -.8f, -.1f, 0, 0, -.1f, -.8f, -3 };
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < widths.Length; i++)
            {
                vertices.Add(new Vector3(x0 + widths[i], y0 + heights[i], z));
                vertices.Add(new Vector3(x1 + widths[i], y1 + heights[i], z + length));
                uv.Add(new Vector2(widths[i] / 8, 0));
                uv.Add(new Vector2(widths[i] / 8, 1));
                if (i < widths.Length - 1)
                {
                    int start = i * 2;
                    triangles.AddRange(new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 });
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var item = Card(parent, surfaces, name, Vector3.zero, Vector2.one, material);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            return item;
        }

        private static Mesh SleeveMesh(string name, float side)
        {
            string path = ArtPath + name + " sleeve continuation.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = name + " sleeve continuation" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.vertices = new[]
            {
                new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0),
                new Vector3(-.5f + side * .9f, -1.8f, 0), new Vector3(.5f + side * .9f, -1.8f, 0),
            };
            // Match the hand's bottom texels at the seam, then carry the cloth
            // diagonally out of frame with an authored extension of the forearm.
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh FragmentMesh(int index)
        {
            string path = ArtPath + "Wood grain split " + index + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh)
            {
                return mesh;
            }
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row <= 12; row++)
            {
                float y = row / 12f;
                for (int edge = index; edge <= index + 1; edge++)
                {
                    float x = edge / 4f;
                    if (edge > 0 && edge < 4)
                    {
                        x += Mathf.Sin(row * 2.7f + edge * 1.9f) * .025f;
                    }
                    vertices.Add(new Vector3(x - .5f, y - .5f, 0));
                    uv.Add(new Vector2(x, y));
                }
                if (row == 12)
                {
                    continue;
                }
                int start = row * 2;
                triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }
            mesh = new Mesh { name = "Authored grain fracture " + index };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static JourneyScenery Motion(Transform item, float sway)
        {
            return new JourneyScenery
            {
                item = item, position = item.localPosition, rotation = item.localEulerAngles, sway = sway,
            };
        }

        private static Transform Card(Transform parent, List<Renderer> surfaces, string name,
            Vector3 position, Vector2 size, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.name = name;
            item.layer = SceneryLayer;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surfaces.Add(renderer);
            return item.transform;
        }

        private static Material Material(string name, string texture, Color tint,
            float sway = 0, float wood = 0, float sky = 0)
        {
            string path = ArtPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("RhythmDojo/Journey Pigment"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + texture + ".png");
            if (!material.mainTexture)
            {
                throw new InvalidOperationException("Journey artwork missing: " + texture);
            }
            material.color = tint;
            material.SetFloat("_Sway", sway);
            material.SetFloat("_Wood", wood);
            material.SetFloat("_Sky", sky);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PrepareArt()
        {
            ConfigureTexture(ArtPath + "MountainDistance.png");
            ConfigureTexture(ArtPath + "TrainingCourt.png");
            ConfigureTexture(ArtPath + "TrailSurface.png", true);
            Extract("NatureAtlas", 2, new[] { "Cliff", "Pine", "Brush", "Gate" });
            Extract("DummyAtlas", 3, new[]
            {
                "Head", "Upper", "Lower", "LeftArm", "RightArm", "Base", "LeftHand", "RightHand", "Splinters",
            });
        }

        private static void Extract(string source, int columns, string[] names)
        {
            string path = ArtPath + source + ".png";
            ConfigureTexture(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.SaveAndReimport();
            try
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int width = atlas.width / columns;
                int height = atlas.height / columns;
                for (int i = 0; i < names.Length; i++)
                {
                    int x = i % columns * width;
                    int y = (columns - 1 - i / columns) * height;
                    int x0 = width;
                    int x1 = -1;
                    int y0 = height;
                    int y1 = -1;
                    var pixels = atlas.GetPixels(x, y, width, height);
                    for (int py = 0; py < height; py++)
                    {
                        for (int px = 0; px < width; px++)
                        {
                            if (pixels[py * width + px].a < .5f)
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
                        throw new InvalidOperationException("Empty journey atlas cell: " + names[i]);
                    }
                    var tile = new Texture2D(x1 - x0 + 1, y1 - y0 + 1, TextureFormat.RGBA32, false);
                    try
                    {
                        tile.SetPixels(atlas.GetPixels(x + x0, y + y0, tile.width, tile.height));
                        tile.Apply();
                        string output = ArtPath + names[i] + ".png";
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

        private static void ConfigureTexture(string path, bool ground = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer)
            {
                throw new InvalidOperationException("Training journey source missing: " + path);
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = ground;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (ground)
            {
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 4;
            }
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
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
