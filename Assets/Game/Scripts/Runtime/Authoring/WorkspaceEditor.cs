using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public sealed class WorkspaceEditor : MonoBehaviour
    {
        public const string ScenePath = ChartEditorMenu.ScenePath;
        private const double OnAirHoldMinimumSeconds = .15;
        private const string HiddenRecentProjectKeyPrefix = "RhythmDojo.Workspace.HiddenRecent.";
        public WorkspaceView view;
        public ChartEditorScreen screen;
        public ChartEditorMenu menu;
        public ChartAudioTimeline timeline;
        public ChartBeatGrid grid;

        [Serializable]
        public sealed class ResourcesSettings
        {
            public string jacket;
            public string sound;
            public float volume = 65;
        }

        private readonly WorkspaceNoteEditing editing = new WorkspaceNoteEditing();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private EditorSession observed;
        private Guid chartId;
        private EditorNote[] cachedNotes = Array.Empty<EditorNote>();
        private Guid[] recentIds = Array.Empty<Guid>();
        private ResourcesSettings resources = new ResourcesSettings();
        private bool resourcesDirty;
        private bool working;
        private bool looping;
        private bool previousLoopPlaying;
        private bool draggingSelection;
        private bool draggingScroll;
        private int scrollPointerId;
        private bool movingNote;
        private bool grabMoved;
        private int grabPointerId;
        private EditorNote grabbedNote;
        private EditorNote[] grabbedNotes = Array.Empty<EditorNote>();
        private Guid grabbedChartId;
        private EditorSession grabbedSession;
        private double grabBeatOffset;
        private int grabLane;
        private double grabTime;
        private Vector2 selectionStart;
        private int tool;
        private Texture2D jacketTexture;
        private SongAudioLease soundLease;
        private AudioSource soundSource;
        private double previousPreviewTime;
        private static Guid returningProject;
        private static string returningResources;
        private static bool returningResourcesDirty;
        private int resourceVersion;

        private int bpmAnalysisVersion;
        private double? estimatedBpm;
        private string estimatedAudio;

        private ColorBlock[] difficultyColors;

        private EditorSession onAirSession;
        private Guid onAirChart;
        private AudioClip onAirClip;
        private Key[] onAirKeys;
        private double?[] onAirPressTimes;
        private Keyboard onAirKeyboard;
        private readonly List<NoteData> onAirNotes = new List<NoteData>();
        private readonly List<long> onAirRecordedIds = new List<long>();
        private Coroutine onAirQuantization;
        private double onAirStart;
        private double onAirDeadline;
        private bool onAirRecording;
        private EventSystem onAirEventSystem;
        private bool onAirNavigation;

        private bool IsOnAirPromptOpen => view.onAirPopup && view.onAirPopup.activeSelf;

        public bool IsWorking => working || onAirQuantization != null;

        public WorkspaceNoteEditing Editing => editing;
        public bool HasUnsavedChanges => screen.Session.IsDirty || resourcesDirty;

        private IEnumerator Start()
        {
            while (!menu.IsInitialized)
            {
                yield return null;
            }

            soundSource = gameObject.AddComponent<AudioSource>();
            difficultyColors = Array.ConvertAll(view.difficulty, button => button.colors);
            soundSource.playOnAwake = false;
            grid.SelectedNoteIds = editing.Selected;
            screen.PointerTool = PointerDown;
            screen.DragTool = PointerDrag;
            screen.ReleaseTool = PointerUp;
            foreach (var binding in view.actions)
            {
                var action = binding.action;
                binding.button.onClick.AddListener(() => Run(action));
            }

            foreach (var binding in view.toolActions)
            {
                var action = binding.action;
                binding.button.onClick.AddListener(() => Run(action));
            }
            RefreshTools();

            view.quantization.onValueChanged.AddListener(SetQuantization);
            view.quantizationCycle.onClick.AddListener(CycleQuantization);
            view.title.onEndEdit.AddListener(_ => ApplyMetadata());
            view.artist.onEndEdit.AddListener(_ => ApplyMetadata());
            view.jacketPath.onEndEdit.AddListener(value => ImportPath(value, WorkspaceAction.ImportJacket));
            view.audioPath.onEndEdit.AddListener(value => ImportPath(value, WorkspaceAction.ImportAudio));
            var timelineView = timeline.GetComponent<ChartAudioTimelineView>();
            view.bpm.onValueChanged.AddListener(value => timelineView.bpm.SetTextWithoutNotify(value));
            timelineView.bpm.onValueChanged.AddListener(value => view.bpm.SetTextWithoutNotify(value));
            view.bpm.onEndEdit.AddListener(ApplyBpm);
            timelineView.bpm.onEndEdit.AddListener(ApplyBpm);
            view.volumeControl.onValueChanged.AddListener(ApplyVolume);
            timelineView.noteSnapControl.SetValueWithoutNotify(view.quantization.value);
            timelineView.noteSnapControl.onValueChanged.AddListener(SetQuantization);
            SetQuantization(view.quantization.value);
            Observe();
            RefreshRecent();
            view.exitPopup.SetActive(false);
            view.newPopup.SetActive(false);
            view.saveAsPopup.SetActive(false);
            InputSystem.onAfterUpdate += PrepareShortcutInput;
        }

        private void Update()
        {
            if (!menu.IsInitialized || observed == null)
            {
                return;
            }

            if (UpdateOnAir())
            {
                return;
            }

            if (observed != screen.Session)
            {
                Observe();
            }

            if (draggingScroll)
            {
                if (working || screen.IsBusy || !timeline.IsSeeking)
                {
                    EndPlaybackSeek();
                }
                else if (!screen.isActiveAndEnabled || !grid.isActiveAndEnabled)
                {
                    EndPlaybackSeek();
                }
            }

            if (chartId != screen.ChartId)
            {
                chartId = screen.ChartId;
                RefreshNoteCache();
                editing.Selected.Clear();
            }

            view.selection.text = "선택 " + editing.Selected.Count + " / "
                + cachedNotes.Length + " · 도구 "
                + new[] { "그리기", "선택", "지우개", "잡기", "재생 위치" }[tool];
            if (view.playback)
            {
                view.playback.text = "속도 " + (timeline.PreviewSpeed * 100).ToString("0") + "% · 루프 "
                    + looping.ToString() + " · " + timeline.MusicTime.ToString("0.000") + " s";
            }

            bool mayUpdateLoop = looping && !working;
            if (timeline.IsBusy || timeline.IsSeeking)
            {
                mayUpdateLoop = false;
            }
            if (mayUpdateLoop)
            {
                double end = timeline.Duration;
                if (double.TryParse(view.loopEnd?.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double entered))
                {
                    end = entered;
                }

                bool mayLoop = timeline.IsPlaying;
                if (!mayLoop && previousLoopPlaying && timeline.MusicTime >= timeline.Duration)
                {
                    mayLoop = true;
                }
                if (mayLoop && end > 0 && timeline.MusicTime >= end)
                {
                    if (!double.TryParse(view.loopStart?.text ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out double loopStart))
                    {
                        timeline.PausePreview();
                        looping = false;
                        return;
                    }
                    if (loopStart < 0 || loopStart >= end)
                    {
                        timeline.PausePreview();
                        looping = false;
                        return;
                    }
                    timeline.RestartPreviewLoop(loopStart);
                }
            }
            if (!timeline.IsSeeking)
            {
                previousLoopPlaying = timeline.IsPlaying;
            }

            if (soundLease != null && timeline.IsPlaying)
            {
                double time = timeline.MusicTime + timeline.AudioOffset;
                if (time >= previousPreviewTime)
                {
                    foreach (var note in cachedNotes)
                    {
                        if (note.Data.StartTime > previousPreviewTime && note.Data.StartTime <= time)
                        {
                            soundSource.PlayOneShot(soundLease.Clip, resources.volume / 100f);
                            break;
                        }
                    }
                }

                previousPreviewTime = time;
            }
            else
            {
                previousPreviewTime = timeline.MusicTime + timeline.AudioOffset;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || working)
            {
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
                return;
            }
            if (HandlePopupKeyboard(keyboard) || screen.IsBusy)
            {
                return;
            }

            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
            {
                var field = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
                if (field)
                {
                    if (field == view.noteSpeed && field.isFocused)
                    {
                        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        {
                            field.DeactivateInputField();
                            EventSystem.current.SetSelectedGameObject(null);
                        }
                    }

                    return;
                }
            }

            if (WorkspaceShortcuts.TryGetSlot(keyboard, out int slot))
            {
                EventSystem.current?.SetSelectedGameObject(null);
                screen.SelectSlot(slot);
                return;
            }
            if (WorkspaceShortcuts.TryGetAction(keyboard, out var action))
            {
                EventSystem.current?.SetSelectedGameObject(null);
                Run(action);
            }
        }

        private void ShowOnAirPrompt()
        {
            if (!view.onAirPopup || timeline.IsBusy)
            {
                return;
            }

            if (!timeline.PreviewClip || timeline.MusicTime >= timeline.Duration)
            {
                view.status.text = "ON AIR를 시작하려면 음원을 불러오고 재생할 위치를 선택하세요.";
                return;
            }

            screen.IsBusy = true;
            view.onAirPopup.transform.SetAsLastSibling();
            view.onAirPopup.SetActive(true);
        }

        private bool HandleOnAirAction(WorkspaceAction action)
        {
            if (onAirQuantization != null)
            {
                return true;
            }

            if (onAirSession != null)
            {
                if (action == WorkspaceAction.OnAir)
                {
                    FinishOnAir();
                }

                return true;
            }

            if (!IsOnAirPromptOpen)
            {
                return action == WorkspaceAction.ConfirmOnAir || action == WorkspaceAction.CancelOnAir;
            }

            if (action == WorkspaceAction.ConfirmOnAir)
            {
                CloseOnAirPrompt();
                BeginOnAir();
            }
            else if (action == WorkspaceAction.CancelOnAir || action == WorkspaceAction.OnAir)
            {
                CloseOnAirPrompt();
            }

            return true;
        }

        private void CloseOnAirPrompt()
        {
            view.onAirPopup.SetActive(false);
            screen.IsBusy = false;
        }

        private void BeginOnAir()
        {
            if (timeline.IsBusy || !timeline.PreviewClip || timeline.MusicTime >= timeline.Duration)
            {
                return;
            }

            EndPlaybackSeek();
            CancelGrab();
            draggingSelection = false;
            timeline.PausePreview();
            timeline.UseOnAirPageFollow = true;
            onAirStart = timeline.MusicTime;
            onAirRecordedIds.Clear();
            onAirSession = screen.Session;
            onAirChart = screen.ChartId;
            onAirClip = timeline.PreviewClip;
            var mode = screen.Mode;
            onAirKeys = new Key[mode.LaneCount];
            onAirPressTimes = new double?[mode.LaneCount];
            for (int lane = 0; lane < onAirKeys.Length; lane++)
            {
                onAirKeys[lane] = PlayKeyBindings.GetKey(mode, mode.name, lane);
            }

            onAirEventSystem = EventSystem.current;
            if (onAirEventSystem)
            {
                onAirNavigation = onAirEventSystem.sendNavigationEvents;
                onAirEventSystem.SetSelectedGameObject(null);
                onAirEventSystem.sendNavigationEvents = false;
            }

            screen.IsEditingLocked = true;
            onAirDeadline = Time.realtimeSinceStartupAsDouble + 3;
            view.onAirCountdown.text = "3";
            view.onAirCountdown.gameObject.SetActive(true);
            view.onAirIcon.color = new Color(.85f, .48f, .12f);
            view.status.text = "ON AIR 준비 · Esc 또는 F8로 취소";
        }

        private bool UpdateOnAir()
        {
            if (onAirQuantization != null)
            {
                return true;
            }

            var keyboard = Keyboard.current;
            if (IsOnAirPromptOpen)
            {
                if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.f8Key.wasPressedThisFrame))
                {
                    CloseOnAirPrompt();
                }

                return true;
            }

            if (onAirSession == null)
            {
                return false;
            }

            if (screen.Session != onAirSession || screen.ChartId != onAirChart || timeline.PreviewClip != onAirClip)
            {
                FinishOnAir();
                return true;
            }

            if (keyboard != null)
            {
                bool stopWithF8 = keyboard.f8Key.wasPressedThisFrame
                    && (!onAirRecording || Array.IndexOf(onAirKeys, Key.F8) < 0);
                if (keyboard.escapeKey.wasPressedThisFrame || stopWithF8)
                {
                    FinishOnAir();
                    return true;
                }
            }

            if (!onAirRecording)
            {
                double remaining = onAirDeadline - Time.realtimeSinceStartupAsDouble;
                if (remaining > 0)
                {
                    view.onAirCountdown.text = Math.Ceiling(remaining).ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                timeline.SeekInViewport(onAirStart);
                timeline.PlayPreview();
                onAirRecording = true;
                view.onAirCountdown.gameObject.SetActive(false);
                view.onAirIcon.color = new Color(.83f, .12f, .15f);
                view.status.text = "● ON AIR · Esc 또는 ON AIR 버튼으로 종료";
            }
            else if (!timeline.IsPlaying || timeline.MusicTime >= timeline.Duration)
            {
                FinishOnAir();
            }
            else
            {
                RecordOnAirNotes(keyboard);
            }

            return true;
        }

        private void RecordOnAirNotes(Keyboard keyboard)
        {
            if (onAirKeyboard != keyboard)
            {
                Array.Clear(onAirPressTimes, 0, onAirPressTimes.Length);
                onAirKeyboard = keyboard;
            }

            if (keyboard == null)
            {
                return;
            }

            double time = timeline.MusicTime + timeline.AudioOffset;
            onAirNotes.Clear();
            for (int lane = 0; lane < onAirKeys.Length; lane++)
            {
                var key = onAirKeys[lane];
                if (key == Key.None)
                {
                    continue;
                }

                var control = keyboard[key];
                if (time >= 0 && control.wasPressedThisFrame && !onAirPressTimes[lane].HasValue)
                {
                    onAirPressTimes[lane] = time;
                }

                if (!control.wasReleasedThisFrame || !onAirPressTimes[lane].HasValue)
                {
                    continue;
                }

                double start = onAirPressTimes[lane].Value;
                onAirPressTimes[lane] = null;
                if (time >= 0 && control.isPressed)
                {
                    onAirPressTimes[lane] = time;
                }

                if (time < start)
                {
                    continue;
                }

                var recorded = NoteData.Tap(lane, start);
                if (time - start >= OnAirHoldMinimumSeconds)
                {
                    recorded = NoteData.Hold(lane, start, time);
                }

                int pressedLane = lane;
                if (!cachedNotes.Any(note => note.Data.Lane == pressedLane
                    && note.Data.StartTime <= recorded.EndTime && note.Data.EndTime >= recorded.StartTime))
                {
                    onAirNotes.Add(recorded);
                }
            }

            if (onAirNotes.Count == 0)
            {
                return;
            }

            var chart = onAirSession.Snapshot.Charts.Single(item => item.Id == onAirChart);
            var addedIds = new List<long>(onAirNotes.Count);
            onAirSession.Edit(edit =>
            {
                double lastNoteEnd = onAirNotes.Max(note => note.EndTime);
                if (lastNoteEnd >= chart.CompletionTimeSeconds)
                {
                    double end = timeline.Timing.TimeAtBeat(timeline.Timing.BeatAtTime(lastNoteEnd) + 1);
                    edit.SetChartSettings(chart.Id, chart.ModeId, chart.LaneCount, end);
                }

                foreach (var note in onAirNotes)
                {
                    addedIds.Add(edit.AddNote(chart.Id, note));
                }
            });
            onAirRecordedIds.AddRange(addedIds);
        }

        private void FinishOnAir()
        {
            var session = onAirSession;
            screen.IsEditingLocked = false;
            if (timeline)
            {
                timeline.UseOnAirPageFollow = false;
                if (timeline.PreviewClip == onAirClip)
                {
                    timeline.PausePreview();
                    timeline.Seek(onAirStart);
                }
            }

            if (onAirEventSystem)
            {
                onAirEventSystem.SetSelectedGameObject(null);
                onAirEventSystem.sendNavigationEvents = onAirNavigation;
            }

            onAirEventSystem = null;
            onAirSession = null;
            onAirClip = null;
            onAirKeys = null;
            onAirPressTimes = null;
            onAirKeyboard = null;
            onAirRecording = false;
            view.CloseMenus();
            view.onAirCountdown.gameObject.SetActive(false);
            view.onAirIcon.color = new Color(.16f, .16f, .16f);
            view.status.text = "ON AIR 종료";

            if (!isActiveAndEnabled || session != screen.Session || screen.IsBusy)
            {
                return;
            }

            if (screen.SnapEnabled && onAirRecordedIds.Count > 0)
            {
                screen.IsBusy = true;
                view.SetOnAirQuantizing(true);
                onAirQuantization = StartCoroutine(QuantizeOnAir(session, onAirChart, screen.SnapBeats));
            }
        }

        private IEnumerator QuantizeOnAir(EditorSession session, Guid chart, double step)
        {
            // Give the modal a rendered frame before changing the chart.
            yield return null;
            yield return null;

            try
            {
                int skipped = editing.QuantizeRecordedNotes(session, chart, onAirRecordedIds, step);
                view.status.text = "ON AIR 종료 · 퀀타이즈 완료";
                if (skipped > 0)
                {
                    view.status.text += $" · 겹치는 노트 {skipped}개는 원래 위치 유지";
                }
            }
            catch (Exception error)
            {
                view.status.text = "ON AIR 퀀타이즈 실패 · " + error.Message;
            }
            finally
            {
                CloseOnAirQuantization();
            }
        }

        private void CloseOnAirQuantization()
        {
            onAirQuantization = null;
            view.SetOnAirQuantizing(false);
            screen.IsBusy = false;
        }

        private void PrepareShortcutInput()
        {
            if (!isActiveAndEnabled || onAirSession != null || onAirQuantization != null)
            {
                return;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.enterKey.wasPressedThisFrame)
            {
                return;
            }
            if (!WorkspaceShortcuts.TryGetAction(keyboard, out var action))
            {
                return;
            }
            if (action != WorkspaceAction.ApplySettings)
            {
                return;
            }
            if (view.saveAsPopup.activeSelf || view.newPopup.activeSelf)
            {
                return;
            }
            if (view.exitPopup.activeSelf || !EventSystem.current)
            {
                return;
            }
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected && !selected.GetComponent<InputField>())
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void HandleEscape()
        {
            if (view.IsProjectListOpen)
            {
                Run(WorkspaceAction.CloseProjectList);
                return;
            }

            if (view.IsNoteSpeedWarningOpen)
            {
                Run(WorkspaceAction.CloseNoteSpeedWarning);
                return;
            }

            if (timeline.IsTempoChangeOpen)
            {
                timeline.CloseTempoChange();
                return;
            }

            if (draggingScroll)
            {
                EndPlaybackSeek();
                return;
            }
            if (movingNote)
            {
                CancelGrab();
                return;
            }
            if (view.saveAsPopup.activeSelf)
            {
                Run(WorkspaceAction.CancelSaveAs);
                return;
            }
            if (view.newPopup.activeSelf)
            {
                view.newPopup.SetActive(false);
                screen.IsBusy = false;
                return;
            }
            if (view.exitPopup.activeSelf)
            {
                Run(WorkspaceAction.CancelExit);
                return;
            }
            if (view.HasOpenMenu)
            {
                view.CloseMenus();
                return;
            }
            if (menu.ClosePanels())
            {
                return;
            }
            foreach (var dropdown in view.GetComponentsInChildren<Dropdown>())
            {
                var list = dropdown.transform.Find("Dropdown List");
                if (list && list.gameObject.activeInHierarchy)
                {
                    dropdown.Hide();
                    return;
                }
            }
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
            {
                var field = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
                if (field && field.gameObject.activeInHierarchy)
                {
                    field.DeactivateInputField();
                    EventSystem.current.SetSelectedGameObject(null);
                    return;
                }
            }
            if (!screen.IsBusy)
            {
                Run(WorkspaceAction.Exit);
            }
        }

        private bool HandlePopupKeyboard(Keyboard keyboard)
        {
            if (view.IsNoteSpeedWarningOpen || view.IsProjectListOpen)
            {
                return true;
            }

            if (timeline.IsTempoChangeOpen)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    var selected = EventSystem.current?.currentSelectedGameObject;
                    if (selected && selected.GetComponent<Button>())
                    {
                        return true;
                    }

                    timeline.ApplyTempoChange();
                }

                return true;
            }

            WorkspaceAction action;
            if (view.saveAsPopup.activeSelf)
            {
                action = WorkspaceAction.ConfirmSaveAs;
            }
            else if (view.newPopup.activeSelf)
            {
                action = WorkspaceAction.SaveNew;
            }
            else if (view.exitPopup.activeSelf)
            {
                action = WorkspaceAction.SaveExit;
            }
            else
            {
                return false;
            }

            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                return true;
            }
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected.activeInHierarchy && selected.GetComponent<Button>())
                {
                    return true;
                }
            }
            Run(action);
            return true;
        }

        private void RefreshNoteCache()
        {
            cachedNotes = screen.Session.GetNotes(screen.ChartId);
        }

        private void Observe()
        {
            EndPlaybackSeek();
            ClearBpmSuggestion();

            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
                observed.Changed -= RefreshNoteCache;
            }

            observed = screen.Session;
            observed.Changed += RefreshDifficulty;
            observed.Changed += RefreshBpm;
            observed.Changed += RefreshNoteCache;
            RefreshNoteCache();
            chartId = screen.ChartId;
            editing.Selected.Clear();
            resources = new ResourcesSettings();
            string path = Path.Combine(menu.Store.GetSongFolder(observed.Snapshot.Id), "workspace.json");
            if (File.Exists(path))
            {
                try
                {
                    resources = JsonUtility.FromJson<ResourcesSettings>(File.ReadAllText(path)) ?? new ResourcesSettings();
                }
                catch (Exception error)
                {
                    view.status.text = "리소스 설정을 읽지 못했습니다: " + error.Message;
                }
            }

            resourcesDirty = false;
            if (returningProject == observed.Snapshot.Id && returningResources != null)
            {
                resources = JsonUtility.FromJson<ResourcesSettings>(returningResources);
                resourcesDirty = returningResourcesDirty;
                returningResources = null;
            }
            FillSettings();
            RefreshDifficulty();
            _ = LoadResources();
        }

        private void FillSettings()
        {
            var project = observed.Snapshot;
            view.title.text = project.Title;
            view.artist.text = project.Artist;
            view.bpm.text = project.TempoPoints[0].Bpm.ToString("R", CultureInfo.InvariantCulture);
            view.offset.text = (project.AudioOffsetSeconds * 1000).ToString("R", CultureInfo.InvariantCulture);
            RefreshVolume();
            view.saveAsName.text = project.Title;
            view.audioPath.text = "";
            view.jacketPath.text = "";
            if (project.Audio != null)
            {
                view.audioPath.text = menu.Store.ResolveAudioPath(project.Id, project.Audio);
            }

            timeline.PreviewVolume = resources.volume / 100f;
        }

        private void RefreshBpm()
        {
            if (estimatedAudio != screen.Session.Snapshot.Audio)
            {
                ClearBpmSuggestion();
            }

            string value = screen.Session.Snapshot.TempoPoints[0].Bpm.ToString("R", CultureInfo.InvariantCulture);
            view.bpm.SetTextWithoutNotify(value);
            timeline.GetComponent<ChartAudioTimelineView>().bpm.SetTextWithoutNotify(value);
        }

        private void ApplyBpm(string value)
        {
            if (!this || working || screen.IsEditingBlocked)
            {
                return;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm))
            {
                view.status.text = "BPM은 양수를 입력하세요.";
                RefreshBpm();
                return;
            }

            if (bpm <= 0 || double.IsNaN(bpm) || double.IsInfinity(bpm))
            {
                view.status.text = "BPM은 양수를 입력하세요.";
                RefreshBpm();
                return;
            }

            var project = screen.Session.Snapshot;
            if (project.TempoPoints[0].Bpm == bpm)
            {
                return;
            }

            var points = project.TempoPoints;
            points[0] = new TempoPoint(0, bpm);
            screen.Session.Edit(edit => edit.SetTiming(project.AudioOffsetSeconds, points));
        }

        private void ApplyVolume(float value)
        {
            if (working || screen.IsBusy)
            {
                RefreshVolume();
                return;
            }

            resources.volume = Mathf.Round(Mathf.Clamp(value, 0, 100));
            resourcesDirty = true;
            timeline.PreviewVolume = resources.volume / 100f;
            RefreshVolume();
        }

        private void RefreshVolume()
        {
            view.volumeControl.SetValueWithoutNotify(resources.volume);
            view.volume.text = resources.volume.ToString("0", CultureInfo.InvariantCulture);
            view.volume.textComponent.text = view.volume.text + "%";
            view.volumeControl.fillRect.GetComponent<Image>().color = new Color(.6f, .64f, .7f);
        }

        private void ApplyMetadata()
        {
            if (!this || !screen || !menu)
            {
                return;
            }

            if (working || screen.IsEditingBlocked || !menu.IsInitialized)
            {
                return;
            }

            var project = screen.Session.Snapshot;
            if (project.Title == view.title.text && project.Artist == view.artist.text)
            {
                return;
            }

            screen.Session.Edit(edit => edit.SetMetadata(view.title.text, view.artist.text));
            view.status.text = "곡 제목과 작곡가를 반영했습니다. 저장하면 파일에 기록됩니다.";
        }

        private void ImportPath(string value, WorkspaceAction action)
        {
            if (!this)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Run(action, false);
        }

        public void Run(WorkspaceAction action, bool closeMenus = true)
        {
            Run(action, closeMenus, Guid.Empty);
        }

        private async void Run(WorkspaceAction action, bool closeMenus, Guid listedProjectId)
        {
            if (HandleOnAirAction(action))
            {
                return;
            }

            if (view.IsNoteSpeedWarningOpen)
            {
                if (action == WorkspaceAction.CloseNoteSpeedWarning)
                {
                    view.noteSpeedWarning.SetActive(false);
                }

                return;
            }

            if (working || !menu.IsInitialized)
            {
                return;
            }

            bool projectListAction = action == WorkspaceAction.CloseProjectList ||
                action == WorkspaceAction.OpenListedProject;
            if (view.IsProjectListOpen != projectListAction)
            {
                return;
            }

            if (view.saveAsPopup.activeSelf)
            {
                if (action == WorkspaceAction.CancelSaveAs)
                {
                    view.saveAsPopup.SetActive(false);
                    screen.IsBusy = false;
                    return;
                }

                if (action != WorkspaceAction.ConfirmSaveAs)
                {
                    return;
                }
            }
            else if (action == WorkspaceAction.ConfirmSaveAs || action == WorkspaceAction.CancelSaveAs)
            {
                return;
            }

            if (view.newPopup.activeSelf)
            {
                if (action != WorkspaceAction.SaveNew && action != WorkspaceAction.DiscardNew)
                {
                    return;
                }
            }
            else if (action == WorkspaceAction.SaveNew || action == WorkspaceAction.DiscardNew)
            {
                return;
            }

            if (view.exitPopup.activeSelf)
            {
                if (action == WorkspaceAction.CancelExit)
                {
                    view.exitPopup.SetActive(false);
                    screen.IsBusy = false;
                    return;
                }

                if (action == WorkspaceAction.DiscardExit)
                {
                    SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
                    return;
                }

                if (action != WorkspaceAction.SaveExit)
                {
                    return;
                }
            }

            bool confirmationAction = action == WorkspaceAction.SaveNew || action == WorkspaceAction.DiscardNew;
            if (action == WorkspaceAction.ConfirmSaveAs)
            {
                confirmationAction = true;
            }
            if (screen.IsBusy && action != WorkspaceAction.SaveExit && !confirmationAction)
            {
                return;
            }

            if (view.menus[1].panel.activeSelf)
            {
                ApplyMetadata();
            }

            working = true;
            try
            {
                if (closeMenus)
                {
                    view.CloseMenus();
                }

                await Execute(action, listedProjectId);
                if (this)
                {
                    grid.SetVerticesDirty();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                if (this)
                {
                    view.status.text = error.Message;
                    if (view.saveAsPopup.activeSelf)
                    {
                        view.saveAsError.text = error.Message;
                    }
                    if (view.IsProjectListOpen)
                    {
                        view.projectListStatus.text = error.Message;
                    }
                }
            }
            finally
            {
                working = false;
                if (this)
                {
                    RefreshTools();
                }
            }
        }

        private void RefreshTools()
        {
            if (tool != 4)
            {
                EndPlaybackSeek();
            }

            WorkspaceAction action;
            switch (tool)
            {
                case 1:
                    action = WorkspaceAction.Select;
                    break;
                case 2:
                    action = WorkspaceAction.Eraser;
                    break;
                case 3:
                    action = WorkspaceAction.Position;
                    break;
                case 4:
                    action = WorkspaceAction.Scroll;
                    break;
                default:
                    action = WorkspaceAction.Pencil;
                    break;
            }

            foreach (var binding in view.toolActions)
            {
                var colors = binding.button.colors;
                colors.normalColor = new Color(.94f, .94f, .94f);
                colors.highlightedColor = new Color(.78f, .87f, .96f);
                colors.pressedColor = new Color(.65f, .79f, .93f);
                colors.selectedColor = colors.normalColor;
                if (binding.action == action)
                {
                    colors.normalColor = new Color(.6f, .64f, .7f);
                    colors.highlightedColor = new Color(.53f, .59f, .67f);
                    colors.pressedColor = new Color(.45f, .51f, .6f);
                    colors.selectedColor = colors.normalColor;
                }
                binding.button.colors = colors;
            }
        }

        private async Task Execute(WorkspaceAction action, Guid listedProjectId)
        {
            var session = screen.Session;
            switch (action)
            {
                case WorkspaceAction.ShowProjectList:
                    ShowProjectList();
                    break;
                case WorkspaceAction.CloseProjectList:
                    view.projectListPopup.SetActive(false);
                    break;
                case WorkspaceAction.OpenListedProject:
                    await OpenProject(listedProjectId);
                    view.projectListPopup.SetActive(false);
                    break;
                case WorkspaceAction.ProjectFiles:
                    menu.ToggleFile();
                    break;
                case WorkspaceAction.ChartSettings:
                    menu.ToggleSettings();
                    break;
                case WorkspaceAction.ResetView:
                    menu.ResetView();
                    break;
                case WorkspaceAction.New:
                    view.CloseMenus();
                    if (HasUnsavedChanges)
                    {
                        view.newPopup.SetActive(true);
                        view.newPopup.transform.SetAsLastSibling();
                        screen.IsBusy = true;
                        break;
                    }

                    CreateNew();
                    break;
                case WorkspaceAction.SaveNew:
                    await Save();
                    CreateNew();
                    break;
                case WorkspaceAction.DiscardNew:
                    CreateNew();
                    break;
                case WorkspaceAction.Open:
                    if (recentIds.Length == 0)
                    {
                        throw new InvalidOperationException("저장된 채보가 없습니다.");
                    }
                    Guid id = recentIds[view.recent.value];
                    await OpenProject(id);
                    break;
                case WorkspaceAction.OpenByProjectId:
                    if (!Guid.TryParse(view.openProjectId.text, out Guid projectId) || projectId == Guid.Empty)
                    {
                        throw new InvalidOperationException("불러올 채보의 전체 프로젝트 ID를 입력하세요.");
                    }

                    await OpenProject(projectId);
                    view.openProjectId.SetTextWithoutNotify("");
                    break;
                case WorkspaceAction.Save:
                    await Save();
                    break;
                case WorkspaceAction.SaveAs:
                    view.saveAsName.text = session.Snapshot.Title;
                    view.saveAsError.text = "";
                    view.saveAsPopup.SetActive(true);
                    view.saveAsPopup.transform.SetAsLastSibling();
                    screen.IsBusy = true;
                    view.saveAsName.Select();
                    view.saveAsName.ActivateInputField();
                    break;
                case WorkspaceAction.ConfirmSaveAs:
                    if (string.IsNullOrWhiteSpace(view.saveAsName.text))
                    {
                        view.saveAsError.text = "이름을 입력하세요.";
                        view.saveAsName.ActivateInputField();
                        break;
                    }

                    await SaveAs();
                    view.saveAsPopup.SetActive(false);
                    screen.IsBusy = false;
                    break;
                case WorkspaceAction.Exit:
                    if (HasUnsavedChanges)
                    {
                        view.exitPopup.SetActive(true);
                        screen.IsBusy = true;
                        view.exitPopup.transform.SetAsLastSibling();
                    }
                    else
                    {
                        SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
                    }
                    break;
                case WorkspaceAction.SaveExit:
                    await Save();
                    SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
                    break;
                case WorkspaceAction.ApplySettings:
                    double bpm = Number(view.bpm);
                    if (bpm <= 0)
                    {
                        throw new ArgumentException("BPM은 양수여야 합니다.");
                    }
                    double volume = Number(view.volume);
                    if (volume < 0 || volume > 100)
                    {
                        throw new ArgumentException("볼륨 범위는 0–100%입니다.");
                    }
                    var points = session.Snapshot.TempoPoints;
                    points[0] = new TempoPoint(0, bpm);
                    double offset = Number(view.offset) / 1000;
                    session.Edit(edit =>
                    {
                        edit.SetMetadata(view.title.text, view.artist.text);
                        edit.SetTiming(offset, points);
                    });
                    resources.volume = (float)volume;
                    resourcesDirty = true;
                    timeline.PreviewVolume = resources.volume / 100f;
                    view.status.text = "메타 정보와 오디오 설정을 적용했습니다.";
                    break;
                case WorkspaceAction.ImportAudio:
                    ClearBpmSuggestion();
                    string audio = PathText(view.audioPath);
                    string extension = Path.GetExtension(audio).ToLowerInvariant();
                    if (!new[] { ".wav", ".ogg", ".mp3" }.Contains(extension))
                    {
                        throw new ArgumentException("WAV / OGG / MP3 파일을 입력하세요.");
                    }
                    string relative = await menu.Store.ImportAsync(session.Snapshot.Id, audio, lifetime.Token);
                    session.Edit(edit => edit.SetAudio(relative));
                    view.status.text = "음원을 적용했습니다.";
                    await AutoEstimateBpm();
                    break;
                case WorkspaceAction.ImportJacket:
                    await ImportResources();
                    break;
                case WorkspaceAction.EstimateBpm:
                    await EstimateBpm();
                    break;
                case WorkspaceAction.ApplyEstimatedBpm:
                    if (estimatedBpm.HasValue && estimatedAudio == session.Snapshot.Audio)
                    {
                        var project = session.Snapshot;
                        var suggestedPoints = project.TempoPoints;
                        suggestedPoints[0] = new TempoPoint(0, estimatedBpm.Value);
                        session.Edit(edit => edit.SetTiming(project.AudioOffsetSeconds, suggestedPoints));
                        ClearBpmSuggestion();
                    }
                    break;
                case WorkspaceAction.Undo:
                    session.Undo();
                    break;
                case WorkspaceAction.Redo:
                    session.Redo();
                    break;
                case WorkspaceAction.Copy:
                    editing.Copy(session, screen.ChartId);
                    break;
                case WorkspaceAction.Paste:
                    editing.Paste(session, screen.ChartId, timeline.MusicTime + timeline.AudioOffset);
                    break;
                case WorkspaceAction.Delete:
                    editing.Delete(session, screen.ChartId);
                    break;
                case WorkspaceAction.SelectAll:
                    editing.Select(session, screen.ChartId);
                    break;
                case WorkspaceAction.SelectTaps:
                    editing.Select(session, screen.ChartId, NoteKind.Tap);
                    break;
                case WorkspaceAction.SelectHolds:
                    editing.Select(session, screen.ChartId, NoteKind.Hold);
                    break;
                case WorkspaceAction.Deselect:
                    editing.Selected.Clear();
                    break;
                case WorkspaceAction.Mirror:
                    editing.Remap(session, screen.ChartId, new[] { 3, 2, 1, 0 });
                    break;
                case WorkspaceAction.Randomize:
                    var lanes = new[] { 0, 1, 2, 3 };
                    for (int i = lanes.Length - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        int swap = lanes[i];
                        lanes[i] = lanes[j];
                        lanes[j] = swap;
                    }
                    editing.Remap(session, screen.ChartId, lanes);
                    break;
                case WorkspaceAction.PlayPause:
                    timeline.TogglePreview();
                    break;
                case WorkspaceAction.PlayReturn:
                    timeline.TogglePreview(true);
                    break;
                case WorkspaceAction.Stop:
                    timeline.StopPreview();
                    break;
                case WorkspaceAction.Loop:
                    double start = 0;
                    if (view.loopStart)
                    {
                        start = Number(view.loopStart);
                    }
                    double end = timeline.Duration;
                    if (!string.IsNullOrWhiteSpace(view.loopEnd?.text))
                    {
                        end = Number(view.loopEnd);
                    }
                    if (start < 0 || end <= start || end > timeline.Duration)
                    {
                        throw new ArgumentException("루프 범위를 음원 안의 시작 < 끝으로 입력하세요.");
                    }
                    looping = !looping;
                    break;
                case WorkspaceAction.Slow:
                    if (timeline.PreviewSpeed < 1)
                    {
                        timeline.SetPreviewSpeed(1);
                    }
                    else
                    {
                        timeline.SetPreviewSpeed(.5f);
                    }
                    break;
                case WorkspaceAction.Test:
                    CacheResources();
                    menu.PlaytestFrom(0, false);
                    break;
                case WorkspaceAction.TestHere:
                    CacheResources();
                    menu.PlaytestFrom(timeline.MusicTime, false);
                    break;
                case WorkspaceAction.AutoTest:
                    CacheResources();
                    menu.PlaytestFrom(0, true);
                    break;
                case WorkspaceAction.AutoTestHere:
                    CacheResources();
                    menu.PlaytestFrom(timeline.MusicTime, true);
                    break;
                case WorkspaceAction.Pencil:
                    screen.MinimumHoldOnClick = false;
                    tool = 0;
                    break;
                case WorkspaceAction.Select:
                    tool = 1;
                    break;
                case WorkspaceAction.Eraser:
                    tool = 2;
                    editing.Delete(session, screen.ChartId);
                    break;
                case WorkspaceAction.Position:
                    tool = 3;
                    break;
                case WorkspaceAction.Scroll:
                    tool = 4;
                    break;
                case WorkspaceAction.OnAir:
                    ShowOnAirPrompt();
                    break;
                case WorkspaceAction.Tap:
                    screen.MinimumHoldOnClick = false;
                    tool = 0;
                    break;
                case WorkspaceAction.Hold:
                    screen.MinimumHoldOnClick = true;
                    tool = 0;
                    break;
                case WorkspaceAction.ImportPupa:
                    menu.ImportPupa();
                    break;
            }
        }

        private void CreateNew()
        {
            var replacement = screen.CreateDraft(ChartSlot.Easy);
            menu.ReplaceSession(replacement);
            Observe();
            SetQuantization(0);
            view.newPopup.SetActive(false);
            screen.IsBusy = false;
            view.status.text = "새 채보를 만들었습니다.";
        }

        private void RefreshDifficulty()
        {
            var chart = Array.Find(screen.Session.Snapshot.Charts, item => item.Id == screen.ChartId);
            for (int i = 0; i < view.difficulty.Length; i++)
            {
                var button = view.difficulty[i];
                var colors = difficultyColors[i];
                if (chart?.Slot == (ChartSlot)i)
                {
                    colors.normalColor = new Color(.6f, .64f, .7f);
                    colors.highlightedColor = new Color(.53f, .59f, .67f);
                    colors.pressedColor = new Color(.45f, .51f, .6f);
                    colors.selectedColor = colors.normalColor;
                }

                if (!button.colors.Equals(colors))
                {
                    button.colors = colors;
                }
            }
        }

        private async Task OpenProject(Guid id)
        {
            if (HasUnsavedChanges)
            {
                await Save();
            }

            var replacement = await EditorSession.LoadAsync(menu.Store, id, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            menu.ReplaceSession(replacement);
            Observe();
            view.CloseMenus();
            view.status.text = "채보를 불러왔습니다.";
        }

        private async Task Save()
        {
            await screen.Session.SaveAsync(menu.Store, lifetime.Token);
            string folder = menu.Store.GetSongFolder(screen.Session.Snapshot.Id);
            SaveResources(folder, resources);
            resourcesDirty = false;
            menu.NotifySaved();
            RefreshRecent();
            view.status.text = "저장 완료: " + Path.Combine(folder, FileSongProjectStore.FileName);
        }

        private static void SaveResources(string folder, ResourcesSettings resources)
        {
            string destination = Path.Combine(folder, "workspace.json");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(resources, true));
            if (File.Exists(destination))
            {
                File.Replace(temporary, destination, null);
            }
            else
            {
                File.Move(temporary, destination);
            }
        }

        private async Task SaveAs()
        {
            var project = screen.Session.Snapshot;
            var copiedResources = JsonUtility.FromJson<ResourcesSettings>(JsonUtility.ToJson(resources));
            Guid previous = project.Id;
            project.Id = Guid.NewGuid();
            foreach (var chart in project.Charts)
            {
                chart.Id = Guid.NewGuid();
            }
            project.Title = view.saveAsName.text.Trim();
            if (project.Audio != null)
            {
                project.Audio = await menu.Store.ImportAsync(project.Id,
                    menu.Store.ResolveAudioPath(previous, project.Audio), lifetime.Token);
            }
            if (!string.IsNullOrWhiteSpace(resources.jacket))
            {
                copiedResources.jacket = await menu.Store.ImportAsync(project.Id,
                    menu.Store.ResolveAudioPath(previous, resources.jacket), lifetime.Token);
            }
            if (!string.IsNullOrWhiteSpace(resources.sound))
            {
                copiedResources.sound = await menu.Store.ImportAsync(project.Id,
                    menu.Store.ResolveAudioPath(previous, resources.sound), lifetime.Token);
            }

            var replacement = new EditorSession(project);
            await replacement.SaveAsync(menu.Store, lifetime.Token);
            SaveResources(menu.Store.GetSongFolder(project.Id), copiedResources);
            menu.ReplaceSession(replacement);
            ClearBpmSuggestion();
            resources = copiedResources;
            resourcesDirty = false;
            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
                observed.Changed -= RefreshNoteCache;
            }
            observed = screen.Session;
            observed.Changed += RefreshDifficulty;
            observed.Changed += RefreshBpm;
            observed.Changed += RefreshNoteCache;
            RefreshNoteCache();
            chartId = screen.ChartId;
            editing.Selected.Clear();
            menu.NotifySaved();
            RefreshRecent();
            view.status.text = "다른 이름으로 저장했습니다: " + project.Title;
            FillSettings();
            RefreshDifficulty();
            await LoadResources();
        }

        private void RefreshRecent()
        {
            recentIds = SavedProjectIds().Where(ShowsInRecent).Take(20).ToArray();
            view.recent.ClearOptions();
            view.recent.AddOptions(recentIds.Select(ProjectLabel).ToList());
        }

        private IEnumerable<Guid> SavedProjectIds()
        {
            return menu.Store.ListSongIds().OrderByDescending(LastSavedUtc);
        }

        private DateTime LastSavedUtc(Guid id)
        {
            return File.GetLastWriteTimeUtc(Path.Combine(menu.Store.GetSongFolder(id), FileSongProjectStore.FileName));
        }

        private string ProjectLabel(Guid id)
        {
            try
            {
                string title = menu.Store.Read(id).Title;
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "제목 없음";
                }

                return title + " · " + id.ToString("D").Substring(0, 8);
            }
            catch
            {
                return "읽기 오류 · " + id.ToString("D");
            }
        }

        private static bool ShowsInRecent(Guid id)
        {
            return !PlayerPrefs.HasKey(HiddenRecentProjectKeyPrefix + id.ToString("D"));
        }

        private bool ToggleRecent(Guid id)
        {
            if (working)
            {
                return ShowsInRecent(id);
            }

            string key = HiddenRecentProjectKeyPrefix + id.ToString("D");
            bool show = !ShowsInRecent(id);
            if (show)
            {
                PlayerPrefs.DeleteKey(key);
            }
            else
            {
                PlayerPrefs.SetInt(key, 1);
            }

            PlayerPrefs.Save();
            RefreshRecent();
            return show;
        }

        private void ShowProjectList()
        {
            var content = view.projectListScroll.content;
            foreach (Transform row in content)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            var ids = SavedProjectIds().ToArray();
            foreach (Guid id in ids)
            {
                var row = Instantiate(view.projectListRow, content);
                row.Initialize(ProjectLabel(id), ShowsInRecent(id),
                    () => Run(WorkspaceAction.OpenListedProject, false, id), () => ToggleRecent(id));
                string savedAt = LastSavedUtc(id).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                row.savedAtLabel.text = "마지막 저장 " + savedAt;
                row.copyProjectId.onClick.AddListener(() =>
                {
                    GUIUtility.systemCopyBuffer = id.ToString("D");
                    view.projectListStatus.text = "전체 프로젝트 ID를 복사했습니다.";
                });
                row.gameObject.SetActive(true);
            }

            view.projectListStatus.text = "";
            if (ids.Length == 0)
            {
                view.projectListStatus.text = "저장된 채보가 없습니다.";
            }

            menu.ClosePanels();
            EventSystem.current?.SetSelectedGameObject(null);
            view.projectListPopup.transform.SetAsLastSibling();
            view.projectListPopup.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            view.projectListScroll.verticalNormalizedPosition = 1;
        }

        private async Task ImportResources()
        {
            if (!string.IsNullOrWhiteSpace(view.jacketPath.text))
            {
                string path = PathText(view.jacketPath);
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(extension))
                {
                    throw new ArgumentException("자켓은 PNG / JPG 이미지를 사용하세요.");
                }
                resources.jacket = await menu.Store.ImportAsync(screen.Session.Snapshot.Id, path, lifetime.Token);
            }

            resourcesDirty = true;
            await LoadResources();
            view.status.text = "자켓 정보를 반영했습니다. 이미지 표시는 아직 지원하지 않습니다.";
        }

        private async Task LoadResources()
        {
            int version = ++resourceVersion;
            try
            {
                Guid id = screen.Session.Snapshot.Id;
                if (jacketTexture)
                {
                    Destroy(jacketTexture);
                }
                jacketTexture = null;
                view.jacket.texture = null;
                view.jacket.enabled = false;
                if (!string.IsNullOrWhiteSpace(resources.jacket))
                {
                    string path = menu.Store.ResolveAudioPath(id, resources.jacket);
                    if (new FileInfo(path).Length > 20 * 1024 * 1024)
                    {
                        throw new IOException("자켓은 20MB 이하로 지정하세요.");
                    }
                    var texture = new Texture2D(2, 2);
                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        Destroy(texture);
                        throw new IOException("자켓 이미지를 읽을 수 없습니다.");
                    }
                    jacketTexture = texture;
                    view.jacket.texture = texture;
                    view.jacket.enabled = true;
                    view.jacketPath.text = path;
                }

                soundLease?.Dispose();
                soundLease = null;
                if (!string.IsNullOrWhiteSpace(resources.sound))
                {
                    string path = menu.Store.ResolveAudioPath(id, resources.sound);
                    var loaded = await ChartEditorCompositionRoot.LoadAudio(path, lifetime.Token);
                    if (!this || version != resourceVersion)
                    {
                        loaded.Dispose();
                        return;
                    }
                    soundLease = loaded;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                if (this)
                {
                    view.status.text = "리소스: " + error.Message;
                }
            }
        }

        private async Task AutoEstimateBpm()
        {
            ClearBpmSuggestion();
            int version = bpmAnalysisVersion;
            var session = screen.Session;
            view.status.text = "음원을 적용했습니다. BPM 자동 분석 준비 중…";
            view.bpmSuggestion.text = "BPM 분석 중…";
            while (timeline.IsLoadingAudio)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                await Task.Yield();
            }
            lifetime.Token.ThrowIfCancellationRequested();
            if (!IsCurrentBpmAnalysis(version, session))
            {
                return;
            }

            try
            {
                await EstimateBpm();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (this)
                {
                    view.status.text = "음원은 적용했습니다. BPM 자동 분석 실패: " + error.Message;
                }
            }
        }

        private async Task EstimateBpm()
        {
            ClearBpmSuggestion();
            int version = bpmAnalysisVersion;
            var session = screen.Session;
            view.bpmSuggestion.text = "BPM 분석 중…";
            view.status.text = view.bpmSuggestion.text;
            try
            {
                var clip = timeline.PreviewClip;
                if (!clip)
                {
                    throw new InvalidOperationException("먼저 음원을 가져오세요.");
                }

                int count = Math.Min(clip.samples, clip.frequency * 120) * clip.channels;
                var samples = new float[count];
                if (!clip.GetData(samples, 0))
                {
                    throw new IOException("오디오 PCM 데이터를 읽을 수 없습니다.");
                }

                int rate = clip.frequency;
                int channels = clip.channels;
                double reference = Number(view.bpm);
                view.status.text = "BPM 분석 중…";
                double bpm = await Task.Run(() => AudioBpmEstimator.Estimate(samples, rate, channels, reference), lifetime.Token);
                if (!IsCurrentBpmAnalysis(version, session))
                {
                    return;
                }

                estimatedBpm = bpm;
                view.bpmSuggestion.text = "자동 분석된 BPM은 " + bpm.ToString("0.##", CultureInfo.InvariantCulture) + "입니다.";
                view.applyEstimatedBpm.gameObject.SetActive(true);
                view.status.text = view.bpmSuggestion.text;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception error)
            {
                if (IsCurrentBpmAnalysis(version, session))
                {
                    view.bpmSuggestion.text = "BPM 자동 분석 실패: " + error.Message;
                    view.status.text = view.bpmSuggestion.text;
                }
            }
        }

        private bool IsCurrentBpmAnalysis(int version, EditorSession session)
        {
            if (!this || version != bpmAnalysisVersion)
            {
                return false;
            }

            return session == screen.Session && estimatedAudio == session.Snapshot.Audio;
        }

        private void ClearBpmSuggestion()
        {
            bpmAnalysisVersion++;
            estimatedBpm = null;
            estimatedAudio = screen.Session.Snapshot.Audio;
            view.bpmSuggestion.text = "";
            view.applyEstimatedBpm.gameObject.SetActive(false);
        }

        private void SetQuantization(int index)
        {
            int[] divisions = { 4, 8, 12, 16, 24, 32, 48, 64, 96, 192 };
            view.quantization.SetValueWithoutNotify(index);
            view.quantizationCycleValue.text = view.quantization.options[index].text;
            screen.SnapEnabled = index < divisions.Length;
            if (screen.SnapEnabled)
            {
                screen.SnapBeats = 4.0 / divisions[index];
            }
            timeline.GetComponent<ChartAudioTimelineView>().noteSnapControl.SetValueWithoutNotify(index);
        }

        private void CycleQuantization()
        {
            if (working || screen.IsBusy)
            {
                return;
            }
            SetQuantization((view.quantization.value + 1) % view.quantization.options.Count);
        }

        private bool PointerDown(PointerEventData data)
        {
            if (working || onAirSession != null)
            {
                return true;
            }

            var keyboard = Keyboard.current;
            if (data.button == PointerEventData.InputButton.Left && keyboard != null && keyboard.ctrlKey.isPressed)
            {
                if (Point(data, out var click) && grid.TryGetNearestBeatLine(click.y, out double beat))
                {
                    timeline.SeekInViewport(timeline.Timing.TimeAtBeat(beat) - timeline.AudioOffset);
                }

                return true;
            }

            if (tool == 0)
            {
                return false;
            }
            if (!Point(data, out var point))
            {
                return true;
            }
            if (tool == 4)
            {
                if (data.button != PointerEventData.InputButton.Left)
                {
                    return true;
                }

                draggingScroll = true;
                scrollPointerId = data.pointerId;
                timeline.BeginSeek();
                SetPlaybackPosition(point);
                return true;
            }
            long id = grid.HitTest(point);
            if (tool == 1)
            {
                if (id != 0)
                {
                    if (!editing.Selected.Add(id))
                    {
                        editing.Selected.Remove(id);
                    }
                }
                else
                {
                    selectionStart = point;
                    draggingSelection = true;
                }
            }
            else if (tool == 2 && id != 0)
            {
                if (editing.Selected.Contains(id))
                {
                    editing.Delete(screen.Session, screen.ChartId);
                }
                else
                {
                    screen.Session.Edit(edit => edit.DeleteNote(screen.ChartId, id));
                }
            }
            else if (tool == 3)
            {
                if (id == 0 || data.button != PointerEventData.InputButton.Left)
                {
                    return true;
                }

                grabbedNote = Array.Find(screen.Session.GetNotes(screen.ChartId), note => note.Id == id);
                grabbedNotes = new[] { grabbedNote };
                if (editing.Selected.Contains(id))
                {
                    grabbedNotes = screen.Session.GetNotes(screen.ChartId)
                        .Where(note => editing.Selected.Contains(note.Id)).ToArray();
                }
                grabbedSession = screen.Session;
                grabbedChartId = screen.ChartId;
                grabPointerId = data.pointerId;
                grabBeatOffset = grid.Projection.Unproject(point.y)
                    - timeline.Timing.BeatAtTime(grabbedNote.Data.StartTime);
                grabLane = grabbedNote.Data.Lane;
                grabTime = grabbedNote.Data.StartTime;
                grabMoved = false;
                movingNote = true;
            }

            grid.SetVerticesDirty();
            return true;
        }

        private void PointerDrag(PointerEventData data)
        {
            if (draggingScroll)
            {
                DragScroll(data);
                return;
            }
            if (movingNote)
            {
                if (data.pointerId == grabPointerId)
                {
                    grabMoved = true;
                    PreviewGrab(data);
                }
                return;
            }

            if (!draggingSelection || working || screen.IsBusy)
            {
                return;
            }
            if (!Point(data, out var point))
            {
                return;
            }
            var bounds = Rect.MinMaxRect(Mathf.Min(selectionStart.x, point.x), Mathf.Min(selectionStart.y, point.y),
                Mathf.Max(selectionStart.x, point.x), Mathf.Max(selectionStart.y, point.y));
            foreach (var note in cachedNotes)
            {
                var rect = grid.NoteBounds(note.Data.Lane, timeline.Timing.BeatAtTime(note.Data.StartTime),
                    timeline.Timing.BeatAtTime(note.Data.EndTime));
                if (bounds.Overlaps(rect))
                {
                    editing.Selected.Add(note.Id);
                }
            }
            grid.SetVerticesDirty();
        }

        private void PointerUp(PointerEventData data)
        {
            if (data.pointerId == scrollPointerId && data.button == PointerEventData.InputButton.Left)
            {
                EndPlaybackSeek();
            }
            draggingSelection = false;
            if (!movingNote || data.pointerId != grabPointerId)
            {
                return;
            }

            if (grabMoved && PreviewGrab(data))
            {
                if (editing.MoveNotes(screen.Session, screen.ChartId, grabbedNotes.Select(note => note.Id).ToArray(),
                    grabLane - grabbedNote.Data.Lane, grabTime - grabbedNote.Data.StartTime))
                {
                    view.status.text = "노트를 옮겼습니다.";
                }
                else
                {
                    view.status.text = "다른 노트와 겹쳐 이동할 수 없습니다.";
                }
            }
            CancelGrab();
        }

        private void DragScroll(PointerEventData data)
        {
            if (tool != 4 || working || screen.IsBusy)
            {
                EndPlaybackSeek();
                return;
            }
            if (data.pointerId != scrollPointerId)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid.rectTransform,
                data.position, data.pressEventCamera, out var point);
            SetPlaybackPosition(point);
        }

        private void EndPlaybackSeek()
        {
            draggingScroll = false;
            if (timeline)
            {
                timeline.EndSeek();
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                EndPlaybackSeek();
                if (onAirPressTimes != null)
                {
                    Array.Clear(onAirPressTimes, 0, onAirPressTimes.Length);
                }
            }
        }

        private void OnDisable()
        {
            EndPlaybackSeek();
            if (onAirQuantization != null)
            {
                StopCoroutine(onAirQuantization);
                CloseOnAirQuantization();
            }

            if (onAirSession != null)
            {
                FinishOnAir();
            }
            else if (view && IsOnAirPromptOpen)
            {
                CloseOnAirPrompt();
            }
        }

        private void SetPlaybackPosition(Vector2 point)
        {
            var rect = grid.rectTransform.rect;
            double beat = grid.Projection.Unproject(Mathf.Clamp(point.y, rect.yMin, rect.yMax));
            if (screen.SnapEnabled)
            {
                double topBeat = grid.Projection.Unproject(rect.yMax);
                beat = timeline.Timing.SnapBeat(beat, screen.SnapBeats, screen.ScrollBeatOffset, topBeat);
            }

            timeline.SeekInViewport(timeline.Timing.TimeAtBeat(beat) - timeline.AudioOffset);
        }

        private bool PreviewGrab(PointerEventData data)
        {
            if (tool != 3)
            {
                CancelGrab();
                return false;
            }

            if (working || screen.IsBusy)
            {
                CancelGrab();
                return false;
            }

            if (grabbedSession != screen.Session || grabbedChartId != screen.ChartId)
            {
                CancelGrab();
                return false;
            }

            if (!Point(data, out var point))
            {
                grid.SetPreview(-1);
                return false;
            }

            var chart = Array.Find(screen.Session.Snapshot.Charts, item => item.Id == screen.ChartId);
            var area = grid.rectTransform.rect;
            grabLane = Mathf.Clamp(Mathf.FloorToInt((point.x - area.xMin) / area.width * chart.LaneCount),
                0, chart.LaneCount - 1);
            int laneShift = Mathf.Clamp(grabLane - grabbedNote.Data.Lane,
                -grabbedNotes.Min(note => note.Data.Lane),
                chart.LaneCount - 1 - grabbedNotes.Max(note => note.Data.Lane));
            grabLane = grabbedNote.Data.Lane + laneShift;
            double pointerTime = timeline.Timing.TimeAtBeat(grid.Projection.Unproject(point.y));
            double grabbedBeat = timeline.Timing.BeatAtTime(grabbedNote.Data.StartTime);
            double pointerOffset = timeline.Timing.TimeAtBeat(grabbedBeat + grabBeatOffset) - grabbedNote.Data.StartTime;
            double beat = timeline.Timing.BeatAtTime(pointerTime - pointerOffset);
            if (screen.SnapEnabled)
            {
                beat = Math.Round(beat / screen.SnapBeats) * screen.SnapBeats;
            }
            grabTime = Math.Max(0, timeline.Timing.TimeAtBeat(beat));
            double timeShift = Math.Max(grabTime - grabbedNote.Data.StartTime,
                -grabbedNotes.Min(note => note.Data.StartTime));
            grabTime = grabbedNote.Data.StartTime + timeShift;
            if (grabbedNotes.Length > 1)
            {
                var previews = new NoteData[grabbedNotes.Length];
                for (int i = 0; i < grabbedNotes.Length; i++)
                {
                    var note = grabbedNotes[i].Data;
                    if (note.Kind == NoteKind.Hold)
                    {
                        previews[i] = NoteData.Hold(note.Lane + laneShift,
                            note.StartTime + timeShift, note.EndTime + timeShift);
                    }
                    else
                    {
                        previews[i] = NoteData.Tap(note.Lane + laneShift, note.StartTime + timeShift);
                    }
                }
                grid.SetNotePreviews(previews);
                return true;
            }
            double end = grabTime + grabbedNote.Data.EndTime - grabbedNote.Data.StartTime;
            grid.SetPreview(grabLane, timeline.Timing.BeatAtTime(grabTime), timeline.Timing.BeatAtTime(end));
            return true;
        }

        private void CancelGrab()
        {
            movingNote = false;
            grabMoved = false;
            grabbedSession = null;
            grabbedNotes = Array.Empty<EditorNote>();
            grid.SetPreview(-1);
        }

        private bool Point(PointerEventData data, out Vector2 point)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(grid.rectTransform,
                data.position, data.pressEventCamera, out point);
            return grid.rectTransform.rect.Contains(point);
        }

        private static string PathText(InputField input) => input.text.Trim().Trim('"');

        private void CacheResources()
        {
            returningProject = screen.Session.Snapshot.Id;
            returningResources = JsonUtility.ToJson(resources);
            returningResourcesDirty = resourcesDirty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReturn()
        {
            returningProject = Guid.Empty;
            returningResources = null;
        }

        private static double Number(InputField input)
        {
            if (!double.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new ArgumentException("숫자를 입력하세요: " + input.text);
            }
            if (!double.IsFinite(value))
            {
                throw new ArgumentException("유한한 숫자를 입력하세요.");
            }
            return value;
        }

        private void OnDestroy()
        {
            EndPlaybackSeek();
            InputSystem.onAfterUpdate -= PrepareShortcutInput;
            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
                observed.Changed -= RefreshNoteCache;
            }

            lifetime.Cancel();
            lifetime.Dispose();
            soundLease?.Dispose();
            if (jacketTexture)
            {
                Destroy(jacketTexture);
            }
        }
    }
}
