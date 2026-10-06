using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RhythmDojo.UI
{
    public sealed class ChartEditorScreen : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
        IPointerUpHandler, IDragHandler, IScrollHandler
    {
        private const int DefaultVisibleBeats = 16;
        private const int MinVisibleBeats = 4;
        private const int MaxVisibleBeats = 7200;
        private const int BeatsPerWheelStep = 4;
        private const double DefaultChartDuration = 8.5;

        [SerializeField] private GameModeDefinition mode;
        [SerializeField] private ChartBeatGrid beatGrid;

        private EditorSession session;
        private Guid chartId;
        private int visibleBeats = DefaultVisibleBeats;
        private double scrollBeatOffset;
        private bool pressing;
        private int pressPointerId;
        private int pressLane;
        private int pressBeat;
        private int previewEndBeat;

        public int VisibleBeats => visibleBeats;
        public double ScrollBeatOffset => scrollBeatOffset;
        public EditorSession Session => session;
        public Guid ChartId => chartId;
        public GameModeDefinition Mode => mode;
        public bool IsBusy { get; set; }
        private TempoMap tempo;

        public void Configure(GameModeDefinition gameMode, ChartBeatGrid timeline)
        {
            mode = gameMode;
            beatGrid = timeline;
        }

        private void Awake()
        {
            if (!mode || mode.LaneCount != 4 || !beatGrid)
                throw new InvalidOperationException("Chart editor requires a four-lane mode and timeline renderer.");

            session = new EditorSession(new SongProject());
            session.Edit(edit => chartId = edit.AddChart("Normal", mode.name, mode.LaneCount, DefaultChartDuration));
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
            beatGrid.SetPreview(-1);
            visibleBeats = DefaultVisibleBeats;
            scrollBeatOffset = 0;
            RefreshSession();
        }

        private double TimeAtBeat(double beat) => tempo.TimeAtBeat(beat);

        private void OnRectTransformDimensionsChange()
        {
            if (session == null || !beatGrid || tempo == null) return;
            RefreshBeatLines();
            RefreshNotes();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (IsBusy || eventData.scrollDelta.y == 0) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || (!keyboard.leftCtrlKey.isPressed && !keyboard.rightCtrlKey.isPressed))
            {
                Scroll(-Math.Sign(eventData.scrollDelta.y) * 0.5);
                return;
            }
            Zoom(Math.Sign(eventData.scrollDelta.y));
        }

        public void Scroll(double beats)
        {
            if (IsBusy || !double.IsFinite(beats)) return;
            scrollBeatOffset = Math.Max(0, scrollBeatOffset + beats);
            RefreshBeatLines();
        }

        public void Zoom(int steps)
        {
            if (IsBusy) return;
            int next = (int)Math.Max(MinVisibleBeats, Math.Min(MaxVisibleBeats,
                (long)visibleBeats - (long)steps * BeatsPerWheelStep));
            if (next == visibleBeats) return;
            visibleBeats = next;
            RefreshBeatLines();
        }

        private void RefreshBeatLines()
        {
            beatGrid.SetViewport(visibleBeats, scrollBeatOffset);
            if (pressing) beatGrid.SetPreview(pressLane, pressBeat, previewEndBeat);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsBusy || eventData.button != PointerEventData.InputButton.Left || !TryGetPoint(eventData, out var point)) return;
            var grid = beatGrid.rectTransform;
            pressing = true;
            pressPointerId = eventData.pointerId;
            pressLane = Mathf.FloorToInt((point.x - grid.rect.xMin) / grid.rect.width * mode.LaneCount);
            pressBeat = BeatAt(point.y);
            previewEndBeat = pressBeat;
            beatGrid.SetPreview(pressLane, pressBeat, previewEndBeat);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!pressing || eventData.pointerId != pressPointerId) return;
            int endBeat = Mathf.Max(pressBeat, BeatAtPointer(eventData));
            previewEndBeat = endBeat;
            beatGrid.SetPreview(pressLane, pressBeat, previewEndBeat);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressing || eventData.pointerId != pressPointerId || eventData.button != PointerEventData.InputButton.Left)
                return;
            pressing = false;
            beatGrid.SetPreview(-1);
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
            long id = beatGrid.HitTest(point);
            if (id != 0) session.Edit(edit => edit.DeleteNote(chartId, id));
        }

        private bool TryGetPoint(PointerEventData eventData, out Vector2 point)
        {
            var rect = beatGrid.rectTransform;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out point) &&
                rect.rect.Contains(point);
        }

        private int BeatAtPointer(PointerEventData eventData)
        {
            var rect = beatGrid.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out var point);
            return BeatAt(Mathf.Clamp(point.y, rect.rect.yMin, rect.rect.yMax));
        }

        private int BeatAt(float localY)
        {
            double beat = beatGrid.Projection.Unproject(localY);
            return (int)Math.Max(Math.Ceiling(scrollBeatOffset), Math.Min(Math.Floor(scrollBeatOffset + visibleBeats),
                Math.Round(beat, MidpointRounding.AwayFromZero)));
        }

        private void RefreshNotes()
        {
            beatGrid.SetNotes(tempo, mode, session.GetNotes(chartId));
        }
    }
}
