using System;
using System.Collections;
using System.Linq;
using RhythmDojo.Application;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class GameplayHudFlowTests : GameplayTestContext
    {
        private IEnumerator LoadGameplay()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
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
            public void Judge(Judgment grade, double error) => Judged?.Invoke(new JudgmentEvent(0, new NoteData(0,NoteKind.Tap,1,1),grade,error));
            public void Hold() => HoldStarted?.Invoke(new HoldStartedEvent(new JudgmentEvent(0,new NoteData(0,NoteKind.Hold,1,2),Judgment.Perfect,-.010)));
            public void Reset() => ResetOccurred?.Invoke();
            public void Set(SessionState state, int perfect, int good, int miss, int combo, ReadyReason reason = ReadyReason.Initial)
            {
                Snapshot = new GameplaySnapshot(new SessionSnapshot(state,perfect,good,miss,combo,8),
                    "HUD 검증곡","Normal",ScrollMode.Bpm,1,0,120,reason);
            }
        }

        [UnityTest]
        public IEnumerator TrainingFeedbackTracksHoldMissPeakComboAndFocusReset()
        {
            yield return LoadGameplay();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            var hud = UnityEngine.Object.FindFirstObjectByType<RhythmHud>();
            var model = new HudReadModel(); model.Set(SessionState.Ready,0,0,0,0);
            hud.Initialize(model,Resources.FindObjectsOfTypeAll<GameModeDefinition>().First(m=>m.LaneCount==4),
                Resources.FindObjectsOfTypeAll<GameplayPresentationSettings>().First());
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.songTitle.text,Is.EqualTo("HUD 검증곡"));
            model.Set(SessionState.Playing,1,0,0,1); model.Judge(Judgment.Perfect,-.012);
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text,Is.EqualTo("1"));
            Assert.That(view.judgment.text,Is.EqualTo("PERFECT"));
            StringAssert.Contains("EARLY",view.timing.text);
            model.Hold(); yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.judgment.text,Is.EqualTo("HOLD · PERFECT"));
            Assert.That(view.combo.text,Is.EqualTo("1"),"Hold heads must not add combo before resolution.");
            model.Set(SessionState.Playing,1,1,0,2); model.Judge(Judgment.Good,.055);
            model.Set(SessionState.Playing,1,1,1,0); model.Judge(Judgment.Miss,.25);
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.combo.text,Is.EqualTo("0"));
            Assert.That(view.bestCombo.text,Is.EqualTo("BEST  2"),"Keep peak combo even when a miss arrives in the same frame.");
            Assert.That(view.judgment.text,Is.EqualTo("MISS"));
            Assert.That(view.timing.text,Is.EqualTo("COMBO BREAK"));
            Assert.That(view.accuracy.text,Is.EqualTo("정확도  50.0%"));
            Assert.That(view.progressFill.rectTransform.anchorMax.x,Is.EqualTo(3f/8).Within(.001));
            model.Set(SessionState.Ready,0,0,0,0,ReadyReason.FocusLost); model.Reset();
            yield return new WaitForSecondsRealtime(.07f);
            Assert.That(view.bestCombo.text,Is.EqualTo("BEST  0"));
            Assert.That(view.judgment.text,Is.EqualTo("READY"));
            StringAssert.Contains("창 전환",view.timing.text);
            Assert.That(view.judgmentGroup.alpha,Is.EqualTo(1));
            Assert.That(view.progressFill.rectTransform.anchorMax.x,Is.Zero);
            model.Set(SessionState.Completed,8,0,0,8); model.Judge(Judgment.Perfect,0);
            yield return new WaitForSecondsRealtime(1.12f);
            Assert.That(view.judgment.text,Is.EqualTo("FINISH"));
            Assert.That(view.judgmentGroup.alpha,Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReturnButtonInReadyStateLoadsSongSelectionAndReleasesSong()
        {
            yield return LoadGameplay();
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest, Is.Null);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<RhythmGameController>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturnButtonIsHiddenWhilePlayingAndEscapeReturnsToSelection()
        {
            yield return LoadGameplay();
            yield return Press(Key.Space);
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.False);
            yield return Press(Key.Escape);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().CurrentRequest, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturnButtonReappearsAfterCompletionAndLoadsSongSelection()
        {
            yield return LoadGameplay();
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);
            double deadline = Time.realtimeSinceStartupAsDouble + game.Chart.CompletionTime + 10;
            while (game.Session.State != SessionState.Completed)
            {
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
                yield return null;
            }
            yield return null;
            var view = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            Assert.That(view.returnButton.gameObject.activeSelf, Is.True);
            yield return Click(view.returnButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
