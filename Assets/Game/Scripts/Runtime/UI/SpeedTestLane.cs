using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    internal sealed class SpeedTestLane
    {
        private const float LaneWidth = 300;
        private const float LaneHeight = 452;
        private const float HitLine = 393;
        private const float Perspective = .8f;
        private const double VisibleDistance = 20;

        private readonly RectTransform rail;
        private readonly RectTransform[] notes = new RectTransform[32];
        private readonly CanvasGroup feedback;
        private readonly Text judgment;
        private readonly Text timingLabel;

        private int nextBeat;
        private int lastHitBeat;
        private double feedbackTime;

        public GameObject GameObject => rail.gameObject;
        public Transform Transform => rail;

        public SpeedTestLane(Transform parent)
            : this(parent.GetComponent<SpeedTestLaneView>())
        {
        }

        public SpeedTestLane(SpeedTestLaneView view)
        {
            view.Validate();
            rail = view.rail;
            notes = view.notes;
            feedback = view.feedback;
            judgment = view.judgment;
            timingLabel = view.timingLabel;
            Reset();
            rail.gameObject.SetActive(false);
        }

        private static float WidthAt(float depth)
        {
            return Mathf.Lerp(SpeedTestLaneGraphic.FarWidth, SpeedTestLaneGraphic.NearWidth, depth / LaneHeight);
        }

        public void Reset()
        {
            nextBeat = 0;
            lastHitBeat = -1;
            feedback.alpha = 0;

            foreach (var note in notes)
            {
                note.gameObject.SetActive(false);
            }
        }

        public void ShowFeedback(Judgment grade, string timing, double time)
        {
            judgment.text = JudgmentDisplay.Label(grade);
            judgment.color = grade switch
            {
                Judgment.Perfect => new Color(.46f, .84f, .81f),
                Judgment.Good => new Color(1f, .77f, .42f),
                _ => new Color(.96f, .29f, .31f),
            };
            timingLabel.text = timing;
            feedbackTime = time;
            feedback.alpha = .65f;
        }

        public bool Advance(double time, double interval, JudgmentSettings timing)
        {
            int firstPending = Math.Max(
                nextBeat,
                (int)Math.Ceiling((time - timing.GoodWindow - RhythmSession.TimingTolerance) / interval)
            );
            bool missed = firstPending > nextBeat;

            nextBeat = firstPending;

            return missed;
        }

        public bool TryHit(
            double time,
            double interval,
            JudgmentSettings timing,
            out Judgment grade,
            out double error
        )
        {
            int beat = (int)Math.Round(time / interval);
            grade = Judgment.Miss;
            error = time - beat * interval;

            if (beat < 0 || beat == lastHitBeat || beat < nextBeat - 1)
            {
                return false;
            }

            if (beat < nextBeat)
            {
                return true;
            }

            grade = JudgmentEvaluator.Grade(error, timing);
            if (grade != Judgment.Miss)
            {
                lastHitBeat = beat;
                nextBeat = beat + 1;
            }

            return true;
        }

        public void Render(double time, double interval, IScrollTimeline scroll)
        {
            if (feedback.alpha > 0)
            {
                feedback.alpha = .65f * (1 - Mathf.Clamp01((float)(time - feedbackTime - .32) / .30f));
            }

            for (int i = 0; i < notes.Length; i++)
            {
                var note = notes[i];
                double distance = scroll.DistanceBetween(time, (nextBeat + i) * interval);
                float depth = (float)(distance / VisibleDistance);
                float perspective = 1 + Perspective * Mathf.Max(0, depth);
                float y = -HitLine + depth * (1 + Perspective) / perspective * (HitLine - 10);
                bool visible = y <= -9 && y >= -LaneHeight + 12;
                note.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float width = WidthAt(-y) * .88f;
                float height = Mathf.Lerp(8, 18, -y / LaneHeight);
                note.sizeDelta = new Vector2(width, height);
                note.anchoredPosition = new Vector2((LaneWidth - note.sizeDelta.x) / 2, y + note.sizeDelta.y / 2);
            }
        }
    }
}
