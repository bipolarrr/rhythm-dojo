using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class DojoBackgroundFlowTests : GameplayTestContext
    {
        private sealed class Clock : ISongClock, IResumableSongClock, IScheduledResumeClock
        {
            public double SongTime { get; set; }
            public bool Running { get; private set; }
            public bool ResumePending { get; private set; }
            public bool ResumeReady;

            public void Schedule()
            {
                SongTime = 0;
                Running = true;
            }

            public void Stop() => Running = false;
            public void Pause() => Running = false;
            public void Resume() => ResumePending = true;
            public void CancelResume() => ResumePending = false;
            public bool IsInputAfterResume(double timestamp) => timestamp >= SongTime;
            public double InputTimeToSongTime(double timestamp) => timestamp;

            public bool TryCompleteResume()
            {
                if (ResumeReady)
                {
                    ResumePending = false;
                    Running = true;
                }
                return Running;
            }
        }

        private sealed class Input : ILaneInput
        {
            public int LaneCount => 4;
            public event Action<int, double> Pressed;
            public event Action<int, double> Released;
            public event Action StartRequested;

            public void ResetHeld() { }
            public void Start() => StartRequested?.Invoke();

            public void Press(int lane, double time) => Pressed?.Invoke(lane, time);

            public void Release(int lane, double time) => Released?.Invoke(lane, time);

            public void Hit(int lane, double time)
            {
                Pressed?.Invoke(lane, time);
                Released?.Invoke(lane, time + .001);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator JudgmentsDriveRoomPauseRestartAndTimedFallbackWithoutReplacingComposition()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return WaitForGameplayIntroduction();
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var root = UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>();
            root.enabled = false; // Isolated tape must never write a player record or navigate to Results.
            var director = UnityEngine.Object.FindFirstObjectByType<GameplayBackgroundDirector>();
            var room = director.room;
            var notes = UnityEngine.Object.FindFirstObjectByType<NotePresenter>();
            var hud = UnityEngine.Object.FindFirstObjectByType<RhythmHud>();
            var mode = flow.Settings.catalog[0].Mode;
            var clock = new Clock();
            var input = new Input();
            var chartNotes = new List<NoteData>();
            for (int i = 0; i < 20; i++)
            {
                chartNotes.Add(NoteData.Tap(i % 4, 1 + i * .75));
            }
            chartNotes.Add(NoteData.Tap(0, 78));
            var chart = new ChartData(chartNotes.ToArray(), 80);
            var tempo = new TempoMap(new[] { new TempoPoint(0, 120), new TempoPoint(4, 170) });
            var rules = new PlayRules { GaugeFailure = GaugeFailureMode.Continue };
            var drawing = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>().field;
            var cameraPosition = drawing.position;
            var cameraRotation = drawing.rotation;
            var bout = (DojoCombatStage)room.scenery[0];
            var authoredTarget = bout.partner;
            int authoredCount = room.GetComponentsInChildren<Transform>(true).Length;

            game.Initialize(chart, rules.Timing, mode.ToRules(), tempo, "도장 수련실", "시각 검증",
                ScrollMode.Constant, 1, new ConstantScrollTimeline(10, 1), input, clock, notes, null, rules);
            hud.Initialize(game.ReadModel, mode, flow.Settings.presentation, mode.name);
            director.Initialize(game, 80, 0, true);
            input.Start();
            yield return null;

            Advance(game, director, clock, input, .94, false);
            var ready = bout.trainee.sprite;
            Advance(game, director, clock, input, 1.02, true);
            Assert.That(bout.trainee.sprite, Is.Not.EqualTo(ready));
            Assert.That(bout.contact.enabled, Is.True);
            Assert.That(bout.partner.sprite, Is.EqualTo(bout.partnerPoses[5]));
            Assert.That(director.Performance.Progress(0), Is.EqualTo(.05).Within(.001));
            Assert.That(room.RecentFlow, Is.GreaterThan(0));

            game.PauseSession();
            var contactPose = bout.trainee.sprite;
            var recoil = bout.partner.transform.localPosition;
            yield return null;
            yield return null;
            Assert.That(bout.trainee.sprite, Is.EqualTo(contactPose));
            Assert.That(bout.partner.transform.localPosition, Is.EqualTo(recoil));
            game.ResumeSession();
            clock.ResumeReady = true;
            yield return Await(() => !game.IsPaused);

            Advance(game, director, clock, input, 7.4, true);
            double earned = director.Performance.Progress(0);
            float flowBeforeMiss = room.RecentFlow;
            Advance(game, director, clock, input, 8.0, false);
            Assert.That(game.Session.Miss, Is.EqualTo(1));
            Assert.That(director.Performance.Progress(0), Is.EqualTo(earned));
            Assert.That(room.RecentFlow, Is.LessThan(flowBeforeMiss));
            var stumble = bout.trainee.sprite;

            Advance(game, director, clock, input, 8.28, false);
            Assert.That(bout.trainee.sprite, Is.Not.EqualTo(stumble));

            Advance(game, director, clock, input, 15.6, true);
            Assert.That(room.RecentFlow, Is.GreaterThan(flowBeforeMiss));
            var flowAtRest = room.RecentFlow;
            Advance(game, director, clock, input, 19.9, false);
            Assert.That(director.Performance.Mistakes, Is.EqualTo(1));
            Assert.That(room.RecentFlow, Is.EqualTo(flowAtRest));

            Advance(game, director, clock, input, 20.3, false);
            Assert.That(director.ActivePhase, Is.EqualTo(1));
            Assert.That(room.stageCamera.transform.position.x, Is.GreaterThan(0));
            Assert.That(room.scenery[0].gameObject.activeSelf, Is.True);
            Advance(game, director, clock, input, 20.8, false);
            Assert.That(room.scenery[0].gameObject.activeSelf, Is.False);
            Assert.That(room.scenery[1].gameObject.activeSelf, Is.True);
            Assert.That(drawing.position, Is.EqualTo(cameraPosition));
            Assert.That(drawing.rotation, Is.EqualTo(cameraRotation));

            game.RestartSession();
            yield return null;
            Assert.That(room.gameObject.activeSelf, Is.True);
            Assert.That(bout.partner, Is.SameAs(authoredTarget));
            Assert.That(room.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(authoredCount));
            Assert.That(room.Strikes, Is.Zero);
            Assert.That(room.RecentFlow, Is.Zero);
            Assert.That(director.Performance.Progress(0), Is.Zero);
            director.Initialize(game, 80, 0, false);
            Assert.That(bout.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator FourPhasesReactPauseAndFinishFromWholeSongFactsOnShortSongs()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return WaitForGameplayIntroduction();
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().enabled = false;
            var director = UnityEngine.Object.FindFirstObjectByType<GameplayBackgroundDirector>();
            var notes = UnityEngine.Object.FindFirstObjectByType<NotePresenter>();
            var mode = flow.Settings.catalog[0].Mode;
            var clock = new Clock();
            var input = new Input();
            var chartNotes = new List<NoteData>();
            for (int phase = 0; phase < 4; phase++)
            {
                for (int i = 0; i < 8; i++)
                {
                    chartNotes.Add(NoteData.Tap(i % 4, phase * 4 + .5 + i * .35));
                }
            }
            var chart = new ChartData(chartNotes.ToArray(), 16);
            var rules = new PlayRules { GaugeFailure = GaugeFailureMode.Continue };
            var drawing = UnityEngine.Object.FindFirstObjectByType<GameplayViewport>().field;
            var cameraPosition = drawing.position;
            var cameraRotation = drawing.rotation;
            var scale = drawing.localScale;
            var piece = ((DojoCombatStage)director.room.scenery[3]).wood;
            int count = director.room.GetComponentsInChildren<Transform>(true).Length;

            game.Initialize(chart, rules.Timing, mode.ToRules(), new TempoMap(new[] { new TempoPoint(0, 120) }),
                "네 페이즈", "검증", ScrollMode.Constant, 1, new ConstantScrollTimeline(10, 1),
                input, clock, notes, null, rules);
            director.Initialize(game, 16, 0, true);
            UnityEngine.Object.FindFirstObjectByType<RhythmHud>().Initialize(game.ReadModel,
                mode, flow.Settings.presentation, mode.name);
            for (int scenario = 0; scenario < 3; scenario++)
            {
                game.RestartSession();
                director.SendMessage("LateUpdate");
                bool success = scenario != 1;
                if (scenario == 2)
                {
                    // A previous-phase empty press must veto even a perfect final phase.
                    input.Hit(3, .1);
                    director.SendMessage("LateUpdate");
                }
                var phases = new HashSet<int>();
                for (int phase = 0; phase < 4; phase++)
                {
                    var stage = director.room.scenery[phase];
                    var drawings = new HashSet<Sprite>();
                    bool captured = scenario == 0 && Environment.GetEnvironmentVariable("RHYTHM_DOJO_CAPTURE") == "1";
                    for (int frame = 0; frame < 96; frame++)
                    {
                        double at = phase * 4 + frame / 24.0;
                        AdvanceJourney(game, director, clock, input, at, success, phases);
                        drawings.Add(stage.trainee.sprite);
                        if (captured)
                        {
                            yield return CaptureJourneyState((phase * 96 + frame).ToString("D4"));
                        }
                        if (CaptureLanes && scenario == 0 && frame == 22)
                        {
                            var field = UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>();
                            field.ConfigureMeasureLines(new TempoMap(new[] { new TempoPoint(0, 120) }),
                                new ConstantScrollTimeline(10, 1), chart);
                            field.RenderMeasureLines(SessionState.Playing, clock.SongTime);
                            yield return CaptureJourneyState("lane-stage-" + phase);
                            if (phase == 0)
                            {
                                field.viewport.field.gameObject.SetActive(false);
                                try
                                {
                                    yield return CaptureJourneyState("lane-background");
                                }
                                finally
                                {
                                    field.viewport.field.gameObject.SetActive(true);
                                }
                            }
                        }
                        if (frame == 38)
                        {
                            Assert.That(director.ActivePhase, Is.EqualTo(phase));
                            if (scenario == 0)
                            {
                                AssertActorRendered(director.room.stageCamera, stage.trainee);
                            }
                            float age = director.room.VisualTime;
                            var foot = stage.trainee.transform.position;
                            var pose = stage.trainee.sprite;
                            game.PauseSession();
                            yield return null;
                            yield return null;
                            Assert.That(stage.trainee.transform.position, Is.EqualTo(foot));
                            Assert.That(stage.trainee.sprite, Is.SameAs(pose));
                            Assert.That(director.room.VisualTime, Is.EqualTo(age));
                            game.ResumeSession();
                            clock.ResumeReady = true;
                            yield return Await(() => !game.IsPaused);
                        }
                    }
                    Assert.That(drawings.Count, Is.GreaterThan(2), "Expected full-body changes during each drill.");
                    Assert.That(stage.gameObject.activeInHierarchy, Is.True);
                    Assert.That(director.Performance.Progress(phase), Is.EqualTo(Convert.ToInt32(success)).Within(.001));
                }
                Assert.That(phases, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
                AdvanceJourney(game, director, clock, input, 16.1, success, phases);
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Completed));
                Assert.That(director.room.Finishing, Is.True);
                Assert.That(director.room.Destroyed, Is.EqualTo(scenario == 0));
                if (scenario == 0)
                {
                    Assert.That(game.Session.Snapshot.IsFullCombo, Is.True);
                    yield return Await(() => director.room.FinishAge >= .65f);
                    Assert.That(Quaternion.Angle(piece.transform.localRotation, Quaternion.identity), Is.GreaterThan(70));
                    Assert.That(game.Session.Snapshot.Good, Is.GreaterThan(0));
                }
                else
                {
                    yield return Await(() => director.room.FinishAge > .75f);
                    Assert.That(Quaternion.Angle(piece.transform.localRotation, Quaternion.identity), Is.LessThan(1));
                }
                Assert.That(drawing.position, Is.EqualTo(cameraPosition));
                Assert.That(drawing.rotation, Is.EqualTo(cameraRotation));
                Assert.That(drawing.localScale, Is.EqualTo(scale));
                Assert.That(game.ReadModel.Snapshot.ScrollMultiplier, Is.EqualTo(1));
            }

            Assert.That(((DojoCombatStage)director.room.scenery[3]).wood, Is.SameAs(piece));
            Assert.That(director.room.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            game.RestartSession();
            director.SendMessage("LateUpdate");
            Assert.That(director.room.Destroyed, Is.False);
            Assert.That(director.ActivePhase, Is.Zero);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator DensePatternsKeepContactAndComboReadableThroughHoldAndMisses()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return WaitForGameplayIntroduction();
            UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().enabled = false;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var director = UnityEngine.Object.FindFirstObjectByType<GameplayBackgroundDirector>();
            var notes = UnityEngine.Object.FindFirstObjectByType<NotePresenter>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameplayHudView>();
            var hits = hud.GetComponent<GameplayHitFeedback>();
            var mode = flow.Settings.catalog[0].Mode;
            var clock = new Clock();
            var input = new Input();
            var chartNotes = new List<NoteData>();
            for (int i = 0; i < 64; i++)
            {
                double time = .5 + i / 16.0;
                int lane = i % 4;
                if (i >= 48)
                {
                    lane = 1 + i % 3;
                }
                else if (i >= 32)
                {
                    lane = (i / 4) % 4;
                }
                else if (i >= 16)
                {
                    lane = i % 2;
                }

                if (i == 48)
                {
                    chartNotes.Add(NoteData.Hold(0, time, 5.25));
                }
                chartNotes.Add(NoteData.Tap(lane, time));
                if (i == 56)
                {
                    chartNotes.Add(NoteData.Tap(1, time));
                    chartNotes.Add(NoteData.Tap(2, time));
                }
            }
            chartNotes.Add(NoteData.Tap(1, 5.5));
            chartNotes.Add(NoteData.Tap(2, 5.75));
            var rules = new PlayRules { GaugeFailure = GaugeFailureMode.Continue };
            game.Initialize(new ChartData(chartNotes.ToArray(), 28), rules.Timing, mode.ToRules(),
                new TempoMap(new[] { new TempoPoint(0, 120) }), "타격 수련", "고밀도",
                ScrollMode.Constant, 1, new ConstantScrollTimeline(15, 1), input, clock, notes, null, rules);
            director.Initialize(game, 28, 0, true);
            hud.GetComponent<RhythmHud>().Initialize(game.ReadModel, mode, flow.Settings.presentation);
            hits.Initialize(game.ReadModel, mode, UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>());
            input.Start();
            int next = 0;
            int lastHead = 0;
            for (int frame = 0; frame <= 189; frame++)
            {
                clock.SongTime = frame / 32.0;
                while (next < chartNotes.Count && chartNotes[next].StartTime <= clock.SongTime)
                {
                    var note = chartNotes[next++];
                    if (note.Kind == NoteKind.Hold)
                    {
                        input.Press(note.Lane, note.StartTime);
                    }
                    else if (note.StartTime < 5.5)
                    {
                        input.Hit(note.Lane, note.StartTime);
                    }
                }
                game.SendMessage("Update");
                if (frame == 168)
                {
                    input.Release(0, clock.SongTime);
                }
                yield return null;

                bool captureHold = frame == 100 || frame == 145 || frame == 166;
                bool capturePattern = frame == 62 || frame == 128;
                if (CaptureLanes && (captureHold || capturePattern))
                {
                    var field = UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>();
                    field.ConfigureMeasureLines(new TempoMap(new[] { new TempoPoint(0, 120) }),
                        new ConstantScrollTimeline(15, 1), game.Chart);
                    field.RenderMeasureLines(SessionState.Playing, clock.SongTime);
                    yield return CaptureJourneyState("lane-dense-" + frame);
                }
                if (frame == 16)
                {
                    Assert.That(((DojoCombatStage)director.room.scenery[0]).contact.enabled, Is.True);
                }
                if (frame == 22)
                {
                    Assert.That(director.room.Strikes, Is.LessThan(4), "Dense heads need room to recover.");
                }
                if (frame == 144)
                {
                    lastHead = director.room.Strikes;
                }
                if (frame == 168)
                {
                    Assert.That(director.ActivePhase, Is.Zero);
                    Assert.That(director.room.Strikes, Is.EqualTo(lastHead), "Hold ticks and release cannot trigger another strike.");
                }
                if (frame == 128)
                {
                    Assert.That(game.Session.Snapshot.Combo, Is.GreaterThan(50));
                    Assert.That(hud.combo.text, Is.EqualTo(game.Session.Snapshot.Combo.ToString()));
                    Assert.That(hits.GetComponentsInChildren<GameplayImpactGraphic>()
                        .Count(graphic => graphic.name.StartsWith("Hold Glow")), Is.EqualTo(1));
                }
            }

            Assert.That(game.Session.Snapshot.Miss, Is.EqualTo(2));
            Assert.That(hud.combo.text, Is.Empty);
            Assert.That(hud.judgment.text, Is.EqualTo("빗나감"));
        }

        private static void AssertActorRendered(Camera camera, SpriteRenderer actor)
        {
            int width = Mathf.RoundToInt(160 * camera.aspect);
            var target = RenderTexture.GetTemporary(width, 160, 24);
            var texture = new Texture2D(width, 160, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            bool enabled = actor.enabled;
            try
            {
                Color32[] ReadStage()
                {
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, 160), 0, 0);
                    texture.Apply();
                    return texture.GetPixels32();
                }

                var withActor = ReadStage();
                actor.enabled = false;
                var withoutActor = ReadStage();
                int visible = 0;
                for (int i = 0; i < withActor.Length; i++)
                {
                    if (!withActor[i].Equals(withoutActor[i]))
                    {
                        visible++;
                    }
                }
                Assert.That(visible, Is.GreaterThan(120), "The stage camera must actually draw the trainee.");
            }
            finally
            {
                actor.enabled = enabled;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture);
            }
        }

        internal static bool CaptureLanes =>
            Environment.GetEnvironmentVariable("RHYTHM_DOJO_CAPTURE_LANES") == "1";

        internal static IEnumerator CaptureJourneyState(string name)
        {
            Directory.CreateDirectory("Logs/action-stages/frames");
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes("Logs/action-stages/frames/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void AdvanceJourney(RhythmGameController game, GameplayBackgroundDirector director,
            Clock clock, Input input, double end, bool success, HashSet<int> phases)
        {
            double time = clock.SongTime;
            while (time < end - 1e-8)
            {
                double next = Math.Min(time + 1.0 / 60, end);
                if (success)
                {
                    for (int i = 0; i < game.Chart.Count; i++)
                    {
                        var note = game.Chart[i];
                        if (note.StartTime > time + 1e-8 && note.StartTime <= next + 1e-8)
                        {
                            double hitTime = note.StartTime;
                            if (i == 0)
                            {
                                hitTime += .075; // A Good head still counts towards the genuine flawless finish.
                            }
                            input.Hit(note.Lane, hitTime);
                        }
                    }
                }
                clock.SongTime = next;
                game.SendMessage("Update");
                director.SendMessage("LateUpdate");
                phases.Add(director.ActivePhase);
                time = next;
            }
        }

        private static void Advance(RhythmGameController game, GameplayBackgroundDirector director,
            Clock clock, Input input, double end, bool success)
        {
            double time = clock.SongTime;
            while (time < end - 1e-8)
            {
                double next = Math.Min(time + 1.0 / 60, end);
                if (success)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        double scheduled = 1 + i * .75;
                        if (scheduled > time + 1e-8 && scheduled <= next + 1e-8)
                        {
                            input.Hit(i % 4, scheduled);
                        }
                    }
                }
                clock.SongTime = next;
                game.SendMessage("Update");
                director.SendMessage("LateUpdate");
                time = next;
            }
        }
    }
}
