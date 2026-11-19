using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Presentation
{
    public sealed class GameplayBackgroundDirector : MonoBehaviour
    {
        public DojoRoomPhaseView room;

        private RhythmGameController controller;
        private IGameplayReadModel model;
        private IBackgroundJudgmentReadModel observations;
        private BackgroundPhasePlan plan;
        private BackgroundPerformanceState performance;
        private readonly Queue<JudgmentResolvedEvent> pending = new Queue<JudgmentResolvedEvent>();
        private readonly Queue<InputResolutionEvent> inputs = new Queue<InputResolutionEvent>();
        private float readyAge;
        private bool scoredFinish;

        public int ActivePhase { get; private set; }
        public BackgroundPerformanceState Performance => performance;

        public void Validate()
        {
            if (!room)
            {
                throw new InvalidOperationException("The gameplay action stages are missing.");
            }
            room.Validate();
        }

        public void Initialize(RhythmGameController controller, double length, double offset, bool eligible)
        {
            Shutdown();
            Validate();
            this.controller = controller;
            scoredFinish = eligible;
            model = controller.ReadModel;
            observations = model as IBackgroundJudgmentReadModel;
            if (observations == null)
            {
                throw new InvalidOperationException("Gameplay background judgment observations missing.");
            }
            try
            {
                plan = new BackgroundPhasePlan(controller.Chart, observations.Schedule, length, offset);
                performance = new BackgroundPerformanceState(plan, observations.SessionId);
                observations.JudgmentResolved += Observe;
                observations.InputResolved += ObserveInput;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Using the courtyard drill: " + exception.Message, this);
            }
            model.ResetOccurred += ResetPresentation;
            ResetPresentation();
        }

        private void Observe(JudgmentResolvedEvent result) => pending.Enqueue(result);

        private void ObserveInput(InputResolutionEvent result) => inputs.Enqueue(result);

        private void ResetPresentation()
        {
            pending.Clear();
            inputs.Clear();
            performance?.Reset(observations.SessionId);
            ActivePhase = 0;
            readyAge = 0;
            room.ResetPresentation();
        }

        private void LateUpdate()
        {
            if (model == null || controller.IsPaused)
            {
                return;
            }
            var snapshot = model.Snapshot;
            if (performance != null && performance.SessionId != observations.SessionId)
            {
                // Start can change identity after ResetOccurred. Keep this session's queued judgments.
                performance.Reset(observations.SessionId);
                room.ResetPresentation();
                readyAge = 0;
            }
            if (snapshot.Session.State == SessionState.Ready)
            {
                readyAge += Time.unscaledDeltaTime;
                room.Step(0, 0, readyAge, readyAge * 1.6f, 1, 0);
                return;
            }

            double time = snapshot.SongTime;
            float beat = (float)controller.Tempo.BeatAtTime(time);
            if (plan == null)
            {
                room.Step(0, time, Mathf.Max(0, (float)time), beat, 1, 0);
                return;
            }
            ActivePhase = plan.PhaseAt(time);
            Resolve(time);
            float age = Mathf.Max(0, (float)(time - plan.AudioOffset - ActivePhase * plan.PhaseDuration));
            float overlap = Mathf.Clamp01(age / (float)plan.TransitionDuration);
            if (snapshot.Session.State == SessionState.Completed)
            {
                ActivePhase = 3;
                room.BeginFinish(scoredFinish && performance.CanFinishFlawlessly(snapshot.Session));
                age += room.FinishAge;
                overlap = 1;
            }
            room.Step(ActivePhase, time, age, beat, overlap, (float)plan.PhaseDuration);
        }

        private void Resolve(double time)
        {
            float headStrength = 0;
            bool missed = false;
            while (pending.Count > 0)
            {
                var result = pending.Dequeue();
                if (!performance.Consume(result))
                {
                    continue;
                }
                // Score every scheduled unit. Only the head can trigger a physical action.
                if (result.Unit.Part != 0 || plan.PhaseOf(result.Unit.Id) != ActivePhase)
                {
                    continue;
                }
                if (result.Grade == Judgment.Miss)
                {
                    missed = true;
                }
                else if (result.Grade == Judgment.Perfect)
                {
                    headStrength = 1;
                }
                else if (result.Grade == Judgment.Good)
                {
                    headStrength = Mathf.Max(headStrength, .55f);
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
                if (result.Resolution == InputResolution.HoldInterrupted || result.Resolution == InputResolution.EmptyPress)
                {
                    missed = true;
                }
            }
            if (headStrength > 0)
            {
                room.Hit(ActivePhase, time, headStrength);
            }
            if (missed)
            {
                room.Miss(time);
            }
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
