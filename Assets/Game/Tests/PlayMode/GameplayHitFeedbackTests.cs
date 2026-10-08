using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class GameplayHitFeedbackTests : GameplayTestContext
    {
        private sealed class FeedbackReadModel : IGameplayReadModel
        {
            public GameplaySnapshot Snapshot => new GameplaySnapshot(new SessionSnapshot(SessionState.Playing,0,0,0,0,8),
                "Feedback test","Normal",ScrollMode.Constant,1,0,120,ReadyReason.Initial);
            public int LaneCount => 4;
            public bool Holding;
            public bool IsHeld(int lane) => Holding && lane == 1;
            public NoteState GetNoteState(int index) => Holding ? NoteState.Holding : NoteState.Completed;
            public event Action<JudgmentEvent> Judged;
            public event Action<HoldStartedEvent> HoldStarted;
            public event Action ResetOccurred;
            public void Hit(int lane, Judgment grade = Judgment.Perfect) => Judged?.Invoke(new JudgmentEvent(0,new NoteData(lane,NoteKind.Tap,1,1),grade,0));
            public void Hold()
            {
                Holding = true;
                HoldStarted?.Invoke(new HoldStartedEvent(new JudgmentEvent(0,new NoteData(1,NoteKind.Hold,1,2),Judgment.Perfect,0)));
            }
            public void ReleaseHold()
            {
                Holding = false;
                Judged?.Invoke(new JudgmentEvent(0,new NoteData(1,NoteKind.Hold,1,2),Judgment.Good,0));
            }
            public void Reset() { Holding = false; ResetOccurred?.Invoke(); }
        }
        private IEnumerator LoadGameplay()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<GameplayHitFeedback>());
            yield return null;
        }
        private static void Bind(GameplayHitFeedback feedback, FeedbackReadModel model)
        {
            feedback.Initialize(model,Resources.FindObjectsOfTypeAll<GameModeDefinition>().First(m=>m.LaneCount==4),
                Camera.main,UnityEngine.Object.FindFirstObjectByType<GameplayHudView>().GetComponent<Canvas>());
        }
        private static int ActiveBursts(GameplayHitFeedback feedback) => feedback.GetComponentsInChildren<GameplayImpactGraphic>()
            .Count(g=>g.name.StartsWith("Hit Burst"));
        private static int ActiveHolds(GameplayHitFeedback feedback) => feedback.GetComponentsInChildren<GameplayImpactGraphic>()
            .Count(g=>g.name.StartsWith("Hold Glow"));

        [UnityTest]
        public IEnumerator HitsReachAudioOutputWhileMissesStayQuietAndPoolsRemainBounded()
        {
            yield return LoadGameplay();
            var feedback=UnityEngine.Object.FindFirstObjectByType<GameplayHitFeedback>();
            var model=new FeedbackReadModel(); Bind(feedback,model);
            yield return new WaitForSecondsRealtime(.12f);
            var source=feedback.GetComponent<AudioSource>();
            model.Hit(0,Judgment.Miss);
            Assert.That(source.isPlaying,Is.False,"Automatic misses must not play an attack sound.");
            Assert.That(ActiveBursts(feedback),Is.EqualTo(1));
            model.Reset(); yield return null; model.Hit(0);
            var samples=new float[512]; float peak=0;
            double deadline=Time.realtimeSinceStartupAsDouble+1;
            while(peak<.00001f && Time.realtimeSinceStartupAsDouble<deadline)
            {
                AudioListener.GetOutputData(samples,0);
                foreach(float value in samples)peak=Mathf.Max(peak,Mathf.Abs(value));
                yield return null;
            }
            Assert.That(peak,Is.GreaterThan(.00001f),"Successful note must reach the listener output.");
            for(int i=0;i<100;i++)model.Hit(i%4);
            Assert.That(feedback.GetComponentsInChildren<GameplayImpactGraphic>(true).Length,Is.EqualTo(28));
            Assert.That(feedback.GetComponentsInChildren<GameplayImpactGraphic>(true).All(g=>!g.raycastTarget),Is.True);
            Assert.That(model.Snapshot.Session.Resolved,Is.Zero,"Presentation must not change judgment counts.");
            model.Reset();
            Assert.That(ActiveBursts(feedback),Is.Zero); Assert.That(source.isPlaying,Is.False);
        }

        [UnityTest]
        public IEnumerator HoldGlowSurvivesBurstFadeAndResetRestoresCameraEvenWhenPaused()
        {
            yield return LoadGameplay();
            var feedback=UnityEngine.Object.FindFirstObjectByType<GameplayHitFeedback>();
            var model=new FeedbackReadModel(); Bind(feedback,model);
            yield return null; var camera=Camera.main; var origin=camera.transform.position;
            model.Hold(); Assert.That(ActiveHolds(feedback),Is.EqualTo(1));
            float previous=Time.timeScale;
            try
            {
                Time.timeScale=0;
                for(int i=0;i<8;i++)
                {
                    yield return null;
                    Assert.That(Vector3.Distance(camera.transform.position,origin),Is.LessThanOrEqualTo(.0251f));
                }
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(ActiveBursts(feedback),Is.Zero); Assert.That(ActiveHolds(feedback),Is.EqualTo(1));
                Assert.That(Vector3.Distance(camera.transform.position,origin),Is.LessThan(.0001f));
                model.ReleaseHold(); Assert.That(ActiveHolds(feedback),Is.Zero); Assert.That(ActiveBursts(feedback),Is.EqualTo(1));
                model.Reset(); Assert.That(ActiveBursts(feedback),Is.Zero);
                Assert.That(Vector3.Distance(camera.transform.position,origin),Is.LessThan(.0001f));
            }
            finally {Time.timeScale=previous;}
        }

        [UnityTest]
        public IEnumerator RebindingDetachesOldEventsAndReturningToSelectionRemovesEffects()
        {
            yield return LoadGameplay();
            var feedback=UnityEngine.Object.FindFirstObjectByType<GameplayHitFeedback>();
            var first=new FeedbackReadModel(); Bind(feedback,first); yield return null;
            var second=new FeedbackReadModel(); Bind(feedback,second); yield return null;
            first.Hit(0); first.Hold(); Assert.That(ActiveBursts(feedback),Is.Zero); Assert.That(ActiveHolds(feedback),Is.Zero);
            second.Hit(0); Assert.That(ActiveBursts(feedback),Is.EqualTo(1));
            yield return Click(UnityEngine.Object.FindFirstObjectByType<GameplayHudView>().returnButton);
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GameplayHitFeedback>(),Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<GameplayImpactGraphic>(FindObjectsSortMode.None),Is.Empty);
        }
    }
}
