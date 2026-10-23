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
        [field: SerializeField] public ChartAudioTimeline Owner { get; set; }
        [field: SerializeField] public bool Overview { get; set; }
        private double dragTime;
        private double dragStart;
        private bool dragged;
        [SerializeField] private Text[] labels;
        private double[] labelBeats;
        private float lastWidth;
        private RhythmDojo.Gameplay.BeatGridTiming labelTiming;
        private WaveformPeaks cachedPeaks;
        private int cachedRevision;
        private double cachedStart;
        private double cachedDuration;
        private Vector2[] columnPeaks;
        private Color[] columnColors;
        private readonly List<Vector2> samplePoints = new List<Vector2>();

        public bool ShowsSamples { get; private set; }

        public void ConfigureLabels(Text[] labels)
        {
            this.labels = labels;
            InitializeLabels();
        }

        public void InitializeLabels()
        {
            labels ??= Array.Empty<Text>();
            labelBeats = new double[labels.Length];
            for (int i = 0; i < labelBeats.Length; i++)
            {
                labelBeats[i] = double.NaN;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeLabels();
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
            if (labels == null || !Owner)
            {
                return;
            }

            if (labels.Length == 0 || Owner.Timing == null)
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
            double start = StartTime;
            double duration = Duration;
            if (Owner.Peaks != null && Owner.Peaks.Ready)
            {
                int resolution = Math.Min(1500, Mathf.CeilToInt(area.width * canvas.scaleFactor));
                resolution = Math.Max(1, Math.Min(resolution, 4000 / Owner.Peaks.Channels));
                ShowsSamples = duration * Owner.Peaks.Frequency <= resolution;
                if (ShowsSamples)
                {
                    AddSamples(vertices, area, start, duration);
                }
                else
                {
                    AddWaveform(vertices, area, resolution, start, duration);
                }
            }
            else
            {
                ShowsSamples = false;
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

        private void AddWaveform(VertexHelper vertices, Rect area, int columns, double start, double duration)
        {
            CacheColumns(columns, start, duration);
            float feather = 1 / Math.Max(0.01f, canvas.scaleFactor);
            int first = vertices.currentVertCount;
            // Shared vertices form a continuous envelope, with a one-screen-pixel alpha fringe.
            // Only the display is sampled in columns; pointer and music time remain continuous.
            for (int i = 0; i <= columns; i++)
            {
                var peak = columnPeaks[i];
                float low = area.center.y + peak.x * area.height * 0.44f;
                float high = area.center.y + peak.y * area.height * 0.44f;
                float x = area.xMin + area.width * i / columns;
                Color tint = columnColors[i];
                float edge = Math.Min(feather, (high - low) * 0.25f);
                AddWaveVertex(vertices, x, low - feather, tint, 0);
                AddWaveVertex(vertices, x, low, tint, 0.95f);
                AddWaveVertex(vertices, x, Math.Min(area.center.y, low + edge), tint, 0.65f);
                AddWaveVertex(vertices, x, area.center.y, tint, 0.3f);
                AddWaveVertex(vertices, x, Math.Max(area.center.y, high - edge), tint, 0.65f);
                AddWaveVertex(vertices, x, high, tint, 0.95f);
                AddWaveVertex(vertices, x, high + feather, tint, 0);
                if (i == 0)
                {
                    continue;
                }

                int previous = first + (i - 1) * 7;
                for (int band = 0; band < 6; band++)
                {
                    int index = previous + band;
                    vertices.AddTriangle(index, index + 1, index + 8);
                    vertices.AddTriangle(index, index + 8, index + 7);
                }
            }
        }

        private void CacheColumns(int columns, double start, double duration)
        {
            bool sameAudio = cachedPeaks == Owner.Peaks && cachedRevision == Owner.Peaks.Revision;
            bool sameWindow = cachedStart == start && cachedDuration == duration;
            if (sameAudio && sameWindow)
            {
                if (columnPeaks != null && columnPeaks.Length == columns + 1)
                {
                    return;
                }
            }

            if (columnPeaks == null || columnPeaks.Length != columns + 1)
            {
                columnPeaks = new Vector2[columns + 1];
                columnColors = new Color[columns + 1];
            }

            for (int i = 0; i <= columns; i++)
            {
                double left = start + duration * Math.Max(0, i - 0.5) / columns;
                double right = start + duration * Math.Min(columns, i + 0.5) / columns;
                columnPeaks[i] = Owner.Peaks.Range(left, right);
                columnColors[i] = Owner.Peaks.ColorRange(left, right);
            }

            cachedPeaks = Owner.Peaks;
            cachedRevision = Owner.Peaks.Revision;
            cachedStart = start;
            cachedDuration = duration;
        }

        private void AddSamples(VertexHelper vertices, Rect area, double start, double duration)
        {
            var peaks = Owner.Peaks;
            double first = Math.Max(0, start * peaks.Frequency);
            double last = Math.Min(peaks.SampleCount - 1, (start + duration) * peaks.Frequency);
            if (last <= first)
            {
                return;
            }

            for (int channel = 0; channel < peaks.Channels; channel++)
            {
                samplePoints.Clear();
                AddSamplePoint(area, start, duration, first, channel);
                for (int frame = (int)Math.Floor(first) + 1; frame < last; frame++)
                {
                    AddSamplePoint(area, start, duration, frame, channel);
                }

                AddSamplePoint(area, start, duration, last, channel);
                Color tint = Color.HSVToRGB((0.12f + channel * 0.47f) % 1, 0.65f, 1);
                AddSampleLine(vertices, area, tint);
            }
        }

        private void AddSamplePoint(Rect area, double start, double duration, double frame, int channel)
        {
            var peaks = Owner.Peaks;
            int lower = (int)Math.Floor(frame);
            int upper = Math.Min(peaks.SampleCount - 1, lower + 1);
            float value = Mathf.Lerp(peaks.Sample(lower, channel), peaks.Sample(upper, channel),
                (float)(frame - lower));
            float x = X(frame / peaks.Frequency, area, start, duration);
            samplePoints.Add(new Vector2(x, area.center.y + value * area.height * 0.44f));
        }

        private void AddSampleLine(VertexHelper vertices, Rect area, Color tint)
        {
            float feather = 1 / Math.Max(0.01f, canvas.scaleFactor);
            int first = vertices.currentVertCount;
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector2 tangent = samplePoints[Math.Min(i + 1, samplePoints.Count - 1)]
                    - samplePoints[Math.Max(0, i - 1)];
                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized * feather;
                AddLineVertex(vertices, area, samplePoints[i] - normal * 1.5f, tint, 0);
                AddLineVertex(vertices, area, samplePoints[i] - normal * 0.5f, tint, 1);
                AddLineVertex(vertices, area, samplePoints[i] + normal * 0.5f, tint, 1);
                AddLineVertex(vertices, area, samplePoints[i] + normal * 1.5f, tint, 0);
                if (i == 0)
                {
                    continue;
                }

                int previous = first + (i - 1) * 4;
                for (int band = 0; band < 3; band++)
                {
                    int index = previous + band;
                    vertices.AddTriangle(index, index + 1, index + 5);
                    vertices.AddTriangle(index, index + 5, index + 4);
                }
            }
        }

        private static void AddLineVertex(VertexHelper vertices, Rect area, Vector2 point, Color tint, float alpha)
        {
            AddWaveVertex(vertices, Mathf.Clamp(point.x, area.xMin, area.xMax),
                Mathf.Clamp(point.y, area.yMin, area.yMax), tint, alpha);
        }

        private static void AddWaveVertex(VertexHelper vertices, float x, float y, Color tint, float alpha)
        {
            tint.a = alpha;
            vertices.AddVert(new Vector3(x, y), tint, Vector2.zero);
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
