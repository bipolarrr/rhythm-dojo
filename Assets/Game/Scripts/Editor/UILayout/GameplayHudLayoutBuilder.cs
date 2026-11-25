using System;
using RhythmDojo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class GameplayHudLayoutBuilder
    {
        private static Font font;
        private static readonly Color Cream = new Color(.94f, .93f, .85f);
        private static readonly Color Muted = new Color(.56f, .65f, .67f);
        private static readonly Color Cyan = new Color(.46f, .84f, .81f);
        private static readonly Color Red = new Color(.96f, .29f, .31f);

        public static GameplayHudView Build()
        {
            font = AssetDatabase.LoadAssetAtPath<Font>(MainMenuLayoutBuilder.FontPath);

            if (!font)
            {
                throw new InvalidOperationException("Gameplay Korean font missing.");
            }

            var canvas = UiElements.Canvas("HUD");
            var view = canvas.gameObject.AddComponent<GameplayHudView>();
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = .5f;

            var song = Card("Training Information", canvas.transform, .035f, .65f, .24f, .88f, Cyan);
            view.songTitle = Label("Song Title", song, "", .07f, .48f, .93f, .80f, 25, role: UiTextRole.Title);
            view.songTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.songDetails = Label("Song Details", song, "", .07f, .24f, .93f, .47f, 14);
            view.songDetails.color = Muted;
            view.sessionState = Label("Training State", song, "", .07f, .04f, .93f, .23f, 16);
            view.sessionState.color = Cyan;

            var feedback = Card("Rhythm Feedback", canvas.transform, .74f, .22f, .97f, .88f, Red);
            view.combo = Label("Combo Value", feedback, "0", .04f, .56f, .96f, .87f, 76, role: UiTextRole.Title);
            view.combo.alignment = TextAnchor.MiddleCenter;
            view.healthLabel = Label(
                "Health Value",
                feedback,
                "체력  100 / 100",
                .12f,
                .385f,
                .88f,
                .43f,
                13
            );
            view.healthLabel.color = Cyan;

            var healthTrack = Panel(
                "Health Track",
                feedback,
                new Color(.10f, .15f, .17f),
                .12f,
                .335f,
                .88f,
                .37f
            );
            view.healthFill = Panel("Health Fill", healthTrack, Cyan, 0, 0, 1, 1)
                .GetComponent<Image>();

            var judgmentRoot = new GameObject(
                "Judgment Feedback",
                typeof(RectTransform),
                typeof(CanvasGroup)
            );
            judgmentRoot.transform.SetParent(feedback, false);
            Bounds((RectTransform)judgmentRoot.transform, 0, .04f, 1, .30f);
            view.judgmentGroup = judgmentRoot.GetComponent<CanvasGroup>();
            view.judgmentGroup.blocksRaycasts = false;
            view.judgmentGroup.interactable = false;
            view.judgment = Label(
                "Judgment Grade",
                judgmentRoot.transform,
                "SPACE로 수련 시작",
                .02f,
                .43f,
                .98f,
                .96f,
                31,
                role: UiTextRole.Title
            );
            view.judgment.alignment = TextAnchor.MiddleCenter;
            view.judgment.color = Cyan;
            view.timing = Label(
                "Judgment Timing",
                judgmentRoot.transform,
                "",
                .03f,
                0,
                .97f,
                .45f,
                15
            );
            view.timing.alignment = TextAnchor.MiddleCenter;
            view.timing.color = Muted;

            var railOpacityLabel = Label(
                "Rail Opacity Label",
                canvas.transform,
                "레인 불투명도",
                .03f,
                .11f,
                .26f,
                .15f,
                15
            );
            railOpacityLabel.raycastTarget = false;

            var railOpacityObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            railOpacityObject.name = "Rail Opacity";
            railOpacityObject.transform.SetParent(canvas.transform, false);
            Bounds((RectTransform)railOpacityObject.transform, .03f, .075f, .26f, .11f);
            view.railOpacity = railOpacityObject.GetComponent<Slider>();
            view.railOpacity.value = 1;
            railOpacityObject.transform.Find("Background").GetComponent<Image>().color =
                new Color(.23f, .13f, .075f);
            view.railOpacity.fillRect.GetComponent<Image>().color = new Color(.84f, .57f, .27f);
            view.railOpacity.targetGraphic.color = Cream;

            var laneRoot = new GameObject(
                "Lane Feedback",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            laneRoot.SetParent(canvas.transform, false);
            laneRoot.anchorMin = Vector2.zero;
            laneRoot.anchorMax = Vector2.one;
            laneRoot.offsetMin = laneRoot.offsetMax = Vector2.zero;
            view.lanes = canvas.gameObject.AddComponent<HudLaneLayout>();
            view.lanes.laneRoot = laneRoot;
            view.returnButton = DojoScreenStyle.Action(
                "Song List",
                canvas.transform,
                "곡 선택으로",
                Vector2.zero,
                Vector2.zero
            );
            Bounds((RectTransform)view.returnButton.transform, .74f, .12f, .97f, .19f);
            var caption = view.returnButton.GetComponentInChildren<Text>();
            caption.font = font;
            caption.fontSize = 16;
            caption.color = Cream;
            caption.verticalOverflow = VerticalWrapMode.Overflow;

            var gameOverRoot = Panel(
                "Game Over Overlay",
                canvas.transform,
                new Color(0, 0, 0, .62f),
                0,
                0,
                1,
                1
            );
            view.gameOverPanel = gameOverRoot.gameObject;
            view.gameOverText = Label(
                "Game Over Title",
                gameOverRoot,
                "GAME OVER",
                .15f,
                .44f,
                .85f,
                .60f,
                58,
                role: UiTextRole.Title
            );
            view.gameOverText.alignment = TextAnchor.MiddleCenter;
            view.gameOverText.color = Red;

            var gameOverHint = Label(
                "Game Over Hint",
                gameOverRoot,
                "SPACE  다시 도전   /   ESC  곡 선택",
                .15f,
                .36f,
                .85f,
                .44f,
                20,
                role: UiTextRole.Help
            );
            gameOverHint.alignment = TextAnchor.MiddleCenter;
            gameOverHint.color = Cream;
            view.gameOverHint = gameOverHint;
            var resultOutline = view.gameOverText.gameObject.AddComponent<Outline>();
            resultOutline.effectColor = new Color(.10f, .055f, .035f, .95f);
            resultOutline.effectDistance = new Vector2(2, -2);
            view.gameOverPanel.SetActive(false);

            PixelGameplayStyle.Apply(view);
            BuildPauseMenu(view.transform, view.railOpacity);
            UiFontSettings.ApplyTo(view.transform);
            PixelGameplayStyle.RefineReadability(view);
            BuildFocusHud(view);
            view.gameOverPanel.GetComponent<Image>().enabled = false;
            view.gameOverHint.gameObject.SetActive(false);
            view.gameOverFrame.SetActive(false);
            view.gameOverText.rectTransform.anchorMin = new Vector2(.15f, .42f);
            view.gameOverText.rectTransform.anchorMax = new Vector2(.85f, .58f);
            var help = Label("Navigation Help", view.transform,
                "타격 키   ·   Esc  일시정지", .30f, .09f, .70f, .13f, 15,
                role: UiTextRole.Help);
            help.alignment = TextAnchor.MiddleCenter;
            UiFontSettings.Apply(help);
            view.introduction = help.gameObject;
            ApplyIngameLayout(view);
            GameplayAtmosphereBuilder.RefineHud(view);
            var menu = view.GetComponent<GameplayPauseMenu>();
            DojoScreenStyle.Typography(menu.panel.transform);
            DojoScreenStyle.Heading(view.gameOverText);

            return view;
        }

        private static void ApplyIngameLayout(GameplayHudView view)
        {
            var song = (RectTransform)view.songInformationPanel.transform;
            Bounds(song, .035f, .27f, .245f, .77f);
            var frame = Panel("Song Wood Frame", song, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = DojoGameplayAssets.Wood;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 12;
            frame.transform.SetAsFirstSibling();
            view.album = Panel("Album Jacket", song, Color.white, .10f, .32f, .90f, .92f).GetComponent<Image>();
            view.fallbackCover = DojoEntranceAssets.Load("SelectionBackground");
            view.album.sprite = view.fallbackCover;
            view.album.preserveAspect = true;
            Bounds(view.songTitle.rectTransform, .10f, .13f, .90f, .30f);
            Bounds(view.songDetails.rectTransform, .10f, .035f, .90f, .13f);
            view.songTitle.alignment = TextAnchor.MiddleCenter;
            view.songDetails.alignment = TextAnchor.MiddleCenter;
            view.songDetails.color = Cream;

            var feedback = (RectTransform)view.combo.transform.parent;
            Bounds(feedback, .722f, .18f, .758f, .76f);
            view.healthGauge = feedback;
            var healthTrack = (RectTransform)view.healthFill.transform.parent;
            Bounds(healthTrack, .18f, .02f, .82f, .98f);
            var gaugeFrame = Panel("Gauge Wood Frame", feedback, Color.white, 0, 0, 1, 1).GetComponent<Image>();
            gaugeFrame.sprite = DojoGameplayAssets.Wood;
            gaugeFrame.type = Image.Type.Sliced;
            gaugeFrame.pixelsPerUnitMultiplier = 16;
            gaugeFrame.transform.SetAsFirstSibling();
            view.healthDamage = Panel("Health Damage", healthTrack, new Color(1, .66f, .40f, 0), 0, 0, 1, 1).GetComponent<Image>();
            view.healthDamage.transform.SetAsFirstSibling();
            for (int i = 1; i < 20; i++)
            {
                var mark = Panel("Gauge Segment " + i, healthTrack, new Color(.16f, .10f, .07f, .65f),
                    0, i / 20f, 1, i / 20f);
                mark.sizeDelta = new Vector2(0, 2);
            }
            view.sessionState.gameObject.SetActive(false);
            // Insert the readout before the notes so passing heads stay visible.
            view.laneReadout = PresentationAssetBuilder.Group("Lane Readout", view.viewport.field);
            PositionJudgmentFeedback(view);
            view.combo.text = "";
            view.combo.fontSize = 46;
            view.healthLabel.gameObject.SetActive(false);
            view.focusHudButton.gameObject.SetActive(false);
            view.introduction.SetActive(false);
            view.lanes.laneRoot.gameObject.SetActive(false);
            view.judgment.text = "";
            view.judgmentGroup.alpha = 0;
        }

        public static void PositionJudgmentFeedback(GameplayHudView view)
        {
            var readout = view.laneReadout;
            readout.anchorMin = readout.anchorMax = new Vector2(.5f, .75f);
            readout.anchoredPosition = Vector2.zero;
            readout.sizeDelta = new Vector2(192, 120);
            readout.localScale = Vector3.one * .25f;
            view.combo.transform.SetParent(readout, false);
            Bounds(view.combo.rectTransform, 0, .61f, 1, 1);

            var judgmentRoot = (RectTransform)view.judgmentGroup.transform;
            judgmentRoot.SetParent(readout, false);
            Bounds(judgmentRoot, 0, 0, 1, .61f);
            view.judgment.fontSize = 36;
            view.timing.fontSize = 18;

            foreach (var label in new[] { view.judgment, view.timing })
            {
                var outline = label.GetComponent<Outline>();
                if (!outline)
                {
                    outline = label.gameObject.AddComponent<Outline>();
                }

                outline.effectColor = new Color(.08f, .04f, .02f, .95f);
                outline.effectDistance = new Vector2(2, -2);
            }
        }

        private static void BuildPauseMenu(Transform parent, Slider railOpacity)
        {
            var menu = parent.gameObject.AddComponent<GameplayPauseMenu>();
            var shade = Panel("Pause Overlay", parent, new Color(.08f, .035f, .015f, .78f), 0, 0, 1, 1);
            shade.GetComponent<Image>().raycastTarget = true;
            menu.panel = shade.gameObject;
            var dialog = Panel("Pause Dialog", shade, Color.white, .32f, .20f, .68f, .80f);
            var frame = PixelSongSelectionStyle.Frame(dialog, Color.white, 20);
            frame.artwork = SettingsArtAssets.Load("SettingsPanel", UiArtworkCrop.Full,
                new Vector4(128, 128, 128, 128));
            frame.RefreshArtwork();
            frame.color = DojoScreenStyle.Paper;
            menu.resumeButton = PauseButton("Resume Play", dialog, "이어하기", .14f, .69f, .86f, .80f);
            var title = Label("Pause Heading", dialog, "수련 일시정지", .08f, .84f, .92f, .96f, 30, role: UiTextRole.Title);
            title.color = new Color(.24f, .12f, .085f);
            title.alignment = TextAnchor.MiddleCenter;
            menu.restartButton = PauseButton("Restart Play", dialog, "처음부터 다시", .14f, .35f, .86f, .51f);
            menu.exitButton = PauseButton("Exit Play", dialog, "곡 선택", .14f, .12f, .86f, .28f);
            menu.recoveryStatus = Label("Audio Recovery Status", dialog, "", .04f, .005f, .96f, .115f, 14);
            menu.recoveryStatus.color = new Color(.24f, .12f, .085f);
            menu.recoveryStatus.alignment = TextAnchor.MiddleCenter;
            menu.recoveryStatus.raycastTarget = false;
            menu.recoveryStatus.gameObject.SetActive(false);
            DojoMenuNavigation.Link(menu.resumeButton, menu.exitButton, menu.restartButton);
            DojoMenuNavigation.Link(menu.restartButton, menu.resumeButton, railOpacity);
            DojoMenuNavigation.Link(railOpacity, menu.restartButton, menu.exitButton);
            DojoMenuNavigation.Link(menu.exitButton, railOpacity, menu.resumeButton);
            var group = dialog.gameObject.AddComponent<CanvasGroup>();
            var reveal = dialog.gameObject.AddComponent<DojoReveal>();
            reveal.group = group;
            shade.gameObject.SetActive(false);

            var countdown = Panel("Resume Countdown", parent, new Color(.08f, .035f, .015f, .55f), 0, 0, 1, 1);
            countdown.GetComponent<Image>().raycastTarget = true;
            menu.countdown = countdown.gameObject;
            var plaque = Panel("Countdown Wooden Seal", countdown, Color.white, .412f, .348f, .588f, .652f);
            plaque.GetComponent<Image>().sprite = DojoEntranceAssets.Load("RoundPlaque", UiArtworkCrop.Alpha);
            plaque.GetComponent<Image>().preserveAspect = true;
            menu.countdownText = Label("Countdown Number", plaque, "3", .08f, .03f, .92f, .97f, 102, role: UiTextRole.Title);
            menu.countdownText.color = new Color(1, .91f, .72f);
            menu.countdownText.alignment = TextAnchor.MiddleCenter;
            var shadow = menu.countdownText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.20f, .07f, .025f);
            shadow.effectDistance = new Vector2(3, -3);
            var countdownHint = Label("Countdown Hint", countdown, "Esc  ·  일시정지로 돌아가기",
                .25f, .27f, .75f, .32f, 18, role: UiTextRole.Help);
            countdownHint.alignment = TextAnchor.MiddleCenter;
            countdownHint.color = Cream;
            countdown.gameObject.SetActive(false);
            menu.Validate();
        }

        internal static void BuildFocusHud(GameplayHudView view)
        {
            view.songInformationPanel = view.songTitle.transform.parent.gameObject;

            var menu = view.GetComponent<GameplayPauseMenu>();
            var dialog = menu.resumeButton.transform.parent;
            view.focusHudButton = PauseButton("Focus HUD", dialog, "HUD · 기본", .14f, .57f, .86f, .67f);
            view.focusHudButton.GetComponentInChildren<Text>().fontSize = 25;
            view.focusHudButton.interactable = false;
            view.focusHudButton.gameObject.SetActive(false);
        }

        private static Button PauseButton(string name, Transform parent, string text,
            float x0, float y0, float x1, float y1)
        {
            var button = DojoScreenStyle.Action(name, parent, text, Vector2.zero, Vector2.zero,
                name == "Resume Play");
            Bounds((RectTransform)button.transform, x0, y0, x1, y1);
            button.GetComponentInChildren<Text>().fontSize = 40;
            return button;
        }

        private static RectTransform Card(
            string name,
            Transform parent,
            float x0,
            float y0,
            float x1,
            float y1,
            Color accent
        )
        {
            var rect = Panel(name, parent, Color.clear, x0, y0, x1, y1);

            return rect;
        }

        private static RectTransform Panel(
            string name,
            Transform parent,
            Color color,
            float x0,
            float y0,
            float x1,
            float y1
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            Bounds(rect, x0, y0, x1, y1);

            return rect;
        }

        private static Text Label(
            string name,
            Transform parent,
            string text,
            float x0,
            float y0,
            float x1,
            float y1,
            int size,
            UiTextRole role = UiTextRole.Body
        )
        {
            var label = UiElements.Label(name, parent, text, Vector2.zero, Vector2.zero, size, role);
            label.font = font;
            label.color = Cream;
            label.alignment = TextAnchor.MiddleLeft;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            Bounds(label.rectTransform, x0, y0, x1, y1);

            return label;
        }

        private static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
