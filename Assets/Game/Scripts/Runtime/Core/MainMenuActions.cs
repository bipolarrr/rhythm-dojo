using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.Services;

namespace RhythmDojo.Core
{
    public sealed class MainMenuActions : IMainMenuActions
    {
        public const string BootstrapScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        public bool Transitioning { get; private set; }

        public void StartGame()
        {
            if (Transitioning) return;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(BootstrapScenePath))
                throw new InvalidOperationException("Bootstrap scene is not registered in Build Settings.");
            Transitioning = true;
            try
            {
                if (SceneManager.LoadSceneAsync(BootstrapScenePath, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException("Cannot load Bootstrap.");
            }
            catch { Transitioning = false; throw; }
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