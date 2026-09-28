using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;

namespace RhythmDojo.Core
{
    public sealed class AppFlowController : MonoBehaviour, IAppNavigation, IEditorPlaytestService
    {
        public const string SelectionPath = "Assets/Game/Scenes/SongSelection.unity";
        public const string GameplayPath = "Assets/Game/Scenes/Gameplay.unity";
        public const string SettingsPath = "Assets/Game/Scenes/Settings.unity";
        [SerializeField] private GameSettings settings;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private string editorSessionId, returnScenePath;
        private Action<string> returned;
        public GameSettings Settings => settings;
        public SongLibrary Library { get; private set; }
        public SongSelectionService Selection { get; private set; }
        public AudioSettingsService Audio { get; private set; }
        public PlayRequest CurrentRequest { get; private set; }
        public bool Transitioning { get; private set; }
        // Compatibility facade for existing integration callers. Screens use injected services.
        public int SelectedSongIndex
        {
            get { for (int i = 0; i < Library.Entries.Count; i++) if (Library.Entries[i].Id == Selection.SelectedSongId) return i; return -1; }
        }
        public DifficultyProfile SelectedDifficulty => Selection.SelectedDifficulty;
        public ScrollMode SelectedScrollMode => Selection.SelectedScrollMode;
        public double SelectedMultiplier => Selection.SelectedMultiplier;
        public int DefaultBufferSize => Audio.DefaultBufferSize;
        public int SelectedBufferOption => Audio.SelectedBufferOption;
        public int RequestedBufferSize => Audio.RequestedBufferSize;
        public string BufferStatus => Audio.BufferStatus;
        public static int BufferOptionCount => 8;
        public static int GetBufferSize(int option) => option == 0 ? 0 : 1 << (option + 4);
        public void SelectBufferOption(int option) => Audio.SelectBufferOption(option);
        public bool ApplyBufferSize() => Audio.ApplyBufferSize();

        public static AppFlowController Create(GameSettings settings)
        {
            var existing = FindFirstObjectByType<AppFlowController>();
            if (existing) return existing;
            settings.ValidateOptions();
            var flow = new GameObject("App Flow").AddComponent<AppFlowController>();
            flow.settings = settings;
            flow.Library = new SongLibrary(); flow.Library.Register(new BuiltInSongProvider(settings.catalog));
            flow.Selection = new SongSelectionService(settings, flow.Library);
            flow.Audio = new AudioSettingsService(() => !flow.Transitioning && SceneManager.GetSceneByPath(SettingsPath).isLoaded);
            DontDestroyOnLoad(flow.gameObject); return flow;
        }
        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Transitioning = false;
            if (scene.path == GameplayPath && CurrentRequest != null)
                FindFirstObjectByType<GameplayCompositionRoot>().Initialize(CurrentRequest, Settings, this);
            else if (scene.path != GameplayPath)
            {
                ReleaseRequest();
                if (returned != null && scene.path == returnScenePath)
                {
                    var callback = returned; var id = editorSessionId;
                    ClearEditorReturn(); callback(id);
                }
            }
        }
        public void UpdateSelection(int songIndex, DifficultyProfile difficulty, ScrollMode mode, double multiplier)
        {
            if (Transitioning) return;
            if (songIndex < 0 || songIndex >= Library.Entries.Count) throw new ArgumentOutOfRangeException(nameof(songIndex));
            Selection.UpdateSelection(Library.Entries[songIndex].Id, difficulty, mode, multiplier);
        }
        public async void PlaySelected()
        {
            try { await PlaySelectedAsync(CancellationToken.None); }
            catch (Exception e) { Debug.LogException(e); }
        }
        public async Task PlaySelectedAsync(CancellationToken cancellationToken)
        {
            if (Transitioning) return;
            if (!SceneManager.GetSceneByPath(SelectionPath).isLoaded)
                throw new InvalidOperationException("Select a song before requesting playback.");
            var songId = Selection.SelectedSongId;
            var difficulty = SelectedDifficulty; var scrollMode = SelectedScrollMode; var multiplier = SelectedMultiplier;
            Transitioning = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            PlayableSong song = null;
            try
            {
                song = await Library.LoadAsync(songId, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                var request = new PlayRequest(song, difficulty, scrollMode, multiplier);
                ReleaseRequest(); CurrentRequest = request; song = null;
                ClearEditorReturn(); Load(GameplayPath);
            }
            catch { song?.Dispose(); ReleaseRequest(); Transitioning = false; throw; }
        }
        public async Task PlayAsync(SongDocument document, ISongDocumentLoader loader, string sessionId,
            string returnPath, Action<string> onReturned, CancellationToken cancellationToken)
        {
            if (Transitioning) throw new InvalidOperationException("A scene transition is already in progress.");
            if (SceneManager.GetSceneByPath(GameplayPath).isLoaded)
                throw new InvalidOperationException("Return from gameplay before requesting an editor playtest.");
            if (document == null || loader == null || onReturned == null || string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(returnPath) || returnPath == GameplayPath || !UnityEngine.Application.CanStreamedLevelBeLoaded(returnPath))
                throw new ArgumentException("Document, loader, editor session and registered return scene are required.");
            var difficulty = SelectedDifficulty; var scrollMode = SelectedScrollMode; var multiplier = SelectedMultiplier;
            Transitioning = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            PlayableSong song = null;
            try
            {
                song = await loader.LoadAsync(document.Copy(), linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                var request = new PlayRequest(song, difficulty, scrollMode, multiplier);
                ReleaseRequest(); CurrentRequest = request; song = null;
                editorSessionId = sessionId; returnScenePath = returnPath; returned = onReturned;
                Load(GameplayPath);
            }
            catch { song?.Dispose(); ReleaseRequest(); ClearEditorReturn(); Transitioning = false; throw; }
        }
        public void ReturnFromGameplay()
        {
            if (!Transitioning) Load(returned == null ? SelectionPath : returnScenePath);
        }
        public void ShowSelection()
        {
            if (Transitioning) return;
            ClearEditorReturn(); Load(SelectionPath);
        }
        public void ShowSettings()
        {
            if (Transitioning || !SceneManager.GetSceneByPath(SelectionPath).isLoaded) return;
            Load(SettingsPath);
        }
        private void Load(string path)
        {
            Transitioning = true;
            try { if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Single) == null) throw new InvalidOperationException("Cannot load scene: " + path); }
            catch { Transitioning = false; throw; }
        }
        private void ReleaseRequest() { CurrentRequest?.Song.Dispose(); CurrentRequest = null; }
        private void ClearEditorReturn() { returned = null; editorSessionId = null; returnScenePath = null; }
        private void OnDestroy()
        {
            lifetime.Cancel(); lifetime.Dispose(); Audio?.Dispose(); ReleaseRequest();
        }
    }
}
