using System;
using UnityEngine;
using UnityEngine.EventSystems;
using RhythmDojo.Services;

namespace RhythmDojo.UI
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        [Header("Title Beat Bounce")]
        [SerializeField, Min(1f)] private float beatsPerMinute = 120f;
        [SerializeField, Range(0f, .3f)] private float titleBounceAmount = .08f;
        [SerializeField, Range(0f, .3f)] private float promptBounceAmount = .05f;
        [Tooltip("Fraction of each beat used to return to the original size.")]
        [SerializeField, Range(.1f, 1f)] private float bounceDurationInBeats = .65f;
        private IMainMenuActions actions;
        private IMainMenuBeatClock beatClock;
        private bool starting, quitting;
        private Vector3 titleScale, promptScale;
        private double beatStartTime;
        private bool bounceInitialized;
        private bool menuExpanded;
        private float expansion;

        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Main menu view missing.");
            view.Validate();
        }

        public void Initialize(IMainMenuActions menuActions, IMainMenuBeatClock menuBeatClock = null)
        {
            Unbind();
            Validate();
            titleScale = view.logo.transform.localScale;
            promptScale = view.startPrompt.rectTransform.localScale;
            beatStartTime = AudioSettings.dspTime;
            bounceInitialized = true;
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            beatClock = menuBeatClock;
            starting = quitting = false;
            menuExpanded = false; expansion = 0f;
            view.SetExpansion(0f);
            view.menuPanel.gameObject.SetActive(false);
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            view.confirmQuit.interactable = view.cancelQuit.interactable = true;
            view.start.onClick.AddListener(StartGame);
            view.logo.onClick.AddListener(ToggleMenu);
            view.settings.onClick.AddListener(OpenSettings);
            view.quit.onClick.AddListener(ShowQuit);
            view.confirmQuit.onClick.AddListener(ConfirmQuit);
            view.cancelQuit.onClick.AddListener(CancelQuit);
        }

        private bool Busy => actions == null || starting || quitting || actions.Transitioning;

        private void Update()
        {
            if (!bounceInitialized) return;
            expansion = Mathf.MoveTowards(expansion, menuExpanded ? 1f : 0f, Time.unscaledDeltaTime / .25f);
            view.SetExpansion(expansion);
            view.menuPanel.gameObject.SetActive(expansion > 0f || menuExpanded);
            if (Busy || view.quitPopup.activeSelf)
            {
                RestoreScale();
                return;
            }

            // Absolute audio time keeps both labels on the same beat without frame drift.
            double beats = beatClock != null ? beatClock.BeatPosition :
                Math.Max(0, AudioSettings.dspTime - beatStartTime) * Math.Max(1f, beatsPerMinute) / 60d;
            if (beats < 0)
            {
                RestoreScale();
                return;
            }
            float phase = (float)(beats - Math.Floor(beats));
            float progress = Mathf.Clamp01(phase / Mathf.Clamp(bounceDurationInBeats, .1f, 1f));
            float bounce = 1f - Mathf.SmoothStep(0f, 1f, progress);
            view.logo.transform.localScale = titleScale * (1f + bounce * Mathf.Clamp(titleBounceAmount, 0f, .3f));
            view.startPrompt.rectTransform.localScale = promptScale * (1f + bounce * Mathf.Clamp(promptBounceAmount, 0f, .3f));
        }

        private void RestoreScale()
        {
            if (!bounceInitialized || !view) return;
            if (view.logo) view.logo.transform.localScale = titleScale;
            if (view.startPrompt) view.startPrompt.rectTransform.localScale = promptScale;
        }

        private void OnEnable() => beatStartTime = AudioSettings.dspTime;
        private void OnDisable() => RestoreScale();

        private void StartGame()
        {
            if (Busy || !menuExpanded || view.quitPopup.activeSelf) return;
            starting = true;
            view.SetMenuInteractable(false);
            try { actions.StartGame(); }
            catch (Exception error)
            {
                starting = false;
                view.SetMenuInteractable(true);
                Debug.LogException(error, this);
            }
        }

        private void ToggleMenu()
        {
            if (Busy || view.quitPopup.activeSelf) return;
            menuExpanded = !menuExpanded;
        }

        private void OpenSettings()
        {
            if (Busy || !menuExpanded || view.quitPopup.activeSelf) return;
            starting = true; view.SetMenuInteractable(false);
            try { actions.ShowSettings(); }
            catch (Exception error)
            {
                starting = false; view.SetMenuInteractable(true); Debug.LogException(error, this);
            }
        }

        private void ShowQuit()
        {
            if (Busy || !menuExpanded) return;
            view.SetMenuInteractable(false);
            view.quitPopup.SetActive(true);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(view.cancelQuit.gameObject);
        }

        private void CancelQuit()
        {
            if (Busy) return;
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        private void ConfirmQuit()
        {
            if (Busy || !view.quitPopup.activeSelf) return;
            quitting = true;
            view.confirmQuit.interactable = view.cancelQuit.interactable = false;
            actions.QuitGame();
        }

        private void Unbind()
        {
            RestoreScale();
            if (!view) return;
            view.start.onClick.RemoveListener(StartGame);
            view.logo.onClick.RemoveListener(ToggleMenu);
            view.settings.onClick.RemoveListener(OpenSettings);
            view.quit.onClick.RemoveListener(ShowQuit);
            view.confirmQuit.onClick.RemoveListener(ConfirmQuit);
            view.cancelQuit.onClick.RemoveListener(CancelQuit);
        }

        private void OnDestroy() => Unbind();
    }
}
