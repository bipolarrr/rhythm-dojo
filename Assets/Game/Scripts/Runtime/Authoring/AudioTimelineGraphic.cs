using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class AudioTimelineGraphic : MaskableGraphic,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        public ChartAudioTimeline Owner { get; set; }
        public bool Overview { get; set; }
        private double dragTime;
        private double dragStart;
        private bool dragged;
        private Text[] labels;
        private double[] labelBeats;
        private float lastWidth;
        private RhythmDojo.Gameplay.BeatGridTiming labelTiming;

        public void InitializeLabels()
        {
            if (Overview)
            {
                return;
            }

            labels = new Text[24];
            labelBeats = new double[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = AudioTimelineUi.Label(transform, "Beat Label " + i, "", 0, 0, 86, 19);
                labels[i].fontSize = 12;
                labels[i].gameObject.SetActive(false);
                labelBeats[i] = double.NaN;
            }
        }

        private void LateUpdate()
        {
            if (rectTransform.rect.width != lastWidth)
            {
                lastWidth = rectTransform.rect.width;
                RefreshLabels();
            }
        }

        public void RefreshLabels()
        {
            if (labels == null || Owner.Timing == null)
            {
                return;
            }

            if (labelTiming != Owner.Timing)
            {
                labelTiming = Owner.Timing;
                for (int i = 0; i < labelBeats.Length; i++)
                {
                    labelBeats[i] = double.NaN;
                }
            }

            double first = Owner.Timing.BeatAtTime(StartTime + Owner.AudioOffset);
            double last = Owner.Timing.BeatAtTime(StartTime + Duration + Owner.AudioOffset);
            int count = 0;
            float previousX = -100;
            foreach (double beat in Owner.Timing.Lines(first, last, 1))
            {
                float x = (float)((Owner.Timing.TimeAtBeat(beat) - Owner.AudioOffset - StartTime)
                    / Duration * rectTransform.rect.width);
                if (x - previousX < 60)
                {
                    continue;
                }

                bool measure = Owner.Timing.IsMeasure(beat);
                if (!measure && last - first > 12)
                {
                    continue;
                }

                var label = labels[count];
                label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = new Vector2(x + 3, -2);
                if (labelBeats[count] != beat)
                {
                    Owner.Timing.GetMusicalPosition(beat, out int bar, out double position);
                    label.text = bar + "." + (position + 1).ToString("0.#");
                    labelBeats[count] = beat;
                }

                label.color = new Color(0.8f, 0.83f, 0.87f);
                if (measure)
                {
                    label.color = new Color(1, 0.4f, 0.35f);
                }

                previousX = x;
                count++;
                if (count == labels.Length)
                {
                    break;
                }
            }

            for (int i = count; i < labels.Length; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }

        private double StartTime
        {
            get
            {
                if (Overview)
                {
                    return 0;
                }

                return Owner.WindowStart;
            }
        }

        private double Duration
        {
            get
            {
                if (Overview)
                {
                    return Math.Max(0.001, Owner.Duration);
                }

                return Owner.WindowDuration;
            }
        }

        public double FirstVisibleGridTime
        {
            get
            {
                int columns = Math.Min(1500, Mathf.CeilToInt(rectTransform.rect.width));
                foreach (double beat in VisibleGridLines(Math.Max(1, columns)))
                {
                    return Owner.Timing.TimeAtBeat(beat) - Owner.AudioOffset;
                }

                return StartTime;
            }
        }

        private IEnumerable<double> VisibleGridLines(int columns)
        {
            double first = Owner.Timing.BeatAtTime(StartTime + Owner.AudioOffset);
            double last = Owner.Timing.BeatAtTime(StartTime + Duration + Owner.AudioOffset);
            double step = Owner.DisplayStep;
            while ((last - first) / step > columns / 3.0)
            {
                step *= 2;
            }

            return Owner.Timing.Lines(first, last, step, Math.Max(1, columns / 2));
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!Owner)
            {
                return;
            }

            Rect area = rectTransform.rect;
            if (area.width <= 0 || area.height <= 0)
            {
                return;
            }

            AddRect(vertices, area, new Color(0.025f, 0.035f, 0.05f));
            int columns = Math.Min(1500, Mathf.CeilToInt(area.width));
            float width = area.width / columns;
            double start = StartTime;
            double duration = Duration;
            if (Owner.Peaks != null && Owner.Peaks.Ready)
            {
                for (int i = 0; i < columns; i++)
                {
                    var peak = Owner.Peaks.Range(start + duration * i / columns,
                        start + duration * (i + 1) / columns);
                    float low = area.center.y + peak.x * area.height * 0.44f;
                    float high = area.center.y + peak.y * area.height * 0.44f;
                    Color tint = Owner.Peaks.ColorRange(start + duration * i / columns,
                        start + duration * (i + 1) / columns);
                    AddRect(vertices, new Rect(area.xMin + i * width, low, width,
                        Math.Max(0.6f, high - low)), tint);
                }
            }

            if (Overview)
            {
                float left = X(Owner.WindowStart, area, start, duration);
                float right = X(Owner.WindowStart + Owner.WindowDuration, area, start, duration);
                AddRect(vertices, new Rect(left, area.yMin, Math.Max(0, right - left), area.height),
                    new Color(0.4f, 0.7f, 1, 0.16f));
            }
            else if (Owner.Timing != null)
            {
                int lineCount = 0;
                foreach (double beat in VisibleGridLines(columns))
                {
                    lineCount++;
                    if (lineCount > 5000)
                    {
                        break;
                    }

                    double time = Owner.Timing.TimeAtBeat(beat) - Owner.AudioOffset;
                    bool measure = Owner.Timing.IsMeasure(beat);
                    Color tint = new Color(0.35f, 0.4f, 0.48f, 0.65f);
                    float thickness = 0.7f;
                    if (measure)
                    {
                        tint = new Color(1, 0.22f, 0.19f, 0.9f);
                        thickness = 2;
                    }
                    else if (Math.Abs(beat - Math.Round(beat)) < 1e-6)
                    {
                        tint = new Color(0.65f, 0.69f, 0.74f, 0.8f);
                        thickness = 1;
                    }

                    float x = X(time, area, start, duration);
                    AddRect(vertices, new Rect(x - thickness * 0.5f, area.yMin, thickness, area.height), tint);
                }
            }

            double position = Owner.MusicTime;
            if (position >= start && position <= start + duration)
            {
                AddRect(vertices, new Rect(X(position, area, start, duration) - 1,
                    area.yMin, 2, area.height), Color.white);
            }
        }

        private static float X(double time, Rect area, double start, double duration)
        {
            return area.xMin + (float)Math.Max(0, Math.Min(1, (time - start) / duration)) * area.width;
        }

        private static void AddRect(VertexHelper vertices, Rect area, Color tint)
        {
            int index = vertices.currentVertCount;
            vertices.AddVert(new Vector3(area.xMin, area.yMin), tint, Vector2.zero);
            vertices.AddVert(new Vector3(area.xMin, area.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(area.xMax, area.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(area.xMax, area.yMin), tint, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private double TimeAtPointer(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,
                data.position, data.pressEventCamera, out var local);
            Rect area = rectTransform.rect;
            return StartTime + Math.Max(0, Math.Min(1, (local.x - area.xMin) / area.width)) * Duration;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || Owner.IsBusy)
            {
                return;
            }

            dragTime = TimeAtPointer(data);
            dragStart = Owner.WindowStart;
            dragged = false;
            if (Overview)
            {
                Owner.Seek(dragTime);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || Owner.IsBusy)
            {
                return;
            }

            dragged = true;
            if (Overview)
            {
                Owner.Seek(TimeAtPointer(data));
                return;
            }

            double time = TimeAtPointer(data) - Owner.WindowStart + dragStart;
            Owner.Pan(dragStart + dragTime - time);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!dragged && data.button == PointerEventData.InputButton.Left)
            {
                Owner.Seek(TimeAtPointer(data));
            }
        }

        public void OnScroll(PointerEventData data)
        {
            var keyboard = Keyboard.current;
            bool control = false;
            if (keyboard != null)
            {
                control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            }

            if (control)
            {
                Owner.Zoom(data.scrollDelta.y, TimeAtPointer(data));
            }
            else
            {
                Owner.Pan(Owner.WindowStart - data.scrollDelta.y * Owner.WindowDuration * 0.1);
            }
        }
    }
}
