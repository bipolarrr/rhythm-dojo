using System;
using RhythmDojo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    // Uses the editor's upward beat/lane projection without enabling editing tools.
    public sealed class ResultsChartGraphic : MaskableGraphic, IPointerClickHandler, IScrollHandler
    {
        private PlayResult result;
        public double FirstBeat { get; private set; }
        public int VisibleBeats { get; private set; } = 16;
        public int SelectedIndex { get; private set; } = -1;
        public event Action<int> NoteSelected;
        public event Action<float> Scrolled;

        public void SetResult(PlayResult result)
        {
            this.result = result;
            SelectedIndex = -1;
            SetWindow(0, 16);
        }

        public void SetWindow(double firstBeat, int visibleBeats)
        {
            FirstBeat = firstBeat;
            VisibleBeats = visibleBeats;
            SetVerticesDirty();
        }

        public void Select(int index)
        {
            SelectedIndex = index;
            SetVerticesDirty();
        }

        public Rect NoteBounds(int index)
        {
            var note = result.Notes[index].Note;
            var area = rectTransform.rect;
            float width = area.width / result.LaneLabels.Count;
            float y = area.yMin + (float)((result.Tempo.BeatAtTime(note.StartTime) - FirstBeat)
                / VisibleBeats * area.height);
            float end = area.yMin + (float)((result.Tempo.BeatAtTime(note.EndTime) - FirstBeat)
                / VisibleBeats * area.height);
            float low = Mathf.Max(area.yMin, y - 6);
            float high = Mathf.Min(area.yMax, end + 6);

            return new Rect(area.xMin + width * (note.Lane + .1f), low, width * .8f, high - low);
        }

        public int HitTest(Vector2 point)
        {
            if (result == null || !rectTransform.rect.Contains(point))
            {
                return -1;
            }

            for (int i = result.Notes.Count - 1; i >= 0; i--)
            {
                var rect = NoteBounds(i);

                if (rect.height > 0 && rect.Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out var point))
            {
                int index = HitTest(point);

                if (index >= 0)
                {
                    NoteSelected?.Invoke(index);
                }
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            Scrolled?.Invoke(eventData.scrollDelta.y);
        }

        public static Color GradeColor(PlayNoteResult note)
        {
            if (note.Grade == Gameplay.Judgment.Perfect)
            {
                return new Color(.22f, .83f, .72f);
            }

            if (note.Grade == Gameplay.Judgment.Good)
            {
                return new Color(1, .77f, .3f);
            }

            if (note.Grade == Gameplay.Judgment.Miss)
            {
                return new Color(1, .35f, .4f);
            }

            return new Color(.5f, .53f, .58f);
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();

            if (result == null || result.LaneLabels.Count == 0)
            {
                return;
            }

            var area = rectTransform.rect;

            for (int lane = 0; lane <= result.LaneLabels.Count; lane++)
            {
                float x = area.xMin + area.width * lane / result.LaneLabels.Count;
                AddRect(vertices, new Rect(x, area.yMin, 1, area.height), new Color(.4f, .4f, .45f));
            }

            for (double beat = Math.Ceiling(FirstBeat); beat <= FirstBeat + VisibleBeats; beat++)
            {
                float y = area.yMin + (float)((beat - FirstBeat) / VisibleBeats * area.height);
                var tint = new Color(.3f, .3f, .35f);

                if ((int)beat % 4 == 0)
                {
                    tint = new Color(.62f, .55f, .34f);
                }

                AddRect(vertices, new Rect(area.xMin, y, area.width, 1), tint);
            }

            for (int i = 0; i < result.Notes.Count; i++)
            {
                var bounds = NoteBounds(i);

                if (bounds.height <= 0)
                {
                    continue;
                }

                if (i == SelectedIndex)
                {
                    AddRect(vertices, bounds, Color.white);
                    bounds = new Rect(bounds.x + 3, bounds.y + 3, bounds.width - 6, bounds.height - 6);
                }

                AddRect(vertices, bounds, GradeColor(result.Notes[i]));
            }
        }

        private static void AddRect(VertexHelper vertices, Rect rect, Color tint)
        {
            if (rect.width <= 0 || rect.height <= 0)
            {
                return;
            }

            int start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
