using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class GameplayHudFlowTests : GameplayTestContext
    {
        private IEnumerator LoadGameplay()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()
            );
            yield return null;
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator StageCameraAndOverlayDrawGameplayInOrder()
        {
            yield return LoadGameplay();
            Canvas.ForceUpdateCanvases();

            var viewport = LaneViewport();
            var room = UnityEngine.Object.FindFirstObjectByType<DojoRoomPhaseView>();
            Assert.That(Camera.allCameras, Is.EquivalentTo(new[] { room.stageCamera }));
            Assert.That(room.stageCamera.orthographic, Is.True);
            Assert.That(room.stageCamera.targetTexture, Is.Null);
            Assert.That(viewport.GetComponentInParent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(viewport.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PauseMenuFocusHudKeepsPlayIndicatorsAndSavesChoice()
        {
            yield return LoadGameplay();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var pauseMenu = view.GetComponent<GameplayPauseMenu>();
            Assert.That(view.songInformationPanel.activeInHierarchy, Is.True);
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            yield return Await(() => pauseMenu.panel.activeSelf);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(view.songInformationPanel.activeInHierarchy, Is.True);
            Assert.That(view.combo.text, Is.Empty);
            Assert.That(view.healthFill.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.focusHudButton.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator RailOpacityAndMeasureLinesKeepJudgmentLineVisible()
        {
            yield return LoadGameplay();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            var pauseMenu = view.GetComponent<GameplayPauseMenu>();
            Assert.That(view.railOpacity.transform.IsChildOf(pauseMenu.panel.transform), Is.True);
            Assert.That(view.railOpacity.gameObject.activeInHierarchy, Is.False);
            var playfield = UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>();
            var lane = playfield.transform.Find("Lane 0").GetComponent<Image>();
            var rail = playfield.transform.Find("Rail 0").GetComponent<Image>();
            var judgment = playfield.judgmentLine;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var assembly = playfield.GetComponentsInChildren<Image>(true);
            playfield.Build(flow.CurrentRequest.Song.Mode, flow.Settings.presentation);
            CollectionAssert.AreEquivalent(assembly, playfield.GetComponentsInChildren<Image>(true));
            var note = UnityEngine.Object.FindFirstObjectByType<NotePresenter>().GetComponentInChildren<NoteView>(true);
            var head = note.transform.Find("Head").GetComponent<Image>();
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            yield return Await(() => pauseMenu.panel.activeSelf);
            yield return Press(Key.DownArrow);
            yield return Press(Key.DownArrow);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,
                Is.SameAs(view.railOpacity.gameObject));
            yield return Press(Key.LeftArrow);
            Assert.That(lane.color.a, Is.LessThan(1));
            Assert.That(lane.color.a, Is.EqualTo(view.railOpacity.value));

            view.railOpacity.value = 0;
            Assert.That(lane.color.a, Is.Zero);
            Assert.That(rail.color.a, Is.Zero);
            Assert.That(judgment.color.a, Is.EqualTo(1));
            Assert.That(head.color.a, Is.EqualTo(1));
            view.railOpacity.value = .5f;
            Assert.That(lane.color.a, Is.EqualTo(.5f));

            var tempo = new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(1, 60) });
            var scroll = new ConstantScrollTimeline(5, 1);
            var chart = new ChartData(new[] { NoteData.Tap(0, 4) }, 5);
            playfield.ConfigureMeasureLines(tempo, scroll, chart);
            playfield.RenderMeasureLines(SessionState.Playing, 1);
            var line = playfield.grid.GetComponentsInChildren<Image>().Single(i => i.sprite == flow.Settings.presentation.measure);
            Assert.That(line.color.a, Is.EqualTo(.5f));
            Assert.That(line.rectTransform.anchoredPosition.y - judgment.rectTransform.anchoredPosition.y,
                Is.EqualTo(100).Within(.001));
            playfield.RenderMeasureLines(SessionState.Playing, 3);
            line = playfield.grid.GetComponentsInChildren<Image>().First(i => i.sprite == flow.Settings.presentation.measure);
            Assert.That(line.rectTransform.position.y, Is.EqualTo(judgment.rectTransform.position.y).Within(.001));
            playfield.RenderMeasureLines(SessionState.Ready, 0);
            Assert.That(line.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator GameplayWindowRendersJudgmentLineBelowCenter()
        {
            yield return LoadGameplay();
            var viewport = LaneViewport();
            var playfield = viewport.GetComponentInChildren<PlayfieldPresenter>();
            var line = playfield.judgmentLine.rectTransform;
            var point = RectTransformUtility.WorldToScreenPoint(null, line.TransformPoint(3, 0, 0));
            var drawing = (RectTransform)playfield.transform.Find("Lane 0");
            var entry = RectTransformUtility.WorldToScreenPoint(null, drawing.TransformPoint(0, drawing.rect.yMax, 0));
            var bottom = RectTransformUtility.WorldToScreenPoint(null, drawing.TransformPoint(0, drawing.rect.yMin, 0));
            Assert.That((entry.y - bottom.y) / Screen.height, Is.GreaterThanOrEqualTo(.90f));
            Assert.That(bottom.y / Screen.height, Is.InRange(0f, .02f));
            Assert.That(point.y / Screen.height, Is.InRange(.06f, .10f));
            Assert.That((entry.y - point.y) / Screen.height, Is.GreaterThanOrEqualTo(.90f));

            yield return new WaitForEndOfFrame();
            var capture = ScreenCapture.CaptureScreenshotAsTexture();

            try
            {
                bool foundJudgmentLine = false;
                int x = Mathf.RoundToInt(point.x);
                int y = Mathf.RoundToInt(point.y);
                for (int offset = -4; offset <= 4; offset++)
                {
                    var color = capture.GetPixel(x, y + offset);
                    if (color.r > .65f && color.g > .5f)
                    {
                        if (color.b < color.g)
                        {
                            foundJudgmentLine = true;
                            break;
                        }
                    }
                }

                System.IO.File.WriteAllBytes("Logs/gameplay-scroll-render.png", capture.EncodeToPNG());
                Assert.That(foundJudgmentLine, Is.True,
                    "The rendered judgment line must match its lower-screen projection, not a vertically reflected position.");
            }
            finally
            {
                UnityEngine.Object.Destroy(capture);
            }
        }

        internal static GameplayViewport LaneViewport()
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            return hud.viewport;
        }

        private static void AssertFeedbackInsideLane(GameplayHudView view)
        {
            var field = view.viewport.field;
            var combo = RectTransformUtility.CalculateRelativeRectTransformBounds(field, view.combo.rectTransform);
            var grade = RectTransformUtility.CalculateRelativeRectTransformBounds(field, view.judgment.rectTransform);
            var timing = RectTransformUtility.CalculateRelativeRectTransformBounds(field, view.timing.rectTransform);
            foreach (var bounds in new[] { combo, grade, timing })
            {
                Assert.That(bounds.center.x, Is.EqualTo(field.rect.center.x).Within(3));
                Assert.That(bounds.min.x, Is.GreaterThan(field.rect.xMin));
                Assert.That(bounds.max.x, Is.LessThan(field.rect.xMax));
                Assert.That(bounds.min.y, Is.GreaterThan(field.rect.center.y));
                Assert.That(bounds.max.y, Is.LessThan(field.rect.yMax));
            }

            Assert.That(combo.center.y, Is.GreaterThan(grade.max.y));
            Assert.That(timing.center.y, Is.LessThan(grade.min.y));
        }

        internal static Texture2D CaptureGameplayDisplay()
        {
            Canvas.ForceUpdateCanvases();
            var camera = Camera.main;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance))
                .ToArray();
            var layers = canvases.SelectMany(item => item.canvas.GetComponentsInChildren<Transform>(true))
                .Select(item => (item.gameObject, item.gameObject.layer)).ToArray();
            int previousMask = camera.cullingMask;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var previous = RenderTexture.active;
            var capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.cullingMask |= 1 << LayerMask.NameToLayer("UI");
                foreach (var item in layers)
                {
                    item.gameObject.layer = LayerMask.NameToLayer("UI");
                }

                foreach (var item in canvases)
                {
                    item.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    item.canvas.worldCamera = camera;
                    item.canvas.planeDistance = 1;
                }

                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                capture.Apply();
                return capture;
            }
            catch
            {
                UnityEngine.Object.Destroy(capture);
                throw;
            }
            finally
            {
                camera.targetTexture = null;
                camera.cullingMask = previousMask;
                foreach (var item in layers)
                {
                    item.gameObject.layer = item.layer;
                }

                foreach (var item in canvases)
                {
                    item.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    item.canvas.worldCamera = item.worldCamera;
                    item.canvas.planeDistance = item.planeDistance;
                }

                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.Destroy(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ViewportResizesWithoutSeparatingNotesReceptorsAndJudgment()
        {
            yield return LoadGameplay();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var viewport = LaneViewport();
            Assert.That(viewport, Is.Not.Null);
            var host = (RectTransform)viewport.transform;
            var field = viewport.GetComponentInChildren<PlayfieldPresenter>();
            var note = UnityEngine.Object.FindFirstObjectByType<NotePresenter>().GetComponentInChildren<NoteView>(true);
            var head = (RectTransform)note.transform.Find("Head");
            var mode = UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest.Song.Mode;
            var settings = UnityEngine.Object.FindFirstObjectByType<AppFlowController>().Settings.presentation;
            foreach (var size in new[] { new Vector2(650, 500), new Vector2(220, 500), new Vector2(800, 250) })
            {
                host.anchorMin = host.anchorMax = Vector2.one * .5f;
                host.sizeDelta = size;
                Canvas.ForceUpdateCanvases();
                note.Render(NoteData.Tap(2, 1), NoteState.Pending, 1, new ConstantScrollTimeline(8, 1), mode, settings);
                var shoe = (RectTransform)field.transform.Find("Strike Shoe 2");
                Assert.That(head.position.x, Is.EqualTo(shoe.position.x).Within(.01));
                Assert.That(head.position.y, Is.EqualTo(field.judgmentLine.rectTransform.position.y).Within(.01));
                Assert.That(viewport.field.rect.width * viewport.field.localScale.x, Is.LessThanOrEqualTo(size.x + .01f));
                Assert.That(viewport.field.rect.height * viewport.field.localScale.y, Is.EqualTo(size.y).Within(.01f));

                // At this speed the head used to be clipped 2.5 seconds ahead of contact.
                note.Render(NoteData.Tap(2, 3.5), NoteState.Pending, 1, new ConstantScrollTimeline(8, 1), mode, settings);
                Assert.That(note.gameObject.activeSelf, Is.True);
                float travel = head.position.y - field.judgmentLine.rectTransform.position.y;
                Assert.That(travel, Is.EqualTo(200 * viewport.field.lossyScale.y).Within(.01f));
            }
        }

        private sealed class HudReadModel : IGameplayReadModel
        {
            public GameplaySnapshot Snapshot { get; set; }

            public int LaneCount => 4;

            public bool IsHeld(int lane) => false;

            public NoteState GetNoteState(int index) => NoteState.Pending;

            public event Action<JudgmentEvent> Judged;
            public event Action<HoldStartedEvent> HoldStarted;
            public event Action ResetOccurred;

            public void Judge(Judgment grade, double error) =>
                Judged?.Invoke(
                    new JudgmentEvent(0, new NoteData(0, NoteKind.Tap, 1, 1), grade, error)
                );

            public void Hold() =>
                HoldStarted?.Invoke(
                    new HoldStartedEvent(
                        new JudgmentEvent(
                            0,
                            new NoteData(0, NoteKind.Hold, 1, 2),
                            Judgment.Perfect,
                            -.010
                        )
                    )
                );

            public void Reset() => ResetOccurred?.Invoke();

            public void Set(
                SessionState state,
                int perfect,
                int good,
                int miss,
                int combo,
                ReadyReason reason = ReadyReason.Initial
            )
            {
                Snapshot = new GameplaySnapshot(
                    new SessionSnapshot(state, perfect, good, miss, combo, 8),
                    "HUD 검증곡",
                    "Normal",
                    ScrollMode.Bpm,
                    1,
                    0,
                    120,
                    reason
                );
            }
        }

        [UnityTest]
        [Category("Results")]
        [Category("SceneIntegration")]
        public IEnumerator HealthDepletionShowsGameOverFreezesNotesAndSpaceRestarts()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var rules = flow.Rules.Snapshot;
            rules.GaugeFailure = GaugeFailureMode.Stop;
            flow.Rules.Update(rules);
            yield return LoadGameplay();

            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);
            game.Session.Advance(9);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;
            var view = UnityEngine.Object.FindFirstObjectByType<ResultsView>();
            Assert.That(flow.LastResult.Session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(view.outcome.text, Is.EqualTo("FAILED"));
            Assert.That(flow.LastResult.Session.Health, Is.Zero);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmDojo.Audio.SongClock>(), Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<RhythmDojo.Presentation.NoteView>(FindObjectsSortMode.None), Is.Empty);
            int resolved = flow.LastResult.Session.Resolved;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(flow.LastResult.Session.Resolved, Is.EqualTo(resolved));

            // Retry through song selection now that a finished play owns a separate scene.
            yield return Press(Key.Escape);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return LoadGameplay();
            game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);
            Assert.That(game.Session.Health, Is.EqualTo(100));
            Assert.That(game.Clock.Running, Is.True);
            yield return Press(Key.Escape);
            var pauseMenu = UnityEngine.Object.FindFirstObjectByType<GameplayPauseMenu>();
            Assert.That(pauseMenu.panel.activeSelf, Is.True);
            Assert.That(game.IsPaused, Is.True);
            yield return Click(pauseMenu.exitButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator TrainingFeedbackTracksHoldMissPeakComboAndFocusReset()
        {
            yield return LoadGameplay();

            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            var hud = UnityEngine.Object.FindFirstObjectByType<RhythmHud>();
            var model = new HudReadModel();
            model.Set(SessionState.Ready, 0, 0, 0, 0);
            hud.Initialize(
                model,
                Resources.FindObjectsOfTypeAll<GameModeDefinition>().First(m => m.LaneCount == 4),
                Resources.FindObjectsOfTypeAll<GameplayPresentationSettings>().First()
            );
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.songTitle.text, Is.EqualTo("HUD 검증곡"));
            var judgmentRect = (RectTransform)view.judgmentGroup.transform;
            float middle = view.laneReadout.position.y;
            var presentation = PresentationPreferences.Current;
            model.Set(SessionState.Playing, 1, 0, 0, 1);
            presentation.judgmentPosition = JudgmentPosition.Upper;
            model.Judge(Judgment.Perfect, -.012);
            Assert.That(view.laneReadout.position.y, Is.GreaterThan(middle));
            AssertFeedbackInsideLane(view);
            Assert.That(view.judgment.transform.IsChildOf(judgmentRect), Is.True);
            Assert.That(view.timing.transform.IsChildOf(judgmentRect), Is.True);
            Assert.That(view.judgmentGroup.alpha, Is.EqualTo(.96f));
            Assert.That(view.judgment.fontSize, Is.EqualTo(52));
            Assert.That(view.timing.fontSize, Is.EqualTo(14));
            float perfectScale = judgmentRect.localScale.x;
            Assert.That(view.judgmentMotion.echo.color.a, Is.GreaterThan(0));
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text, Is.EqualTo("1"));
            Assert.That(view.judgment.text, Is.EqualTo("절묘"));
            StringAssert.Contains("빠름", view.timing.text);
            presentation.judgmentPosition = JudgmentPosition.Middle;
            model.Hold();
            Assert.That(view.laneReadout.position.y, Is.EqualTo(middle));
            AssertFeedbackInsideLane(view);
            Assert.That(view.judgment.fontSize, Is.EqualTo(44));
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.judgment.text, Is.EqualTo("절묘"));
            Assert.That(
                view.combo.text,
                Is.EqualTo("1"),
                "Hold heads must not add combo before resolution."
            );
            model.Set(SessionState.Playing, 1, 1, 0, 2);
            presentation.judgmentPosition = JudgmentPosition.Lower;
            model.Judge(Judgment.Good, .055);
            Assert.That(judgmentRect.localScale.x, Is.LessThan(perfectScale));
            Assert.That(view.laneReadout.position.y, Is.LessThan(middle));
            AssertFeedbackInsideLane(view);
            StringAssert.Contains("늦음", view.timing.text);
            Assert.That(view.judgment.text, Is.EqualTo("명중"));
            model.Set(SessionState.Playing, 1, 1, 1, 0);
            model.Judge(Judgment.Miss, .25);
            Assert.That(view.judgmentMotion.echo.color.a, Is.Zero,
                "Misses must not use the successful strike's luminous afterimage.");
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text, Is.Empty);
            Assert.That(view.judgment.text, Is.EqualTo("빗나감"));
            Assert.That(view.timing.text, Is.EqualTo("연격 끊김"));
            model.Set(SessionState.Playing, 99, 1, 1, 100);
            model.Judge(Judgment.Perfect, 0);
            yield return Await(() => view.combo.text == "100");
            model.Set(SessionState.Ready, 0, 0, 0, 0, ReadyReason.FocusLost);
            model.Reset();
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.judgment.text, Is.Empty);
            Assert.That(view.laneReadout.position.y, Is.EqualTo(middle));
            StringAssert.Contains("창 전환", view.timing.text);
            Assert.That(view.judgmentGroup.alpha, Is.Zero);
            Assert.That(view.judgmentMotion.echo.text, Is.Empty);
            Assert.That(judgmentRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(judgmentRect.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.judgment.fontSize, Is.EqualTo(36));
            Assert.That(view.timing.fontSize, Is.EqualTo(18));
            model.Set(SessionState.Completed, 8, 0, 0, 7);
            model.Judge(Judgment.Perfect, 0);
            yield return Await(
                () => view.sessionState.text == "결과 화면으로 이동",
                "Completed HUD feedback did not settle."
            );
            Assert.That(view.judgment.text, Is.Empty);
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo("GAME CLEAR"));
            Assert.That(view.gameOverText.color.g, Is.GreaterThan(view.gameOverText.color.r));
            Assert.That(view.gameOverText.rectTransform.localScale.x, Is.GreaterThan(1f));
            model.Set(SessionState.Ready, 0, 0, 0, 0);
            model.Reset();
            Assert.That(view.gameOverPanel.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("Smoke")]
        [Category("SceneIntegration")]
        public IEnumerator ReturnButtonInReadyStateLoadsSongSelectionAndReleasesSong()
        {
            yield return LoadGameplay();

            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest,
                Is.Null
            );
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ReturnButtonIsHiddenWhilePlayingAndEscapeReturnsToSelection()
        {
            yield return LoadGameplay();
            yield return Press(Key.Space);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().Session.State
                == SessionState.Playing
            );
            yield return null;

            // Let the UI Update observe the input-driven state change.
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.False);
            yield return Press(Key.Escape);
            var pauseMenu = UnityEngine.Object.FindFirstObjectByType<GameplayPauseMenu>();
            Assert.That(pauseMenu.panel.activeSelf, Is.True);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>().IsPaused, Is.True);
            yield return Click(pauseMenu.exitButton);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest,
                Is.Null
            );
        }

        [UnityTest]
        [Category("Results")]
        [Category("SceneIntegration")]
        public IEnumerator ReturnButtonReappearsAfterCompletionAndLoadsSongSelection()
        {
            yield return LoadGameplay();

            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);

            var inputs = new System.Collections.Generic.List<(double time, int lane, bool press)>();

            for (int i = 0; i < game.Chart.Count; i++)
            {
                var note = game.Chart[i];
                inputs.Add((note.StartTime, note.Lane, true));
                inputs.Add(
                    (
                        (
                            note.Kind switch
                            {
                                NoteKind.Hold => note.EndTime,
                                _ => note.StartTime + .01,
                            }
                        ),
                        note.Lane,
                        false
                    )
                );
            }

            foreach (var input in inputs.OrderBy(item => item.time).ThenBy(item => item.press))
            {
                if (input.press)
                {
                    game.Session.PressLane(input.lane, input.time);
                }
                else
                {
                    game.Session.ReleaseLane(input.lane, input.time);
                }
            }

            game.Session.Advance(game.Chart.CompletionTime);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<ResultsScreen>());
            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<ResultsView>();
            var result = UnityEngine.Object.FindFirstObjectByType<AppFlowController>().LastResult;
            Assert.That(result.Session.Resolved, Is.EqualTo(result.Session.TotalNotes));
            Assert.That(result.Session.IsFullCombo, Is.True);
            Assert.That(view.outcome.text, Is.EqualTo("S"));
            yield return Press(Key.Escape);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            var selectionView = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            StringAssert.DoesNotContain("Full Combo", selectionView.comboRecord.text);
        }
    }
}
