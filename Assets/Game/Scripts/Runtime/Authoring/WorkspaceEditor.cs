using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
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
        private Guid[] recentIds = Array.Empty<Guid>();
        private ResourcesSettings resources = new ResourcesSettings();
        private bool resourcesDirty;
        private bool working;
        private bool looping;
        private bool previousLoopPlaying;
        private bool draggingSelection;
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
        private ColorBlock[] difficultyColors;

        public bool IsWorking => working;

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

            if (observed != screen.Session)
            {
                Observe();
            }

            if (chartId != screen.ChartId)
            {
                chartId = screen.ChartId;
                editing.Selected.Clear();
            }

            view.selection.text = "선택 " + editing.Selected.Count + " / "
                + screen.Session.GetNotes(screen.ChartId).Length + " · 도구 "
                + new[] { "그리기", "선택", "지우개", "잡기" }[tool];
            if (view.playback)
            {
                view.playback.text = "속도 " + (timeline.PreviewSpeed * 100).ToString("0") + "% · 루프 "
                    + looping.ToString() + " · " + timeline.MusicTime.ToString("0.000") + " s";
            }

            if (looping && !working && !timeline.IsBusy)
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
                    timeline.PausePreview();
                    if (!double.TryParse(view.loopStart?.text ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out double loopStart))
                    {
                        looping = false;
                        return;
                    }
                    if (loopStart < 0 || loopStart >= end)
                    {
                        looping = false;
                        return;
                    }
                    timeline.Seek(loopStart);
                    timeline.PlayPreview();
                }
            }
            previousLoopPlaying = timeline.IsPlaying;

            if (soundLease != null && timeline.IsPlaying)
            {
                double time = timeline.MusicTime + timeline.AudioOffset;
                if (time >= previousPreviewTime)
                {
                    foreach (var note in screen.Session.GetNotes(screen.ChartId))
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
                if (EventSystem.current.currentSelectedGameObject.GetComponent<InputField>())
                {
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

        private void PrepareShortcutInput()
        {
            if (!isActiveAndEnabled)
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

        private void Observe()
        {
            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
            }

            observed = screen.Session;
            observed.Changed += RefreshDifficulty;
            observed.Changed += RefreshBpm;
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
            string value = screen.Session.Snapshot.TempoPoints[0].Bpm.ToString("R", CultureInfo.InvariantCulture);
            view.bpm.SetTextWithoutNotify(value);
            timeline.GetComponent<ChartAudioTimelineView>().bpm.SetTextWithoutNotify(value);
        }

        private void ApplyBpm(string value)
        {
            if (!this || working || screen.IsBusy)
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

            if (working || screen.IsBusy || !menu.IsInitialized)
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

        public async void Run(WorkspaceAction action, bool closeMenus = true)
        {
            if (working || !menu.IsInitialized)
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

                await Execute(action);
                grid.SetVerticesDirty();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this)
                {
                    view.status.text = error.Message;
                    if (view.saveAsPopup.activeSelf)
                    {
                        view.saveAsError.text = error.Message;
                    }
                }
            }
            finally
            {
                working = false;
                RefreshTools();
            }
        }

        private void RefreshTools()
        {
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

        private async Task Execute(WorkspaceAction action)
        {
            var session = screen.Session;
            switch (action)
            {
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
                    await Save();
                    menu.ReplaceSession(await EditorSession.LoadAsync(menu.Store, id, lifetime.Token));
                    Observe();
                    view.CloseMenus();
                    view.status.text = "채보를 불러왔습니다.";
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
                    string audio = PathText(view.audioPath);
                    string extension = Path.GetExtension(audio).ToLowerInvariant();
                    if (!new[] { ".wav", ".ogg", ".mp3" }.Contains(extension))
                    {
                        throw new ArgumentException("WAV / OGG / MP3 파일을 입력하세요.");
                    }
                    string relative = await menu.Store.ImportAsync(session.Snapshot.Id, audio, lifetime.Token);
                    session.Edit(edit => edit.SetAudio(relative));
                    view.status.text = "음원을 적용했습니다.";
                    break;
                case WorkspaceAction.ImportJacket:
                    await ImportResources();
                    break;
                case WorkspaceAction.EstimateBpm:
                    await EstimateBpm();
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
                    if (timeline.IsPlaying)
                    {
                        timeline.PausePreview();
                    }
                    else
                    {
                        timeline.PlayPreview();
                    }
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
            var replacement = new EditorSession(new SongProject());
            replacement.Edit(edit => edit.AddChartSlot(ChartSlot.Easy, screen.Mode.name, 4, 8.5));
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
            resources = copiedResources;
            resourcesDirty = false;
            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
            }
            observed = screen.Session;
            observed.Changed += RefreshDifficulty;
            observed.Changed += RefreshBpm;
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
            recentIds = menu.Store.ListSongIds().OrderByDescending(id =>
                File.GetLastWriteTimeUtc(Path.Combine(menu.Store.GetSongFolder(id), FileSongProjectStore.FileName)))
                .Take(20).ToArray();
            view.recent.ClearOptions();
            view.recent.AddOptions(recentIds.Select(id =>
            {
                try
                {
                    return menu.Store.Read(id).Title + " · " + id.ToString("D").Substring(0, 8);
                }
                catch
                {
                    return "읽기 오류 · " + id.ToString("D");
                }
            }).ToList());
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
            view.status.text = "자켓 이미지를 적용했습니다.";
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
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this)
                {
                    view.status.text = "리소스: " + error.Message;
                }
            }
        }

        private async Task EstimateBpm()
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
            if (!this)
            {
                return;
            }
            view.bpm.text = bpm.ToString("0.##", CultureInfo.InvariantCulture);
            view.status.text = "BPM 추정: " + view.bpm.text + " · 절반/두 배 해석을 확인한 뒤 설정 적용을 누르세요.";
        }

        private void SetQuantization(int index)
        {
            int[] divisions = { 4, 8, 12, 16, 24, 32, 48, 64, 96, 192 };
            view.quantization.SetValueWithoutNotify(index);
            view.quantizationCycleValue.text = view.quantization.options[index].text;
            screen.SnapEnabled = true;
            screen.SnapBeats = 4.0 / divisions[index];
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
            if (working)
            {
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
            foreach (var note in screen.Session.GetNotes(screen.ChartId))
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
            double beat = grid.Projection.Unproject(point.y) - grabBeatOffset;
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
            InputSystem.onAfterUpdate -= PrepareShortcutInput;
            if (observed != null)
            {
                observed.Changed -= RefreshDifficulty;
                observed.Changed -= RefreshBpm;
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
