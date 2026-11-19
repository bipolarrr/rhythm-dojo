using System;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class TrainingPlaybackView : MonoBehaviour
    {
        public DojoActorAnimation actor;
        public Text heading;
        public Text instruction;
        public Text pulse;
        public Text[] keys;
        private RhythmGameController controller;
        private IGameplayReadModel model;
        private bool demonstration;
        private double lastHit = double.NegativeInfinity;
        private int lastLane = -1;
        private readonly double[] laneHits = new double[4];
        private TrainingLesson lesson;

        public void Validate()
        {
            if (!actor || !heading || !instruction)
            {
                throw new InvalidOperationException("Training playback presentation missing. Rebuild Gameplay.");
            }
            if (!pulse || keys == null || keys.Length != 4)
            {
                throw new InvalidOperationException("Training lane presentation missing.");
            }
            foreach (var key in keys)
            {
                if (!key)
                {
                    throw new InvalidOperationException("Training lane label missing.");
                }
            }
            actor.Validate();
        }

        public void Initialize(RhythmGameController controller, TrainingSession session,
            GameModeDefinition mode, string modeId, TrainingLesson lesson = null)
        {
            Validate();
            Unbind();
            gameObject.SetActive(session != null);
            if (session == null)
            {
                return;
            }
            this.controller = controller;
            this.lesson = lesson;
            model = controller.ReadModel;
            demonstration = session.Step == TrainingStep.Demonstration;
            heading.text = TrainingLesson.StepTitle(session.Step);
            if (lesson)
            {
                heading.text = lesson.Title + "\n" + heading.text;
            }
            instruction.text = "직접 입력하세요\n쉼표에서는 손도 쉬기";
            if (demonstration)
            {
                instruction.text = "사형의 손과 노트를 보세요\n시범 중에는 입력을 받지 않습니다";
            }
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].text = (i + 1) + "\n" + PlayKeyBindings.GetLabel(mode, modeId, i);
            }
            model.Judged += OnJudged;
            model.ResetOccurred += Reset;
            Reset();
        }

        private void Reset()
        {
            lastHit = double.NegativeInfinity;
            lastLane = -1;
            Array.Fill(laneHits, double.NegativeInfinity);
        }

        private void OnJudged(JudgmentEvent result)
        {
            if (result.Grade == Judgment.Miss)
            {
                return;
            }
            var note = controller.Chart[result.NoteIndex];
            lastHit = note.StartTime + result.ErrorSeconds;
            if (controller.Session.GetNoteJudgments(result.NoteIndex).End.HasValue)
            {
                lastHit = note.EndTime + result.ErrorSeconds;
            }
            lastLane = result.Lane;
            laneHits[lastLane] = lastHit;
        }

        private void LateUpdate()
        {
            if (model == null)
            {
                return;
            }
            double time = model.Snapshot.SongTime;
            double age = Math.Max(0, time - lastHit);
            var motion = DojoIdleVariation.Motion.None;
            float seconds = (float)Math.Max(0, time);
            if (demonstration && age < .3)
            {
                motion = DojoIdleVariation.Motion.Punch;
                // Contact frame is shown on the same judgment event as the lane effect.
                seconds = .425f + (float)age;
            }
            actor.Sample(motion, seconds);
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].color = new Color(1, .95f, .82f);
                if (model.IsHeld(i) || time - laneHits[i] < .16)
                {
                    keys[i].color = new Color(.55f, 1, .65f);
                }
            }
            pulse.text = "";
            if (time >= 0 && time < 2.4)
            {
                pulse.text = "준비  " + (Math.Min(3, (int)(time / .6)) + 1) + " / 4";
            }
            else if (model.Snapshot.Session.State == SessionState.Playing)
            {
                pulse.text = "BPM 100 · 1-3 / 2-4";
                if (lesson)
                {
                    pulse.text = lesson.Title + " · BPM 100";
                }
            }
        }

        public void Unbind()
        {
            if (model != null)
            {
                model.Judged -= OnJudged;
                model.ResetOccurred -= Reset;
            }
            model = null;
            controller = null;
        }

        private void OnDisable() => Unbind();
    }
}
