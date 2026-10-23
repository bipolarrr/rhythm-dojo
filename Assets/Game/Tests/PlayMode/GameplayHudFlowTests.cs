using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

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
        public IEnumerator GameplayWindowRendersJudgmentLineBelowCenter()
        {
            yield return LoadGameplay();
            yield return new WaitForEndOfFrame();

            var viewport = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>();
            var point = viewport.gameplayCamera.WorldToScreenPoint(new Vector3(.2f, .05f, 0));
            Assert.That(point.y, Is.LessThan(Screen.height * .5f));

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

        [UnityTest]
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
        public IEnumerator HealthDepletionShowsGameOverFreezesNotesAndSpaceRestarts()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var rules = flow.Rules.Snapshot;
            rules.GaugeFailure = GaugeFailureMode.Stop;
            flow.Rules.Update(rules);
            yield return LoadGameplay();

            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            yield return Press(Key.Space);
            game.Session.Advance(9);
            yield return null;
            yield return null;
            Assert.That(game.Session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo("GAME OVER"));
            Assert.That(view.healthFill.rectTransform.anchorMax.x, Is.Zero);
            Assert.That(view.healthLabel.text, Does.Contain("0 / 100"));
            Assert.That(game.Clock.Running, Is.False);
            Assert.That(
                UnityEngine
                    .Object.FindFirstObjectByType<RhythmDojo.Audio.SongClock>()
                    .GetComponent<AudioSource>()
                    .isPlaying,
                Is.False
            );

            double frozen = game.ReadModel.Snapshot.SongTime;
            double frozenClock = game.Clock.SongTime;
            var notes = UnityEngine.Object.FindObjectsByType<RhythmDojo.Presentation.NoteView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            var positions = notes.Select(note => note.transform.position).ToArray();
            int resolved = game.Session.Resolved;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.ReadModel.Snapshot.SongTime, Is.EqualTo(frozen));
            Assert.That(game.Clock.SongTime, Is.EqualTo(frozenClock));
            Assert.That(game.Session.Resolved, Is.EqualTo(resolved));

            for (int i = 0; i < notes.Length; i++)
            {
                Assert.That(notes[i].transform.position, Is.EqualTo(positions[i]));
            }

            yield return Press(Key.Space);
            yield return null;
            Assert.That(game.Session.Health, Is.EqualTo(100));
            Assert.That(game.Clock.Running, Is.True);
            Assert.That(view.gameOverPanel.activeSelf, Is.False);
            Assert.That(view.healthFill.rectTransform.anchorMax.x, Is.EqualTo(1));
            game.Session.Advance(9);
            yield return null;
            yield return null;
            yield return Press(Key.Escape);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
        }

        [UnityTest]
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
            model.Set(SessionState.Playing, 1, 0, 0, 1);
            model.Judge(Judgment.Perfect, -.012);
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text, Is.EqualTo("1"));
            Assert.That(view.judgment.text, Is.EqualTo("Perfect"));
            StringAssert.Contains("EARLY", view.timing.text);
            model.Hold();
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.judgment.text, Is.EqualTo("HOLD · Perfect"));
            Assert.That(
                view.combo.text,
                Is.EqualTo("1"),
                "Hold heads must not add combo before resolution."
            );
            model.Set(SessionState.Playing, 1, 1, 0, 2);
            model.Judge(Judgment.Good, .055);
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
            Assert.That(view.judgment.text, Is.EqualTo("READY"));
            StringAssert.Contains("창 전환", view.timing.text);
            Assert.That(view.judgmentGroup.alpha, Is.EqualTo(1));
            Assert.That(view.progressFill.rectTransform.anchorMax.x, Is.Zero);
            model.Set(SessionState.Completed, 8, 0, 0, 8);
            model.Judge(Judgment.Perfect, 0);
            yield return Await(
                () => view.judgment.text == "FINISH",
                "Completed HUD feedback did not settle."
            );
            Assert.That(view.judgment.text, Is.EqualTo("FINISH"));
            Assert.That(view.judgmentGroup.alpha, Is.EqualTo(1));
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo("GAME CLEAR"));
            Assert.That(view.gameOverText.color.g, Is.GreaterThan(view.gameOverText.color.r));
            model.Set(SessionState.Ready, 0, 0, 0, 0);
            model.Reset();
            Assert.That(view.gameOverPanel.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("Smoke")]
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
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
            Assert.That(
                UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest,
                Is.Null
            );
        }

        [UnityTest]
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

            double deadline = Time.realtimeSinceStartupAsDouble + game.Chart.CompletionTime + 10;

            while (game.Session.State != SessionState.Completed)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }

            yield return null;

            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(game.Session.Resolved, Is.EqualTo(game.Session.Snapshot.TotalNotes));
            Assert.That(view.gameOverPanel.activeSelf, Is.True);
            Assert.That(view.gameOverText.text, Is.EqualTo("GAME CLEAR"));
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() =>
                UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()
            );
        }
    }
}
