using System;
using UnityEngine;
using RhythmDojo.UI;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Core
{
    public sealed class MainMenuCompositionRoot : MonoBehaviour
    {
        [SerializeField] private MainMenuScreen screen;
        [SerializeField] private TitleMusicSettings titleMusic;
        [SerializeField] private GameSettings gameSettings;

        public void Validate()
        {
            if (!screen) throw new InvalidOperationException("Main menu screen missing.");
            screen.Validate();
            if (titleMusic) titleMusic.Validate();
            if (!gameSettings) throw new InvalidOperationException("Title game settings missing.");
            gameSettings.ValidateOptions();
        }

        private void Start()
        {
            Validate();
            TitleMusicPlayer music = null;
            if (titleMusic)
            {
                music = gameObject.AddComponent<TitleMusicPlayer>();
                music.Initialize(titleMusic);
            }
            screen.Initialize(new MainMenuActions(gameSettings), music);
        }
    }
}
