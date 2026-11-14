using System;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Core
{
    [DefaultExecutionOrder(-200)]
    public sealed class MainMenuCompositionRoot : MonoBehaviour
    {
        [SerializeField]
        private MainMenuScreen screen;

        [SerializeField] private MainMenuEntranceScreen entranceScreen;
        [SerializeField] private MainMenuView view;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private EventSystem events;

        public MainMenuView View => view;
        public bool IsInitialized { get; private set; }

        [SerializeField]
        private TitleMusicPlayer music;

        [SerializeField]
        private TitleMusicSettings titleMusic;

        [SerializeField]
        private GameSettings gameSettings;

        public void Validate()
        {
            if (!screen && !entranceScreen)
            {
                throw new InvalidOperationException("Main menu screen missing.");
            }

            if (!view || !sceneCamera || !events)
            {
                throw new InvalidOperationException("Main menu scene composition missing.");
            }
            view.Validate();
            if (screen)
            {
                screen.Validate();
            }
            if (entranceScreen)
            {
                entranceScreen.Validate();
            }

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

        private void Awake()
        {
            // Additive previews must never activate a second camera, listener or input module.
            Validate();
            SetOwnership(false);
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

            if (screen)
            {
                screen.Initialize(new MainMenuActions(gameSettings, view), beatClock);
                screen.enabled = false;
                view.modeSelection.InputEnabled = false;
                bool preview = SceneManager.GetSceneByPath(MainMenuActions.TitleScenePath).isLoaded;
                if (preview)
                {
                    view.SetExpansion(0f);
                    view.SetMenuInteractable(false);
                }
                else
                {
                    TakeOwnershipFrom(null);
                    flow.PrepareSelection();
                }
            }
            IsInitialized = true;
            if (entranceScreen)
            {
                entranceScreen.Initialize(this, beatClock);
            }
        }

        public void SetOwnership(bool active)
        {
            sceneCamera.gameObject.SetActive(active);
            events.gameObject.SetActive(active);
            if (music)
            {
                music.gameObject.SetActive(active);
            }
            if (screen)
            {
                screen.enabled = active;
                view.modeSelection.InputEnabled = active;
                view.SetMenuInteractable(active);
                view.menuPanel.interactable = view.menuPanel.blocksRaycasts = active;
            }
        }

        public void TakeOwnershipFrom(MainMenuCompositionRoot previous)
        {
            // Snapshot the playback cursor before disabling the departing scene's source.
            if (previous)
            {
                if (music)
                {
                    music.CopyPlaybackFrom(previous.music);
                }
                previous.SetOwnership(false);
            }
            SetOwnership(true);
            if (screen)
            {
                view.SetExpansion(1f);
                events.SetSelectedGameObject(screen.gameObject);
            }
            else
            {
                events.SetSelectedGameObject(view.logo.gameObject);
            }
        }

        public static MainMenuCompositionRoot InScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                var composition = root.GetComponentInChildren<MainMenuCompositionRoot>(true);
                if (composition)
                {
                    return composition;
                }
            }
            return null;
        }
    }
}
