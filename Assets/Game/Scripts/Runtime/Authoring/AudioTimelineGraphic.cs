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
        private bool snapSeek;
        [SerializeField] private Text[] labels;
        [SerializeField] private Text[] tempoLabels;
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

        public void ConfigureTempoLabels(Text[] source)
        {
            tempoLabels = source;
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

            int count = 0;
            foreach (var line in VisibleGridLines())
            {
                var label = labels[count];
                label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = new Vector2(line.X + 3, -2);
                if (labelBeats[count] != line.Beat)
                {
                    label.text = line.Measure + "." + line.BeatNumber;
                    labelBeats[count] = line.Beat;
                }

                label.color = new Color(0.8f, 0.83f, 0.87f);
                if (line.BeatNumber == 1)
                {
                    label.color = new Color(1, 0.4f, 0.35f);
                }

                count++;
            }

            for (int i = count; i < labels.Length; i++)
            {
                labels[i].gameObject.SetActive(false);
            }

            RefreshTempoLabels();
        }

        private void RefreshTempoLabels()
        {
            if (tempoLabels == null)
            {
                return;
            }

            int count = 0;
            for (int i = 1; i < Owner.TempoPoints.Count; i++)
            {
                double time = Owner.TempoPoints[i].StartTimeSeconds - Owner.AudioOffset;
                if (time < StartTime || time > StartTime + Duration)
                {
                    continue;
                }

                if (count == tempoLabels.Length)
                {
                    break;
                }

                var label = tempoLabels[count];
                label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = new Vector2(
                    (float)((time - StartTime) / Duration * rectTransform.rect.width) + 3, -26);
                label.text = "BPM " + Owner.TempoPoints[i - 1].Bpm.ToString("0.##")
                    + " → " + Owner.TempoPoints[i].Bpm.ToString("0.##");
                count++;
            }

            for (int i = count; i < tempoLabels.Length; i++)
            {
                tempoLabels[i].gameObject.SetActive(false);
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
                foreach (var line in VisibleGridLines())
                {
                    return line.Time;
                }

                return StartTime;
            }
        }

        private IEnumerable<(double Beat, double Time, float X, int Measure, int BeatNumber)> VisibleGridLines()
        {
            var timing = Owner.Timing;
            if (labels == null || labels.Length == 0 || timing == null)
            {
                yield break;
            }

            double start = StartTime;
            double duration = Duration;
            double offset = Owner.AudioOffset;
            float width = rectTransform.rect.width;
            double first = timing.BeatAtTime(start + offset);
            double last = timing.BeatAtTime(start + duration + offset);
            int count = 0;
            float previousX = -100;
            foreach (double beat in timing.Lines(first, last, 1))
            {
                double time = timing.TimeAtBeat(beat) - offset;
                float x = (float)((time - start) / duration * width);
                if (x - previousX < 60 || x + 3 >= width)
                {
                    continue;
                }

                timing.GetMusicalPosition(beat, out int measure, out double position);
                int beatIndex = (int)Math.Round(position);
                if (Math.Abs(position - beatIndex) >= 1e-6 || (beatIndex != 0 && last - first > 12))
                {
                    continue;
                }

                yield return (beat, time, x, measure, beatIndex + 1);
                previousX = x;
                count++;
                if (count == labels.Length)
                {
                    yield break;
                }
            }
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
                foreach (var line in VisibleGridLines())
                {
                    Color tint = new Color(0.65f, 0.69f, 0.74f, 0.8f);
                    float thickness = 1;
                    if (line.BeatNumber == 1)
                    {
                        tint = new Color(1, 0.22f, 0.19f, 0.9f);
                        thickness = 2;
                    }

                    float x = area.xMin + line.X;
                    AddRect(vertices, new Rect(x - thickness * 0.5f, area.yMin, thickness, area.height), tint);
                }

                for (int i = 1; i < Owner.TempoPoints.Count; i++)
                {
                    double time = Owner.TempoPoints[i].StartTimeSeconds - Owner.AudioOffset;
                    if (time < start || time > start + duration)
                    {
                        continue;
                    }

                    float x = X(time, area, start, duration);
                    AddRect(vertices, new Rect(x - 1, area.yMin, 2, area.height), new Color(0.35f, 0.8f, 1));
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
            var keyboard = Keyboard.current;
            snapSeek = !Overview && keyboard != null && keyboard.ctrlKey.isPressed;
            if (snapSeek)
            {
                SeekNearestGridLine(dragTime);
                return;
            }

            if (Overview)
            {
                Owner.Seek(dragTime);
            }
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || Owner.IsBusy || snapSeek)
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
            if (data.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (snapSeek)
            {
                snapSeek = false;
                return;
            }

            if (!dragged)
            {
                Owner.Seek(TimeAtPointer(data));
            }
        }

        private void SeekNearestGridLine(double pointerTime)
        {
            if (Owner.Timing == null)
            {
                return;
            }

            double nearestTime = 0;
            double nearestDistance = double.PositiveInfinity;
            foreach (var line in VisibleGridLines())
            {
                double distance = Math.Abs(line.Time - pointerTime);
                if (distance < nearestDistance)
                {
                    nearestTime = line.Time;
                    nearestDistance = distance;
                }
            }

            if (double.IsFinite(nearestDistance))
            {
                Owner.SeekInViewport(nearestTime);
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
