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
        private Action<string> returned;
        private bool preparingSelection;
        private bool selectionPreparationReady;
        private bool activatePreparedSelection;
        private Scene preparedSelectionScene;
        private Scene entrancePriorScene;
        private SongSelectionScreen preparedSelectionScreen;
        private GameObject preparationPreviousFocus;
        private CanvasGroup selectionPreparationGroup;
        private bool preparationInteractable;
        private bool preparationRaycasts;
        private readonly List<Behaviour> preparedSelectionBehaviours = new List<Behaviour>();
        private readonly List<Canvas> preparedSelectionCanvases = new List<Canvas>();
        private readonly List<int> preparedSelectionOrders = new List<int>();

        public bool IsSelectionPrepared => selectionPreparationReady && preparedSelectionScene.isLoaded;

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

            flow.Audio = new AudioSettingsService(flow.CanApplyAudioSettings);
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

        private void Update() => Audio?.Tick();

        private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != SelectionPath && scene.path != "Assets/Game/Scenes/Title.unity")
            {
                ClearSelectionPreparation();
            }
            if (scene.path == SelectionPath && preparingSelection)
            {
                preparedSelectionScene = scene;
                SuspendPreparedSelection(scene);
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
                LastResult = null;
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
            if (Transitioning)
            {
                return;
            }

            LastResult = result ?? throw new ArgumentNullException(nameof(result));
            Load(ResultsPath);
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
            if (preparingSelection || IsSelectionPrepared)
            {
                return;
            }
            if (SceneManager.GetActiveScene().path != "Assets/Game/Scenes/Title.unity")
            {
                return;
            }

            ClearSelectionPreparation();
            preparingSelection = true;
            try
            {
                if (SceneManager.LoadSceneAsync(SelectionPath, LoadSceneMode.Additive) == null)
                {
                    throw new InvalidOperationException("Cannot prepare song selection.");
                }
            }
            catch
            {
                preparingSelection = false;
                throw;
            }
        }

        private void SuspendPreparedSelection(Scene scene)
        {
            if (EventSystem.current)
            {
                preparationPreviousFocus = EventSystem.current.currentSelectedGameObject;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas)
                    {
                        continue;
                    }
                    preparedSelectionCanvases.Add(canvas);
                    preparedSelectionOrders.Add(canvas.sortingOrder);
                    // Render behind the opaque title so fonts, meshes and materials
                    // really warm up. Disabling the canvas would defer that work.
                    canvas.sortingOrder = -10000;
                }
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (!behaviour.enabled)
                    {
                        continue;
                    }
                    bool input = behaviour is EventSystem || behaviour is GraphicRaycaster;
                    bool output = behaviour is Camera || behaviour is AudioListener;
                    if (input || output || behaviour is SongSelectionScreen)
                    {
                        preparedSelectionBehaviours.Add(behaviour);
                        behaviour.enabled = false;
                    }
                }
                var screen = root.GetComponentInChildren<SongSelectionScreen>(true);
                if (screen)
                {
                    preparedSelectionScreen = screen;
                    selectionPreparationGroup = screen.GetComponent<CanvasGroup>();
                    if (!selectionPreparationGroup)
                    {
                        selectionPreparationGroup = screen.gameObject.AddComponent<CanvasGroup>();
                    }
                    preparationInteractable = selectionPreparationGroup.interactable;
                    preparationRaycasts = selectionPreparationGroup.blocksRaycasts;
                    selectionPreparationGroup.interactable = false;
                    selectionPreparationGroup.blocksRaycasts = false;
                }
            }
        }

        public void SelectionInitialized(SongSelectionScreen screen)
        {
            if (!preparingSelection || screen != preparedSelectionScreen)
            {
                DojoEntranceTransition.SelectionReady(screen);
                return;
            }

            if (EventSystem.current && preparationPreviousFocus)
            {
                EventSystem.current.SetSelectedGameObject(preparationPreviousFocus);
            }
            StartCoroutine(FinishSelectionPreparation());
        }

        private IEnumerator FinishSelectionPreparation()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            if (!preparedSelectionScreen || !preparedSelectionScene.isLoaded)
            {
                ClearSelectionPreparation();
                yield break;
            }

            preparingSelection = false;
            selectionPreparationReady = true;
            if (activatePreparedSelection)
            {
                ActivatePreparedSelection();
            }
        }

        private void ActivatePreparedSelection()
        {
            var priorScene = entrancePriorScene;
            foreach (var root in priorScene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (behaviour is EventSystem || behaviour is AudioListener)
                    {
                        behaviour.enabled = false;
                    }
                }
            }
            foreach (var behaviour in preparedSelectionBehaviours)
            {
                if (behaviour)
                {
                    behaviour.enabled = true;
                }
            }
            selectionPreparationGroup.interactable = preparationInteractable;
            selectionPreparationGroup.blocksRaycasts = preparationRaycasts;
            // Hide entrance panels and block input before bringing the warmed canvas forward.
            DojoEntranceTransition.SelectionReady(preparedSelectionScreen);
            for (int i = 0; i < preparedSelectionCanvases.Count; i++)
            {
                preparedSelectionCanvases[i].sortingOrder = preparedSelectionOrders[i];
            }
            SceneManager.SetActiveScene(preparedSelectionScene);
            var cleanup = SceneManager.UnloadSceneAsync(priorScene);
            if (DojoEntranceTransition.Active)
            {
                DojoEntranceTransition.Active.WaitForSceneCleanup(cleanup);
            }
            Transitioning = false;
            ClearSelectionPreparation();
        }

        private void ClearSelectionPreparation()
        {
            preparingSelection = false;
            selectionPreparationReady = false;
            activatePreparedSelection = false;
            preparedSelectionScene = default;
            preparedSelectionScreen = null;
            selectionPreparationGroup = null;
            preparationPreviousFocus = null;
            preparedSelectionBehaviours.Clear();
            preparedSelectionCanvases.Clear();
            preparedSelectionOrders.Clear();
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

            Audio.CancelBufferValidation();
            if (settingsReturnPath != GameplayPath)
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

        private void OpenSettingsPopup()
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
                    Audio, this, settings, Selection, Preferences, HardMode, Rules,
                    PresentationPreferences.Current);
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
                if (path == SelectionPath && DojoEntranceTransition.Active)
                {
                    LoadEntranceSelection(path);
                    return;
                }
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
            var priorScene = SceneManager.GetActiveScene();
            if (preparingSelection || IsSelectionPrepared)
            {
                entrancePriorScene = priorScene;
                activatePreparedSelection = true;
                if (IsSelectionPrepared)
                {
                    ActivatePreparedSelection();
                }
                return;
            }
            var disabled = new System.Collections.Generic.List<Behaviour>();
            foreach (var root in priorScene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (!behaviour.enabled)
                    {
                        continue;
                    }
                    if (behaviour is UnityEngine.EventSystems.EventSystem || behaviour is AudioListener)
                    {
                        disabled.Add(behaviour);
                        behaviour.enabled = false;
                    }
                }
            }

            try
            {
                // Additive loading avoids the automatic unused-asset collection of a
                // single-scene replacement. Keep the title camera behind the overlay
                // until selection has its own camera, then unload the hidden title.
                var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                if (operation == null)
                {
                    throw new InvalidOperationException("Cannot load scene: " + path);
                }
                operation.completed += _ =>
                {
                    var scene = SceneManager.GetSceneByPath(path);
                    SceneManager.SetActiveScene(scene);
                    var cleanup = SceneManager.UnloadSceneAsync(priorScene);
                    if (DojoEntranceTransition.Active)
                    {
                        DojoEntranceTransition.Active.WaitForSceneCleanup(cleanup);
                    }
                };
            }
            catch
            {
                foreach (var behaviour in disabled)
                {
                    if (behaviour)
                    {
                        behaviour.enabled = true;
                    }
                }
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
            settingsInputScope?.Dispose();
            lifetime.Cancel();
            lifetime.Dispose();
            Audio?.Dispose();
            ReleaseRequest();
        }
    }
}
