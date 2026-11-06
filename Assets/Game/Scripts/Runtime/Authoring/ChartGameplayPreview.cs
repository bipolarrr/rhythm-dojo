using System;
using System.Collections.Generic;
using System.Globalization;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class ChartGameplayPreview : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        public WorkspaceEditor workspace;
        public RawImage image;
        public PlayfieldPresenter playfield;
        public GameplayPresentationSettings presentation;
        public ScrollSettings scrollSettings;
        public NoteView tapTemplate;
        public NoteView holdTemplate;
        public Text clock;

        private readonly List<NoteView> taps = new List<NoteView>();
        private readonly List<NoteView> holds = new List<NoteView>();
        private Camera previewCamera;
        private Camera displayCamera;
        private int displayMask;
        private RenderTexture target;
        private IScrollTimeline scroll;
        private EditorSession observed;
        private EditorNote[] notes = System.Array.Empty<EditorNote>();
        private System.Guid chartId;
        private double noteSpeedMultiplier = 1;

        public double SongTime { get; private set; }
        public int VisibleNoteCount { get; private set; }
        public RenderTexture Target => target;

        private void LateUpdate()
        {
            if (!workspace.menu.IsInitialized)
            {
                return;
            }
            if (!previewCamera)
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

            Resize();
            SongTime = workspace.timeline.MusicTime + workspace.timeline.AudioOffset;
            int tapCount = 0;
            int holdCount = 0;
            foreach (var note in notes)
            {
                if (note.Data.EndTime < SongTime)
                {
                    continue;
                }
                if (scroll.DistanceBetween(SongTime, note.Data.StartTime) > presentation.highwayLength)
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
            SetLayer(playfield.gameObject);
            previewCamera = new GameObject("Editor Preview Camera").AddComponent<Camera>();
            previewCamera.transform.SetParent(playfield.transform, false);
            previewCamera.transform.position = new Vector3(0, 10, -10);
            previewCamera.transform.LookAt(new Vector3(0, 0, 9));
            previewCamera.fieldOfView = 52;
            previewCamera.farClipPlane = 180;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.025f, .035f, .065f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.depth = -50;
            displayCamera = Camera.main;
            if (displayCamera)
            {
                displayMask = displayCamera.cullingMask;
                displayCamera.cullingMask &= ~(1 << PreviewLayer);
            }
            var light = new GameObject("Editor Preview Light").AddComponent<Light>();
            light.transform.SetParent(playfield.transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.cullingMask = 1 << PreviewLayer;
            light.transform.rotation = Quaternion.Euler(50, -25, 0);
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
        }

        private void Resize()
        {
            var canvas = image.GetComponentInParent<Canvas>();
            int width = Mathf.Clamp(Mathf.RoundToInt(image.rectTransform.rect.width * canvas.scaleFactor), 64, 1024);
            int height = Mathf.Clamp(Mathf.RoundToInt(image.rectTransform.rect.height * canvas.scaleFactor), 64, 1024);
            if (target && target.width == width && target.height == height)
            {
                return;
            }
            if (target)
            {
                target.Release();
                Destroy(target);
            }
            target = new RenderTexture(width, height, 24);
            target.name = "Editor Gameplay Preview";
            target.Create();
            previewCamera.targetTexture = target;
            image.texture = target;
        }

        private NoteView Pooled(List<NoteView> pool, NoteView template, int index)
        {
            if (index == pool.Count)
            {
                var view = Instantiate(template, playfield.transform);
                SetLayer(view.gameObject);
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

        private static void SetLayer(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = PreviewLayer;
            }
        }

        private void OnDestroy()
        {
            if (observed != null)
            {
                observed.Changed -= Refresh;
            }
            if (displayCamera)
            {
                displayCamera.cullingMask = displayMask;
            }
            if (previewCamera)
            {
                previewCamera.targetTexture = null;
            }
            if (target)
            {
                target.Release();
                Destroy(target);
            }
            if (playfield)
            {
                Destroy(playfield.gameObject);
            }
        }
    }
}
