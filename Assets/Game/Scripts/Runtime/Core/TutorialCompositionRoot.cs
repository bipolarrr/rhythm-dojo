using System;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Core
{
    public sealed class TutorialCompositionRoot : MonoBehaviour
    {
        [SerializeField] private TutorialScreen screen;
        [SerializeField] private GameSettings gameSettings;
        [SerializeField] private TitleMusicPlayer music;
        [SerializeField] private TitleMusicSettings titleMusic;
        private bool returning;

        public void Validate()
        {
            if (!screen || !gameSettings)
            {
                throw new InvalidOperationException("Tutorial scene dependencies missing.");
            }
            if (!music || !titleMusic)
            {
                throw new InvalidOperationException("Tutorial music composition missing.");
            }
            screen.Validate();
            titleMusic.Validate();
        }

        private void Start()
        {
            Validate();
            AppFlowController.Create(gameSettings).CancelSelectionPreparation();
            music.Initialize(titleMusic);
            screen.Initialize(Return);
        }

        private void Return()
        {
            if (returning)
            {
                return;
            }
            returning = true;
            if (SceneManager.LoadSceneAsync(MainMenuActions.ModeSelectionScenePath, LoadSceneMode.Single) == null)
            {
                returning = false;
                throw new InvalidOperationException("Cannot return to ModeSelection.");
            }
        }
    }
}
