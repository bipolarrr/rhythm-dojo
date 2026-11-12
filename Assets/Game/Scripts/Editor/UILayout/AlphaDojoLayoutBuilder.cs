using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class AlphaDojoLayoutBuilder
    {
        private const string Root = "Assets/Game/Art/AlphaDojo/";
        private static readonly Color Ink = new Color(.12f, .065f, .04f);
        private static readonly Color Paper = new Color(1, .89f, .68f);

        public static void Apply(MainMenuView view) => Apply(view, true);

        public static void Apply(MainMenuView view, bool modeSelection)
        {
            var canvas = view.transform;
            // Keep the established view/actions and door objects; replace their authored presentation.
            view.trainingInterior.gameObject.SetActive(false);
            view.slidingLogo.gameObject.SetActive(false);
            view.slidingPlaque.enabled = false;
            Object.DestroyImmediate(view.logo.GetComponent<DojoPlaqueHitArea>());
            view.logo.transform.SetParent(view.entrance.frame.transform, false);
            Place((RectTransform)view.logo.transform, new Vector2(.5f, .82f), Vector2.zero, new Vector2(535, 115));
            var plaque = Panel("Timber Sign", view.logo.transform, Color.clear);
            Stretch(plaque.rectTransform);
            plaque.transform.SetAsFirstSibling();
            view.logo.targetGraphic = plaque;
            plaque.raycastTarget = true;
            view.title.enabled = true;
            view.title.text = "리듬 도장";
            view.title.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            view.title.fontSize = 100;
            view.title.verticalOverflow = VerticalWrapMode.Overflow;
            view.title.color = Ink;
            foreach (var effect in view.title.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
            Place(view.title.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(505, 115));
            view.logo.gameObject.AddComponent<UiFontScope>().font = view.title.font;
            view.logoArtwork = view.title.rectTransform;
            var lettering = Art("Brush Title", view.logo.transform, DojoRenewalLayoutBuilder.Load("BrushTitle"),
                new Vector2(0, -3), new Vector2(490, 150));
            lettering.preserveAspect = true;
            view.title.enabled = false;
            view.logoArtwork = lettering.rectTransform;
            view.startPrompt.text = "문을 두드려 입장  ·  Enter";
            view.startPrompt.transform.SetParent(canvas, false);
            Place(view.startPrompt.rectTransform, new Vector2(.5f, .15f), Vector2.zero, new Vector2(540, 48));
            view.startPrompt.fontSize = 26;
            view.startPrompt.color = Paper;
            view.startPrompt.gameObject.AddComponent<Outline>().effectColor = Ink;
            var trigger = (RectTransform)view.entranceTrigger.transform;
            trigger.anchorMin = new Vector2(.24f, .17f);
            trigger.anchorMax = new Vector2(.76f, .70f);
            trigger.offsetMin = trigger.offsetMax = Vector2.zero;

            if (!modeSelection)
            {
                view.entrance.trainingMenu = null;
                DojoRenewalLayoutBuilder.BuildEntrance(view);
                return;
            }

            var menu = (RectTransform)view.menuPanel.transform;
            menu.SetParent(canvas, false);
            Stretch(menu);
            menu.localScale = Vector3.one;
            menu.SetSiblingIndex(view.entrance.transform.GetSiblingIndex());
            var stage = new GameObject("Mode Stages", typeof(RectTransform), typeof(DojoModeView));
            stage.transform.SetParent(menu, false);
            Stretch((RectTransform)stage.transform);
            stage.AddComponent<DojoCompositionFit>().Fit();
            stage.transform.SetAsFirstSibling();
            var mode = stage.GetComponent<DojoModeView>();
            view.modeSelection = mode;
            mode.stages = new CanvasGroup[4];
            mode.props = new RectTransform[4];
            mode.companions = new RectTransform[4];
            mode.banners = new RectTransform[4];
            mode.characterMarks = new RectTransform[4];
            mode.lessonMarks = new Image[4];
            mode.scrollMotion = new DojoFigureGraphic[4];
            mode.farLayers = new RectTransform[4];
            mode.middleLayers = new RectTransform[4];
            mode.nearLayers = new RectTransform[4];
            mode.roomRoot = Group("Training Spaces", stage.transform);
            mode.poses = new[] { Actor(0), Actor(1), Actor(2), Actor(3) };
            mode.teacherPoses = new[] { Senior(0), Senior(1), Senior(2), Senior(3), Senior(4), Senior(5) };
            BuildIdleVariations(mode);
            var backgrounds = new[] { DojoRenewalLayoutBuilder.Load("LessonRoom"),
                DojoRenewalLayoutBuilder.Load("PracticeHall"), Summit(), DojoRenewalLayoutBuilder.Load("ScribingRoom") };
            for (int i = 0; i < 4; i++)
            {
                var group = new GameObject("Stage " + (i + 1), typeof(RectTransform), typeof(CanvasGroup));
                group.transform.SetParent(mode.roomRoot, false);
                Stretch((RectTransform)group.transform);
                mode.stages[i] = group.GetComponent<CanvasGroup>();
                mode.stages[i].alpha = 0;
                mode.stages[i].blocksRaycasts = false;
                if (i == 1)
                {
                    mode.stages[i].alpha = 1;
                }
                var backdrop = Art("Backdrop", group.transform, backgrounds[i], Vector2.zero, new Vector2(1280, 720));
                Stretch(backdrop.rectTransform);
                var fit = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = backgrounds[i].rect.width / backgrounds[i].rect.height;
                mode.farLayers[i] = backdrop.rectTransform;
                mode.middleLayers[i] = Group("Training Objects", group.transform);
                var mark = new GameObject("Trainee Standing Mark", typeof(RectTransform));
                mark.transform.SetParent(group.transform, false);
                mode.characterMarks[i] = (RectTransform)mark.transform;
                Place(mode.characterMarks[i], new Vector2(.5f, .5f), new Vector2(-30, -122), Vector2.zero);
                if (i == 3)
                {
                    // The editor is a close view of the manuscript desk. Its sage and tools
                    // belong to the painting, not the standing figures of the training floor.
                    mode.nearLayers[i] = Group("Foreground", group.transform);
                    var light = new GameObject("Manuscript Lamplight", typeof(RectTransform),
                        typeof(DojoInkGraphic), typeof(CanvasGroup));
                    light.transform.SetParent(backdrop.transform, false);
                    var glow = light.GetComponent<DojoInkGraphic>();
                    glow.wash = true;
                    glow.raycastTarget = false;
                    glow.color = new Color(1f, .63f, .22f, .15f);
                    Place(glow.rectTransform, new Vector2(.5f, .5f),
                        new Vector2(-160, -60), new Vector2(190, 205));
                    mode.scribingLight = light.GetComponent<CanvasGroup>();
                    mode.scribingLight.blocksRaycasts = false;
                    continue;
                }

                var prop = Actor(6);
                var companion = Actor(4);
                if (i == 0)
                {
                    companion = mode.teacherPoses[0];
                }
                Vector2 companionPosition = new Vector2(340, -115);
                if (i == 2)
                {
                    prop = Actor(7);
                    companion = Actor(5);
                    companionPosition = new Vector2(420, 145);
                    mode.characterMarks[i].anchoredPosition = new Vector2(-135, -112);
                }
                mode.props[i] = Art("Interactive Prop", group.transform, prop, new Vector2(125, -125), new Vector2(165, 220)).rectTransform;
                mode.props[i].pivot = new Vector2(.5f, .05f);
                mode.props[i].anchoredPosition += new Vector2(0, -99);
                var figure = DojoFigureGraphic.Figure.Master;
                if (i == 0)
                {
                    figure = DojoFigureGraphic.Figure.Senior;
                }
                if (i == 2)
                {
                    figure = DojoFigureGraphic.Figure.Sage;
                }
                var masterArtwork = Figure("Dojo Master", group.transform, companion,
                    companionPosition, new Vector2(165, 220), figure);
                mode.companions[i] = masterArtwork.rectTransform;
                if (i == 0)
                {
                    mode.teacherMotion = masterArtwork;
                    mode.teacherGesture = masterArtwork.rectTransform;
                }
                if (i == 2)
                {
                    mode.sageMotion = masterArtwork;
                    mode.sageGesture = masterArtwork.rectTransform;
                }
                BuildInstructionScroll(mode, i);
                if (i == 0)
                {
                    // Both trainees share the front floor, with the target between them.
                    mode.characterMarks[i].anchoredPosition = new Vector2(-150, -112);
                    mode.props[i].anchoredPosition = new Vector2(-20, -224);
                    mode.props[i].sizeDelta = new Vector2(145, 193);
                    // The new sheet has less transparent space below the soles than Actor0.
                    mode.companions[i].anchoredPosition = new Vector2(170, -94);
                    mode.companions[i].sizeDelta = new Vector2(253, 253);
                    mode.lessonCue = Label("Teacher Cue", group.transform, "내 손을 보고, 천천히 따라 해 봐.", 20, Ink);
                    Place(mode.lessonCue.rectTransform, new Vector2(.5f, .5f), new Vector2(270, 66), new Vector2(360, 36));
                    mode.lessonCue.gameObject.SetActive(false);
                    for (int beat = 0; beat < 4; beat++)
                    {
                        var stamp = Art("Lesson Beat " + (beat + 1), group.transform,
                            DojoEntranceAssets.Load("RoundPlaque"), new Vector2(-60 + beat * 40, 57), new Vector2(30, 30));
                        stamp.color = new Color(.52f, .38f, .24f);
                        mode.lessonMarks[beat] = stamp;
                        var number = Label("Beat Number", stamp.transform, (beat + 1).ToString(), 17, Paper);
                        Stretch(number.rectTransform);
                        stamp.gameObject.SetActive(false);
                    }
                }
                if (i == 2)
                {
                    mode.props[i].anchoredPosition = new Vector2(155, -220);
                    mode.props[i].gameObject.SetActive(false);
                }
                DojoRenewalLayoutBuilder.AddRoomAtmosphere(group.transform, i);
                mode.props[i].SetParent(mode.middleLayers[i], false);
                mode.companions[i].SetParent(mode.middleLayers[i], false);
                if (mode.banners[i])
                {
                    mode.banners[i].SetAsLastSibling();
                }
                if (i == 0)
                {
                    mode.lessonCue.transform.SetParent(mode.middleLayers[i], false);
                    foreach (var stamp in mode.lessonMarks)
                    {
                        stamp.transform.SetParent(mode.middleLayers[i], false);
                    }
                }
                if (i == 1)
                {
                    mode.props[i].anchoredPosition = new Vector2(100, -224);
                    var rig = PracticeSelectionLayoutBuilder.BuildRig(mode.props[i]);
                    var rigRect = (RectTransform)rig.transform;
                    rigRect.anchorMin = rigRect.anchorMax = new Vector2(.5f, .05f);
                    rigRect.anchoredPosition = new Vector2(0, 89);
                    rigRect.localScale = Vector3.one * .3f;
                    rig.jacketGroup.alpha = 0;
                    mode.props[i].GetComponent<Image>().color = Color.clear;
                    mode.practiceTarget = mode.props[i].gameObject.AddComponent<Button>();
                    mode.practiceTarget.targetGraphic = mode.props[i].GetComponent<Image>();
                    mode.practiceTarget.targetGraphic.raycastTarget = true;
                    mode.practiceTarget.transition = Selectable.Transition.None;
                    mode.practiceTarget.navigation = new Navigation { mode = Navigation.Mode.None };
                    mode.companions[i].gameObject.SetActive(false);
                }
                mode.nearLayers[i] = Group("Foreground", group.transform);
                var foreground = Art("Near Timber", mode.nearLayers[i],
                    DojoRenewalLayoutBuilder.Slice("TimberPost", DojoRenewalLayoutBuilder.Load("HanokEntrance"),
                        new Rect(.215f, .19f, .027f, .48f)), new Vector2(-625, 0), new Vector2(58, 780));
                if (i == 2)
                {
                    foreground.gameObject.SetActive(false);
                    var pole = Panel("Summit Flagpole", mode.middleLayers[i], new Color(.25f, .18f, .13f));
                    Place(pole.rectTransform, new Vector2(.5f, .5f), new Vector2(260, -64), new Vector2(7, 310));
                    var flag = new GameObject("Summit Wind Flag", typeof(RectTransform), typeof(DojoWindFlag));
                    flag.transform.SetParent(mode.middleLayers[i], false);
                    mode.summitFlag = flag.GetComponent<DojoWindFlag>();
                    mode.summitFlag.raycastTarget = false;
                    mode.summitFlag.cloth = DojoRenewalLayoutBuilder.Slice("SummitFlagCloth", Actor(7),
                        new Rect(.32f, .42f, .30f, .22f));
                    Place(mode.summitFlag.rectTransform, new Vector2(.5f, .5f), new Vector2(311, 48), new Vector2(102, 72));
                }
            }
            mode.traineeMotion = Figure("Trainee", mode.roomRoot, mode.poses[0],
                new Vector2(-30, -122), new Vector2(190, 253), DojoFigureGraphic.Figure.Trainee);
            mode.character = mode.traineeMotion;
            DojoAnimationAssetBuilder.Configure(mode);
            mode.travelCover = Art("Passing Timber", mode.roomRoot,
                DojoRenewalLayoutBuilder.Slice("TimberPost", DojoRenewalLayoutBuilder.Load("HanokEntrance"),
                    new Rect(.215f, .19f, .027f, .48f)), new Vector2(1100, 0), new Vector2(190, 940)).rectTransform;
            mode.travelCover.gameObject.SetActive(false);
            DojoArcadeLayoutBuilder.Build(view, mode, stage.transform);
            // Direct left/right input owns mode changes. Up/down retains native UI navigation.
            var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = view.settings };
            view.start.navigation = navigation;
            view.logo.navigation = new Navigation { mode = Navigation.Mode.None };
            view.entrance.trainingMenu = null;
            DojoRenewalLayoutBuilder.BuildEntrance(view);
            view.SetExpansion(0);
            view.quitPopup.transform.SetAsLastSibling();
        }

        private static void BuildInstructionScroll(DojoModeView mode, int i)
        {
            // Only free practice uses a teaching easel; the editor has its own manuscript desk.
            if (i != 1)
            {
                return;
            }

            var board = Figure("Instruction Scroll", mode.middleLayers[i], Actor(7),
                new Vector2(-350, -80), new Vector2(170, 225), DojoFigureGraphic.Figure.Scroll);
            mode.scrollMotion[i] = board;
            mode.banners[i] = board.rectTransform;
            board.rectTransform.anchoredPosition = new Vector2(420, -100);
            BuildPracticeDiagram(board.transform);
        }

        private static void BuildPracticeDiagram(Transform parent)
        {
            var diagram = Group("Practice Diagram", parent);
            Place(diagram, new Vector2(.5f, .5f), new Vector2(-14, 25), new Vector2(72, 86));
            var laneInk = new Color(Ink.r, Ink.g, Ink.b, .55f);
            var judgmentInk = new Color(.56f, .22f, .10f, .9f);
            for (int lane = 0; lane <= 4; lane++)
            {
                float x = -32 + lane * 16;
                DiagramStroke("Lane Boundary " + lane, diagram,
                    new Vector2(x, -35), new Vector2(x, 39), 1.2f, laneInk);
            }

            var notes = new[] { new Vector2(-24, 28), new Vector2(-8, 9),
                new Vector2(8, 23), new Vector2(24, -7), new Vector2(-24, -17), new Vector2(8, -17) };
            for (int note = 0; note < notes.Length; note++)
            {
                DiagramStroke("Falling Note " + note, diagram,
                    notes[note] - new Vector2(5.5f, 0), notes[note] + new Vector2(5.5f, 0), 4, Ink);
            }

            DiagramStroke("Descent", diagram, new Vector2(24, 34), new Vector2(24, 15), 1.5f, Ink);
            DiagramStroke("Descent Left", diagram, new Vector2(20, 20), new Vector2(24, 15), 1.5f, Ink);
            DiagramStroke("Descent Right", diagram, new Vector2(28, 20), new Vector2(24, 15), 1.5f, Ink);
            DiagramStroke("Judgment Line", diagram, new Vector2(-35, -27), new Vector2(35, -27), 2.5f, judgmentInk);
            for (int lane = 0; lane < 4; lane++)
            {
                float x = -24 + lane * 16;
                DiagramStroke("Key " + lane, diagram,
                    new Vector2(x - 5, -34), new Vector2(x + 5, -34), 4.5f, Ink);
            }
        }

        private static void DiagramStroke(string name, Transform parent, Vector2 start, Vector2 end,
            float width, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(DojoInkGraphic));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<DojoInkGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            var direction = end - start;
            Place(graphic.rectTransform, new Vector2(.5f, .5f), (start + end) * .5f,
                new Vector2(direction.magnitude, width));
            graphic.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private static Sprite Summit()
        {
            return UiArtworkAssets.Load(Root + "Summit.png", Root + "Summit.asset", "Summit", UiArtworkCrop.Full, Vector4.zero);
        }

        private static Sprite Senior(int index)
        {
            string path = Root + "SeniorStudent" + index + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            UiArtworkAssets.Load(Root + "SeniorStudent.png", Root + "SeniorStudent.asset",
                "SeniorStudent", UiArtworkCrop.Full, Vector4.zero);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "SeniorStudent.png");
            int width = texture.width / 3;
            int height = texture.height / 2;
            var rect = new Rect(index % 3 * width, (1 - index / 3) * height, width, height);
            // Bake frame registration into the sprite rect. The planted rear shoe stays in place
            // despite unequal sheet margins; the demonstration may move the leading foot.
            var home = SeniorFootAnchor(texture, new Rect(0, height, width, height));
            rect.position += SeniorFootAnchor(texture, rect) - home;
            if (sprite && sprite.rect == rect)
            {
                return sprite;
            }
            var generated = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            generated.name = "SeniorStudent" + index;
            if (sprite)
            {
                EditorUtility.CopySerialized(generated, sprite);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(sprite);
                AssetDatabase.SaveAssetIfDirty(sprite);
                return sprite;
            }
            sprite = generated;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Vector2 SeniorFootAnchor(Texture2D texture, Rect rect)
        {
            int width = (int)rect.width;
            int height = Mathf.CeilToInt(rect.height * .22f);
            var pixels = texture.GetPixels((int)rect.x, (int)rect.y, width, height);
            int right = -1;
            int bottom = height;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < .5f)
                    {
                        continue;
                    }
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                }
            }
            if (right < 0)
            {
                throw new System.InvalidOperationException("Senior sprite has no grounded foot.");
            }
            return new Vector2(right, bottom);
        }

        private static void BuildIdleVariations(DojoModeView mode)
        {
            // Trainee and senior use the authored clips configured after their views exist.
            mode.sageIdle.cooldown = new Vector2(7, 12);
            mode.sageIdle.variations = new[]
            {
                Variation(DojoIdleVariation.Motion.Look, 1.8f, 3, 4),
                Variation(DojoIdleVariation.Motion.Sleeve, 1.9f, 2, 4),
                Variation(DojoIdleVariation.Motion.Wind, 2.3f, 1, 4)
            };
        }

        private static DojoIdleVariation.Variation Variation(DojoIdleVariation.Motion motion,
            float duration, float weight, int modeMask, Sprite sprite = null)
        {
            return new DojoIdleVariation.Variation
            {
                motion = motion,
                duration = duration,
                weight = weight,
                modeMask = modeMask,
                sprite = sprite
            };
        }

        private static RectTransform Group(string name, Transform parent)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            var rect = (RectTransform)group.transform;
            Stretch(rect);
            return rect;
        }

        private static Sprite SagePart(bool upper)
        {
            string name = "SageHem";
            if (upper)
            {
                name = "SageUpper";
            }
            string path = Root + name + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }
            var source = Actor(5);
            var rect = source.rect;
            rect.height *= .5f;
            if (upper)
            {
                rect.y += rect.height;
            }
            sprite = Sprite.Create(source.texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Sprite Actor(int index)
        {
            string path = Root + "Actor" + index + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }
            UiArtworkAssets.Load(Root + "Actors.png", Root + "Actors.asset", "Actors", UiArtworkCrop.Full, Vector4.zero);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Actors.png");
            int width = texture.width / 4;
            int height = texture.height / 2;
            var rect = new Rect(index % 4 * width, (1 - index / 4) * height, width, height);
            sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Actor" + index;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Text Label(string name, Transform parent, string caption, int size, Color color)
        {
            var text = UiElements.Label(name, parent, caption, Vector2.zero, Vector2.zero, size);
            text.alignment = TextAnchor.MiddleCenter;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Image Art(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var image = Panel(name, parent, Color.white);
            image.sprite = sprite;
            Place(image.rectTransform, new Vector2(.5f, .5f), position, size);
            return image;
        }

        private static DojoFigureGraphic Figure(string name, Transform parent, Sprite sprite,
            Vector2 position, Vector2 size, DojoFigureGraphic.Figure figure)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(DojoFigureGraphic));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<DojoFigureGraphic>();
            image.sprite = sprite;
            image.figure = figure;
            image.raycastTarget = false;
            Place(image.rectTransform, new Vector2(.5f, .5f), position, size);
            return image;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
