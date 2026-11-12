using System;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Core
{
    public sealed class MainMenuActions : IMainMenuActions
    {
        public const string BootstrapScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        public const string TitleScenePath = "Assets/Game/Scenes/Title.unity";
        public const string ModeSelectionScenePath = "Assets/Game/Scenes/ModeSelection.unity";

        public bool Transitioning { get; private set; }

        private readonly GameSettings settings;
        private readonly MainMenuView view;
        private AppFlowController selectionFlow;

        public bool RecoverSelectionFailure()
        {
            if (!Transitioning || !selectionFlow)
            {
                return false;
            }
            if (selectionFlow.SelectionPreparationError == null)
            {
                return false;
            }
            Transitioning = false;
            selectionFlow = null;
            return true;
        }

        public MainMenuActions(GameSettings gameSettings, MainMenuView view = null)
        {
            settings = gameSettings;
            this.view = view;
        }

        public void ShowSettings()
        {
            if (Transitioning)
            {
                return;
            }

            Transitioning = true;

            try
            {
                AppFlowController.Create(settings).ShowTitleSettings();
            }
            catch
            {
                Transitioning = false;
                throw;
            }
        }

        public void ResumeFromSettings() => Transitioning = false;

        public void StartGame()
        {
            if (Transitioning)
            {
                return;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(BootstrapScenePath))
            {
                throw new InvalidOperationException(
                    "Bootstrap scene is not registered in Build Settings."
                );
            }

            Transitioning = true;

            try
            {
                if (view && view.modeSelection)
                {
                    var flow = AppFlowController.Create(settings);
                    selectionFlow = flow;
                    var rules = flow.Rules.Snapshot;
                    rules.Mode = RhythmDojo.Gameplay.PlayMode.Gauge;
                    if (view.modeSelection.SelectedIndex == 2)
                    {
                        rules.Mode = RhythmDojo.Gameplay.PlayMode.Challenge;
                    }
                    flow.Rules.Update(rules);
                    flow.ShowSelection();
                    return;
                }
                if (view && view.entrance)
                {
                    DojoEntranceTransition.Begin(
                        view.entrance, view.doorSlide, view.startPrompt.font, ReturnToTitle);
                    // The title already has the settings needed to compose selection.
                    // Avoid activating and unloading an intermediate Bootstrap scene.
                    AppFlowController.Create(settings).ShowSelection();
                    return;
                }

                AppFlowController.Create(settings).CancelSelectionPreparation();
                if (SceneManager.LoadSceneAsync(BootstrapScenePath, LoadSceneMode.Single) == null)
                {
                    throw new InvalidOperationException("Cannot load Bootstrap.");
                }
            }
            catch
            {
                if (DojoEntranceTransition.Active)
                {
                    DojoEntranceTransition.Active.Cancel();
                }

                Transitioning = false;
                throw;
            }
        }

        public void ReturnToTitle()
        {
            if (!view || !view.modeSelection)
            {
                AppFlowController.Create(settings).CancelSelectionPreparation();
                Transitioning = false;
                SceneManager.LoadSceneAsync(TitleScenePath, LoadSceneMode.Single);
                return;
            }
            if (Transitioning)
            {
                return;
            }
            AppFlowController.Create(settings).CancelSelectionPreparation();
            Transitioning = true;
            try
            {
                if (SceneManager.LoadSceneAsync(TitleScenePath, LoadSceneMode.Additive) == null)
                {
                    throw new InvalidOperationException("Cannot load Title.");
                }
            }
            catch
            {
                Transitioning = false;
                throw;
            }
        }

        public void ShowEditor()
        {
            if (Transitioning)
            {
                return;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(RhythmDojo.UI.ChartEditorMenu.ScenePath))
            {
                throw new InvalidOperationException("Chart editor scene is not registered in Build Settings.");
            }

            Transitioning = true;

            try
            {
                AppFlowController.Create(settings).CancelSelectionPreparation();
                if (SceneManager.LoadSceneAsync(RhythmDojo.UI.ChartEditorMenu.ScenePath, LoadSceneMode.Single) == null)
                {
                    throw new InvalidOperationException("Cannot load ChartEditor.");
                }
            }
            catch
            {
                Transitioning = false;
                throw;
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(0);
#endif
        }
    }
}
