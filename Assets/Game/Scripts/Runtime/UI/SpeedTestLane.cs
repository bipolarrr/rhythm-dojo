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
        {
            rail = Image(
                "Speed Test Lane",
                parent,
                new Vector2(18, -56),
                new Vector2(LaneWidth, LaneHeight),
                new Color(.12f, .085f, .07f)
            );
            var lane = new GameObject("Perspective Rail", typeof(RectTransform), typeof(SpeedTestLaneGraphic));
            lane.transform.SetParent(rail, false);
            UiElements.Position((RectTransform)lane.transform, Vector2.zero, rail.sizeDelta);

            var surface = lane.GetComponent<SpeedTestLaneGraphic>();
            surface.color = new Color(.145f, .16f, .18f);
            surface.raycastTarget = false;

            var sprite = Resources.Load<Sprite>("UI/SongpanNote");
            for (int i = 0; i < notes.Length; i++)
            {
                var note = Image("Beat Note " + i, rail, Vector2.zero, new Vector2(136, 17), Color.white);
                note.GetComponent<Image>().sprite = sprite;
                notes[i] = note;
            }

            float hitWidth = WidthAt(HitLine);
            Image(
                "Speed Test Hit Line",
                rail,
                new Vector2((LaneWidth - hitWidth) * .5f, -HitLine + 2),
                new Vector2(hitWidth, 4),
                new Color(.26f, .92f, .91f)
            );

            var display = new GameObject("Speed Test Feedback", typeof(RectTransform), typeof(CanvasGroup));
            display.transform.SetParent(rail, false);
            UiElements.Position(
                (RectTransform)display.transform,
                new Vector2(0, -HitLine + 102),
                new Vector2(LaneWidth, 80)
            );

            feedback = display.GetComponent<CanvasGroup>();
            feedback.interactable = false;
            feedback.blocksRaycasts = false;
            judgment = CreateFeedbackLabel("Judgment Grade", display.transform, Vector2.zero, 26, 42);
            timingLabel = CreateFeedbackLabel("Judgment Timing", display.transform, new Vector2(0, -37), 16, 28);
            timingLabel.color = new Color(.98f, .92f, .77f);

            Reset();
            rail.gameObject.SetActive(false);
        }

        private static Text CreateFeedbackLabel(
            string name,
            Transform parent,
            Vector2 position,
            int size,
            float height
        )
        {
            var label = UiElements.Label(name, parent, "", position, new Vector2(LaneWidth, height), size);
            label.alignment = TextAnchor.MiddleCenter;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.08f, .05f, .035f, .9f);
            outline.effectDistance = new Vector2(1, -1);

            return label;
        }

        private static float WidthAt(float depth)
        {
            return Mathf.Lerp(SpeedTestLaneGraphic.FarWidth, SpeedTestLaneGraphic.NearWidth, depth / LaneHeight);
        }

        private static RectTransform Image(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            UiElements.Position(rect, position, size);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
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
