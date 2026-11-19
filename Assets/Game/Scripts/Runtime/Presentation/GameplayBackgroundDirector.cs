using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Presentation
{
    public sealed class GameplayBackgroundDirector : MonoBehaviour
    {
        public DojoRoomPhaseView room;
        public GameObject fallback;
        public GameplayAtmosphere atmosphere;
        public Image transition;
        public GameplayWindowLight[] fallbackLights;
        public MountainPhaseView mountain;
        public RunPhaseView run;
        public DummyPhaseView dummy;
        public JourneyPassageGraphic passage;

        private RhythmGameController controller;
        private IGameplayReadModel model;
        private IBackgroundJudgmentReadModel observations;
        private BackgroundPhasePlan plan;
        private BackgroundPerformanceState performance;
        private readonly Queue<JudgmentResolvedEvent> pending = new Queue<JudgmentResolvedEvent>();
        private readonly Queue<InputResolutionEvent> inputs = new Queue<InputResolutionEvent>();
        private double previousTime;
        private bool sampled;
        private float handoffAge;

        private const float HandoffDuration = 1.6f;

        public int ActivePhase { get; private set; }
        public BackgroundPerformanceState Performance => performance;

        public void Validate()
        {
            if (!room || !fallback)
            {
                throw new InvalidOperationException("Gameplay background room or fallback missing.");
            }
            if (!atmosphere || !transition)
            {
                throw new InvalidOperationException("Gameplay background authored references missing.");
            }
            room.Validate();
            if (!mountain || !run || !dummy)
            {
                throw new InvalidOperationException("Training journey phases are missing. Rebuild Gameplay.");
            }
            if (!passage)
            {
                throw new InvalidOperationException("Training journey wind passage is missing.");
            }
            mountain.Validate();
            run.Validate();
            dummy.Validate();
            if (fallbackLights == null || fallbackLights.Length == 0)
            {
                throw new InvalidOperationException("Legacy background light references missing.");
            }
            foreach (var light in fallbackLights)
            {
                if (!light)
                {
                    throw new InvalidOperationException("Legacy background light reference missing.");
                }
            }
        }

        public void Initialize(RhythmGameController controller, double length, double offset, bool eligible)
        {
            Shutdown();
            Validate();
            if (!eligible)
            {
                ShowLegacy();
                return;
            }
            this.controller = controller;
            model = controller.ReadModel;
            observations = model as IBackgroundJudgmentReadModel;
            if (observations == null)
            {
                throw new InvalidOperationException("Gameplay background judgment observations missing.");
            }
            try
            {
                plan = new BackgroundPhasePlan(controller.Chart, observations.Schedule, length, offset);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Dojo background kept legacy scenery: " + exception.Message, this);
                ShowLegacy();
                model = null;
                observations = null;
                return;
            }
            performance = new BackgroundPerformanceState(plan, observations.SessionId);
            mountain.ConfigureDuration(plan.PhaseDuration);
            observations.JudgmentResolved += Observe;
            observations.InputResolved += ObserveInput;
            model.ResetOccurred += ResetPresentation;
            ResetPresentation();
        }

        private void Observe(JudgmentResolvedEvent result) => pending.Enqueue(result);

        private void ObserveInput(InputResolutionEvent result) => inputs.Enqueue(result);

        private void ResetPresentation()
        {
            pending.Clear();
            inputs.Clear();
            performance.Reset(observations.SessionId);
            ActivePhase = 0;
            sampled = false;
            handoffAge = 0;
            room.gameObject.SetActive(true);
            room.picture.gameObject.SetActive(true);
            room.picture.alpha = 1;
            fallback.SetActive(false);
            atmosphere.enabled = false;
            foreach (var light in fallbackLights)
            {
                light.AnimationControlled = true;
            }
            room.ResetPresentation();
            ResetJourney();
            transition.color = Color.clear;
        }

        private void ResetJourney()
        {
            mountain.ResetPresentation();
            run.ResetPresentation();
            dummy.ResetPresentation();
            mountain.SetVisible(false);
            run.SetVisible(false);
            dummy.SetVisible(false);
            passage.SetPassage(0);
        }

        private void LateUpdate()
        {
            if (model == null || plan == null)
            {
                return;
            }
            // Coordinator intentionally resets before notifying existing observers,
            // then Start resets the simulation again. Read the final session identity
            // here without discarding judgments already queued by that new session.
            if (performance.SessionId != observations.SessionId)
            {
                performance.Reset(observations.SessionId);
                room.ResetPresentation();
                ResetJourney();
                sampled = false;
                handoffAge = 0;
            }
            double time = model.Snapshot.SongTime;
            float delta = 0;
            if (sampled && !controller.IsPaused)
            {
                delta = (float)Math.Max(0, time - previousTime);
            }
            previousTime = time;
            sampled = true;
            if (controller.IsPaused)
            {
                return;
            }

            ActivePhase = plan.PhaseAt(time);

            float reaction = 0;
            bool missed = false;
            while (pending.Count > 0)
            {
                var result = pending.Dequeue();
                if (!performance.Consume(result))
                {
                    continue;
                }
                if (plan.PhaseOf(result.Unit.Id) != ActivePhase)
                {
                    continue;
                }
                if (result.Grade == Judgment.Miss)
                {
                    missed = true;
                    if (ActivePhase == 3)
                    {
                        dummy.React(controller.Chart[result.Unit.Note].Lane, result.Unit.Part, 0, true);
                    }
                }
                else
                {
                    float strength = .55f;
                    if (result.Grade == Judgment.Perfect)
                    {
                        strength = 1;
                    }
                    reaction = Mathf.Min(1, reaction + strength * (float)result.Unit.Weight);
                    if (ActivePhase == 3)
                    {
                        dummy.React(controller.Chart[result.Unit.Note].Lane, result.Unit.Part,
                            strength * (float)result.Unit.Weight, false);
                    }
                }
            }
            while (inputs.Count > 0)
            {
                var result = inputs.Dequeue();
                if (result.SessionId != performance.SessionId)
                {
                    continue;
                }
                performance.Consume(result);
                if (result.Resolution == InputResolution.HoldInterrupted ||
                    result.Resolution == InputResolution.EmptyPress)
                {
                    missed = true;
                    if (ActivePhase == 3)
                    {
                        dummy.React(result.Lane, 0, 0, true);
                    }
                }
            }

            var state = model.Snapshot.Session.State;
            if (state == SessionState.GameOver)
            {
                if (ActivePhase == 3)
                {
                    dummy.BeginFinish(false);
                    dummy.Step(Time.unscaledDeltaTime, (float)performance.Progress(3));
                }
                return;
            }
            if (state == SessionState.Ready)
            {
                room.Step(Time.unscaledDeltaTime * .35f, 0, 0, false);
                return;
            }
            if (state == SessionState.Completed)
            {
                ActivePhase = 3;
                room.gameObject.SetActive(false);
                room.picture.gameObject.SetActive(false);
                mountain.SetVisible(false);
                run.SetVisible(false);
                dummy.SetVisible(true);
                fallback.SetActive(false);
                this.passage.SetPassage(0);
                // Consume the entire final judgment batch before deciding. The root
                // retains ownership of the existing one/two-second results transition.
                dummy.BeginFinish(performance.CanFinishFlawlessly(model.Snapshot.Session));
                dummy.Step(Time.unscaledDeltaTime, (float)performance.Progress(3));
                return;
            }
            if (ActivePhase > 0)
            {
                room.BeginTimedExit();
            }
            bool roomVisible = handoffAge < HandoffDuration;
            if (roomVisible && state == SessionState.Playing)
            {
                room.Step(delta, (float)performance.Progress(0), reaction, missed);
                if (ActivePhase > 0 && room.ReadyForHandoff)
                {
                    handoffAge = Mathf.Min(HandoffDuration, handoffAge + Mathf.Min(delta, .1f));
                }
                else if (ActivePhase > 0 && plan.PhaseDuration < 16)
                {
                    // A short song cannot spend several phases waiting for the full
                    // architectural passage. Carry its view into mist within Phase 2;
                    // the existing door travel and earned progress remain untouched.
                    float duration = (float)Math.Min(HandoffDuration, plan.PhaseDuration * .3);
                    handoffAge = Mathf.Min(HandoffDuration,
                        handoffAge + delta * HandoffDuration / Mathf.Max(.001f, duration));
                }
                else if (ActivePhase >= 2)
                {
                    handoffAge = HandoffDuration;
                }
            }
            roomVisible = handoffAge < HandoffDuration;
            room.gameObject.SetActive(roomVisible);
            room.picture.gameObject.SetActive(roomVisible);
            fallback.SetActive(false);

            float passage = Mathf.Clamp01(handoffAge / HandoffDuration);
            room.picture.alpha = 1 - passage * passage * passage * (passage * (passage * 6 - 15) + 10);
            StepJourney(time, delta, reaction, missed);
            if (handoffAge > 0 && roomVisible)
            {
                this.passage.SetPassage(passage);
            }
            transition.color = Color.clear;
        }

        private void StepJourney(double time, float delta, float reaction, bool missed)
        {
            double age = time - plan.AudioOffset - ActivePhase * plan.PhaseDuration;
            float t = Mathf.Clamp01((float)(age / plan.TransitionDuration));
            float blend = Mathf.SmoothStep(0, 1, t);
            bool mountainVisible = ActivePhase == 1 || ActivePhase == 2 && t < 1;
            bool runVisible = ActivePhase == 2 || ActivePhase == 3 && t < 1;
            mountain.SetVisible(mountainVisible);
            run.SetVisible(runVisible);
            dummy.SetVisible(ActivePhase == 3);
            passage.SetPassage(0);

            if (ActivePhase == 1)
            {
                mountain.Step(delta, (float)performance.Progress(1), reaction, missed);
            }
            else if (ActivePhase == 2)
            {
                mountain.Step(delta, (float)performance.Progress(1), 0, false);
                run.Step(delta, (float)performance.Progress(2), reaction, missed);
                run.picture.alpha = blend;
                passage.SetPassage(t);
            }
            else if (ActivePhase == 3)
            {
                if (runVisible)
                {
                    run.Step(delta, (float)performance.Progress(2), 0, false);
                }
                dummy.Step(delta, (float)performance.Progress(3));
                dummy.picture.alpha = blend;
                passage.SetPassage(t);
            }
        }

        private void ShowLegacy()
        {
            room.gameObject.SetActive(false);
            room.picture.gameObject.SetActive(false);
            mountain.SetVisible(false);
            run.SetVisible(false);
            dummy.SetVisible(false);
            passage.SetPassage(0);
            fallback.SetActive(true);
            atmosphere.enabled = true;
            foreach (var light in fallbackLights)
            {
                light.AnimationControlled = false;
            }
            transition.color = Color.clear;
        }

        public void Shutdown()
        {
            if (observations != null)
            {
                observations.JudgmentResolved -= Observe;
                observations.InputResolved -= ObserveInput;
            }
            if (model != null)
            {
                model.ResetOccurred -= ResetPresentation;
            }
            pending.Clear();
            inputs.Clear();
            model = null;
            observations = null;
            controller = null;
            plan = null;
            performance = null;
        }

        private void OnDisable() => Shutdown();
    }
}
