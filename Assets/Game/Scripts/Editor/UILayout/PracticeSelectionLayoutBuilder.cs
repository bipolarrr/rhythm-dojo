using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class PracticeSelectionLayoutBuilder
    {
        private const string Root = "Assets/Game/Art/PracticeSelection/";
        private static readonly Color Paper = new Color(1f, .91f, .72f);

        public static void Build(SongSelectionView view, GameObject browser)
        {
            var stage = Group("Wooden Dummy Selection", view.transform);
            stage.gameObject.AddComponent<DojoCompositionFit>().Fit();
            var scope = stage.gameObject.AddComponent<UiFontScope>();
            scope.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            scope.useSmoothRendering = true;
            var practice = stage.gameObject.AddComponent<PracticeSelectionView>();
            view.practice = practice;
            practice.browser = browser;
            practice.room = Group("Practice Room Camera", stage);
            var backdrop = Art("Practice Hall", practice.room, DojoRenewalLayoutBuilder.Load("PracticeHall"),
                Vector2.zero, new Vector2(1280, 720));
            backdrop.rectTransform.localScale = Vector3.one * 1.04f;
            practice.rig = BuildRig(stage);
            practice.dummy = (RectTransform)practice.rig.transform;
            practice.rig.jacket.sprite = view.fallbackCover;

            var interfaceRoot = Group("Ink and Hanging Song Labels", stage);
            practice.interfaceGroup = interfaceRoot.gameObject.AddComponent<CanvasGroup>();
            var heading = Label("Practice Heading", interfaceRoot, "자유 연습", new Vector2(-410, 276), new Vector2(360, 65), 58);
            heading.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Fonts/Dokdo-Regular.ttf");
            var headingScope = heading.gameObject.AddComponent<UiFontScope>();
            headingScope.font = heading.font;
            headingScope.useSmoothRendering = true;
            Label("Practice Subheading", interfaceRoot, "목인을 두드려, 다음 가락으로", new Vector2(-410, 223), new Vector2(360, 35), 19);

            practice.information = Group("Song Calligraphy", interfaceRoot);
            Place(practice.information, new Vector2(-415, 53), new Vector2(365, 270));
            practice.title = Label("Selected Song Title", practice.information, "곡을 선택하세요", new Vector2(0, 68), new Vector2(365, 115), 35);
            practice.title.resizeTextForBestFit = true;
            practice.title.resizeTextMinSize = 22;
            practice.title.resizeTextMaxSize = 35;
            practice.artist = Label("Selected Artist and Tempo", practice.information, "", new Vector2(0, -33), new Vector2(350, 86), 19);
            practice.chart = Label("Selected Chart", interfaceRoot, "", new Vector2(-415, -106), new Vector2(360, 50), 19);

            practice.difficulties = new Button[view.difficultyButtons.Length];
            for (int i = 0; i < practice.difficulties.Length; i++)
            {
                practice.difficulties[i] = Button("Practice Difficulty " + i, interfaceRoot,
                    view.difficultyButtons[i].GetComponentInChildren<Text>(true).text,
                    new Vector2(-533 + i * 118, -162), new Vector2(112, 45), 17);
            }
            practice.previous = Button("Strike Left", interfaceRoot, "◀\n이전 곡", new Vector2(-205, 118), new Vector2(115, 85), 22);
            practice.next = Button("Strike Right", interfaceRoot, "▶\n다음 곡", new Vector2(205, 118), new Vector2(115, 85), 22);
            practice.play = Button("Begin Practice", interfaceRoot, "수련 시작", new Vector2(0, -260), new Vector2(270, 68), 27);
            practice.back = Button("Return to Dojo", interfaceRoot, "도장으로", new Vector2(-505, 324), new Vector2(230, 40), 17);
            practice.settings = Button("Practice Settings", interfaceRoot, "설정", new Vector2(548, 324), new Vector2(105, 40), 17);
            practice.library = Button("Browse Song Library", interfaceRoot, "곡 목록 · 정렬 / 속도", new Vector2(430, -155), new Vector2(270, 46), 18);
            practice.preview = Button("Preview Selected Song", interfaceRoot, "미리듣기", new Vector2(430, -103), new Vector2(270, 46), 18);
            practice.status = Label("Practice Playback Status", interfaceRoot, "", new Vector2(0, -202), new Vector2(460, 50), 18);
            practice.status.alignment = TextAnchor.MiddleCenter;

            var hint = Label("Strike Instructions", interfaceRoot, "← →  곡 선택 / 길게 눌러 연타   ·   ↑ ↓  난이도   ·   Enter  시작   ·   Esc  도장으로", new Vector2(190, -328), new Vector2(820, 36), 16);
            hint.alignment = TextAnchor.MiddleRight;

            var handRoot = Group("First Person Hands", stage);
            practice.hands = handRoot.gameObject.AddComponent<CanvasGroup>();
            practice.hands.blocksRaycasts = false;
            handRoot.gameObject.AddComponent<Canvas>();
            practice.strikes = handRoot.gameObject.AddComponent<PracticeStrikeAnimator>();
            practice.strikes.rig = practice.rig;
            practice.strikes.frames = StrikeFrames();
            practice.strikes.afterimages = new PracticeStrikeAnimator.Afterimage[4];
            for (int i = 0; i < practice.strikes.afterimages.Length; i++)
            {
                var image = Art("Strike Afterimage " + i, handRoot, practice.strikes.frames[0],
                    new Vector2(-1250, -620), new Vector2(600, 590));
                image.enabled = false;
                practice.strikes.afterimages[i] = new PracticeStrikeAnimator.Afterimage { image = image };
            }
            practice.strikes.visuals = new PracticeStrikeAnimator.StrikeVisual[3];
            for (int i = 0; i < practice.strikes.visuals.Length; i++)
            {
                var speedRoot = Group("Strike Speed Lines " + i, handRoot);
                var speedLines = speedRoot.gameObject.AddComponent<CanvasGroup>();
                speedLines.blocksRaycasts = false;
                speedLines.alpha = 0;
                for (int j = 0; j < 5; j++)
                {
                    Stroke("Speed Stroke " + j, speedRoot, new Vector2(-j * 13, (j - 2) * 28),
                        new Vector2(100 - j * 12, 3 + j % 2 * 2), Paper);
                }
                string name = "Follow Through";
                if (i == 0)
                {
                    name = "Left Punch";
                }
                else if (i == 1)
                {
                    name = "Right Punch";
                }
                var image = Art(name, handRoot, practice.strikes.frames[0],
                    new Vector2(-1250, -620), new Vector2(600, 590));
                image.enabled = false;
                practice.strikes.visuals[i] = new PracticeStrikeAnimator.StrikeVisual
                {
                    image = image,
                    speedLines = speedLines
                };
            }
            practice.leftFist = practice.strikes.visuals[0].image.rectTransform;
            practice.rightFist = practice.strikes.visuals[1].image.rectTransform;
            var impactRoot = Group("Ink Contact Burst", stage);
            practice.impact = impactRoot.gameObject.AddComponent<CanvasGroup>();
            practice.impact.alpha = 0;
            practice.impact.blocksRaycasts = false;
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f;
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var line = Stroke("Impact Stroke " + i, impactRoot, direction * 40, new Vector2(48, 7), Paper);
                line.localRotation = Quaternion.Euler(0, 0, angle);
            }
            practice.effects = stage.gameObject.AddComponent<AudioSource>();
            practice.effects.playOnAwake = false;
            practice.effects.spatialBlend = 0;
            practice.contact = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/DojoKnock.wav");
            practice.ResetPresentation();
            stage.gameObject.SetActive(false);
        }

        public static WoodenDummyRig BuildRig(Transform parent)
        {
            var root = Group("Articulated Wooden Dummy", parent);
            var rig = root.gameObject.AddComponent<WoodenDummyRig>();
            rig.frames = WoodenDummyRotationAssets.Load();
            var fixedSprite = WoodenDummyRotationAssets.LoadBase();
            var fixedImage = Art("Fixed Feet and Lower Column", root, fixedSprite,
                new Vector2(0, -315), fixedSprite.rect.size * WoodenDummyRotationAssets.Scale);
            fixedImage.rectTransform.pivot = fixedSprite.pivot / fixedSprite.rect.size;
            rig.fixedBase = fixedImage.rectTransform;
            rig.rotationJoint = Group("Vertical Rotation Bearing", root);
            Place(rig.rotationJoint, new Vector2(0, -315 + (504 - WoodenDummyRotationAssets.JointHeight)
                * WoodenDummyRotationAssets.Scale), Vector2.one);
            rig.bodyHome = Vector2.zero;
            rig.body = Group("Rope Bound Body", rig.rotationJoint);
            Place(rig.body, rig.bodyHome, Vector2.one);
            var size = rig.frames[0].sprite.rect.size * WoodenDummyRotationAssets.Scale;
            rig.side = Art("Timber Side", rig.body, rig.frames[2].sprite, Vector2.zero, size);
            rig.rear = Art("Timber Rear", rig.body, rig.frames[4].sprite, Vector2.zero, size);
            rig.front = Art("Timber Front", rig.body, rig.frames[0].sprite, Vector2.zero, size);
            rig.head = Group("Rotating Head", rig.body);
            Place(rig.head, rig.frames[0].panelPosition, rig.frames[0].panelSize);
            rig.jacketGroup = rig.head.gameObject.AddComponent<CanvasGroup>();
            rig.jacketGroup.blocksRaycasts = false;
            var recess = Group("Jacket Recess", rig.head);
            Place(recess, new Vector2(0, 12), new Vector2(110, 72));
            recess.gameObject.AddComponent<RectMask2D>();
            rig.jacket = Art("Actual Album Jacket", recess, null, Vector2.zero, new Vector2(110, 72));
            var fit = rig.jacket.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            rig.title = Label("Display Song Title", rig.head, "", new Vector2(0, -33), new Vector2(110, 17), 12);
            rig.title.alignment = TextAnchor.MiddleCenter;
            rig.title.resizeTextForBestFit = true;
            rig.title.resizeTextMinSize = 8;
            rig.title.resizeTextMaxSize = 12;
            rig.artist = Label("Display Artist", rig.head, "", new Vector2(0, -46), new Vector2(110, 11), 8);
            rig.artist.alignment = TextAnchor.MiddleCenter;
            BuildBearing(rig.rotationJoint);
            rig.Pose(0, 0);
            return rig;
        }

        private static void BuildBearing(RectTransform parent)
        {
            // Authored pixel-stepped iron collar covers the seam between the two timber parts.
            var rim = Art("Bearing Rim", parent, null, Vector2.zero, new Vector2(106, 23));
            rim.color = new Color(.16f, .13f, .10f);
            var band = Art("Iron Bearing", parent, null, new Vector2(0, 2), new Vector2(100, 15));
            band.color = new Color(.38f, .36f, .28f);
            var shine = Art("Bearing Highlight", parent, null, new Vector2(-4, 8), new Vector2(88, 3));
            shine.color = new Color(.64f, .60f, .43f);
            var seam = Art("Rotating Timber Seam", parent, null, new Vector2(0, -1), new Vector2(100, 3));
            seam.color = new Color(.10f, .08f, .06f);
            for (int i = 0; i < 3; i++)
            {
                var rivet = Art("Bearing Rivet " + i, parent, null,
                    new Vector2((i - 1) * 37, 3), new Vector2(4, 4));
                rivet.color = new Color(.73f, .67f, .47f);
            }
        }

        private static Sprite[] StrikeFrames()
        {
            var sheet = UiArtworkAssets.Load(Root + "StrikePoses.png", Root + "StrikePoses.asset",
                "Practice Strike Poses", UiArtworkCrop.Full, Vector4.zero);
            var texture = sheet.texture;
            var pixels = texture.GetPixels32();
            int width = texture.width / 4;
            int height = texture.height / 3;
            var frames = new Sprite[12];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = Root + "StrikePose" + i + ".asset";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                int startX = i % 4 * width;
                int startY = texture.height - (i / 4 + 1) * height;
                int left = startX + width, right = startX, bottom = startY + height, top = startY;
                for (int y = startY; y < startY + height; y++)
                {
                    for (int x = startX; x < startX + width; x++)
                    {
                        if (pixels[y * texture.width + x].a < 64)
                        {
                            continue;
                        }
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        bottom = Math.Min(bottom, y);
                        top = Math.Max(top, y);
                    }
                }
                var pivot = new Vector2(.92f, .82f);
                if (i / 4 == 1)
                {
                    pivot = new Vector2(.94f, .9f);
                }
                else if (i / 4 == 2)
                {
                    pivot = new Vector2(.94f, .62f);
                }
                // Keep the hand/foot and only the distal sleeve/trouser. The source tip
                // remains the pivot after cropping so every pose lands on the same landmark.
                var size = new Vector2(right - left + 1, top - bottom + 1);
                var contact = new Vector2(left, bottom) + Vector2.Scale(size, pivot);
                left += Mathf.RoundToInt(size.x * .3f);
                bottom += Mathf.RoundToInt(size.y * .38f);
                var rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
                pivot = (contact - rect.position) / rect.size;
                var generated = UnityEngine.Sprite.Create(texture, rect, pivot, 100, 0, SpriteMeshType.FullRect);
                generated.name = "Practice Strike Pose " + i;
                if (frames[i])
                {
                    EditorUtility.CopySerialized(generated, frames[i]);
                    UnityEngine.Object.DestroyImmediate(generated);
                    EditorUtility.SetDirty(frames[i]);
                    AssetDatabase.SaveAssetIfDirty(frames[i]);
                }
                else
                {
                    frames[i] = generated;
                    AssetDatabase.CreateAsset(frames[i], path);
                }
            }
            return frames;
        }

        private static Sprite FrontFace()
        {
            string path = Root + "RecessedFront.asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }
            var source = Sprite(1);
            // Crop the recessed plane in the editor; its rim projects separately from the log.
            var rect = new Rect(665, 688, 200, 194);
            sprite = UnityEngine.Sprite.Create(source.texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Recessed Front";
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static Sprite Sprite(int index)
        {
            string path = Root + "DummyPart" + index + ".asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite)
            {
                return sprite;
            }
            var sheet = UiArtworkAssets.Load(Root + "WoodenDummyAtlas.png", Root + "WoodenDummyAtlas.asset",
                "Wooden Dummy Atlas", UiArtworkCrop.Full, Vector4.zero);
            var texture = sheet.texture;
            int width = texture.width / 3;
            int height = texture.height / 2;
            int startX = index % 3 * width;
            int startY = (1 - index / 3) * height;
            int left = startX + width, right = startX, bottom = startY + height, top = startY;
            var pixels = texture.GetPixels32();
            for (int y = startY; y < startY + height; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128)
                    {
                        continue;
                    }
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y);
                    top = Math.Max(top, y);
                }
            }
            var rect = new Rect(left, bottom, right - left + 1, top - bottom + 1);
            sprite = UnityEngine.Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Dummy Part " + index;
            AssetDatabase.CreateAsset(sprite, path);
            return sprite;
        }

        private static RectTransform Group(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, Vector2.zero, new Vector2(1280, 720));
            return rect;
        }

        private static Image Art(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var rect = Group(name, parent);
            Place(rect, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var rect = Group(name, parent);
            Place(rect, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);
            text.text = value;
            text.fontSize = fontSize;
            text.color = Paper;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.supportRichText = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.06f, .025f, .01f, .85f);
            shadow.effectDistance = new Vector2(1, -2);
            return text;
        }

        private static Button Button(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var image = Art(name, parent, null, position, size);
            image.color = Color.clear;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label("Caption", image.transform, value, Vector2.zero, size - new Vector2(10, 4), fontSize);
            label.alignment = TextAnchor.MiddleCenter;
            button.targetGraphic = label;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, .75f, .45f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1f, .48f, .24f);
            button.colors = colors;
            return button;
        }

        private static RectTransform Stroke(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = Group(name, parent);
            Place(rect, position, size);
            var graphic = rect.gameObject.AddComponent<DojoInkGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
