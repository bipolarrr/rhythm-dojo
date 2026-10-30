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

        public bool Transitioning { get; private set; }

        private readonly GameSettings settings;
        private readonly MainMenuView view;

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

        public void ShowChartEditor()
        {
            if (Transitioning)
            {
                return;
            }

            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(ChartEditorMenu.ScenePath))
            {
                throw new InvalidOperationException("Chart editor scene is not registered in Build Settings.");
            }

            Transitioning = true;

            try
            {
                if (SceneManager.LoadSceneAsync(ChartEditorMenu.ScenePath, LoadSceneMode.Single) == null)
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

        public void ShowChartWorkspace() => ShowChartEditor();

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
                if (view && view.entrance)
                {
                    DojoEntranceTransition.Begin(
                        view.entrance, view.doorSlide, view.startPrompt.font, ReturnToTitle);
                }

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

        private static void ReturnToTitle()
        {
            SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity", LoadSceneMode.Single);
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
