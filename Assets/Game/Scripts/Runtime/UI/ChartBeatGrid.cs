using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // One local rectangle, one projection, one mesh for the complete timeline.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChartBeatGrid : MaskableGraphic
    {
        private int visibleBeats = 16;
        private double bottomBeat;
        private TempoMap tempo;
        private GameModeDefinition mode;
        private EditorNote[] notes = Array.Empty<EditorNote>();
        private int previewLane = -1;
        private double previewStart, previewEnd;
        private readonly Dictionary<int, Color> lineRows = new Dictionary<int, Color>();
        private static readonly Color BeatColor = new Color(0.24f, 0.27f, 0.32f);
        private static readonly Color MeasureColor = new Color(0.65f, 0.61f, 0.23f);
        public BeatProjection Projection => new BeatProjection(bottomBeat, rectTransform.rect.yMin,
            Math.Max(0.001, rectTransform.rect.height) / visibleBeats);

        public void SetViewport(int beats, double offset)
        {
            visibleBeats = beats;
            bottomBeat = offset;
            SetVerticesDirty();
        }

        public void SetNotes(TempoMap timing, GameModeDefinition gameMode, EditorNote[] source)
        {
            tempo = timing;
            mode = gameMode;
            notes = source;
            SetVerticesDirty();
        }

        public void SetPreview(int lane, double startBeat = 0, double endBeat = 0)
        {
            previewLane = lane;
            previewStart = startBeat;
            previewEnd = endBeat;
            SetVerticesDirty();
        }

        public Rect NoteBounds(int lane, double startBeat, double endBeat)
        {
            var area = rectTransform.rect;
            var projection = Projection;
            float laneWidth = area.width / 4;
            float bottom = Mathf.Max(area.yMin, (float)projection.Project(startBeat) - 3.5f);
            float top = Mathf.Min(area.yMax, (float)projection.Project(endBeat) + 3.5f);
            return new Rect(area.xMin + laneWidth * (lane + 0.1f), bottom, laneWidth * 0.8f,
                Mathf.Max(0, top - bottom));
        }

        public long HitTest(Vector2 point)
        {
            if (tempo == null || !rectTransform.rect.Contains(point)) return 0;
            // Last drawn note is visually on top.
            for (int i = notes.Length - 1; i >= 0; i--)
            {
                var note = notes[i];
                if (NoteBounds(note.Data.Lane, tempo.BeatAtTime(note.Data.StartTime),
                    tempo.BeatAtTime(note.Data.EndTime)).Contains(point)) return note.Id;
            }
            return 0;
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var area = rectTransform.rect;
            if (area.height <= 0 || area.width <= 0) return;
            var projection = Projection;
            // Coverage at physical pixel edges avoids alternating bright/missing lines at
            // fractional canvas scales, without rounding musical coordinates to pixels.
            var camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float screenBottom = RectTransformUtility.WorldToScreenPoint(camera,
                rectTransform.TransformPoint(new Vector3(0, area.yMin))).y;
            float screenTop = RectTransformUtility.WorldToScreenPoint(camera,
                rectTransform.TransformPoint(new Vector3(0, area.yMax))).y;
            float scale = Mathf.Abs(screenTop - screenBottom) / area.height;
            if (scale <= 0) scale = 1;
            lineRows.Clear();
            for (double beat = Math.Ceiling(bottomBeat); beat <= Math.Floor(bottomBeat + visibleBeats); beat++)
            {
                bool measure = beat % 4 == 0;
                float y = (float)projection.Project(beat);
                AccumulateLine(area, y, measure ? 2 : 1,
                    measure ? MeasureColor : BeatColor, screenBottom, scale);
            }
            // Composite every line's coverage before emitting rows. At extreme overview
            // zoom this keeps the grid bounded by screen height, not the UI vertex limit.
            foreach (var row in lineRows)
            {
                float low = Mathf.Max(area.yMin, area.yMin + (row.Key - screenBottom) / scale);
                float high = Mathf.Min(area.yMax, area.yMin + (row.Key + 1 - screenBottom) / scale);
                var tint = row.Value;
                tint.r /= tint.a; tint.g /= tint.a; tint.b /= tint.a;
                AddRect(vertices, new Rect(area.xMin, low, area.width, Mathf.Max(0, high - low)), tint);
            }
            if (tempo == null || !mode) return;
            foreach (var note in notes)
                AddRect(vertices, NoteBounds(note.Data.Lane, tempo.BeatAtTime(note.Data.StartTime),
                    tempo.BeatAtTime(note.Data.EndTime)), mode.GetLane(note.Data.Lane).color);
            if (previewLane >= 0)
                AddRect(vertices, NoteBounds(previewLane, previewStart, previewEnd),
                    mode.GetLane(previewLane).color * new Color(1, 1, 1, 0.55f));
        }

        private void AccumulateLine(Rect area, float y, float thickness,
            Color color, float screenBottom, float scale)
        {
            float center = screenBottom + (y - area.yMin) * scale;
            float bottom = center - thickness * 0.5f, top = center + thickness * 0.5f;
            for (int row = Mathf.FloorToInt(bottom); row < Mathf.CeilToInt(top); row++)
            {
                float coverage = Mathf.Min(top, row + 1) - Mathf.Max(bottom, row);
                if (coverage <= 0) continue;
                lineRows.TryGetValue(row, out var previous);
                float alpha = color.a * coverage;
                lineRows[row] = new Color(color.r * alpha + previous.r * (1 - alpha),
                    color.g * alpha + previous.g * (1 - alpha),
                    color.b * alpha + previous.b * (1 - alpha), alpha + previous.a * (1 - alpha));
            }
        }

        private static void AddRect(VertexHelper vertices, Rect rect, Color color)
        {
            if (rect.height <= 0) return;
            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
