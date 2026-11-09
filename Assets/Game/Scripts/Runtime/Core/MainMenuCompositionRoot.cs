using System;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Core
{
    public sealed class MainMenuCompositionRoot : MonoBehaviour
    {
        [SerializeField]
        private MainMenuScreen screen;

        [SerializeField]
        private TitleMusicPlayer music;

        [SerializeField]
        private TitleMusicSettings titleMusic;

        [SerializeField]
        private GameSettings gameSettings;

        public void Validate()
        {
            if (!screen)
            {
                throw new InvalidOperationException("Main menu screen missing.");
            }

            screen.Validate();

            if (titleMusic)
            {
                if (!music)
                {
                    throw new InvalidOperationException("Title music player missing.");
                }

                titleMusic.Validate();
            }

            if (!gameSettings)
            {
                throw new InvalidOperationException("Title game settings missing.");
            }

            gameSettings.ValidateOptions();
        }

        private void Start()
        {
            Validate();

            // Discover saved songs and initialize audio/graphics before the title
            // becomes interactive, rather than doing that work on the Play click.
            var flow = AppFlowController.Create(gameSettings);

            var presentation =
                RhythmDojo.Services.PresentationPreferences.Current
                ?? RhythmDojo.Services.PresentationPreferences.Load();
            AudioListener.volume = presentation.masterVolume;

            RhythmDojo.Services.IMainMenuBeatClock beatClock = null;
            if (titleMusic)
            {
                music.Initialize(titleMusic);
                beatClock = music;
            }

            screen.Initialize(new MainMenuActions(gameSettings, screen.GetComponent<MainMenuView>()), beatClock);
            flow.PrepareSelection();
        }
    }
}
