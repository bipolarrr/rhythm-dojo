using System;
using System.Collections.Generic;
using System.Globalization;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class ChartGameplayPreview : MonoBehaviour
    {
        public WorkspaceEditor workspace;
        public Image image;
        public PlayfieldPresenter playfield;
        public GameplayPresentationSettings presentation;
        public ScrollSettings scrollSettings;
        public NoteView tapTemplate;
        public NoteView holdTemplate;
        public Text clock;

        private readonly List<NoteView> taps = new List<NoteView>();
        private readonly List<NoteView> holds = new List<NoteView>();
        private bool initialized;
        private IScrollTimeline scroll;
        private EditorSession observed;
        private EditorNote[] notes = System.Array.Empty<EditorNote>();
        private System.Guid chartId;
        private double noteSpeedMultiplier = 1;

        public double SongTime { get; private set; }
        public int VisibleNoteCount { get; private set; }

        private void LateUpdate()
        {
            if (!workspace.menu.IsInitialized)
            {
                return;
            }
            if (!initialized)
            {
                Initialize();
            }
            if (observed != workspace.screen.Session)
            {
                if (observed != null)
                {
                    observed.Changed -= Refresh;
                }
                observed = workspace.screen.Session;
                observed.Changed += Refresh;
                Refresh();
            }
            if (chartId != workspace.screen.ChartId)
            {
                Refresh();
            }

            SongTime = workspace.timeline.MusicTime + workspace.timeline.AudioOffset;
            playfield.RenderMeasureLines(SessionState.Playing, SongTime);
            int tapCount = 0;
            int holdCount = 0;
            foreach (var note in notes)
            {
                if (note.Data.EndTime < SongTime)
                {
                    continue;
                }
                double distance = scroll.DistanceBetween(SongTime, note.Data.StartTime);
                if (GameplayViewport.Position(0, 1, distance).y > GameplayViewport.Height + 4)
                {
                    continue;
                }

                NoteView view;
                var state = NoteState.Pending;
                if (note.Data.Kind == NoteKind.Hold)
                {
                    view = Pooled(holds, holdTemplate, holdCount++);
                    if (note.Data.StartTime < SongTime)
                    {
                        state = NoteState.Holding;
                    }
                }
                else
                {
                    view = Pooled(taps, tapTemplate, tapCount++);
                }
                view.Render(note.Data, state, SongTime, scroll, workspace.screen.Mode, presentation);
            }
            HideUnused(taps, tapCount);
            HideUnused(holds, holdCount);
            VisibleNoteCount = tapCount + holdCount;
            clock.text = "미리보기 · " + workspace.timeline.MusicTime.ToString("0.000") + " s";
        }

        public void ApplyNoteSpeed(string value)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double multiplier)
                || !double.IsFinite(multiplier))
            {
                ShowNoteSpeedWarning();
                return;
            }

            if (multiplier < 1 || multiplier > 10)
            {
                ShowNoteSpeedWarning();
                return;
            }

            noteSpeedMultiplier = Math.Round(multiplier, 1, MidpointRounding.AwayFromZero);
            UpdateNoteSpeedText();
            UpdateScroll();
        }

        private void ShowNoteSpeedWarning()
        {
            UpdateNoteSpeedText();
            var warning = workspace.view.noteSpeedWarning;
            warning.transform.SetAsLastSibling();
            warning.SetActive(true);
        }

        private void UpdateNoteSpeedText()
        {
            workspace.view.noteSpeed.SetTextWithoutNotify(noteSpeedMultiplier.ToString("0.0", CultureInfo.InvariantCulture));
        }

        private void Initialize()
        {
            playfield.Build(workspace.screen.Mode, presentation);
            initialized = true;
        }

        private void Refresh()
        {
            chartId = workspace.screen.ChartId;
            notes = observed.GetNotes(chartId);
            UpdateScroll();
        }

        private void UpdateScroll()
        {
            scroll = scrollSettings.CreateTimeline(ScrollMode.Constant, null, noteSpeedMultiplier);
            if (observed != null)
            {
                var tempo = new TempoMap(observed.Snapshot.TempoPoints);
                double end = workspace.timeline.Duration + workspace.timeline.AudioOffset;
                foreach (var note in notes)
                {
                    end = Math.Max(end, note.Data.EndTime);
                }
                playfield.ConfigureMeasureLines(tempo, scroll, end);
            }
        }

        private NoteView Pooled(List<NoteView> pool, NoteView template, int index)
        {
            if (index == pool.Count)
            {
                var view = Instantiate(template, playfield.transform);
                pool.Add(view);
            }
            return pool[index];
        }

        private static void HideUnused(List<NoteView> pool, int count)
        {
            for (int i = count; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (observed != null)
            {
                observed.Changed -= Refresh;
            }
        }
    }
}
