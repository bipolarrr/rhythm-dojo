using System;
using System.Globalization;
using System.Text;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class ResultsChartReview : MonoBehaviour
    {
        [SerializeField]
        private ResultsReviewView view;
        private PlayResult result;
        private bool bound;
        private int selectedIndex;

        public PlayResult Result => result;
        public int SelectedIndex => selectedIndex;
        public event Action Closed;

        public void Show(PlayResult result)
        {
            if (result?.Chart == null || result.Tempo == null)
            {
                return;
            }

            view.Validate();
            Bind();
            this.result = result;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            view.heading.text = result.Title + "  /  " + result.Difficulty + "  /  전체 " + result.Notes.Count + "노트";
            view.lanes.text = "왼쪽부터  " + string.Join("  |  ", result.LaneLabels);
            view.chart.SetResult(result);
            view.zoom.SetValueWithoutNotify(1);
            view.position.SetValueWithoutNotify(0);
            RefreshWindow();
            SelectNote(0);

            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.closeButton.gameObject);
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Bind()
        {
            if (bound)
            {
                return;
            }

            bound = true;
            view.closeButton.onClick.AddListener(Close);
            view.previousButton.onClick.AddListener(Previous);
            view.nextButton.onClick.AddListener(Next);
            view.position.onValueChanged.AddListener(PositionChanged);
            view.zoom.onValueChanged.AddListener(ZoomChanged);
            view.chart.NoteSelected += SelectNote;
            view.chart.Scrolled += Scroll;
        }

        private void Previous() => SelectNote(selectedIndex - 1);
        private void Next() => SelectNote(selectedIndex + 1);
        private void PositionChanged(float value) => RefreshWindow();
        private void ZoomChanged(int value) => RefreshWindow();

        private void Scroll(float value)
        {
            view.position.value += value * view.chart.VisibleBeats / 4f;
        }

        private void RefreshWindow()
        {
            int beats = 8 << view.zoom.value;
            double last = Math.Ceiling(result.Tempo.BeatAtTime(result.Chart.CompletionTime));
            view.position.maxValue = (float)Math.Max(0, last - beats);
            view.chart.SetWindow(view.position.value, beats);
            view.range.text = string.Format(CultureInfo.InvariantCulture,
                "{0:0.#}–{1:0.#}박 / 전체 {2:0.#}박   ·   휠 또는 아래 슬라이더로 탐색",
                view.chart.FirstBeat, Math.Min(last, view.chart.FirstBeat + beats), last);
        }

        public void SelectNote(int index)
        {
            if (result == null || result.Notes.Count == 0)
            {
                selectedIndex = -1;
                view.selected.text = "채보에 노트가 없습니다";
                view.events.text = "";
                view.previousButton.interactable = false;
                view.nextButton.interactable = false;

                return;
            }

            selectedIndex = Math.Clamp(index, 0, result.Notes.Count - 1);
            var note = result.Notes[selectedIndex];
            view.chart.Select(selectedIndex);
            double beat = result.Tempo.BeatAtTime(note.Note.StartTime);

            if (beat < view.chart.FirstBeat || beat >= view.chart.FirstBeat + view.chart.VisibleBeats)
            {
                view.position.value = (float)Math.Max(0, beat - view.chart.VisibleBeats / 4.0);
            }

            string kind = "일반 노트";

            if (note.Note.Kind == NoteKind.Hold)
            {
                kind = "롱노트";
            }

            string state = "판정 완료";

            if (note.Judgments.Count == 0)
            {
                state = "미판정 · 플레이 종료 전 도달하지 못한 노트";
            }
            else if (note.State == NoteState.Holding || note.State == NoteState.Pending)
            {
                state = "일부 판정 후 플레이 종료";
            }

            view.selected.text = string.Format(CultureInfo.InvariantCulture,
                "노트 #{0}  ·  {1} 레인  ·  {2}\n{3:F3}초 – {4:F3}초  /  {5:0.###}박\n{6}",
                selectedIndex + 1, result.LaneLabels[note.Note.Lane], kind,
                note.Note.StartTime, note.Note.EndTime, beat, state);
            view.previousButton.interactable = selectedIndex > 0;
            view.nextButton.interactable = selectedIndex < result.Notes.Count - 1;
            RefreshEvents();
        }

        private void RefreshEvents()
        {
            if (selectedIndex < 0)
            {
                return;
            }

            var judgments = result.Notes[selectedIndex];
            var text = new StringBuilder("시작·끝 판정\n");
            AppendJudgment(text, "시작", judgments.Start);

            if (judgments.Note.Kind == NoteKind.Hold)
            {
                AppendJudgment(text, "끝", judgments.End);
            }

            text.Append("\n유지 판정은 전체 집계에만 포함됩니다.\n노트 색상은 시작·끝 중 낮은 판정입니다.");
            view.events.text = text.ToString();
        }

        private static void AppendJudgment(StringBuilder text, string label, JudgmentEvent? judgment)
        {
            text.Append(label).Append(": ");
            if (!judgment.HasValue)
            {
                text.AppendLine("미판정");
                return;
            }

            text.Append(judgment.Value.Grade).Append("  · 오차 ")
                .Append((judgment.Value.ErrorSeconds * 1000)
                    .ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)).AppendLine(" ms");
        }

        private void OnDestroy()
        {
            if (!bound || !view)
            {
                return;
            }

            view.closeButton.onClick.RemoveListener(Close);
            view.previousButton.onClick.RemoveListener(Previous);
            view.nextButton.onClick.RemoveListener(Next);
            view.position.onValueChanged.RemoveListener(PositionChanged);
            view.zoom.onValueChanged.RemoveListener(ZoomChanged);
            view.chart.NoteSelected -= SelectNote;
            view.chart.Scrolled -= Scroll;
        }
    }
}
