using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ChartEditorMenu : MonoBehaviour
    {
        public const string ScenePath = "Assets/Game/Scenes/ChartEditor.unity";
        [SerializeField] private ChartEditorScreen screen;
        [SerializeField] private GameObject filePanel, settingsPanel, toolsPanel;
        [SerializeField] private InputField projectId, audioPath, title, artist, bpm, offset, duration;
        [SerializeField] private Text status;
        [SerializeField] private Button undo, redo;
        [SerializeField] private CanvasGroup controls;
        private FileSongProjectStore store;
        private IEditorPlaytestService playtest;
        private Func<SongProject, ISongDocumentLoader> loaderFactory;
        private EditorSession observed;
        private bool leavingForPlaytest;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private static EditorSession returningSession;
        public bool IsInitialized => store != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetReturn() => returningSession = null;

        public static EditorSession TakeReturningSession()
        {
            var result = returningSession;
            returningSession = null;
            return result;
        }

        public void Initialize(FileSongProjectStore projectStore, IEditorPlaytestService service,
            Func<SongProject, ISongDocumentLoader> createLoader)
        {
            store = projectStore;
            playtest = service;
            loaderFactory = createLoader;
            Observe();
            FillSettings();
            ShowStatus("파일에서 음원을 가져오고 노트를 배치하세요.");
        }

        private void Observe()
        {
            if (observed != null) observed.Changed -= Refresh;
            observed = screen.Session;
            observed.Changed += Refresh;
            projectId.text = observed.Snapshot.Id.ToString("D");
            Refresh();
        }

        private void Refresh()
        {
            undo.interactable = observed.CanUndo;
            redo.interactable = observed.CanRedo;
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            if (observed != null) observed.Changed -= Refresh;
        }

        public void ToggleFile() => Toggle(filePanel);
        public void ToggleSettings() { FillSettings(); Toggle(settingsPanel); }
        public void ToggleTools() => Toggle(toolsPanel);
        private void Toggle(GameObject panel)
        {
            if (screen.IsBusy) return;
            bool open = !panel.activeSelf;
            filePanel.SetActive(false); settingsPanel.SetActive(false); toolsPanel.SetActive(false);
            panel.SetActive(open);
        }

        private void FillSettings()
        {
            var p = screen.Session.Snapshot;
            title.text = p.Title; artist.text = p.Artist;
            bpm.text = p.TempoPoints[0].Bpm.ToString("R", CultureInfo.InvariantCulture);
            offset.text = p.AudioOffsetSeconds.ToString("R", CultureInfo.InvariantCulture);
            duration.text = Array.Find(p.Charts, c => c.Id == screen.ChartId).CompletionTimeSeconds.ToString("R", CultureInfo.InvariantCulture);
        }

        public void ApplySettings() => Run(() =>
        {
            double beats = Number(bpm.text), shift = Number(offset.text), end = Number(duration.text);
            if (beats <= 0 || end <= 0) throw new InvalidOperationException("BPM과 종료 시간은 0보다 커야 합니다.");
            var points = screen.Session.Snapshot.TempoPoints;
            points[0] = new TempoPoint(0, beats);
            screen.Session.Edit(edit =>
            {
                edit.SetMetadata(title.text, artist.text);
                edit.SetTiming(shift, points);
                edit.SetChartSettings(screen.ChartId, screen.Mode.name, screen.Mode.LaneCount, end);
            });
            ShowStatus("설정을 적용했습니다. 노트의 초 단위 시간은 유지됩니다.");
        });

        private static double Number(string value)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
                throw new InvalidOperationException("유효한 숫자를 입력하세요. 소수점은 . 을 사용하세요.");
            return number;
        }

        public void Undo() => Run(() => { screen.Session.Undo(); FillSettings(); ShowStatus("실행을 취소했습니다."); });
        public void Redo() => Run(() => { screen.Session.Redo(); FillSettings(); ShowStatus("다시 실행했습니다."); });
        public void ResetView() => Run(() => { screen.ResetView(); ShowStatus("처음 위치와 기본 확대 배율로 돌아왔습니다."); });
        public void Save() => RunAsync(async () =>
        {
            await screen.Session.SaveAsync(store, lifetime.Token);
            ShowStatus("저장 완료: " + Path.Combine(store.GetSongFolder(screen.Session.Snapshot.Id), FileSongProjectStore.FileName));
        });

        public void Load() => RunAsync(async () =>
        {
            if (!Guid.TryParse(projectId.text, out Guid id)) throw new InvalidOperationException("불러올 곡의 ID를 입력하세요.");
            // Save the current draft before replacing it; a failed load leaves it on screen.
            if (screen.Session.IsDirty) await screen.Session.SaveAsync(store, lifetime.Token);
            var replacement = await EditorSession.LoadAsync(store, id, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            screen.SetSession(replacement);
            Observe(); FillSettings();
            ShowStatus("불러왔습니다. 이전 작업은 자동 저장했습니다.");
        });

        public void ImportAudio() => RunAsync(async () =>
        {
            string path = audioPath.text.Trim().Trim('"');
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".wav" && extension != ".ogg" && extension != ".mp3")
                throw new InvalidOperationException("WAV, OGG 또는 MP3 파일 경로를 입력하세요.");
            string relative = await store.ImportAsync(screen.Session.Snapshot.Id, path, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            screen.Session.Edit(edit => edit.SetAudio(relative));
            ShowStatus("음원을 가져왔습니다: " + Path.GetFileName(path));
        });

        public void Playtest() => RunAsync(async () =>
        {
            var snapshot = screen.Session.Snapshot;
            var document = ChartPlaybackAdapter.ToDocument(snapshot, screen.ChartId, screen.Mode.name,
                screen.Mode.ToRules(), audio => store.AudioExists(snapshot.Id, audio));
            returningSession = screen.Session;
            try
            {
                await playtest.PlayAsync(document, loaderFactory(snapshot), snapshot.Id.ToString("D"),
                    ScenePath, Returned, lifetime.Token);
                leavingForPlaytest = true;
            }
            catch { returningSession = null; throw; }
        });

        // The returning scene restores the session in Awake; never capture the destroyed screen.
        private static void Returned(string sessionId) { }

        private void Run(Action action)
        {
            if (screen.IsBusy) return;
            try { action(); } catch (Exception e) { ShowStatus(e.Message); }
        }

        private async void RunAsync(Func<Task> action)
        {
            if (screen.IsBusy || store == null) return;
            screen.IsBusy = true; controls.interactable = false;
            ShowStatus("처리 중…");
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (this) ShowStatus(e.Message); }
            finally
            {
                if (this && !leavingForPlaytest) { screen.IsBusy = false; controls.interactable = true; Refresh(); }
            }
        }

        private void ShowStatus(string message) { if (status) status.text = message; }
    }
}
