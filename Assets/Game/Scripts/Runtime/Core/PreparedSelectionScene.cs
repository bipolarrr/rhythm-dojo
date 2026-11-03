using System;
using System.Collections.Generic;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmDojo.Core
{
    // A single outstanding Unity load is drained before another generation can load the same path.
    public sealed class PreparedSelectionScene : IDisposable
    {
        private readonly AppFlowController flow;
        private readonly Func<AsyncOperation> load;
        private readonly Func<bool> initializationReady;
        private readonly double readyTimeout;
        private readonly List<Canvas> preparedSelectionCanvases = new List<Canvas>();
        private readonly List<int> preparedSelectionOrders = new List<int>();
        private readonly HashSet<int> ownedScenes = new HashSet<int>();
        private bool preparingSelection;
        private bool selectionPreparationReady;
        private bool activatePreparedSelection;
        private Scene preparedSelectionScene;
        private Scene entrancePriorScene;
        private SongSelectionScreen preparedSelectionScreen;
        private GameObject preparationPreviousFocus;
        private CanvasGroup selectionPreparationGroup;
        private ScreenCompositionRoot composition;
        private InputStateScope inputScope;
        private InputStateScope outputScope;
        private InputStateScope priorScope;
        private AsyncOperation operation;
        private AsyncOperation cleanup;
        private int generation;
        private int loadingGeneration;
        private int warmupFrames;
        private double started;
        private bool queued;
        private bool initializing;
        private bool initialized;
        private bool disposed;
        private bool subscribed = true;

        public bool IsSelectionPrepared => selectionPreparationReady && preparedSelectionScene.isLoaded;
        public bool IsActivating => activatePreparedSelection;
        public bool IsBusy => operation != null || cleanup != null;
        public Exception LastError { get; private set; }

        public PreparedSelectionScene(AppFlowController flow, Func<bool> initializationReady = null,
            Func<AsyncOperation> load = null, double readyTimeout = 20)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.initializationReady = initializationReady ?? (() => true);
            this.load = load ?? (() => SceneManager.LoadSceneAsync(
                AppFlowController.SelectionPath, LoadSceneMode.Additive));
            this.readyTimeout = readyTimeout;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        public void PrepareSelection(Scene priorScene)
        {
            if (disposed || preparingSelection || IsSelectionPrepared)
            {
                return;
            }
            entrancePriorScene = priorScene;
            LastError = null;
            started = Time.realtimeSinceStartupAsDouble;
            preparingSelection = true;
            if (operation != null || cleanup != null)
            {
                queued = true;
                return;
            }
            BeginLoad();
        }

        public void ActivatePreparedSelection(Scene priorScene)
        {
            if (disposed || activatePreparedSelection)
            {
                return;
            }
            started = Time.realtimeSinceStartupAsDouble;
            PrepareSelection(priorScene);
            if (!preparingSelection && !IsSelectionPrepared)
            {
                flow.SelectionPreparationFailed();
                return;
            }
            activatePreparedSelection = true;
            entrancePriorScene = priorScene;
            Tick();
        }

        private void BeginLoad()
        {
            loadingGeneration = ++generation;
            try
            {
                operation = load() ?? throw new InvalidOperationException("Cannot prepare song selection.");
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleSceneLoaded(scene);
            if (scene.path != AppFlowController.SelectionPath && scene != entrancePriorScene)
            {
                Cancel();
            }
        }

        public bool HandleSceneLoaded(Scene scene)
        {
            if (ownedScenes.Contains(scene.handle))
            {
                return true;
            }
            if (scene.path != AppFlowController.SelectionPath || operation == null)
            {
                return false;
            }
            ownedScenes.Add(scene.handle);
            preparedSelectionScene = scene;
            if (disposed || loadingGeneration != generation)
            {
                DiscardLoadedScene();
                return true;
            }
            try
            {
                SuspendPreparedSelection(scene);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
            return true;
        }

        private void SuspendPreparedSelection(Scene scene)
        {
            inputScope = new InputStateScope();
            outputScope = new InputStateScope();
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
                    canvas.sortingOrder = -10000;
                }
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    switch (behaviour)
                    {
                        case Camera _:
                        case AudioListener _:
                            outputScope.Suspend(behaviour);
                            break;
                        case EventSystem _:
                        case BaseInputModule _:
                        case GraphicRaycaster _:
                        case SongSelectionScreen _:
                        case ScreenCompositionRoot _:
                            inputScope.Suspend(behaviour);
                            break;
                    }
                }
                var screen = root.GetComponentInChildren<SongSelectionScreen>(true);
                if (!screen)
                {
                    continue;
                }
                preparedSelectionScreen = screen;
                composition = root.GetComponentInChildren<ScreenCompositionRoot>(true);
                selectionPreparationGroup = screen.GetComponent<CanvasGroup>();
                if (!selectionPreparationGroup)
                {
                    selectionPreparationGroup = screen.gameObject.AddComponent<CanvasGroup>();
                }
                inputScope.Block(selectionPreparationGroup);
            }
            if (!composition || !preparedSelectionScreen)
            {
                throw new InvalidOperationException("Prepared selection composition is missing.");
            }
        }

        public bool CanInitialize(Scene scene)
        {
            if (!ownedScenes.Contains(scene.handle))
            {
                return true;
            }
            return initializing && !disposed && loadingGeneration == generation;
        }

        public bool SelectionInitialized(SongSelectionScreen screen)
        {
            return ownedScenes.Contains(screen.gameObject.scene.handle);
        }

        public void Tick()
        {
            if (disposed)
            {
                return;
            }
            if (queued && operation == null && cleanup == null)
            {
                queued = false;
                BeginLoad();
            }
            if (!preparingSelection && !activatePreparedSelection)
            {
                return;
            }
            if (!entrancePriorScene.IsValid() || !entrancePriorScene.isLoaded)
            {
                if (cleanup == null)
                {
                    Cancel();
                    return;
                }
            }
            if (activatePreparedSelection && DojoEntranceTransition.Active)
            {
                if (DojoEntranceTransition.Active.Failed)
                {
                    Cancel();
                    flow.SelectionPreparationFailed();
                    return;
                }
            }
            if (Time.realtimeSinceStartupAsDouble - started >= readyTimeout)
            {
                Fail(new TimeoutException("Song selection preparation timed out."));
                return;
            }
            if (cleanup != null)
            {
                return;
            }
            if (!preparedSelectionScene.IsValid() || !preparedSelectionScene.isLoaded)
            {
                return;
            }
            try
            {
                if (!initialized)
                {
                    if (!initializationReady())
                    {
                        return;
                    }
                    InitializeSelection();
                    return;
                }
                if (!selectionPreparationReady)
                {
                    Canvas.ForceUpdateCanvases();
                    if (++warmupFrames < 2)
                    {
                        return;
                    }
                    preparingSelection = false;
                    selectionPreparationReady = true;
                }
                if (activatePreparedSelection)
                {
                    Activate();
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private void InitializeSelection()
        {
            var system = EventSystem.current;
            preparationPreviousFocus = null;
            if (system)
            {
                preparationPreviousFocus = system.currentSelectedGameObject;
            }
            initializing = true;
            try
            {
                composition.Initialize(flow);
                initialized = true;
            }
            finally
            {
                initializing = false;
                if (system && system.isActiveAndEnabled)
                {
                    if (preparationPreviousFocus && preparationPreviousFocus.activeInHierarchy)
                    {
                        system.SetSelectedGameObject(preparationPreviousFocus);
                    }
                    else
                    {
                        system.SetSelectedGameObject(null);
                    }
                }
            }
        }

        private void Activate()
        {
            int current = generation;
            priorScope = new InputStateScope();
            foreach (var root in entrancePriorScene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
                {
                    switch (behaviour)
                    {
                        case EventSystem _:
                        case BaseInputModule _:
                        case GraphicRaycaster _:
                        case AudioListener _:
                        case IModalInputParticipant _:
                            priorScope.Suspend(behaviour);
                            break;
                    }
                }
            }
            // Keep a display camera throughout cleanup; selection input remains leased.
            outputScope.Dispose();
            outputScope = null;
            SceneManager.SetActiveScene(preparedSelectionScene);
            cleanup = SceneManager.UnloadSceneAsync(entrancePriorScene)
                ?? throw new InvalidOperationException("Cannot unload the previous scene.");
            cleanup.completed += _ =>
            {
                cleanup = null;
                if (disposed || current != generation)
                {
                    DiscardLoadedScene();
                    return;
                }
                try
                {
                    CompleteActivation();
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            };
        }

        private void CompleteActivation()
        {
            // Readiness is delivered only once initialization and old-scene cleanup both finished.
            DojoEntranceTransition.SelectionReady(preparedSelectionScreen);
            inputScope.Dispose();
            inputScope = null;
            priorScope.Dispose();
            priorScope = null;
            RestoreCanvasOrders();
            var scene = preparedSelectionScene;
            ownedScenes.Remove(scene.handle);
            ClearState();
            flow.SelectionPreparationActivated();
        }

        private void RestoreCanvasOrders()
        {
            for (int i = 0; i < preparedSelectionCanvases.Count; i++)
            {
                if (preparedSelectionCanvases[i])
                {
                    preparedSelectionCanvases[i].sortingOrder = preparedSelectionOrders[i];
                }
            }
            preparedSelectionCanvases.Clear();
            preparedSelectionOrders.Clear();
        }

        private void Fail(Exception exception)
        {
            bool activating = activatePreparedSelection;
            LastError = exception;
            Cancel();
            if (activating && flow)
            {
                flow.SelectionPreparationFailed();
            }
        }

        public void Cancel()
        {
            generation++;
            preparingSelection = false;
            selectionPreparationReady = false;
            activatePreparedSelection = false;
            queued = false;
            if (preparedSelectionScene.IsValid() && preparedSelectionScene.isLoaded)
            {
                DiscardLoadedScene();
            }
            else
            {
                ReleaseScopes();
            }
        }

        private void DiscardLoadedScene()
        {
            if (!preparedSelectionScene.IsValid() || !preparedSelectionScene.isLoaded)
            {
                FinishDiscard();
                return;
            }
            var scene = preparedSelectionScene;
            foreach (var root in scene.GetRootGameObjects())
            {
                // Start must not attach services, even if this was an uncancellable late load.
                root.SetActive(false);
            }
            ReleaseScopes();
            if (cleanup != null)
            {
                return;
            }
            cleanup = SceneManager.UnloadSceneAsync(scene);
            if (cleanup == null)
            {
                FinishDiscard();
                return;
            }
            cleanup.completed += _ =>
            {
                ownedScenes.Remove(scene.handle);
                FinishDiscard();
            };
        }

        private void ReleaseScopes()
        {
            try
            {
                inputScope?.Dispose();
            }
            finally
            {
                inputScope = null;
                try
                {
                    outputScope?.Dispose();
                }
                finally
                {
                    outputScope = null;
                    try
                    {
                        priorScope?.Dispose();
                    }
                    finally
                    {
                        priorScope = null;
                        RestoreCanvasOrders();
                    }
                }
            }
        }

        private void FinishDiscard()
        {
            operation = null;
            cleanup = null;
            preparedSelectionScene = default;
            preparedSelectionScreen = null;
            composition = null;
            initialized = false;
            warmupFrames = 0;
            if (disposed)
            {
                Unsubscribe();
            }
        }

        private void ClearState()
        {
            preparingSelection = false;
            selectionPreparationReady = false;
            activatePreparedSelection = false;
            operation = null;
            preparedSelectionScene = default;
            preparedSelectionScreen = null;
            selectionPreparationGroup = null;
            composition = null;
            initialized = false;
            warmupFrames = 0;
        }

        private void Unsubscribe()
        {
            if (subscribed)
            {
                SceneManager.sceneLoaded -= SceneLoaded;
                subscribed = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Cancel();
            if (operation == null && cleanup == null)
            {
                Unsubscribe();
            }
        }
    }
}
