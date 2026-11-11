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
            view.ambientLights = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                var light = Art("Lantern Glow " + i, view.entrance.transform,
                    DojoEntranceAssets.Load("RoundPlaque"), Vector2.zero, new Vector2(105, 125));
                Place(light.rectTransform, new Vector2(.145f + .71f * i, .78f), Vector2.zero, new Vector2(105, 125));
                light.color = new Color(1, .68f, .23f, .045f);
                view.ambientLights[i] = light;
            }
            // Keep the established view/actions and door objects; replace their authored presentation.
            view.trainingInterior.gameObject.SetActive(false);
            view.slidingLogo.gameObject.SetActive(false);
            view.slidingPlaque.enabled = false;
            Object.DestroyImmediate(view.logo.GetComponent<DojoPlaqueHitArea>());
            view.logo.transform.SetParent(canvas, false);
            Place((RectTransform)view.logo.transform, new Vector2(.5f, .88f), Vector2.zero, new Vector2(400, 105));
            var plaque = Art("Timber Sign", view.logo.transform, DojoEntranceAssets.Load("SettingsPanel"), Vector2.zero, new Vector2(400, 105));
            plaque.transform.SetAsFirstSibling();
            view.logo.targetGraphic = plaque;
            plaque.raycastTarget = true;
            view.title.enabled = true;
            view.title.text = "리듬 도장";
            view.title.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            view.title.fontSize = 76;
            view.title.verticalOverflow = VerticalWrapMode.Overflow;
            view.title.color = Ink;
            foreach (var effect in view.title.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
            Place(view.title.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(350, 90));
            view.logoArtwork = view.title.rectTransform;
            view.startPrompt.text = "문을 두드려 입장  ·  Enter";
            view.startPrompt.transform.SetParent(canvas, false);
            Place(view.startPrompt.rectTransform, new Vector2(.5f, .15f), Vector2.zero, new Vector2(540, 48));
            view.startPrompt.fontSize = 26;
            view.startPrompt.color = Paper;
            view.startPrompt.gameObject.AddComponent<Outline>().effectColor = Ink;
            var trigger = (RectTransform)view.entranceTrigger.transform;
            trigger.anchorMin = new Vector2(.22f, .15f);
            trigger.anchorMax = new Vector2(.78f, .79f);
            trigger.offsetMin = trigger.offsetMax = Vector2.zero;

            var menu = (RectTransform)view.menuPanel.transform;
            menu.SetParent(canvas, false);
            Stretch(menu);
            menu.localScale = Vector3.one;
            menu.SetSiblingIndex(view.entrance.transform.GetSiblingIndex());
            var stage = new GameObject("Mode Stages", typeof(RectTransform), typeof(DojoModeView));
            stage.transform.SetParent(menu, false);
            Stretch((RectTransform)stage.transform);
            stage.transform.SetAsFirstSibling();
            var mode = stage.GetComponent<DojoModeView>();
            view.modeSelection = mode;
            mode.stages = new CanvasGroup[4];
            mode.props = new RectTransform[4];
            mode.companions = new RectTransform[4];
            mode.banners = new RectTransform[4];
            mode.poses = new[] { Actor(0), Actor(1), Actor(2), Actor(3) };
            var backgrounds = new[] { DojoEntranceAssets.Load("TrainingInterior"),
                DojoEntranceAssets.Load("SelectionBackground"), Summit(), DojoEntranceAssets.Load("TrainingInterior") };
            for (int i = 0; i < 4; i++)
            {
                var group = new GameObject("Stage " + (i + 1), typeof(RectTransform), typeof(CanvasGroup));
                group.transform.SetParent(stage.transform, false);
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
                var prop = Actor(6);
                var companion = Actor(4);
                Vector2 companionPosition = new Vector2(340, -115);
                if (i == 2)
                {
                    prop = Actor(7);
                    companion = Actor(5);
                    companionPosition = new Vector2(420, 145);
                }
                if (i == 3)
                {
                    prop = Actor(7);
                }
                mode.props[i] = Art("Interactive Prop", group.transform, prop, new Vector2(125, -125), new Vector2(165, 220)).rectTransform;
                mode.props[i].pivot = new Vector2(.5f, .05f);
                mode.props[i].anchoredPosition += new Vector2(0, -99);
                mode.companions[i] = Art("Dojo Master", group.transform, companion, companionPosition, new Vector2(165, 220)).rectTransform;
                var board = Art("Instruction Scroll", group.transform, Actor(7), new Vector2(-350, -80), new Vector2(170, 225));
                mode.banners[i] = board.rectTransform;
                var inscription = Label("Scroll Inscription", board.transform, "心\n技\n體", 27, Ink);
                Place(inscription.rectTransform, new Vector2(.5f, .58f), Vector2.zero, new Vector2(80, 110));
                if (i == 2)
                {
                    inscription.text = "風\n林\n山";
                    board.color = new Color(.85f, .93f, 1);
                    // Separate robe hem, authored as sprite pieces, catches the summit breeze.
                    var master = mode.companions[i];
                    master.GetComponent<Image>().enabled = false;
                    Art("Sage Upper Body", master, SagePart(true), new Vector2(0, 55), new Vector2(165, 110));
                    mode.banners[i] = Art("Sage Robe Hem", master, SagePart(false), new Vector2(0, -55), new Vector2(165, 110)).rectTransform;
                    mode.banners[i].pivot = new Vector2(.5f, 1);
                    mode.banners[i].anchoredPosition = Vector2.zero;
                }
            }
            mode.character = Art("Trainee", stage.transform, mode.poses[0], new Vector2(-30, -122), new Vector2(190, 253));
            var top = Panel("Mode Heading", menu, new Color(.09f, .045f, .025f, .94f));
            Place(top.rectTransform, new Vector2(.5f, 1), new Vector2(0, -136), new Vector2(760, 136));
            mode.counter = Label("Mode Number", top.transform, "02 / 04", 18, new Color(.77f, .53f, .29f));
            Place(mode.counter.rectTransform, new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(200, 25));
            mode.heading = Label("Mode Title", top.transform, "자유 연습", 48, Paper);
            Place(mode.heading.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(650, 60));
            mode.heading.font = view.title.font;
            mode.description = Label("Mode Description", top.transform, "자유롭게 연습하자!", 25, Paper);
            Place(mode.description.rectTransform, new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(720, 35));
            var bottom = Panel("Mode Footer", menu, new Color(.09f, .045f, .025f, .94f));
            Place(bottom.rectTransform, new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(1280, 140));
            mode.detail = Label("Mode Detail", bottom.transform, "자유 수련 · 곡과 난이도를 골라 도전하세요", 22, Paper);
            Place(mode.detail.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 42), new Vector2(1080, 32));
            var help = Label("Navigation Help", bottom.transform, "← → 이동     Enter 선택     Esc 대문으로", 18, new Color(.72f, .63f, .49f));
            Place(help.rectTransform, new Vector2(.5f, 0), new Vector2(0, 15), new Vector2(750, 24));
            StyleButton(view.start, "수련 시작", new Vector2(.5f, 0), new Vector2(0, 58), new Vector2(280, 52));
            mode.actionLabel = view.start.GetComponentInChildren<Text>();
            view.start.transform.SetAsLastSibling();
            StyleButton(view.settings, "설정", new Vector2(1, 1), new Vector2(-180, -32), new Vector2(100, 44));
            StyleButton(view.quit, "종료", new Vector2(1, 1), new Vector2(-64, -32), new Vector2(100, 44));
            StyleButton(view.editor, "채보 공방", new Vector2(0, 1), new Vector2(90, -32), new Vector2(150, 44));
            mode.previous = Control("Previous Mode", menu, "〈", new Vector2(.08f, .5f));
            mode.next = Control("Next Mode", menu, "〉", new Vector2(.92f, .5f));
            // Direct left/right input owns mode changes. Up/down retains native UI navigation.
            var navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = view.settings };
            view.start.navigation = navigation;
            view.logo.navigation = new Navigation { mode = Navigation.Mode.None };
            view.entrance.trainingMenu = null;
            view.SetExpansion(0);
            view.quitPopup.transform.SetAsLastSibling();
        }

        private static Sprite Summit()
        {
            return UiArtworkAssets.Load(Root + "Summit.png", Root + "Summit.asset", "Summit", UiArtworkCrop.Full, Vector4.zero);
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

        private static void StyleButton(Button button, string caption, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Place((RectTransform)button.transform, anchor, position, size);
            var graphic = button.GetComponent<Image>();
            graphic.sprite = DojoEntranceAssets.Load("SettingsPanel");
            graphic.preserveAspect = false;
            var text = button.GetComponentInChildren<Text>();
            Stretch(text.rectTransform);
            text.text = caption;
            text.fontSize = 27;
            text.material = null;
            text.color = Ink;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            foreach (var effect in text.GetComponents<Shadow>())
            {
                effect.enabled = false;
            }
        }

        private static Button Control(string name, Transform parent, string caption, Vector2 anchor)
        {
            var panel = Panel(name, parent, new Color(.12f, .07f, .04f, .9f));
            panel.raycastTarget = true;
            Place(panel.rectTransform, anchor, Vector2.zero, new Vector2(68, 90));
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Label("Caption", button.transform, caption, 42, Paper);
            Stretch(text.rectTransform);
            return button;
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
