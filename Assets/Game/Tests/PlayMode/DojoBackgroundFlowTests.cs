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
            var tempo = new TempoMap(new[] { new TempoPoint(0, 120) });
            var rules = new PlayRules { GaugeFailure = GaugeFailureMode.Continue };
            var camera = Camera.main;
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            var authoredDoor = room.leftDoor;
            int authoredCount = room.GetComponentsInChildren<Transform>(true).Length;

            game.Initialize(chart, rules.Timing, mode.ToRules(), tempo, "도장 수련실", "시각 검증",
                ScrollMode.Constant, 1, new ConstantScrollTimeline(10, 1), input, clock, notes, null, rules);
            hud.Initialize(game.ReadModel, mode, flow.Settings.presentation, mode.name);
            director.Initialize(game, 80, 0, true);
            input.Start();
            yield return null;
            Assert.That(room.Opening, Is.Zero);
            yield return Capture("start", room);

            var hangingRotation = room.hanging[0].localRotation;
            var dustPosition = room.dust[0].localPosition;
            Advance(game, director, clock, input, .5, false);
            input.Hit(3, .5); // Empty input must not open the painted doors.
            director.SendMessage("LateUpdate");
            Assert.That(director.Performance.Progress(0), Is.Zero);
            Assert.That(room.Opening, Is.Zero);
            Assert.That(room.hanging[0].localRotation, Is.Not.EqualTo(hangingRotation));
            Assert.That(room.dust[0].localPosition, Is.Not.EqualTo(dustPosition));

            Advance(game, director, clock, input, 7.4, true);
            Assert.That(director.Performance.Progress(0), Is.EqualTo(.45).Within(.001));
            Assert.That(room.Opening, Is.InRange(.45f, .6f));
            yield return Capture("middle", room);
            game.PauseSession();
            float age = room.VisualTime;
            float opening = room.Opening;
            Vector3 position = room.roomCamera.transform.localPosition;
            hangingRotation = room.hanging[0].localRotation;
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }
            Assert.That(room.VisualTime, Is.EqualTo(age));
            Assert.That(room.Opening, Is.EqualTo(opening));
            Assert.That(room.roomCamera.transform.localPosition, Is.EqualTo(position));
            Assert.That(room.hanging[0].localRotation, Is.EqualTo(hangingRotation));
            game.ResumeSession();
            yield return null;
            Assert.That(game.IsPaused, Is.True);
            Assert.That(room.VisualTime, Is.EqualTo(age));
            clock.ResumeReady = true;
            yield return null;
            Assert.That(game.IsPaused, Is.False);

            Advance(game, director, clock, input, 16.5, true);
            Assert.That(director.Performance.Progress(0), Is.EqualTo(1).Within(.001));
            Assert.That(room.Opening, Is.GreaterThan(.99f));
            Assert.That(room.Travel, Is.InRange(3.1f, 8f));
            yield return Capture("open", room);
            Advance(game, director, clock, input, 19.95, true);
            yield return Capture("before-transition", room);
            Advance(game, director, clock, input, 20.2, true);
            Assert.That(director.ActivePhase, Is.EqualTo(1));
            Assert.That(room.gameObject.activeSelf, Is.True);
            Assert.That(room.picture.alpha, Is.GreaterThan(.98f));
            yield return Capture("transition", room);
            Advance(game, director, clock, input, 26, true);
            Assert.That(director.mountain.gameObject.activeSelf, Is.True);
            Assert.That(director.fallback.activeSelf, Is.False);
            Assert.That(room.gameObject.activeSelf, Is.False);
            yield return Capture("fallback", null);

            game.RestartSession();
            yield return null;
            Assert.That(room.leftDoor, Is.SameAs(authoredDoor));
            Assert.That(room.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(authoredCount));
            Assert.That(room.Opening, Is.Zero);
            Advance(game, director, clock, input, 15.8, false);
            Assert.That(game.Session.Miss, Is.EqualTo(20));
            Assert.That(director.Performance.Progress(0), Is.Zero);
            Assert.That(room.Opening, Is.Zero);
            Assert.That(room.Travel, Is.Zero);
            yield return Capture("misses", room);
            Advance(game, director, clock, input, 19.95, false);
            yield return Capture("miss-before-transition", room);
            Advance(game, director, clock, input, 21, false);
            Assert.That(director.ActivePhase, Is.EqualTo(1));
            Assert.That(director.Performance.Progress(0), Is.Zero);
            Assert.That(director.fallback.activeSelf, Is.False);
            Assert.That(room.picture.alpha, Is.EqualTo(1));
            Assert.That(room.Travel, Is.InRange(0.01f, .65f));
            yield return Capture("miss-timed-departure", room);

            game.PauseSession();
            age = room.VisualTime;
            position = room.roomCamera.transform.localPosition;
            yield return null;
            Assert.That(room.VisualTime, Is.EqualTo(age));
            Assert.That(room.roomCamera.transform.localPosition, Is.EqualTo(position));
            game.ResumeSession();
            yield return Await(() => !game.IsPaused);
            Advance(game, director, clock, input, 26, false);
            Assert.That(room.Travel, Is.GreaterThan(3.1f));
            Assert.That(room.picture.alpha, Is.EqualTo(1));
            yield return Capture("miss-passage", room);
            Advance(game, director, clock, input, 34, false);
            Assert.That(room.ReadyForHandoff, Is.True);
            Assert.That(room.gameObject.activeSelf, Is.False);
            Assert.That(director.mountain.gameObject.activeSelf, Is.True);
            Assert.That(director.fallback.activeSelf, Is.False);
            Assert.That(director.Performance.Progress(0), Is.Zero);
            Advance(game, director, clock, input, 61, false);
            Assert.That(director.ActivePhase, Is.EqualTo(3));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(camera.transform.rotation, Is.EqualTo(cameraRotation));
            director.Initialize(game, 80, 0, false);
            Assert.That(director.fallback.activeSelf, Is.True);
            Assert.That(room.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ThresholdBurstAndLateDepartureKeepVelocityAcrossFrameRates()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return WaitForGameplayIntroduction();
            var director = UnityEngine.Object.FindFirstObjectByType<GameplayBackgroundDirector>();
            director.Shutdown();
            var room = director.room;
            float[] reference = new float[16];
            int[] rates = { 120, 60, 30 };
            foreach (int rate in rates)
            {
                room.ResetPresentation();
                float previousTravel = 0;
                float previousSpeed = 0;
                for (int frame = 0; frame < rate * 16; frame++)
                {
                    float progress = .84f;
                    if (frame >= rate * 2)
                    {
                        progress = .85f;
                        room.BeginTimedExit(); // The music boundary coincides with the earned threshold.
                    }
                    room.Step(1f / rate, progress, 0, false);
                    float speed = (room.Travel - previousTravel) * rate;
                    Assert.That(speed, Is.InRange(-.001f, 1.81f));
                    Assert.That(Mathf.Abs(speed - previousSpeed) * rate, Is.LessThan(4f));
                    if (frame == rate * 2)
                    {
                        Assert.That(speed, Is.GreaterThan(previousSpeed * .95f),
                            "Crossing the performance threshold must not restart camera velocity at zero.");
                    }
                    if (room.Travel > 3.4f)
                    {
                        Assert.That(room.Opening, Is.GreaterThan(.94f));
                    }
                    if ((frame + 1) % rate == 0)
                    {
                        int second = (frame + 1) / rate - 1;
                        if (rate == 120)
                        {
                            reference[second] = room.Travel;
                        }
                        else
                        {
                            Assert.That(room.Travel, Is.EqualTo(reference[second]).Within(.035f));
                        }
                    }
                    previousTravel = room.Travel;
                    previousSpeed = speed;
                }
                Assert.That(room.ReadyForHandoff, Is.True);
            }

            room.ResetPresentation();
            room.Step(5, 1, 0, false);
            Assert.That(room.Travel, Is.LessThan(.02f), "A stalled frame must not jump through the door.");
            room.ResetPresentation();
            Assert.That(room.ReadyForHandoff, Is.False);
            Assert.That(room.Travel, Is.Zero);
            Assert.That(room.Opening, Is.Zero);
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
            var camera = Camera.main;
            var cameraPosition = camera.transform.position;
            var cameraRotation = camera.transform.rotation;
            float fieldOfView = camera.fieldOfView;
            var piece = director.dummy.pieces[0].item;
            int count = director.dummy.GetComponentsInChildren<Transform>(true).Length;

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
                    AdvanceJourney(game, director, clock, input, phase * 4 + 1.5, success, phases);
                    Assert.That(director.ActivePhase, Is.EqualTo(phase));
                    float age = director.room.VisualTime;
                    if (phase > 0)
                    {
                        age = JourneyView(director, phase).VisualTime;
                    }
                    game.PauseSession();
                    yield return null;
                    yield return null;
                    float pausedAge = director.room.VisualTime;
                    if (phase > 0)
                    {
                        pausedAge = JourneyView(director, phase).VisualTime;
                    }
                    Assert.That(pausedAge, Is.EqualTo(age));
                    game.ResumeSession();
                    clock.ResumeReady = true;
                    yield return Await(() => !game.IsPaused);
                    float slowed = 0;
                    if (phase == 2 && scenario == 2)
                    {
                        AdvanceJourney(game, director, clock, input, 10.1, true, phases);
                        float speed = director.run.Speed;
                        input.Hit(0, clock.SongTime);
                        AdvanceJourney(game, director, clock, input, 10.3, true, phases);
                        slowed = director.run.Speed;
                        Assert.That(slowed, Is.LessThan(speed));
                    }
                    AdvanceJourney(game, director, clock, input, phase * 4 + 3.03, success, phases);
                    if (phase == 3 && success)
                    {
                        Assert.That(director.dummy.axis.localRotation, Is.Not.EqualTo(Quaternion.identity));
                        Assert.That(director.dummy.hands[1].localPosition.y,
                            Is.GreaterThan(director.dummy.handRest[1].y + .8f));
                    }
                    if (phase > 0 && scenario < 2)
                    {
                        yield return CaptureJourneyState($"scenario-{scenario}-phase-{phase + 1}-reaction");
                    }
                    AdvanceJourney(game, director, clock, input, phase * 4 + 3.97, success, phases);
                    if (phase == 1 && scenario == 0)
                    {
                        yield return CaptureJourneyState("mountain-summit");
                    }
                    if (phase > 0)
                    {
                        var view = JourneyView(director, phase);
                        Assert.That(view.picture.gameObject.activeInHierarchy, Is.True);
                        Assert.That(view.picture.alpha, Is.GreaterThan(.99f));
                        Assert.That(view.VisualTime, Is.GreaterThan(2));
                    }
                    Assert.That(director.Performance.Progress(phase), Is.EqualTo(Convert.ToInt32(success)).Within(.001));
                    if (phase == 1)
                    {
                        Assert.That(director.mountain.SummitReached, Is.EqualTo(success));
                        if (success)
                        {
                            Assert.That(director.mountain.Altitude, Is.GreaterThan(14));
                        }
                        else
                        {
                            Assert.That(director.mountain.Altitude, Is.EqualTo(1.8f));
                        }
                    }
                    if (phase == 2)
                    {
                        Assert.That(director.run.PeakReached, Is.EqualTo(success));
                        if (scenario == 2)
                        {
                            Assert.That(director.run.Speed, Is.GreaterThan(slowed));
                        }
                    }
                }
                Assert.That(phases, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
                AdvanceJourney(game, director, clock, input, 16.1, success, phases);
                Assert.That(game.Session.State, Is.EqualTo(SessionState.Completed));
                Assert.That(director.dummy.Finishing, Is.True);
                Assert.That(director.dummy.Destroyed, Is.EqualTo(scenario == 0));
                if (scenario == 0)
                {
                    Assert.That(game.Session.Snapshot.IsFullCombo, Is.True);
                    yield return Await(() => director.dummy.FinishComplete);
                    Assert.That(director.dummy.pieces[0].item.localPosition.y, Is.LessThan(.3f));
                    yield return CaptureJourneyState("flawless-collapse");
                }
                else
                {
                    yield return Await(() => director.dummy.FinishAge > .75f);
                    Assert.That(director.dummy.pieces[0].item.localPosition,
                        Is.EqualTo(director.dummy.pieces[0].position));
                    yield return CaptureJourneyState($"scenario-{scenario}-standing-finish");
                }
                Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
                Assert.That(camera.transform.rotation, Is.EqualTo(cameraRotation));
                Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
                Assert.That(game.ReadModel.Snapshot.ScrollMultiplier, Is.EqualTo(1));
            }

            Assert.That(director.dummy.pieces[0].item, Is.SameAs(piece));
            Assert.That(director.dummy.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            game.RestartSession();
            director.SendMessage("LateUpdate");
            Assert.That(director.dummy.Destroyed, Is.False);
            Assert.That(piece.localPosition, Is.EqualTo(director.dummy.pieces[0].position));
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
            game.Initialize(new ChartData(chartNotes.ToArray(), 7), rules.Timing, mode.ToRules(),
                new TempoMap(new[] { new TempoPoint(0, 120) }), "타격 수련", "고밀도",
                ScrollMode.Constant, 1, new ConstantScrollTimeline(15, 1), input, clock, notes, null, rules);
            director.Initialize(game, 7, 0, true);
            hud.GetComponent<RhythmHud>().Initialize(game.ReadModel, mode, flow.Settings.presentation);
            hits.Initialize(game.ReadModel, mode, hud.gameplayCamera, hud.GetComponent<Canvas>());
            input.Start();
            int next = 0;
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

                if (frame == 128)
                {
                    Assert.That(game.Session.Snapshot.Combo, Is.GreaterThan(50));
                    Assert.That(hud.combo.text, Is.EqualTo(game.Session.Snapshot.Combo.ToString()));
                    Assert.That(hits.GetComponentsInChildren<GameplayImpactGraphic>()
                        .Count(graphic => graphic.name.StartsWith("Hold Glow")), Is.EqualTo(1));
                    yield return CaptureJourneyState("dense-chord-and-hold");
                }
            }

            Assert.That(game.Session.Snapshot.Miss, Is.EqualTo(2));
            Assert.That(hud.combo.text, Is.Empty);
            Assert.That(hud.judgment.text, Is.EqualTo("빗나감"));
            yield return CaptureJourneyState("dense-combo-break");
        }

        private static TrainingJourneyView JourneyView(GameplayBackgroundDirector director, int phase)
        {
            switch (phase)
            {
                case 1: return director.mountain;
                case 2: return director.run;
                default: return director.dummy;
            }
        }

        private static IEnumerator CaptureJourneyState(string name)
        {
            Directory.CreateDirectory("Logs/training-journey/gameplay");
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes("Logs/training-journey/gameplay/" + name + ".png", texture.EncodeToPNG());
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
                            input.Hit(note.Lane, note.StartTime);
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
            float previousSpeed = 0;
            bool sampled = false;
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
                float previousTravel = director.room.Travel;
                float previousAlpha = director.room.picture.alpha;
                clock.SongTime = next;
                game.SendMessage("Update");
                director.SendMessage("LateUpdate");
                float delta = (float)(next - time);
                float speed = (director.room.Travel - previousTravel) / delta;
                Assert.That(speed, Is.InRange(-.001f, 1.81f));
                Assert.That(Mathf.Abs(director.room.picture.alpha - previousAlpha), Is.LessThan(.025f));
                if (sampled)
                {
                    Assert.That(Mathf.Abs(speed - previousSpeed) / delta, Is.LessThan(4f));
                }
                previousSpeed = speed;
                sampled = true;
                time = next;
            }
        }

        private static IEnumerator Capture(string name, DojoRoomPhaseView room)
        {
            Directory.CreateDirectory("Logs/dojo-room");
            yield return new WaitForEndOfFrame();
            var screen = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes("Logs/dojo-room/" + name + "-game.png", screen.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.Destroy(screen);
            }
            if (!room)
            {
                yield break;
            }
            var previous = room.roomCamera.targetTexture;
            var active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                room.roomCamera.targetTexture = target;
                room.roomCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/dojo-room/" + name + "-room.png", texture.EncodeToPNG());
            }
            finally
            {
                room.roomCamera.targetTexture = previous;
                RenderTexture.active = active;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
