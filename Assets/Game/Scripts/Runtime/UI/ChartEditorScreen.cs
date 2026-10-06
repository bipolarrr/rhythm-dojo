using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ChartEditorScreen : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IDragHandler, IScrollHandler
    {
        private const int DefaultVisibleBeats = 16;
        private const int MinVisibleBeats = 4;
        private const int MaxVisibleBeats = 7200;
        private const int MaxBeatLines = 34;
        private const int BeatsPerWheelStep = 4;
        private const float NoteHeight = 7f;
        private const double DefaultChartDuration = 8.5;

        [SerializeField] private GameModeDefinition mode;
        [SerializeField] private RectTransform noteLayer;

        private EditorSession session;
        private Guid chartId;
        private int visibleBeats = DefaultVisibleBeats;
        private float scrollBeatOffset;
        private bool pressing;
        private int pressPointerId;
        private int pressLane;
        private int pressBeat;
        private RectTransform preview;
        private readonly RectTransform[] beatLines = new RectTransform[MaxBeatLines];

        public int VisibleBeats => visibleBeats;
        public float ScrollBeatOffset => scrollBeatOffset;
        public EditorSession Session => session;
        public Guid ChartId => chartId;
        public GameModeDefinition Mode => mode;
        public bool IsBusy { get; set; }
        private TempoMap tempo;

        public void Configure(GameModeDefinition gameMode, RectTransform notes)
        {
            mode = gameMode;
            noteLayer = notes;
        }

        private void Awake()
        {
            if (!mode || mode.LaneCount != 4 || !noteLayer)
                throw new InvalidOperationException("Chart editor requires a four-lane mode and note layer.");

            session = new EditorSession(new SongProject());
            session.Edit(edit => chartId = edit.AddChart("Normal", mode.name, mode.LaneCount, DefaultChartDuration));
            PrepareBeatLines();
            SetSession(ChartEditorMenu.TakeReturningSession() ?? session);
        }

        public void SetSession(EditorSession replacement)
        {
            var snapshot = replacement.Snapshot;
            var chart = Array.Find(snapshot.Charts, c => c.Id == replacement.ActiveChartId);
            if (chart == null || chart.ModeId != mode.name || chart.LaneCount != mode.LaneCount)
                throw new InvalidOperationException("이 에디터에서는 4레인 채보를 열 수 있습니다.");
            var timing = new TempoMap(snapshot.TempoPoints);
            if (session != null) session.Changed -= RefreshSession;
            session = replacement;
            chartId = chart.Id;
            tempo = timing;
            session.Changed += RefreshSession;
            ResetView();
        }

        private void OnDestroy() { if (session != null) session.Changed -= RefreshSession; }

        private void RefreshSession()
        {
            tempo = new TempoMap(session.Snapshot.TempoPoints);
            RefreshBeatLines();
            RefreshNotes();
        }

        public void ResetView()
        {
            pressing = false;
            if (preview) Destroy(preview.gameObject);
            preview = null;
            visibleBeats = DefaultVisibleBeats;
            scrollBeatOffset = 0;
            RefreshSession();
        }

        private double TimeAtBeat(double beat)
        {
            double accumulated = 0;
            for (int i = 0; i < tempo.Count - 1; i++)
            {
                double segment = (tempo[i + 1].StartTimeSeconds - tempo[i].StartTimeSeconds) * tempo[i].Bpm / 60;
                if (beat < accumulated + segment)
                    return tempo[i].StartTimeSeconds + (beat - accumulated) * 60 / tempo[i].Bpm;
                accumulated += segment;
            }
            var last = tempo[tempo.Count - 1];
            return last.StartTimeSeconds + (beat - accumulated) * 60 / last.Bpm;
        }

        private double BeatAtTime(double time)
        {
            double beat = 0;
            int segment = tempo.FindSegment(time);
            for (int i = 0; i < segment; i++)
                beat += (tempo[i + 1].StartTimeSeconds - tempo[i].StartTimeSeconds) * tempo[i].Bpm / 60;
            return beat + (time - tempo[segment].StartTimeSeconds) * tempo[segment].Bpm / 60;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (session == null || !noteLayer || beatLines[0] == null) return;
            RefreshBeatLines();
            RefreshNotes();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (IsBusy || eventData.scrollDelta.y == 0) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || (!keyboard.leftCtrlKey.isPressed && !keyboard.rightCtrlKey.isPressed))
            {
                float nextOffset = Mathf.Max(0, scrollBeatOffset - Mathf.Sign(eventData.scrollDelta.y) * 0.5f);
                if (nextOffset == scrollBeatOffset) return;
                scrollBeatOffset = nextOffset;
                RefreshBeatLines();
                RefreshNotes();
                return;
            }
            int next = Mathf.Clamp(visibleBeats - (eventData.scrollDelta.y > 0 ? BeatsPerWheelStep : -BeatsPerWheelStep),
                MinVisibleBeats, MaxVisibleBeats);
            if (next == visibleBeats) return;
            visibleBeats = next;
            RefreshBeatLines();
            RefreshNotes();
        }

        private void PrepareBeatLines()
        {
            for (int slot = 0; slot < beatLines.Length; slot++)
            {
                var existing = transform.Find("Beat " + slot);
                if (existing)
                {
                    beatLines[slot] = (RectTransform)existing;
                    continue;
                }
                var line = new GameObject("Beat " + slot, typeof(RectTransform), typeof(Image));
                line.transform.SetParent(transform, false);
                line.transform.SetSiblingIndex(noteLayer.GetSiblingIndex());
                var image = line.GetComponent<Image>();
                image.raycastTarget = false;
                beatLines[slot] = (RectTransform)line.transform;
            }
        }

        private void RefreshBeatLines()
        {
            var grid = (RectTransform)transform;
            int stride = Mathf.NextPowerOfTwo(Mathf.Max(1, Mathf.CeilToInt(visibleBeats / 32f)));
            int firstBeat = Mathf.CeilToInt(scrollBeatOffset / stride) * stride;
            float lastBeat = scrollBeatOffset + visibleBeats;
            for (int slot = 0; slot < beatLines.Length; slot++)
            {
                var line = beatLines[slot];
                int beat = firstBeat + slot * stride;
                bool visible = beat <= lastBeat;
                line.gameObject.SetActive(visible);
                if (!visible) continue;
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(0, 1);
                line.anchoredPosition = new Vector2(0, -grid.rect.height +
                    (beat - scrollBeatOffset) * grid.rect.height / visibleBeats);
                line.sizeDelta = new Vector2(grid.rect.width, beat % 4 == 0 ? 2 : 1);
                line.GetComponent<Image>().color = beat % 4 == 0 ? new Color(0.65f, 0.61f, 0.23f) :
                    new Color(0.24f, 0.27f, 0.32f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsBusy || eventData.button != PointerEventData.InputButton.Left || !TryGetPoint(eventData, out var point)) return;
            var grid = (RectTransform)transform;
            pressing = true;
            pressPointerId = eventData.pointerId;
            pressLane = Mathf.FloorToInt(point.x / grid.rect.width * mode.LaneCount);
            pressBeat = BeatAt(point.y);
            var marker = new GameObject("Note Preview", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(noteLayer, false);
            preview = (RectTransform)marker.transform;
            var image = marker.GetComponent<Image>();
            image.color = mode.GetLane(pressLane).color * new Color(1, 1, 1, 0.55f);
            image.raycastTarget = false;
            PositionMarker(preview, pressLane, TimeAtBeat(pressBeat), TimeAtBeat(pressBeat));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!pressing || eventData.pointerId != pressPointerId || !preview) return;
            int endBeat = Mathf.Max(pressBeat, BeatAtPointer(eventData));
            PositionMarker(preview, pressLane, TimeAtBeat(pressBeat), TimeAtBeat(endBeat));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressing || eventData.pointerId != pressPointerId || eventData.button != PointerEventData.InputButton.Left)
                return;
            pressing = false;
            if (preview) Destroy(preview.gameObject);
            preview = null;
            int endBeat = Mathf.Max(pressBeat, BeatAtPointer(eventData));
            if (IsBusy) return;
            double startTime = TimeAtBeat(pressBeat);
            double endTime = TimeAtBeat(endBeat);
            bool isHold = endBeat > pressBeat;
            var overlaps = Array.FindAll(session.GetNotes(chartId), note => note.Data.Lane == pressLane &&
                note.Data.StartTime <= endTime && note.Data.EndTime >= startTime);
            if (Array.Exists(overlaps, note => !isHold || note.Data.Kind == NoteKind.Hold)) return;

            double completion = Array.Find(session.Snapshot.Charts, c => c.Id == chartId).CompletionTimeSeconds;
            session.Edit(edit =>
            {
                foreach (var note in overlaps) edit.DeleteNote(chartId, note.Id);
                if (endTime >= completion)
                    edit.SetChartSettings(chartId, mode.name, mode.LaneCount, TimeAtBeat(endBeat + 1));
                edit.AddNote(chartId, isHold ? NoteData.Hold(pressLane, startTime, endTime) :
                    NoteData.Tap(pressLane, startTime));
            });
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsBusy || eventData.button != PointerEventData.InputButton.Right || !TryGetPoint(eventData, out var point)) return;
            var grid = (RectTransform)transform;
            int lane = Mathf.FloorToInt(point.x / grid.rect.width * mode.LaneCount);
            double time = TimeAtBeat(BeatAt(point.y));
            var existing = Array.Find(session.GetNotes(chartId), note => note.Data.Lane == lane &&
                note.Data.StartTime <= time && note.Data.EndTime >= time);
            if (existing.Id == 0) return;
            session.Edit(edit => edit.DeleteNote(chartId, existing.Id));
        }

        private bool TryGetPoint(PointerEventData eventData, out Vector2 point)
        {
            var rect = (RectTransform)transform;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out point) &&
                point.x >= 0 && point.x < rect.rect.width && point.y <= 0 && point.y >= -rect.rect.height;
        }

        private int BeatAtPointer(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out var point);
            return BeatAt(Mathf.Clamp(point.y, -rect.rect.height, 0));
        }

        private int BeatAt(float localY)
        {
            var rect = (RectTransform)transform;
            float beat = scrollBeatOffset + visibleBeats * (1 + localY / rect.rect.height);
            return Mathf.Clamp(Mathf.RoundToInt(beat), Mathf.CeilToInt(scrollBeatOffset),
                Mathf.FloorToInt(scrollBeatOffset + visibleBeats));
        }

        private void RefreshNotes()
        {
            for (int i = noteLayer.childCount - 1; i >= 0; i--)
                Destroy(noteLayer.GetChild(i).gameObject);

            double viewStart = TimeAtBeat(scrollBeatOffset);
            double viewEnd = TimeAtBeat(scrollBeatOffset + visibleBeats);
            foreach (var note in session.GetNotes(chartId))
            {
                if (note.Data.EndTime < viewStart || note.Data.StartTime > viewEnd) continue;
                var marker = new GameObject(note.Data.Kind == NoteKind.Hold ? "Hold Note" : "Note",
                    typeof(RectTransform), typeof(Image));
                marker.transform.SetParent(noteLayer, false);
                var image = marker.GetComponent<Image>();
                image.color = mode.GetLane(note.Data.Lane).color;
                image.raycastTarget = false;
                PositionMarker((RectTransform)marker.transform, note.Data.Lane,
                    Math.Max(note.Data.StartTime, viewStart), Math.Min(note.Data.EndTime, viewEnd));
            }
        }

        private void PositionMarker(RectTransform rect, int lane, double startTime, double endTime)
        {
            float laneWidth = noteLayer.rect.width / mode.LaneCount;
            float startBeat = (float)BeatAtTime(startTime);
            float endBeat = (float)BeatAtTime(endTime);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(lane * laneWidth + laneWidth * 0.1f,
                -noteLayer.rect.height + (endBeat - scrollBeatOffset) * noteLayer.rect.height / visibleBeats +
                NoteHeight * 0.5f);
            rect.sizeDelta = new Vector2(laneWidth * 0.8f,
                NoteHeight + (endBeat - startBeat) * noteLayer.rect.height / visibleBeats);
        }
    }
}
