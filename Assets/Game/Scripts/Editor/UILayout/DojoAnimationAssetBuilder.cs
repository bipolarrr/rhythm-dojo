using System;
using System.IO;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.EditorTools
{
    public static class DojoAnimationAssetBuilder
    {
        private const string Root = "Assets/Game/Art/AlphaDojo/Training/";
        private const int Cell = 128;

        public static void Configure(DojoModeView mode)
        {
            var trainee = Bake("Trainee", 5, false);
            var teacher = Bake("Senior", 6, true);
            mode.traineeAnimation = Attach(mode.traineeMotion, "Trainee", new[]
            {
                Pose("Trainee", DojoIdleVariation.Motion.None, trainee, 0, 2f),
                Pose("Trainee", DojoIdleVariation.Motion.Punch, trainee, 1, .72f),
                Pose("Trainee", DojoIdleVariation.Motion.Kick, trainee, 2, 1.05f),
                Pose("Trainee", DojoIdleVariation.Motion.Jump, trainee, 3, 1.15f),
                Pose("Trainee", DojoIdleVariation.Motion.Hands, trainee, 4, 1.25f),
                Pose("Trainee", DojoIdleVariation.Motion.Ready, trainee, 4, 1.05f, new[] { 0, 1, 4, 4, 4, 0 })
            });
            mode.teacherAnimation = Attach(mode.teacherMotion, "Senior", new[]
            {
                Pose("Senior", DojoIdleVariation.Motion.None, teacher, 0, 2.2f),
                Pose("Senior", DojoIdleVariation.Motion.Punch, teacher, 1, .85f),
                Pose("Senior", DojoIdleVariation.Motion.Kick, teacher, 2, 1.15f),
                Pose("Senior", DojoIdleVariation.Motion.Ready, teacher, 3, 1.1f),
                Pose("Senior", DojoIdleVariation.Motion.Point, teacher, 4, .6f),
                Pose("Senior", DojoIdleVariation.Motion.Approve, teacher, 5, .7f)
            });

            // Register legacy travel frames on the same canvas as the new poses.
            mode.poses[1] = Travel(mode.poses[1], "Run1");
            mode.poses[2] = Travel(mode.poses[2], "Run2");
            mode.poses[0] = trainee[0];
            mode.poses[3] = trainee[9];
            mode.teacherPoses = new[] { teacher[0], teacher[27], teacher[27], teacher[33], teacher[0], teacher[9] };
            mode.character.sprite = mode.poses[0];
            mode.teacherMotion.sprite = mode.teacherPoses[0];
            mode.character.rectTransform.sizeDelta = new Vector2(253, 253);
            mode.teacherMotion.rectTransform.sizeDelta = new Vector2(253, 253);
            // Both sheets use the same baked sole baseline (12 / 128).
            mode.teacherMotion.rectTransform.anchoredPosition = new Vector2(170, -112);

            mode.traineeIdle.cooldown = new Vector2(1.5f, 3.5f);
            mode.traineeIdle.variations = new[]
            {
                Variation(mode.traineeAnimation, DojoIdleVariation.Motion.Ready, 3, 6),
                Variation(mode.traineeAnimation, DojoIdleVariation.Motion.Hands, 2, 6),
                Variation(mode.traineeAnimation, DojoIdleVariation.Motion.Punch, 2, 2),
                Variation(mode.traineeAnimation, DojoIdleVariation.Motion.Kick, 1.4f, 2),
                Variation(mode.traineeAnimation, DojoIdleVariation.Motion.Jump, 1.2f, 2)
            };
            mode.teacherIdle.cooldown = new Vector2(1.5f, 3.5f);
            mode.teacherIdle.variations = new[]
            {
                Variation(mode.teacherAnimation, DojoIdleVariation.Motion.Punch, 1, 1),
                Variation(mode.teacherAnimation, DojoIdleVariation.Motion.Kick, 1, 1),
                Variation(mode.teacherAnimation, DojoIdleVariation.Motion.Ready, 1.2f, 1)
            };
        }

        private static DojoIdleVariation.Variation Variation(DojoActorAnimation actor,
            DojoIdleVariation.Motion motion, float weight, int modeMask)
        {
            AnimationClip clip = null;
            foreach (var pose in actor.poses)
            {
                if (pose.motion == motion)
                {
                    clip = pose.clip;
                    break;
                }
            }
            return new DojoIdleVariation.Variation
            {
                motion = motion,
                duration = actor.Duration(motion),
                clip = clip,
                weight = weight,
                modeMask = modeMask
            };
        }

        private static DojoActorAnimation Attach(DojoFigureGraphic graphic, string name,
            DojoActorAnimation.Pose[] poses)
        {
            string path = Root + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            var machine = controller.layers[0].stateMachine;
            foreach (var pose in poses)
            {
                AnimatorState state = null;
                foreach (var child in machine.states)
                {
                    if (child.state.name == pose.clip.name)
                    {
                        state = child.state;
                        break;
                    }
                }
                if (!state)
                {
                    state = machine.AddState(pose.clip.name);
                }
                state.motion = pose.clip;
                state.writeDefaultValues = false;
                if (pose.motion == DojoIdleVariation.Motion.None)
                {
                    machine.defaultState = state;
                }
            }
            EditorUtility.SetDirty(controller);
            var animator = graphic.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.enabled = false;
            var actor = graphic.gameObject.AddComponent<DojoActorAnimation>();
            actor.animator = animator;
            actor.artwork = graphic;
            actor.poses = poses;
            graphic.spriteAnimation = true;
            return actor;
        }

        private static DojoActorAnimation.Pose Pose(string name, DojoIdleVariation.Motion motion,
            Sprite[] sprites, int row, float duration, int[] order = null)
        {
            string title = motion.ToString();
            if (motion == DojoIdleVariation.Motion.None)
            {
                title = "Idle";
            }
            string path = Root + name + title + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (!clip)
            {
                clip = new AnimationClip { name = name + title };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.frameRate = 30;
            var times = new[] { 0f, .12f, .22f, .36f, .56f, .80f, 1f };
            if (motion == DojoIdleVariation.Motion.None)
            {
                times = new[] { 0f, .18f, .34f, .50f, .68f, .84f, 1f };
            }
            var frames = new ObjectReferenceKeyframe[7];
            for (int i = 0; i < frames.Length; i++)
            {
                int column = Mathf.Min(i, 5);
                if (order != null)
                {
                    column = order[column];
                }
                var sprite = sprites[row * 6 + column];
                if (i == 6)
                {
                    sprite = sprites[0];
                }
                frames[i] = new ObjectReferenceKeyframe { time = duration * times[i], value = sprite };
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = motion == DojoIdleVariation.Motion.None;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return new DojoActorAnimation.Pose { motion = motion, clip = clip };
        }

        private static Sprite[] Bake(string name, int rows, bool senior)
        {
            var source = Read(Root + name + "Source.png");
            int size = source.width / 6;
            var home = source.GetPixels(0, (rows - 1) * size, size, size);
            var homeBounds = Bounds(home, size);
            float scale = 104f / homeBounds.height;
            if (senior)
            {
                scale = 112f / homeBounds.height;
            }
            var texture = new Texture2D(Cell * 6, Cell * rows, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[texture.width * texture.height]);
            var idle = new Color[Cell * Cell];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < 6; column++)
                {
                    var pixels = source.GetPixels(column * size, (rows - row - 1) * size, size, size);
                    var bounds = Bounds(pixels, size);
                    bool airborne = !senior && row == 3;
                    airborne &= column == 2 || column == 3;
                    float bottom = bounds.y;
                    float anchor = Foot(pixels, size, bounds, senior);
                    float target = 34;
                    if (senior)
                    {
                        target = 91;
                    }
                    if (airborne)
                    {
                        bottom = homeBounds.y;
                        anchor = homeBounds.center.x;
                        target = 64;
                    }
                    var frame = new Color[Cell * Cell];
                    for (int y = 0; y < Cell; y++)
                    {
                        for (int x = 0; x < Cell; x++)
                        {
                            int sx = Mathf.RoundToInt((x - target) / scale + anchor);
                            int sy = Mathf.RoundToInt((y - 12) / scale + bottom);
                            if (sx < 0 || sx >= size || sy < 0)
                            {
                                continue;
                            }
                            if (sy >= size)
                            {
                                continue;
                            }
                            var pixel = pixels[sy * size + sx];
                            if (pixel.a < .65f)
                            {
                                continue;
                            }
                            pixel.a = 1;
                            frame[y * Cell + x] = pixel;
                        }
                    }
                    if (row == 0 && column == 0)
                    {
                        idle = frame;
                    }
                    if (row == 0)
                    {
                        // Register the breathing drawings to identical feet and lower legs.
                        Array.Copy(idle, frame, Cell * 46);
                    }
                    else if (column == 0 || column == 5)
                    {
                        frame = idle;
                    }
                    texture.SetPixels(column * Cell, (rows - row - 1) * Cell, Cell, Cell, frame);
                }
            }
            texture.Apply();
            string path = Root + name + "Frames.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(texture);
            var imported = Import(path);
            var sprites = new Sprite[rows * 6];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = SpriteAsset(imported,
                    new Rect(i % 6 * Cell, (rows - i / 6 - 1) * Cell, Cell, Cell), name + "Frame" + i);
            }
            return sprites;
        }

        private static float Foot(Color[] pixels, int size, RectInt bounds, bool senior)
        {
            int left = size;
            int right = 0;
            for (int y = bounds.y; y < bounds.y + 8; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a < .75f)
                    {
                        continue;
                    }
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                }
            }
            if (senior)
            {
                return right;
            }
            return left;
        }

        private static RectInt Bounds(Color[] pixels, int size)
        {
            int left = size;
            int bottom = size;
            int right = 0;
            int top = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a < .75f)
                    {
                        continue;
                    }
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }
            if (left > right || bottom > top)
            {
                throw new InvalidOperationException("Animation frame is empty.");
            }
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                throw new InvalidOperationException("Cannot read animation source: " + path);
            }
            return texture;
        }

        private static Texture2D Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite Travel(Sprite source, string name)
        {
            var texture = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            var pixels = new Color[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    // Match the original 190 x 253 artwork inside a square animation canvas.
                    float sx = (x - 16) / 96f;
                    if (sx < 0 || sx >= 1)
                    {
                        continue;
                    }
                    int tx = Mathf.FloorToInt(source.rect.x + sx * source.rect.width);
                    int ty = Mathf.FloorToInt(source.rect.y + y / 128f * source.rect.height);
                    pixels[y * Cell + x] = source.texture.GetPixel(tx, ty);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            string path = Root + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return SpriteAsset(Import(path), new Rect(0, 0, Cell, Cell), name);
        }

        private static Sprite SpriteAsset(Texture2D texture, Rect rect, string name)
        {
            string path = Root + name + ".asset";
            var generated = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            generated.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
    }
}
