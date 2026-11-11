using System;
using System.Collections;
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
        public const string ResultsPath = "Assets/Game/Scenes/Results.unity";

        [SerializeField]
        private GameSettings settings;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private string editorSessionId,
            returnScenePath;
        private string settingsReturnPath = SelectionPath;
        private readonly List<Behaviour> settingsSuspended = new List<Behaviour>();
        private GameObject settingsPreviousFocus;
        private ModalInputScope settingsInputScope;
        private SettingsScreen settingsPopup;
        private GameModeDefinition settingsKeyMode;
        private string settingsKeyModeId;
        private Action<string> returned;
        private PreparedSelectionScene selectionPreparation;
        private Func<CancellationToken, Task> retryPlay;

        public bool IsSelectionPrepared => selectionPreparation != null && selectionPreparation.IsSelectionPrepared;
        public Exception SelectionPreparationError => selectionPreparation?.LastError;

        public GameSettings Settings => settings;

        public SongLibrary Library { get; private set; }
        public SongSelectionService Selection { get; private set; }
        public AudioSettingsService Audio { get; private set; }
        public ISelectionPreferences Preferences { get; private set; }
        public HardModePreferences HardMode { get; private set; }
        public IPlayRulesService Rules { get; private set; }

        public bool IsEditorPlaytest => returned != null;

        public PlayRequest CurrentRequest { get; private set; }
        public PlayResult LastResult { get; private set; }
        public bool CanRetryResult => retryPlay != null && SceneManager.GetSceneByPath(ResultsPath).isLoaded;
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
            PlayKeyBindings.ConfigureDefaultMode(settings.catalog[0].Mode);
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

            flow.Audio = new AudioSettingsService(flow.CanApplyAudioSettings);
            flow.selectionPreparation = new PreparedSelectionScene(flow);
            DontDestroyOnLoad(flow.gameObject);

            return flow;
        }

        private bool CanApplyAudioSettings()
        {
            if (Transitioning)
            {
                return false;
            }

            if (settingsPopup && settingsPopup.gameObject.activeInHierarchy)
            {
                return true;
            }

            return SceneManager.GetSceneByPath(SettingsPath).isLoaded;
        }

        private void Update()
        {
            Audio?.Tick();
            selectionPreparation?.Tick();
        }

        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (selectionPreparation != null && selectionPreparation.HandleSceneLoaded(scene, mode))
            {
                return;
            }
            Transitioning = false;

            if (scene.path == GameplayPath && CurrentRequest != null)
            {
                FindFirstObjectByType<GameplayCompositionRoot>()
                    .Initialize(CurrentRequest, Settings, this);
            }
            else if (scene.path != GameplayPath)
            {
                ReleaseRequest();

                if (scene.path != ResultsPath)
                {
                    retryPlay = null;
                }

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
            await LoadCatalogPlayAsync(songId, difficulty, scrollMode, multiplier, timingOffsetMs,
                healthRules, rules, cancellationToken);
        }

        private async Task LoadCatalogPlayAsync(
            string songId,
            DifficultyProfile difficulty,
            ScrollMode scrollMode,
            double multiplier,
            int timingOffsetMs,
            HardModeHealthRules healthRules,
            PlayRules rules,
            CancellationToken cancellationToken,
            (string Id, string Revision)? expectedChart = null
        )
        {
            Audio.CancelBufferValidation();
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

                if (expectedChart.HasValue)
                {
                    if (song.ChartId != expectedChart.Value.Id || song.Revision != expectedChart.Value.Revision)
                    {
                        throw new SongLoadException(new SongLoadFailure(
                            SongLoadFailureKind.InvalidChart,
                            "채보가 변경되었습니다. 곡 선택에서 새로고침한 뒤 다시 입장해 주세요."));
                    }
                }

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
                Load(GameplayPath);
                LastResult = null;
                retryPlay = null;
                ClearEditorReturn();
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
            Audio.CancelBufferValidation();
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
                LastResult = null;
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

        public void ShowResults(PlayResult result)
        {
            ShowResults(result, CurrentRequest?.Multiplier ?? 1);
        }

        internal void ShowResults(PlayResult result, double multiplier)
        {
            if (Transitioning)
            {
                return;
            }

            LastResult = result ?? throw new ArgumentNullException(nameof(result));
            retryPlay = null;
            if (CurrentRequest != null && !IsEditorPlaytest)
            {
                var songId = CurrentRequest.Song.SongId;
                var expectedChart = (CurrentRequest.Song.ChartId, CurrentRequest.Song.Revision);
                var difficulty = CurrentRequest.Difficulty;
                var scrollMode = CurrentRequest.ScrollMode;
                int timingOffsetMs = CurrentRequest.TimingOffsetMs;

                result.RecommendTimingOffset(timingOffsetMs);

                var healthRules = CurrentRequest.HardModeRules;
                var rules = CurrentRequest.Rules;
                retryPlay = token => LoadCatalogPlayAsync(
                    songId,
                    difficulty,
                    scrollMode,
                    multiplier,
                    result.AppliedTimingOffsetMs ?? timingOffsetMs,
                    healthRules,
                    rules,
                    token,
                    expectedChart
                );
            }

            Load(ResultsPath);
        }

        public Task RetryResultAsync(CancellationToken cancellationToken)
        {
            if (Transitioning || !CanRetryResult)
            {
                return Task.CompletedTask;
            }

            return retryPlay(cancellationToken);
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

        public void PrepareSelection()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == MainMenuActions.ModeSelectionScenePath)
            {
                selectionPreparation.PrepareSelection(scene);
            }
        }

        public bool CanInitializeSelection(Scene scene) => selectionPreparation.CanInitialize(scene);

        public void SelectionInitialized(SongSelectionScreen screen)
        {
            if (!selectionPreparation.SelectionInitialized(screen))
            {
                DojoEntranceTransition.SelectionReady(screen);
            }
        }

        public void CancelSelectionPreparation()
        {
            bool activating = selectionPreparation.IsActivating;
            selectionPreparation.Cancel();
            if (activating)
            {
                Transitioning = false;
            }
        }

        public void SelectionPreparationFailed()
        {
            Transitioning = false;
            if (DojoEntranceTransition.Active)
            {
                DojoEntranceTransition.Active.Fail();
            }
        }

        public void SelectionPreparationActivated() => Transitioning = false;

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
            SelectSettingsKeyMode();
            OpenSettingsPopup();
        }

        internal void ShowResultTimingCalibration()
        {
            if (Transitioning || !SceneManager.GetSceneByPath(ResultsPath).isLoaded)
            {
                return;
            }

            var result = LastResult;
            if (result == null)
            {
                return;
            }

            settingsReturnPath = ResultsPath;
            SelectSettingsKeyMode();
            OpenSettingsPopup(onApplied: offset => result.AppliedTimingOffsetMs = offset);
        }

        public void ShowGameplaySettings()
        {
            if (Transitioning || !SceneManager.GetSceneByPath(GameplayPath).isLoaded)
            {
                return;
            }

            var game = FindFirstObjectByType<RhythmGameController>();
            if (!game || !game.IsPaused)
            {
                return;
            }

            settingsReturnPath = GameplayPath;
            settingsKeyMode = CurrentRequest?.Song.Mode;
            settingsKeyModeId = CurrentRequest?.Song.ModeId ?? "";
            OpenSettingsPopup(game);
        }

        public void ShowTitleSettings()
        {
            if (Transitioning)
            {
                return;
            }

            settingsReturnPath = MainMenuActions.ModeSelectionScenePath;
            if (!SceneManager.GetSceneByPath(settingsReturnPath).isLoaded)
            {
                settingsReturnPath = MainMenuActions.TitleScenePath;
                if (!SceneManager.GetSceneByPath(settingsReturnPath).isLoaded)
                {
                    return;
                }
            }
            settingsKeyMode = settings.catalog[0].Mode;
            settingsKeyModeId = settingsKeyMode.name;
            OpenSettingsPopup();
        }

        private void SelectSettingsKeyMode()
        {
            settingsKeyMode = null;
            settingsKeyModeId = "";
            var entry = Library.Find(Selection.SelectedSongId);
            if (entry == null)
            {
                return;
            }
            settingsKeyMode = entry.Mode;
            settingsKeyModeId = entry.ModeId ?? "";
            foreach (var chart in entry.Charts)
            {
                if (chart.Slot != Selection.SelectedSlot)
                {
                    continue;
                }
                if (chart.Mode && !string.IsNullOrWhiteSpace(chart.ModeId))
                {
                    settingsKeyMode = chart.Mode;
                    settingsKeyModeId = chart.ModeId;
                }
                break;
            }
            if (string.IsNullOrWhiteSpace(settingsKeyModeId))
            {
                settingsKeyMode = null;
            }
        }

        public void ReturnFromSettings()
        {
            if (Transitioning)
            {
                return;
            }

            Audio.CancelBufferValidation();
            if (settingsReturnPath != GameplayPath && settingsReturnPath != ResultsPath)
            {
                ClearEditorReturn();
            }

            if (settingsInputScope == null)
            {
                Load(settingsReturnPath);

                return;
            }

            settingsPopup.gameObject.SetActive(false);
            settingsInputScope.Dispose();
            settingsInputScope = null;
        }

        private void OpenSettingsPopup(RhythmGameController game = null, Action<int> onApplied = null)
        {
            if (SceneManager.GetSceneByPath(SettingsPath).isLoaded)
            {
                return;
            }

            if (settingsInputScope != null)
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(settingsReturnPath);
            settingsInputScope = new ModalInputScope(scene);

            try
            {
                if (!settingsPopup)
                {
                    var prefab = Resources.Load<GameObject>("UI/SettingsPopup");
                    if (!prefab)
                    {
                        throw new InvalidOperationException(
                            "Settings popup prefab missing. Run Build Settings.");
                    }

                    settingsPopup = Instantiate(prefab, transform).GetComponent<SettingsScreen>();
                }

                settingsPopup.gameObject.SetActive(true);
                settingsPopup.Initialize(
                    Audio,
                    this,
                    settings,
                    Selection,
                    Preferences,
                    HardMode,
                    Rules,
                    PresentationPreferences.Current,
                    settingsKeyMode,
                    settingsKeyModeId
                );

                if (game)
                {
                    var snapshot = game.ReadModel.Snapshot;
                    var gameplay = game.GetComponent<GameplayCompositionRoot>();
                    settingsPopup.BindGameplaySpeed(
                        snapshot.ScrollMode,
                        snapshot.ScrollMultiplier,
                        gameplay.SetScrollMultiplier
                    );
                }

                if (onApplied != null)
                {
                    settingsPopup.OpenTimingCalibration(onApplied);
                }
            }
            catch
            {
                if (settingsPopup)
                {
                    settingsPopup.gameObject.SetActive(false);
                }

                settingsInputScope.Dispose();
                settingsInputScope = null;
                throw;
            }
        }

        private void Load(string path)
        {
            Audio?.CancelBufferValidation();
            if (settingsInputScope != null)
            {
                settingsPopup.gameObject.SetActive(false);
                settingsInputScope.Dispose();
                settingsInputScope = null;
            }

            Transitioning = true;

            try
            {
                if (path == SelectionPath)
                {
                    bool fromTitle = SceneManager.GetActiveScene().path == "Assets/Game/Scenes/Title.unity";
                    fromTitle = fromTitle || SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath;
                    if (DojoEntranceTransition.Active || fromTitle)
                    {
                        LoadEntranceSelection(path);
                        return;
                    }
                }
                CancelSelectionPreparation();
                Transitioning = true;
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

        private void LoadEntranceSelection(string path)
        {
            selectionPreparation.ActivatePreparedSelection(SceneManager.GetActiveScene());
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
            selectionPreparation?.Dispose();
            settingsInputScope?.Dispose();
            lifetime.Cancel();
            lifetime.Dispose();
            Audio?.Dispose();
            ReleaseRequest();
        }
    }
}
