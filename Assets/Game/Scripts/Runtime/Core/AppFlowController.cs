using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.Core
{
    public sealed class AppFlowController
        : MonoBehaviour,
            IAppNavigation,
            IEditorPlaytestService,
            ISettingsReturnNavigation,
            ITitleNavigation
    {
        public const string SelectionPath = "Assets/Game/Scenes/SongSelection.unity";
        public const string GameplayPath = "Assets/Game/Scenes/Gameplay.unity";
        public const string SettingsPath = "Assets/Game/Scenes/Settings.unity";

        [SerializeField]
        private GameSettings settings;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private string editorSessionId,
            returnScenePath;
        private string settingsReturnPath = SelectionPath;
        private readonly List<Behaviour> settingsSuspended = new List<Behaviour>();
        private GameObject settingsPreviousFocus;
        private ModalInputScope settingsInputScope;
        private Action<string> returned;

        public GameSettings Settings => settings;

        public SongLibrary Library { get; private set; }
        public SongSelectionService Selection { get; private set; }
        public AudioSettingsService Audio { get; private set; }
        public ISelectionPreferences Preferences { get; private set; }
        public HardModePreferences HardMode { get; private set; }
        public IPlayRulesService Rules { get; private set; }

        public bool IsEditorPlaytest => returned != null;

        public PlayRequest CurrentRequest { get; private set; }
        public bool Transitioning { get; private set; }

        // Compatibility facade for existing integration callers. Screens use injected services.
        public int SelectedSongIndex
        {
            get
            {
                for (int i = 0; i < Library.Entries.Count; i++)
                {
                    if (Library.Entries[i].Id == Selection.SelectedSongId)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public DifficultyProfile SelectedDifficulty => Selection.SelectedDifficulty;

        public ScrollMode SelectedScrollMode => Selection.SelectedScrollMode;

        public double SelectedMultiplier => Selection.SelectedMultiplier;

        public int DefaultBufferSize => Audio.DefaultBufferSize;

        public int SelectedBufferOption => Audio.SelectedBufferOption;

        public int RequestedBufferSize => Audio.RequestedBufferSize;

        public string BufferStatus => Audio.BufferStatus;

        public static int BufferOptionCount => 8;

        public static int GetBufferSize(int option)
        {
            if (option == 0)
            {
                return 0;
            }
            else
            {
                return 1 << (option + 4);
            }
        }

        public void SelectBufferOption(int option) => Audio.SelectBufferOption(option);

        public bool ApplyBufferSize() => Audio.ApplyBufferSize();

        public static AppFlowController Create(GameSettings settings)
        {
            var existing = FindFirstObjectByType<AppFlowController>();

            if (existing)
            {
                return existing;
            }

            settings.ValidateOptions();

            var flow = new GameObject("App Flow").AddComponent<AppFlowController>();
            flow.settings = settings;
            flow.Library = new SongLibrary();
            flow.Library.Register(new BuiltInSongProvider(settings.catalog));
            flow.Library.Register(
                new ProjectSongProvider(
                    new RhythmDojo.Authoring.FileSongProjectStore(
                        System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Songs")
                    ),
                    settings.catalog[0].Mode
                )
            );
            flow.Preferences = new SelectionPreferences();
            flow.HardMode = HardModePreferences.Load();
            flow.Rules = new PlayRulesService();

            var presentation = PresentationPreferences.Load();
            bool hasPresentation = PlayerPrefs.HasKey(PresentationPreferences.StorageKey);
            presentation.SaveAudio();
            presentation.ApplyGraphics(hasPresentation);
            flow.Selection = new SongSelectionService(settings, flow.Library, flow.Preferences);

            if (flow.Library.Find("demo-melody") != null)
            {
                flow.Selection.UpdateSelection(
                    "demo-melody",
                    flow.SelectedDifficulty,
                    flow.SelectedScrollMode,
                    flow.SelectedMultiplier
                );
            }

            flow.Audio = new AudioSettingsService(() =>
                !flow.Transitioning && SceneManager.GetSceneByPath(SettingsPath).isLoaded
            );
            DontDestroyOnLoad(flow.gameObject);

            return flow;
        }

        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Transitioning = false;

            if (scene.path == SettingsPath && settingsInputScope != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>())
                    {
                        camera.enabled = false;
                    }

                    // The background scene supplies the listener while this additive popup is open.
                    foreach (var listener in root.GetComponentsInChildren<AudioListener>())
                    {
                        listener.enabled = false;
                    }

                    foreach (var view in root.GetComponentsInChildren<SettingsView>())
                    {
                        if (view.backdrop)
                        {
                            view.backdrop.SetActive(settingsReturnPath != "Assets/Game/Scenes/Title.unity");
                        }
                    }
                }

                SceneManager.SetActiveScene(scene);
            }

            if (scene.path == GameplayPath && CurrentRequest != null)
            {
                FindFirstObjectByType<GameplayCompositionRoot>()
                    .Initialize(CurrentRequest, Settings, this);
            }
            else if (scene.path != GameplayPath)
            {
                ReleaseRequest();

                if (returned != null && scene.path == returnScenePath)
                {
                    var callback = returned;
                    var id = editorSessionId;
                    ClearEditorReturn();
                    callback(id);
                }
            }
        }

        public void UpdateSelection(
            int songIndex,
            DifficultyProfile difficulty,
            ScrollMode mode,
            double multiplier
        )
        {
            if (Transitioning)
            {
                return;
            }

            if (songIndex < 0 || songIndex >= Library.Entries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(songIndex));
            }

            Selection.UpdateSelection(Library.Entries[songIndex].Id, difficulty, mode, multiplier);
        }

        public async void PlaySelected()
        {
            try
            {
                await PlaySelectedAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task PlaySelectedAsync(CancellationToken cancellationToken)
        {
            if (Transitioning)
            {
                return;
            }

            if (!SceneManager.GetSceneByPath(SelectionPath).isLoaded)
            {
                throw new InvalidOperationException("Select a song before requesting playback.");
            }

            var songId = Selection.SelectedSongId;
            var difficulty = SelectedDifficulty;
            var scrollMode = SelectedScrollMode;
            var multiplier = SelectedMultiplier;
            int timingOffsetMs = Preferences.TimingOffsetMs;
            HardModeHealthRules healthRules;

            if (HardMode.Enabled)
            {
                healthRules = HardMode.CreateRules();
            }
            else
            {
                healthRules = null;
            }

            var rules = Rules.Snapshot;
            Transitioning = true;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetime.Token
            );
            PlayableSong song = null;

            try
            {
                song = await Library.LoadChartAsync(
                    songId,
                    (ChartSlot)Array.IndexOf(settings.difficulties, difficulty),
                    linked.Token
                );
                linked.Token.ThrowIfCancellationRequested();

                var request = new PlayRequest(
                    song,
                    difficulty,
                    scrollMode,
                    multiplier,
                    timingOffsetMs,
                    healthRules,
                    rules
                );
                ReleaseRequest();
                CurrentRequest = request;
                song = null;
                ClearEditorReturn();
                Load(GameplayPath);
            }
            catch
            {
                song?.Dispose();
                ReleaseRequest();
                Transitioning = false;
                throw;
            }
        }

        public async Task PlayAsync(
            SongDocument document,
            ISongDocumentLoader loader,
            string sessionId,
            string returnPath,
            Action<string> onReturned,
            CancellationToken cancellationToken
        )
        {
            if (Transitioning)
            {
                throw new InvalidOperationException("A scene transition is already in progress.");
            }

            if (SceneManager.GetSceneByPath(GameplayPath).isLoaded)
            {
                throw new InvalidOperationException(
                    "Return from gameplay before requesting an editor playtest."
                );
            }

            if ((document == null || loader == null) || onReturned == null)
            {
                throw new ArgumentException(
                    "Document, loader, editor session and registered return scene are required."
                );
            }

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(returnPath))
            {
                throw new ArgumentException(
                    "Document, loader, editor session and registered return scene are required."
                );
            }

            if (
                returnPath == GameplayPath
                || !UnityEngine.Application.CanStreamedLevelBeLoaded(returnPath)
            )
            {
                throw new ArgumentException(
                    "Document, loader, editor session and registered return scene are required."
                );
            }

            var difficulty = SelectedDifficulty;
            var scrollMode = SelectedScrollMode;
            var multiplier = SelectedMultiplier;
            int timingOffsetMs = Preferences.TimingOffsetMs;
            HardModeHealthRules healthRules;

            if (HardMode.Enabled)
            {
                healthRules = HardMode.CreateRules();
            }
            else
            {
                healthRules = null;
            }

            var rules = Rules.Snapshot;
            Transitioning = true;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lifetime.Token
            );
            PlayableSong song = null;

            try
            {
                song = await loader.LoadAsync(document.Copy(), linked.Token);
                linked.Token.ThrowIfCancellationRequested();

                var request = new PlayRequest(
                    song,
                    difficulty,
                    scrollMode,
                    multiplier,
                    timingOffsetMs,
                    healthRules,
                    rules
                );
                ReleaseRequest();
                CurrentRequest = request;
                song = null;
                editorSessionId = sessionId;
                returnScenePath = returnPath;
                returned = onReturned;
                Load(GameplayPath);
            }
            catch
            {
                song?.Dispose();
                ReleaseRequest();
                ClearEditorReturn();
                Transitioning = false;
                throw;
            }
        }

        public void ReturnFromGameplay()
        {
            if (!Transitioning)
            {
                Load(
                    (
                        returned switch
                        {
                            null => SelectionPath,
                            _ => returnScenePath,
                        }
                    )
                );
            }
        }

        public void ShowSelection()
        {
            if (Transitioning)
            {
                return;
            }

            ClearEditorReturn();
            Load(SelectionPath);
        }

        public void ShowTitle()
        {
            if (Transitioning)
            {
                return;
            }

            ClearEditorReturn();
            Load("Assets/Game/Scenes/Title.unity");
        }

        public void ShowSettings()
        {
            if (Transitioning || !SceneManager.GetSceneByPath(SelectionPath).isLoaded)
            {
                return;
            }

            settingsReturnPath = SelectionPath;
            OpenSettingsPopup();
        }

        public void ShowTitleSettings()
        {
            if (
                Transitioning
                || !SceneManager.GetSceneByPath("Assets/Game/Scenes/Title.unity").isLoaded
            )
            {
                return;
            }

            settingsReturnPath = "Assets/Game/Scenes/Title.unity";
            OpenSettingsPopup();
        }

        public void ReturnFromSettings()
        {
            if (Transitioning)
            {
                return;
            }

            ClearEditorReturn();

            if (settingsInputScope == null)
            {
                Load(settingsReturnPath);

                return;
            }

            Transitioning = true;

            try
            {
                settingsInputScope.Close(
                    () => SceneManager.UnloadSceneAsync(SettingsPath),
                    () =>
                    {
                        Transitioning = false;
                        settingsInputScope = null;
                    }
                );
            }
            catch
            {
                Transitioning = false;
                throw;
            }
        }

        private void OpenSettingsPopup()
        {
            if (SceneManager.GetSceneByPath(SettingsPath).isLoaded)
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(settingsReturnPath);
            settingsInputScope = new ModalInputScope(scene);
            Transitioning = true;

            try
            {
                if (SceneManager.LoadSceneAsync(SettingsPath, LoadSceneMode.Additive) == null)
                {
                    throw new InvalidOperationException("Cannot open settings popup.");
                }
            }
            catch
            {
                Transitioning = false;
                settingsInputScope.Dispose();
                settingsInputScope = null;
                throw;
            }
        }

        private void Load(string path)
        {
            Transitioning = true;

            try
            {
                if (SceneManager.LoadSceneAsync(path, LoadSceneMode.Single) == null)
                {
                    throw new InvalidOperationException("Cannot load scene: " + path);
                }
            }
            catch
            {
                Transitioning = false;
                throw;
            }
        }

        private void ReleaseRequest()
        {
            CurrentRequest?.Song.Dispose();
            CurrentRequest = null;
        }

        private void ClearEditorReturn()
        {
            returned = null;
            editorSessionId = null;
            returnScenePath = null;
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
            Audio?.Dispose();
            ReleaseRequest();
        }
    }
}
