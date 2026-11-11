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

        public static void Apply(MainMenuView view)
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
            var backgrounds = new[] { DojoRenewalLayoutBuilder.Load("LessonRoom"),
                DojoRenewalLayoutBuilder.Load("PracticeHall"), Summit(), DojoEntranceAssets.Load("TrainingInterior") };
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
                var prop = Actor(6);
                var companion = Actor(4);
                Vector2 companionPosition = new Vector2(340, -115);
                if (i == 2)
                {
                    prop = Actor(7);
                    companion = Actor(5);
                    companionPosition = new Vector2(420, 145);
                    mode.characterMarks[i].anchoredPosition = new Vector2(-135, -112);
                }
                if (i == 3)
                {
                    prop = Actor(7);
                }
                mode.props[i] = Art("Interactive Prop", group.transform, prop, new Vector2(125, -125), new Vector2(165, 220)).rectTransform;
                mode.props[i].pivot = new Vector2(.5f, .05f);
                mode.props[i].anchoredPosition += new Vector2(0, -99);
                var figure = DojoFigureGraphic.Figure.Master;
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
                var board = Figure("Instruction Scroll", group.transform, Actor(7),
                    new Vector2(-350, -80), new Vector2(170, 225), DojoFigureGraphic.Figure.Scroll);
                mode.scrollMotion[i] = board;
                mode.banners[i] = board.rectTransform;
                var inscription = Label("Scroll Inscription", board.transform, "心\n技\n體", 27, Ink);
                Place(inscription.rectTransform, new Vector2(.5f, .58f), Vector2.zero, new Vector2(80, 110));
                if (i == 0)
                {
                    // A side-on lesson: explanation to the left, teacher on the raised right platform.
                    mode.characterMarks[i].anchoredPosition = new Vector2(-150, -88);
                    mode.props[i].anchoredPosition = new Vector2(-20, -200);
                    mode.props[i].sizeDelta = new Vector2(145, 193);
                    mode.companions[i].anchoredPosition = new Vector2(230, -56);
                    mode.companions[i].sizeDelta = new Vector2(175, 233);
                    Place(board.rectTransform, new Vector2(.5f, .5f), new Vector2(-320, -2), new Vector2(165, 220));
                    inscription.text = "하나\n둘\n셋 · 넷";
                    inscription.fontSize = 20;
                    mode.lessonCue = Label("Teacher Cue", group.transform, "듣고, 따라 두드려 보거라", 20, Ink);
                    Place(mode.lessonCue.rectTransform, new Vector2(.5f, .5f), new Vector2(375, 108), new Vector2(340, 36));
                    for (int beat = 0; beat < 4; beat++)
                    {
                        var stamp = Art("Lesson Beat " + (beat + 1), group.transform,
                            DojoEntranceAssets.Load("RoundPlaque"), new Vector2(-60 + beat * 40, 57), new Vector2(30, 30));
                        stamp.color = new Color(.52f, .38f, .24f);
                        mode.lessonMarks[beat] = stamp;
                        var number = Label("Beat Number", stamp.transform, (beat + 1).ToString(), 17, Paper);
                        Stretch(number.rectTransform);
                    }
                }
                if (i == 2)
                {
                    inscription.text = "風\n林\n山";
                    board.color = new Color(.85f, .93f, 1);
                    mode.props[i].anchoredPosition = new Vector2(155, -220);
                    mode.props[i].gameObject.SetActive(false);
                    board.rectTransform.anchoredPosition = new Vector2(-395, -86);
                }
                DojoRenewalLayoutBuilder.AddRoomAtmosphere(group.transform, i);
                mode.props[i].SetParent(mode.middleLayers[i], false);
                mode.companions[i].SetParent(mode.middleLayers[i], false);
                board.transform.SetParent(mode.middleLayers[i], false);
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
                    mode.practiceTarget = mode.props[i].gameObject.AddComponent<Button>();
                    mode.practiceTarget.targetGraphic = mode.props[i].GetComponent<Image>();
                    mode.practiceTarget.targetGraphic.raycastTarget = true;
                    mode.practiceTarget.transition = Selectable.Transition.None;
                    mode.practiceTarget.navigation = new Navigation { mode = Navigation.Mode.None };
                    mode.companions[i].gameObject.SetActive(false);
                    board.rectTransform.anchoredPosition = new Vector2(420, -100);
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

        private static Sprite Summit()
        {
            return UiArtworkAssets.Load(Root + "Summit.png", Root + "Summit.asset", "Summit", UiArtworkCrop.Full, Vector4.zero);
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
