using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField]
        private SettingsView view;
        private IAppNavigation navigation;
        private SoundSettingsController sound;
        private GraphicsSettingsController graphics;
        private GameplaySettingsController gameplay;
        private KeySettingsController keys;
        private float animationTime;
        private float closeProgress;
        private Action closed;

        public bool IsOpening { get; private set; }
        public bool IsClosing { get; private set; }
        public bool IsOpen => isActiveAndEnabled && !IsOpening && !IsClosing;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Settings view missing.");
            }

            view.Validate();
        }

        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation) =>
            Initialize(audioSettings, appNavigation, null, null, new SelectionPreferences(null));

        public void Initialize(
            IAudioSettingsService audioSettings,
            IAppNavigation appNavigation,
            GameSettings gameSettings,
            ISongSelectionService songs,
            ISelectionPreferences savedPreferences,
            HardModePreferences hardMode = null,
            IPlayRulesService rules = null,
            PresentationPreferences presentation = null,
            GameModeDefinition keyMode = null,
            string modeId = null
        )
        {
            Unbind();
            Validate();
            if (!SettingsAccessPolicy.CanChange)
            {
                gameObject.SetActive(false);
                return;
            }

            navigation = appNavigation;
            presentation ??= new PresentationPreferences();
            sound = new SoundSettingsController(view, presentation);
            graphics = new GraphicsSettingsController(
                view,
                presentation,
                audioSettings,
                appNavigation
            );
            gameplay = new GameplaySettingsController(
                view,
                gameSettings,
                songs,
                savedPreferences ?? new SelectionPreferences(null),
                hardMode ?? new HardModePreferences(null),
                rules,
                presentation
            );
            view.back.onClick.AddListener(Back);
            view.dismiss.onClick.AddListener(Back);
            if (!keyMode && modeId == null && gameSettings)
            {
                keyMode = gameSettings.catalog[0].Mode;
                modeId = keyMode.name;
            }
            keys = new KeySettingsController(view, keyMode, modeId);
            view.keySettingsTab.onClick.AddListener(ShowKeys);
            view.soundTab.onClick.AddListener(ShowSound);
            view.graphicsTab.onClick.AddListener(ShowGraphics);
            view.gameplayTab.onClick.AddListener(ShowGameplay);
            ShowSound();
            animationTime = 0;
            IsOpening = true;
            IsClosing = false;
            closed = null;
            view.panelOpacity.interactable = false;
            SetPresentation(0);

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.soundTab.gameObject);
            }
        }

        public void BindGameplaySpeed(ScrollMode mode, double multiplier, Action<double> onSpeedChanged)
        {
            gameplay.BindGameplaySpeed(mode, multiplier, onSpeedChanged);
        }

        internal void OpenTimingCalibration(Action<int> onApplied)
        {
            ShowGameplay();
            gameplay.OpenTimingCalibration(onApplied);
        }

        private void ShowSound() => SelectTab(false);

        private void ShowGraphics() => SelectTab(true);

        private void ShowGameplay() => SelectTab(false, true);

        private void ShowKeys() => SelectTab(false, false, true);

        private void SelectTab(bool graphics, bool gameplay = false, bool keySettings = false)
        {
            keys?.Cancel();
            view.soundPanel.SetActive(!graphics && !gameplay && !keySettings);
            view.graphicsPanel.SetActive(graphics);
            view.gameplayPanel.SetActive(gameplay);
            view.keySettingsPanel.SetActive(keySettings);

            if (!graphics && !gameplay && !keySettings)
            {
                view.soundTab.GetComponent<Image>().color = new Color(.15f, .46f, .5f);
            }
            else
            {
                view.soundTab.GetComponent<Image>().color = new Color(.13f, .17f, .2f);
            }

            if (graphics)
            {
                view.graphicsTab.GetComponent<Image>().color = new Color(.15f, .46f, .5f);
            }
            else
            {
                view.graphicsTab.GetComponent<Image>().color = new Color(.13f, .17f, .2f);
            }

            if (gameplay)
            {
                view.gameplayTab.GetComponent<Image>().color = new Color(.15f, .46f, .5f);
            }
            else
            {
                view.gameplayTab.GetComponent<Image>().color = new Color(.13f, .17f, .2f);
            }

            if (view.pixelTheme)
            {
                StylePixelTab(view.soundTab, !graphics && !gameplay && !keySettings);
                StylePixelTab(view.graphicsTab, graphics);
                StylePixelTab(view.gameplayTab, gameplay);
                StylePixelTab(view.keySettingsTab, keySettings);
            }
            else if (keySettings)
            {
                view.keySettingsTab.image.color = new Color(.15f, .46f, .5f);
            }
            else
            {
                view.keySettingsTab.image.color = new Color(.13f, .17f, .2f);
            }

            var nav = view.apply.navigation;

            if (keySettings)
            {
                nav.selectOnUp = view.resetKeys;
            }
            else if (gameplay)
            {
                nav.selectOnUp = this.gameplay.HardModeLastControl();
            }
            else if (graphics)
            {
                nav.selectOnUp = view.bufferList;
            }
            else
            {
                nav.selectOnUp = view.gameplayVolume;
            }

            view.apply.navigation = nav;
        }

        private static void StylePixelTab(Button button, bool selected)
        {
            button.GetComponent<Image>().color = Color.clear;
            var frame = button.GetComponentInChildren<PixelDojoFrameGraphic>();
            var label = button.GetComponentInChildren<Text>();
            if (selected)
            {
                frame.color = new Color(.49f, .14f, .10f);
                label.color = new Color(1f, .90f, .70f);
            }
            else
            {
                frame.color = new Color(.79f, .63f, .42f);
                if (frame.artwork)
                {
                    frame.color = Color.white;
                }
                label.color = new Color(.24f, .12f, .085f);
            }
        }

        private void Update()
        {
            if (!SettingsAccessPolicy.CanChange)
            {
                CloseImmediately();
                return;
            }

            if (IsClosing)
            {
                animationTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(animationTime / .32f);
                SetPresentation(closeProgress * (1 - t * t * t));
                if (t >= 1)
                {
                    CloseImmediately();
                }
                return;
            }

            if (IsOpening)
            {
                animationTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(animationTime / .38f);
                SetPresentation(1 - Mathf.Pow(1 - t, 3));
                if (t >= 1)
                {
                    IsOpening = false;
                    view.panelOpacity.interactable = true;
                }
            }
            else
            {
                SetPresentation(1);
            }

            if (gameplay?.TickCalibration() == true)
            {
                return;
            }

            sound?.Tick();
            graphics?.Tick();
            if (keys?.Tick() == true)
            {
                return;
            }

            if (
                navigation == null
                || Keyboard.current == null
                || !Keyboard.current.escapeKey.wasPressedThisFrame
            )
            {
                return;
            }

            if (view.GetComponentsInChildren<Canvas>().Length != 1 || navigation.Transitioning)
            {
                return;
            }

            Back();
        }

        private void Back()
        {
            if (!view.back.interactable)
            {
                return;
            }

            if (navigation is ISettingsReturnNavigation returning)
            {
                returning.ReturnFromSettings();
            }
            else
            {
                navigation.ShowSelection();
            }
        }

        private void SetPresentation(float progress)
        {
            var canvasRect = (RectTransform)view.transform;
            float scale = Mathf.Min(1, Mathf.Min(
                canvasRect.rect.width / 1100f, canvasRect.rect.height / 730f));
            scale = Mathf.Max(.01f, scale);
            view.panelRect.localScale = Vector3.one * scale;
            float hiddenY = (canvasRect.rect.height + view.panelRect.rect.height * scale) * .5f + 24;
            view.panelRect.anchoredPosition = new Vector2(0, hiddenY * (1 - progress));
            view.panelOpacity.alpha = progress;
            view.dimOpacity.alpha = progress;
        }

        public void Close(Action completed)
        {
            if (IsClosing)
            {
                return;
            }
            closed = completed;
            closeProgress = view.panelOpacity.alpha;
            animationTime = 0;
            IsOpening = false;
            IsClosing = true;
            view.panelOpacity.interactable = false;
            Unbind();
        }

        public void CloseImmediately()
        {
            var completed = closed;
            closed = null;
            IsOpening = false;
            IsClosing = false;
            Unbind();
            gameObject.SetActive(false);
            completed?.Invoke();
        }

        private void Unbind()
        {
            keys?.Dispose();
            keys = null;
            sound?.Dispose();
            graphics?.Dispose();
            gameplay?.Dispose();
            sound = null;
            graphics = null;
            gameplay = null;

            if (!view)
            {
                return;
            }

            view.back.onClick.RemoveListener(Back);
            view.dismiss.onClick.RemoveListener(Back);
            view.soundTab.onClick.RemoveListener(ShowSound);
            view.graphicsTab.onClick.RemoveListener(ShowGraphics);
            view.gameplayTab.onClick.RemoveListener(ShowGameplay);
            view.keySettingsTab.onClick.RemoveListener(ShowKeys);
        }

        private void OnDisable() => Unbind();

        private void OnDestroy() => Unbind();
    }
}
