using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RhythmDojo.Services;
using RhythmDojo.Gameplay;
using RhythmDojo.Application;

namespace RhythmDojo.UI
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private SettingsView view;
        private IAppNavigation navigation;
        private SoundSettingsController sound;
        private GraphicsSettingsController graphics;
        private GameplaySettingsController gameplay;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Settings view missing.");
            view.Validate();
        }
        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation)
            => Initialize(audioSettings, appNavigation, null, null, new SelectionPreferences(null));
        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation,
            GameSettings gameSettings, ISongSelectionService songs, ISelectionPreferences savedPreferences,
            HardModePreferences hardMode = null, IPlayRulesService rules = null, PresentationPreferences presentation = null)
        {
            Unbind(); Validate(); navigation = appNavigation;
            presentation ??= new PresentationPreferences();
            sound = new SoundSettingsController(view, presentation);
            graphics = new GraphicsSettingsController(view, presentation, audioSettings, appNavigation);
            gameplay = new GameplaySettingsController(view, gameSettings, songs, savedPreferences ?? new SelectionPreferences(null),
                hardMode ?? new HardModePreferences(null), rules);
            view.back.onClick.AddListener(Back); view.soundTab.onClick.AddListener(ShowSound);
            view.graphicsTab.onClick.AddListener(ShowGraphics); view.gameplayTab.onClick.AddListener(ShowGameplay);
            ShowSound();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.soundTab.gameObject);
        }
        private void ShowSound() => SelectTab(false);
        private void ShowGraphics() => SelectTab(true);
        private void ShowGameplay() => SelectTab(false, true);
        private void SelectTab(bool graphics, bool gameplay = false)
        {
            view.soundPanel.SetActive(!graphics && !gameplay); view.graphicsPanel.SetActive(graphics);
            view.gameplayPanel.SetActive(gameplay);
            view.soundTab.image.color = !graphics && !gameplay ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            view.graphicsTab.image.color = graphics ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            view.gameplayTab.image.color = gameplay ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            var nav = view.apply.navigation; nav.selectOnUp = gameplay ? this.gameplay.HardModeLastControl() : graphics ? view.bufferList : view.gameplayVolume; view.apply.navigation = nav;
        }
        private void Update()
        {
            sound?.Tick(); graphics?.Tick();
            if (navigation != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                view.GetComponentsInChildren<Canvas>().Length == 1 && !navigation.Transitioning) Back();
        }
        private void Back()
        {
            if (navigation is ISettingsReturnNavigation returning) returning.ReturnFromSettings();
            else navigation.ShowSelection();
        }
        private void Unbind()
        {
            sound?.Dispose(); graphics?.Dispose(); gameplay?.Dispose(); sound = null; graphics = null; gameplay = null;
            if (!view) return;
            view.back.onClick.RemoveListener(Back); view.soundTab.onClick.RemoveListener(ShowSound);
            view.graphicsTab.onClick.RemoveListener(ShowGraphics); view.gameplayTab.onClick.RemoveListener(ShowGameplay);
        }
        private void OnDestroy() => Unbind();
    }
}
