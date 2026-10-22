using RhythmDojo.Authoring;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ChartAudioTimelineLayoutBuilder
    {
        public static ChartAudioTimelineView Build(Transform parent, RectTransform content, ChartAudioTimeline owner)
        {
            var view = owner.gameObject.AddComponent<ChartAudioTimelineView>();
            view.content = content;
            view.canvas = (RectTransform)parent;
            view.panel = AudioTimelineUi.Panel(parent, "Audio Timeline", new Color(0.075f, 0.09f, 0.12f));
            view.panel.anchorMin = Vector2.zero;
            view.panel.anchorMax = new Vector2(1, 0);
            view.panel.pivot = Vector2.zero;

            var resize = AudioTimelineUi.Panel(view.panel, "Resize Timeline", new Color(0.23f, 0.3f, 0.38f));
            AudioTimelineUi.Place(resize, 0, 0, 0, 5);
            resize.anchorMax = Vector2.one;
            resize.anchorMin = new Vector2(0, 1);
            resize.offsetMin = new Vector2(0, -5);
            resize.offsetMax = Vector2.zero;
            resize.gameObject.AddComponent<AudioTimelineResize>().Owner = owner;
            view.playAudioControl = AudioTimelineUi.Button(view.panel, "Play Audio", "재생 / Play", 12, 10, 130, null);
            view.playLabel = view.playAudioControl.GetComponentInChildren<Text>();
            view.followAudioControl = AudioTimelineUi.Button(view.panel, "Follow Audio", "재생 따라가기", 150, 10, 140, null);
            view.clock = AudioTimelineUi.Label(view.panel, "Audio Clock", "", 305, 10, 280);
            view.ruler = AudioTimelineUi.Label(view.panel, "Musical Position", "", 590, 10, 630);
            view.overview = AudioTimelineUi.Wave(view.panel, "Full Song Waveform", 12, 48, 36, owner, true);
            view.detail = AudioTimelineUi.Wave(view.panel, "Detailed Waveform", 12, 90, 72, owner, false);
            view.detail.rectTransform.anchorMin = Vector2.zero;
            view.detail.rectTransform.offsetMin = new Vector2(12, 118);
            view.bpm = AudioTimelineUi.Field(view.panel, "Grid BPM", "BPM", 12, 173, 76, "120");
            view.origin = AudioTimelineUi.Field(view.panel, "Grid Origin", "첫 박 위치 ms (+뒤로)", 96, 173, 145, "0");
            view.applyGridControl = AudioTimelineUi.Button(view.panel, "Apply Grid", "적용", 249, 192, 55, null);
            view.cursorOriginControl = AudioTimelineUi.Button(view.panel, "Cursor Origin", "그리드 수정", 310, 192, 112, null);
            view.nudge = AudioTimelineUi.Field(view.panel, "Grid Nudge", "미세 이동량", 430, 173, 75, "1");
            view.nudgeUnit = view.nudgeUnitsControl = AudioTimelineUi.Dropdown(view.panel, "Nudge Units", 512, 192, 65, new[] { "ms", "박" }, null);
            view.nudgeEarlierControl = AudioTimelineUi.Button(view.panel, "Nudge Earlier", "←", 583, 192, 36, null);
            view.nudgeLaterControl = AudioTimelineUi.Button(view.panel, "Nudge Later", "→", 625, 192, 36, null);
            view.doubleBPMControl = AudioTimelineUi.Button(view.panel, "Double BPM", "×2", 667, 192, 43, null);
            view.halfBPMControl = AudioTimelineUi.Button(view.panel, "Half BPM", "÷2", 716, 192, 43, null);
            view.gridLockControl = AudioTimelineUi.Button(view.panel, "Grid Lock", "잠금 OFF", 767, 192, 95, null);
            view.noteSnapControl = AudioTimelineUi.Dropdown(view.panel, "Note Snap", 870, 192, 90,
                new[] { "1/4", "1/8", "1/16", "1/32", "1/8T", "1/16T", "OFF" }, null);
            view.message = AudioTimelineUi.Label(view.panel, "Timeline Status", "", 12, 234, 1200);
            view.tooltip = AudioTimelineUi.Label(view.panel, "Timeline Tooltip", "", 12, 258, 1200, 16);
            view.tooltip.fontSize = 12;
            foreach (RectTransform rect in view.panel)
            {
                if (rect.anchorMin.y != 1 || rect.anchorMax.y != 1)
                {
                    continue;
                }

                if (-rect.anchoredPosition.y < 173)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(rect.anchorMin.x, 0);
                rect.anchorMax = new Vector2(rect.anchorMax.x, 0);
                rect.anchoredPosition += new Vector2(0, 280);
            }

            view.advancedGridControl = AudioTimelineUi.Button(view.panel, "Advanced Grid", "고급 GRID", 974, 192, 130, null);
            var advancedRect = (RectTransform)view.advancedGridControl.transform;
            advancedRect.anchorMin = Vector2.zero;
            advancedRect.anchorMax = Vector2.zero;
            advancedRect.anchoredPosition = new Vector2(974, 88);
            BuildAdvanced(view);
            view.panel.offsetMin = new Vector2(28, 46);
            view.panel.offsetMax = new Vector2(-28, 326);

            foreach (var button in view.panel.GetComponentsInChildren<Button>(true))
            {
                var hint = button.gameObject.AddComponent<AudioTimelineTooltip>();
                hint.Target = view.tooltip;
                hint.Caption = button.GetComponentInChildren<Text>().text;
                switch (button.name)
                {
                    case "Cursor Origin":
                        hint.Caption = "파형에서 선택한 커서 위치를 첫 마디 첫 박으로 지정합니다. 재생 중에는 버튼을 누른 순간의 재생 위치를 사용합니다.";
                        break;
                    case "Double BPM":
                    case "Half BPM":
                        hint.Caption = "모든 템포 구간의 BPM을 배수로 조정합니다.";
                        break;
                    case "Nudge Earlier":
                    case "Nudge Later":
                        hint.Caption = "그리드 첫 박을 이동합니다. +는 음원 뒤쪽, −는 앞쪽입니다.";
                        break;
                    case "Apply Tempo Segment":
                        hint.Caption = "선택한 템포 구간의 시작 시점, BPM, 박자표를 함께 적용합니다.";
                        break;
                }
            }
            view.editControls = new Selectable[]
            {
                view.bpm, view.origin, view.nudge, view.nudgeUnit, view.applyGridControl,
                view.cursorOriginControl, view.nudgeEarlierControl, view.nudgeLaterControl,
                view.doubleBPMControl, view.halfBPMControl, view.noteSnapControl,
                view.segmentTime, view.segmentBpm, view.numerator, view.denominator,
                view.addTempoSegmentControl, view.applyTempoSegmentControl, view.deleteTempoSegmentControl,
                view.alignTempoSegmentControl, view.firstBeat, view.secondBeat, view.beatCount,
                view.captureFirstBeatControl, view.captureSecondBeatControl, view.calculateBPMControl,
                view.existingNotePolicyControl, view.displayDensityControl
            };
            view.lockLabel = view.gridLockControl.GetComponentInChildren<Text>();
            return view;
        }

        private static void BuildAdvanced(ChartAudioTimelineView view)
        {
            var rect = AudioTimelineUi.Panel(view.panel, "Advanced Grid Panel", new Color(0.09f, 0.115f, 0.155f));
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 0);
            rect.offsetMin = new Vector2(0, 6);
            rect.offsetMax = new Vector2(0, 330);
            view.advanced = rect.gameObject;
            AudioTimelineUi.Label(rect, "Advanced Heading", "고급 GRID · 시간은 음원상의 초 · 첫 변경점은 채보 시간 0 고정", 16, 10, 1050);
            view.closeAdvancedControl = AudioTimelineUi.Button(rect, "Close Advanced", "닫기", 1102, 10, 110, null);
            view.segments = view.tempoSegmentsControl = AudioTimelineUi.Dropdown(rect, "Tempo Segments", 16, 48, 350, new[] { "0" }, null);
            view.segmentTime = AudioTimelineUi.Field(rect, "Segment Time", "변경점 위치 s", 16, 88, 166, "0");
            view.segmentBpm = AudioTimelineUi.Field(rect, "Segment BPM", "구간 BPM", 190, 88, 176, "120");
            view.numerator = AudioTimelineUi.Field(rect, "Meter Numerator", "박자표 분자", 16, 148, 166, "4");
            view.denominator = AudioTimelineUi.Field(rect, "Meter Denominator", "박자표 분모", 190, 148, 176, "4");
            view.addTempoSegmentControl = AudioTimelineUi.Button(rect, "Add Tempo Segment", "커서에 구간 추가", 16, 218, 170, null);
            view.applyTempoSegmentControl = AudioTimelineUi.Button(rect, "Apply Tempo Segment", "선택 구간 적용 / 이동", 194, 218, 172, null);
            view.deleteTempoSegmentControl = AudioTimelineUi.Button(rect, "Delete Tempo Segment", "선택 구간 삭제", 16, 266, 170, null);
            view.alignTempoSegmentControl = AudioTimelineUi.Button(rect, "Align Tempo Segment", "커서 = 구간 첫 박", 194, 266, 172, null);
            AudioTimelineUi.Label(rect, "BPM Calculator Heading", "두 실제 박 위치로 BPM 계산 (4분음표 기준)", 400, 48, 395);
            view.firstBeat = AudioTimelineUi.Field(rect, "First Actual Beat", "A: 실제 박 s", 400, 88, 140, "0");
            view.secondBeat = AudioTimelineUi.Field(rect, "Second Actual Beat", "B: 실제 박 s", 548, 88, 140, "1");
            view.captureFirstBeatControl = AudioTimelineUi.Button(rect, "Capture First Beat", "커서 → A", 400, 148, 140, null);
            view.captureSecondBeatControl = AudioTimelineUi.Button(rect, "Capture Second Beat", "커서 → B", 548, 148, 140, null);
            view.beatCount = AudioTimelineUi.Field(rect, "Beat Interval Count", "A–B 사이 박 간격 수", 400, 195, 180, "2");
            view.calculateBPMControl = AudioTimelineUi.Button(rect, "Calculate BPM", "BPM 계산 → 구간 입력", 400, 266, 288, null);
            AudioTimelineUi.Label(rect, "Note Policy Heading", "기존 노트 처리", 730, 48, 455);
            view.existingNotePolicyControl = AudioTimelineUi.Dropdown(rect, "Existing Note Policy", 730, 88, 465,
                new[] { "기본: 노트 음악 시간 유지", "노트 박자 유지 (노트 시간 변경)" }, null);
            AudioTimelineUi.Label(rect, "Grid Density Heading", "표시 밀도 (노트 스냅과 별도)", 730, 148, 465);
            (view.displayDensityControl = AudioTimelineUi.Dropdown(rect, "Display Density", 730, 185, 220,
                new[] { "1/4", "1/8", "1/16", "1/32", "1/8T", "1/16T" }, null)).SetValueWithoutNotify(2);
            AudioTimelineUi.Label(rect, "Note Policy Help",
                "롱노트 양 끝과 모든 채보에 동일 적용\n자동 스냅 없음 · Undo/Redo로 복원\n오디오 오프셋은 기존 설정 메뉴에서 별도 편집",
                730, 234, 465, 74);
            view.advanced.SetActive(false);
        }

    }
}
