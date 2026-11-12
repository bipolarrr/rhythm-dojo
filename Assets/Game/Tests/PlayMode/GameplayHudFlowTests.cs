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
        public IEnumerator GameplayKeepsDisplayCameraWhileWorldRendersIntoWindow()
        {
            yield return LoadGameplay();
            Canvas.ForceUpdateCanvases();

            var viewport = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>();
            var camera = viewport.gameplayCamera;
            Assert.That(camera.targetTexture, Is.Not.Null);
            Assert.That(Camera.main, Is.SameAs(camera));

            var displayCameras = Camera.allCameras.Where(candidate =>
                candidate.targetTexture == null && candidate.targetDisplay == camera.targetDisplay
            ).ToArray();
            Assert.That(displayCameras.Length, Is.EqualTo(1));
            Assert.That(displayCameras[0].cullingMask, Is.Zero,
                "The display camera must not render unclipped world geometry.");
            Assert.That(displayCameras[0].GetComponent<AudioListener>(), Is.Null);
            Assert.That(displayCameras[0].rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
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
            Assert.That(view.judgmentRecordPanel.activeInHierarchy, Is.False);
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            yield return Await(() => pauseMenu.panel.activeSelf);
            view.SetFocusHud(true);
            Assert.That(game.IsPaused, Is.True);
            Assert.That(view.songInformationPanel.activeInHierarchy, Is.True);
            Assert.That(view.judgmentRecordPanel.activeInHierarchy, Is.False);
            Assert.That(view.combo.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.healthFill.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.progress.gameObject.activeInHierarchy, Is.False);
            view.SetFocusHud(false);
            Assert.That(view.judgmentRecordPanel.activeInHierarchy, Is.False);
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
            var lane = playfield.transform.Find("Lane 0").GetComponent<Renderer>();
            var rail = playfield.transform.Find("Rail 0").GetComponent<Renderer>();
            var centerRail = playfield.transform.Find("Rail 2");
            var judgment = playfield.transform.Find("Judgement Line").GetComponent<Renderer>();
            Assert.That(centerRail.localScale.x, Is.GreaterThan(rail.transform.localScale.x));

            var hold = UnityEngine.Object.FindObjectsByType<NoteView>(FindObjectsSortMode.None)
                .First(note => note.HasBody);
            var tailMarker = hold.transform.Find("Hold End Marker");
            var holdBody = hold.transform.Find("Hold Body");
            Assert.That(tailMarker, Is.Not.Null);
            Assert.That(holdBody, Is.Not.Null);
            Assert.That(tailMarker.localScale.x, Is.GreaterThan(holdBody.localScale.x));

            view.railOpacity.value = .5f;
            Assert.That(lane.sharedMaterial.GetColor("_BaseColor").a, Is.EqualTo(.5f));
            Assert.That(rail.sharedMaterial.GetColor("_BaseColor").a, Is.EqualTo(.5f));
            view.railOpacity.value = 0;
            Assert.That(lane.enabled, Is.False);
            Assert.That(rail.enabled, Is.False);
            Assert.That(judgment.enabled, Is.True);
            view.railOpacity.value = 1;
            Assert.That(lane.enabled, Is.True);
            Assert.That(rail.enabled, Is.True);

            var tempo = new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(1, 60) });
            var scroll = new ConstantScrollTimeline(10, 1);
            var chart = new ChartData(new[] { NoteData.Tap(0, 4) }, 5);
            playfield.ConfigureMeasureLines(tempo, scroll, chart);
            playfield.RenderMeasureLines(SessionState.Playing, 1);
            var line = playfield.transform.Find("Measure Line");
            Assert.That(line.position.z, Is.EqualTo(20).Within(.001));
            playfield.RenderMeasureLines(SessionState.Playing, 3);
            Assert.That(line.position.z, Is.Zero.Within(.001));
            playfield.RenderMeasureLines(SessionState.Ready, 0);
            Assert.That(line.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator GameplayWindowRendersJudgmentLineBelowCenter()
        {
            yield return LoadGameplay();
            var viewport = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>();
            var point = viewport.gameplayCamera.WorldToScreenPoint(new Vector3(.2f, .05f, 0));
            Assert.That(point.y, Is.LessThan(Screen.height * .5f));

            var capture = CaptureGameplayDisplay(viewport);

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

        internal static Texture2D CaptureGameplayDisplay(GameplayViewport viewport)
        {
            Canvas.ForceUpdateCanvases();
            viewport.gameplayCamera.Render();
            var camera = Camera.allCameras.Single(candidate =>
                candidate.targetTexture == null && candidate.targetDisplay == viewport.gameplayCamera.targetDisplay);
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
                camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
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
        public IEnumerator GameplayWindowPreservesProjectionAndReleasesCameraOnDisable()
        {
            yield return LoadGameplay();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var viewport = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>();
            Assert.That(viewport, Is.Not.Null);
            var image = viewport.GetComponent<UnityEngine.UI.RawImage>();
            var camera = viewport.gameplayCamera;
            var target = camera.targetTexture;
            Assert.That(target, Is.Not.Null);
            Assert.That(image.texture, Is.SameAs(target));
            Assert.That(target.IsCreated(), Is.True);
            Assert.That(image.raycastTarget, Is.False);

            // A projected judgment point must stay at the same screen position after
            // cropping, so the HUD's world-space hit feedback remains aligned.
            var point = camera.WorldToViewportPoint(Vector3.zero);
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            var uv = image.uvRect;
            Assert.That(uv.xMin, Is.GreaterThan(0));
            Assert.That(uv.xMax, Is.LessThan(1));
            Assert.That(uv.yMin, Is.GreaterThan(0));
            Assert.That(uv.yMax, Is.LessThan(1));
            float displayedX = Mathf.LerpUnclamped(min.x, max.x, (point.x - uv.x) / uv.width);
            float displayedY = Mathf.LerpUnclamped(min.y, max.y, (point.y - uv.y) / uv.height);
            Assert.That(displayedX, Is.EqualTo(point.x * target.width).Within(1));
            Assert.That(displayedY, Is.EqualTo(point.y * target.height).Within(1));

            viewport.enabled = false;
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(image.texture, Is.Null);
            yield return null;
            Assert.That(target == null, Is.True, "The old render texture must be destroyed.");

            viewport.enabled = true;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(camera.targetTexture, Is.Not.Null);
            Assert.That(image.texture, Is.SameAs(camera.targetTexture));
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
            yield return Click(view.backButton);
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
            Vector2 middle = judgmentRect.anchorMin;
            var presentation = PresentationPreferences.Current;
            model.Set(SessionState.Playing, 1, 0, 0, 1);
            presentation.judgmentPosition = JudgmentPosition.Upper;
            model.Judge(Judgment.Perfect, -.012);
            Assert.That(judgmentRect.anchorMin.y, Is.GreaterThan(middle.y));
            Assert.That(view.judgment.transform.IsChildOf(judgmentRect), Is.True);
            Assert.That(view.timing.transform.IsChildOf(judgmentRect), Is.True);
            Assert.That(view.judgmentGroup.alpha, Is.EqualTo(.55f));
            Assert.That(view.judgment.fontSize, Is.EqualTo(25));
            Assert.That(view.timing.fontSize, Is.EqualTo(14));
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text, Is.EqualTo("1"));
            Assert.That(view.judgment.text, Is.EqualTo("Perfect"));
            StringAssert.Contains("빠름", view.timing.text);
            presentation.judgmentPosition = JudgmentPosition.Middle;
            model.Hold();
            Assert.That(judgmentRect.anchorMin, Is.EqualTo(middle));
            Assert.That(view.judgment.fontSize, Is.EqualTo(19));
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.judgment.text, Is.EqualTo("HOLD · Perfect"));
            Assert.That(
                view.combo.text,
                Is.EqualTo("1"),
                "Hold heads must not add combo before resolution."
            );
            model.Set(SessionState.Playing, 1, 1, 0, 2);
            presentation.judgmentPosition = JudgmentPosition.Lower;
            model.Judge(Judgment.Good, .055);
            Assert.That(judgmentRect.anchorMin.y, Is.LessThan(middle.y));
            StringAssert.Contains("늦음", view.timing.text);
            Assert.That(view.judgment.text, Is.EqualTo("Hmm..."));
            model.Set(SessionState.Playing, 1, 1, 1, 0);
            model.Judge(Judgment.Miss, .25);
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text, Is.EqualTo("0"));
            Assert.That(
                view.bestCombo.text,
                Is.EqualTo("BEST  2"),
                "Keep peak combo even when a miss arrives in the same frame."
            );
            Assert.That(view.judgment.text, Is.EqualTo("Nope."));
            Assert.That(view.timing.text, Is.EqualTo("COMBO BREAK"));
            Assert.That(view.accuracy.text, Is.EqualTo("정확도  18.8%"));
            Assert.That(
                view.progressFill.rectTransform.anchorMax.x,
                Is.EqualTo(3f / 8).Within(.001)
            );
            model.Set(SessionState.Ready, 0, 0, 0, 0, ReadyReason.FocusLost);
            model.Reset();
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.bestCombo.text, Is.EqualTo("BEST  0"));
            Assert.That(view.judgment.text, Is.Empty);
            Assert.That(judgmentRect.anchorMin, Is.EqualTo(middle));
            Assert.That(view.judgmentGroup.transform.parent, Is.EqualTo(view.transform));
            StringAssert.Contains("창 전환", view.timing.text);
            Assert.That(view.judgmentGroup.alpha, Is.Zero);
            Assert.That(view.judgment.fontSize, Is.EqualTo(36));
            Assert.That(view.timing.fontSize, Is.EqualTo(18));
            Assert.That(view.progressFill.rectTransform.anchorMax.x, Is.Zero);
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
            Assert.That(view.backButton.gameObject.activeSelf, Is.True);
            yield return Click(view.backButton);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            var selectionView = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            StringAssert.DoesNotContain("Full Combo", selectionView.comboRecord.text);
        }
    }
}
