using RhythmDojo.Core;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public sealed class GameplayBackgroundBuilder
    {
        private const int SceneryLayer = 30;
        private readonly Material material;
        private readonly ActionStageArtwork art;

        private GameplayBackgroundBuilder(ActionStageArtwork art, int index)
        {
            this.art = art;
            material = art.StageMaterial(index);
        }

        public static void Build(Camera displayCamera, GameplayCompositionRoot root)
        {
            var art = new ActionStageArtwork();
            displayCamera.cullingMask = 1 << SceneryLayer;
            displayCamera.orthographic = true;
            displayCamera.orthographicSize = 135;
            displayCamera.farClipPlane = 64;

            var stage = new GameObject("Mountain dojo action stages");
            var room = stage.AddComponent<DojoRoomPhaseView>();
            room.stageCamera = displayCamera;
            room.poses = art.poses;
            room.effects = art.effects;
            room.scenery = new DojoTrainingStage[]
            {
                new GameplayBackgroundBuilder(art, 0).Combat(stage.transform, true),
                new GameplayBackgroundBuilder(art, 1).Waterfall(stage.transform),
                new GameplayBackgroundBuilder(art, 2).Race(stage.transform),
                new GameplayBackgroundBuilder(art, 3).Combat(stage.transform, false)
            };
            var director = stage.AddComponent<GameplayBackgroundDirector>();
            director.room = room;
            SceneDependencyAssembler.Wire(root, "backgroundDirector", director);
            director.Validate();
            room.ResetPresentation();
            AssetDatabase.SaveAssets();
        }

        private T Stage<T>(Transform parent, string name, float x, float y) where T : DojoTrainingStage
        {
            var stage = Child(parent, name).gameObject.AddComponent<T>();
            stage.floor = Child(stage.transform, "Training mark", x, y);
            stage.shadow = Art(stage.floor, "Ground shadow", art.shadow, 0, 0, 18);
            stage.trainee = Art(stage.floor, "Trainee", art.poses[5], 0, 0, 25);
            return stage;
        }

        private DojoCombatStage Combat(Transform parent, bool sparring)
        {
            string name = "Timber gallery";
            int index = 3;
            if (sparring)
            {
                name = "Morning courtyard";
                index = 0;
            }
            var stage = Stage<DojoCombatStage>(parent, name, 397, 36);
            stage.sparring = sparring;
            Art(stage.transform, "Courtyard", art.locations[index], 240, 0, 0);
            stage.flags = art.flags;
            stage.pennant = Art(stage.transform, "Wind pennant", art.flags[0], 143, 52, 5, 1.22f);
            Art(stage.transform, "Garden rock", art.equipment[2], 237, 49, 4, .85f);
            if (sparring)
            {
                stage.partnerPoses = art.partner;
                stage.partner = Art(stage.floor, "Indigo sash partner", art.partner[0], 34, 4, 23, .94f);
                stage.partner.transform.localScale = new Vector3(-.94f, .94f, 1);
                stage.partnerShadow = Art(stage.floor, "Partner shadow", art.shadow, 34, 3, 17, .82f);
                Art(stage.transform, "Practice post in the far yard", art.equipment[6], 50, 47, 4, .57f);
                Art(stage.transform, "Practice post arms", art.equipment[5], 46, 60, 5, .53f);
            }
            else
            {
                Art(stage.floor, "Post shadow", art.shadow, 34, 0, 16);
                Art(stage.floor, "Braced footing", art.equipment[6], 40, 0, 20, .92f);
                stage.wood = Art(stage.floor, "Pivoting striking post", art.equipment[5], 29, 22, 22);
            }
            stage.echo = Echo(stage);
            stage.contact = Art(stage.floor, "Contact flash", art.effects[18], 8, 45, 27);
            stage.debris = Art(stage.floor, "Chips or footfall", art.effects[24], 8, 45, 28);
            Art(stage.transform, "Near leaves", art.equipment[3], 8, -14, 35, 1.5f);
            stage.leaves = new SpriteRenderer[7];
            for (int i = 0; i < stage.leaves.Length; i++)
            {
                int order = 8 + i * 4;
                stage.leaves[i] = Art(stage.transform, "Maple leaf " + i, art.effects[36], 0, 0, order, .83f);
            }
            return stage;
        }

        private WaterfallStage Waterfall(Transform parent)
        {
            var stage = Stage<WaterfallStage>(parent, "Jade falls", 385, 46);
            Art(stage.transform, "Ravine", art.locations[1], 240, 0, 0);
            stage.cascade = art.cascade;
            stage.curtain = Art(stage.transform, "Breaking curtain", art.cascade[0], 375.5f, 44, 2);
            Art(stage.floor, "River stone", art.equipment[2], 0, -19, 14, 1.12f);
            Art(stage.transform, "Near bank reeds", art.equipment[3], 18, -11, 38, 1.8f);
            Art(stage.transform, "Near bank granite", art.equipment[2], 458, -14, 39, 1.45f);
            stage.streams = new SpriteRenderer[6];
            for (int i = 0; i < stage.streams.Length; i++)
            {
                int order = 12;
                if (i == 1 || i == 4)
                {
                    order = 29;
                }
                stage.streams[i] = Art(stage.floor, "Falling rivulet " + i, art.effects[6], 0, 0, order);
            }
            stage.spray = new SpriteRenderer[5];
            var spray = new[] { new Vector2(-40, 8), new Vector2(34, 7), new Vector2(-14, 46),
                new Vector2(14, 46), new Vector2(0, 56) };
            for (int i = 0; i < spray.Length; i++)
            {
                int order = 16;
                if (i > 1)
                {
                    order = 30;
                }
                stage.spray[i] = Art(stage.floor, "Impact spray " + i, art.effects[0], spray[i].x, spray[i].y, order);
            }
            stage.ripples = new SpriteRenderer[4];
            for (int i = 0; i < stage.ripples.Length; i++)
            {
                stage.ripples[i] = Art(stage.floor, "Surface ring " + i, art.effects[12], -53 + i * 36,
                    -15 - i % 3 * 5, 15, 1.22f);
            }
            return stage;
        }

        private RidgeRunStage Race(Transform parent)
        {
            var stage = Stage<RidgeRunStage>(parent, "Evening ridge race", 387, 34);
            stage.horizon = new SpriteRenderer[3];
            stage.forest = new SpriteRenderer[7];
            stage.trail = new SpriteRenderer[3];
            stage.verge = new SpriteRenderer[6];
            for (int i = 0; i < stage.horizon.Length; i++)
            {
                stage.horizon[i] = Art(stage.transform, "Remote peaks " + i, art.locations[2], 0, 0, 0);
                stage.trail[i] = Art(stage.transform, "Ochre track " + i, art.trail, 0, 0, 10);
            }
            for (int i = 0; i < stage.forest.Length; i++)
            {
                float size = 1.05f + .18f * (i % 3);
                stage.forest[i] = Art(stage.transform, "Ridge cedars " + i, art.equipment[1], 0, 0, 3, size);
                stage.forest[i].color = new Color(.65f, .73f, .80f);
            }
            stage.log = Art(stage.floor, "Hurdle log", art.equipment[7], 0, 0, 19, .81f);
            stage.branch = Art(stage.floor, "Low bough", art.branch, 0, 0, 21, .85f);
            stage.echo = Echo(stage);
            stage.debris = Art(stage.floor, "Shoe dust", art.effects[30], 0, 0, 22);
            for (int i = 0; i < stage.verge.Length; i++)
            {
                int drawing = 3;
                float size = 1.18f;
                float y = -22;
                if (i == 1 || i == 5)
                {
                    drawing = 2;
                }
                if (i == 3)
                {
                    drawing = 0;
                    size = 1.25f;
                    y = -87;
                }
                stage.verge[i] = Art(stage.transform, "Passing verge " + i, art.equipment[drawing], 0, y, 34, size);
            }
            return stage;
        }

        private SpriteRenderer Echo(DojoTrainingStage stage)
        {
            var echo = Art(stage.floor, "Movement afterimage", art.poses[6], 0, 0, 24);
            echo.color = new Color(.66f, .54f, .67f, .36f);
            return echo;
        }

        private SpriteRenderer Art(Transform parent, string name, Sprite sprite, float x, float y, int order,
            float size = 1)
        {
            var item = Child(parent, name, x, y);
            item.gameObject.layer = SceneryLayer;
            item.localScale = new Vector3(size, size, 1);
            var renderer = item.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order - 300;
            return renderer;
        }

        private static Transform Child(Transform parent, string name, float x = 0, float y = 0)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = new Vector3(x, y, 0);
            return item;
        }
    }
}
