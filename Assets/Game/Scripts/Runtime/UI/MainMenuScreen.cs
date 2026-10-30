using System;
using System.Collections;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RhythmDojo.UI
{
    public sealed class MainMenuScreen : MonoBehaviour, IModalInputParticipant
    {
        [SerializeField]
        private MainMenuView view;

        [Header("Title Beat Bounce")]
        [SerializeField, Min(1f)]
        private float beatsPerMinute = 120f;

        [SerializeField, Range(0f, .3f)]
        private float titleBounceAmount = .08f;

        [SerializeField, Range(0f, .3f)]
        private float promptBounceAmount = 0f;

        [Tooltip("Fraction of each beat used to return to the original size.")]
        [SerializeField, Range(.1f, 1f)]
        private float bounceDurationInBeats = .65f;
        private IMainMenuActions actions;
        private IMainMenuBeatClock beatClock;
        private bool starting,
            quitting;
        private Vector3 titleScale,
            promptScale;
        private double beatStartTime;
        private bool bounceInitialized;
        private bool menuExpanded;
        private float expansion;
        private AudioSource effects;
        private float knockTime;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Main menu view missing.");
            }

            view.Validate();
        }

        public void Initialize(
            IMainMenuActions menuActions,
            IMainMenuBeatClock menuBeatClock = null
        )
        {
            StopAllCoroutines();
            Unbind();
            Validate();
            view.RestoreBoards();
            view.logoArtwork.localRotation = Quaternion.identity;
            titleScale = view.logoArtwork.localScale;
            promptScale = view.startPrompt.rectTransform.localScale;
            beatStartTime = AudioSettings.dspTime;
            bounceInitialized = true;
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            beatClock = menuBeatClock;
            starting = quitting = false;
            menuExpanded = view.alwaysVisibleMenu;
            expansion = 0f;
            if (view.alwaysVisibleMenu)
            {
                expansion = 1f;
            }
            view.SetExpansion(0f);
            view.menuPanel.gameObject.SetActive(view.alwaysVisibleMenu);
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            view.confirmQuit.interactable = view.cancelQuit.interactable = true;
            view.start.onClick.AddListener(StartGame);
            view.logo.onClick.AddListener(ToggleMenu);
            view.settings.onClick.AddListener(OpenSettings);
            view.chartEditor.onClick.AddListener(OpenChartEditor);
            if (view.chartWorkspace)
            {
                view.chartWorkspace.onClick.AddListener(OpenChartWorkspace);
            }
            view.quit.onClick.AddListener(ShowQuit);
            view.confirmQuit.onClick.AddListener(ConfirmQuit);
            view.cancelQuit.onClick.AddListener(CancelQuit);
            if (!effects)
            {
                effects = gameObject.AddComponent<AudioSource>();
                effects.playOnAwake = false;
            }

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.logo.gameObject);
            }
        }

        private bool Busy => actions == null || starting || quitting || actions.Transitioning;

        private void Update()
        {
            if (!bounceInitialized)
            {
                return;
            }

            if (starting)
            {
                RestoreScale();
                return;
            }

            if (menuExpanded)
            {
                expansion = Mathf.MoveTowards(expansion, 1f, Time.unscaledDeltaTime / .25f);
            }
            else
            {
                expansion = Mathf.MoveTowards(expansion, 0f, Time.unscaledDeltaTime / .25f);
            }

            view.SetExpansion(expansion);
            view.menuPanel.gameObject.SetActive(expansion > 0f || menuExpanded);
            if (knockTime > 0f)
            {
                knockTime = Mathf.Max(0f, knockTime - Time.unscaledDeltaTime);
                float angle = Mathf.Sin(knockTime * 65f) * knockTime * 10f;
                view.logoArtwork.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (Busy || view.quitPopup.activeSelf)
            {
                RestoreScale();

                return;
            }

            // Absolute audio time keeps both labels on the same beat without frame drift.
            double beats;

            if (beatClock != null)
            {
                beats = beatClock.BeatPosition;
            }
            else
            {
                beats =
                    Math.Max(0, AudioSettings.dspTime - beatStartTime)
                    * Math.Max(1f, beatsPerMinute)
                    / 60d;
            }

            if (beats < 0)
            {
                RestoreScale();

                return;
            }

            float phase = (float)(beats - Math.Floor(beats));
            float progress = Mathf.Clamp01(phase / Mathf.Clamp(bounceDurationInBeats, .1f, 1f));
            float bounce = 1f - Mathf.SmoothStep(0f, 1f, progress);
            view.logoArtwork.localScale =
                titleScale * (1f + bounce * Mathf.Clamp(titleBounceAmount, 0f, .3f));
            view.startPrompt.rectTransform.localScale =
                promptScale * (1f + bounce * Mathf.Clamp(promptBounceAmount, 0f, .3f));
        }

        private void RestoreScale()
        {
            if (!bounceInitialized || !view)
            {
                return;
            }

            if (view.logoArtwork)
            {
                view.logoArtwork.localScale = titleScale;
            }

            if (view.startPrompt)
            {
                view.startPrompt.rectTransform.localScale = promptScale;
            }
        }

        private void OnEnable() => beatStartTime = AudioSettings.dspTime;

        private void OnDisable() => RestoreScale();

        private void StartGame()
        {
            if (Busy || !menuExpanded || view.quitPopup.activeSelf)
            {
                return;
            }

            if (expansion < .99f)
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);
            StartCoroutine(BreakAndInvoke(view.start, actions.StartGame, true));
        }

        private IEnumerator BreakAndInvoke(
            UnityEngine.UI.Button button, Action action, bool leaving)
        {
            var board = button.GetComponent<DojoBoardBreak>();
            if (board)
            {
                if (view.boardCrack)
                {
                    effects.PlayOneShot(view.boardCrack, .7f);
                }

                yield return board.Play();
            }
            else
            {
                yield return new WaitForSecondsRealtime(.25f);
            }

            if (leaving)
            {
                float elapsed = 0f;
                while (elapsed < .15f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = 1f - Mathf.Clamp01(elapsed / .15f);
                    view.logoGroup.alpha = alpha;
                    view.menuPanel.alpha = alpha;
                    yield return null;
                }
            }

            try
            {
                action();
            }
            catch (Exception error)
            {
                starting = false;
                view.RestoreBoards();
                view.SetMenuInteractable(true);
                Debug.LogException(error, this);
            }
        }

        private void ToggleMenu()
        {
            if (Busy || view.quitPopup.activeSelf)
            {
                return;
            }

            if (view.alwaysVisibleMenu)
            {
                return;
            }

            menuExpanded = !menuExpanded;
            knockTime = .18f;
            if (view.knock)
            {
                effects.PlayOneShot(view.knock, .6f);
            }

            StartCoroutine(FocusMenuAfterExpansion());
        }

        private IEnumerator FocusMenuAfterExpansion()
        {
            yield return new WaitForSecondsRealtime(.26f);
            if (Busy || view.quitPopup.activeSelf || !EventSystem.current)
            {
                yield break;
            }

            if (menuExpanded)
            {
                EventSystem.current.SetSelectedGameObject(view.start.gameObject);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(view.logo.gameObject);
            }
        }

        public void ResumeFromSettings()
        {
            if (actions is RhythmDojo.Core.MainMenuActions mainMenuActions)
            {
                mainMenuActions.ResumeFromSettings();
            }

            starting = false;
            view.RestoreBoards();
            view.SetMenuInteractable(true);
        }

        public void ResumeAfterModal() => ResumeFromSettings();

        private void OpenSettings()
        {
            if (Busy || !menuExpanded || view.quitPopup.activeSelf)
            {
                return;
            }

            if (expansion < .99f)
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);
            StartCoroutine(BreakAndInvoke(view.settings, actions.ShowSettings, false));
        }

        private void OpenChartEditor()
        {
            if (Busy || view.quitPopup.activeSelf)
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);

            try
            {
                actions.ShowChartEditor();
            }
            catch (Exception error)
            {
                starting = false;
                view.SetMenuInteractable(true);
                Debug.LogException(error, this);
            }
        }

        private void OpenChartWorkspace()
        {
            if (Busy || view.quitPopup.activeSelf)
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);
            try
            {
                actions.ShowChartWorkspace();
            }
            catch (Exception error)
            {
                starting = false;
                view.SetMenuInteractable(true);
                Debug.LogException(error, this);
            }
        }

        private void ShowQuit()
        {
            if (Busy || !menuExpanded)
            {
                return;
            }

            if (expansion < .99f)
            {
                return;
            }

            view.SetMenuInteractable(false);
            starting = true;
            StartCoroutine(BreakAndInvoke(view.quit, OpenQuitPopup, false));
        }

        private void OpenQuitPopup()
        {
            starting = false;
            view.quitPopup.SetActive(true);

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.cancelQuit.gameObject);
            }
        }

        private void CancelQuit()
        {
            if (Busy)
            {
                return;
            }

            view.quitPopup.SetActive(false);
            view.RestoreBoards();
            view.SetMenuInteractable(true);

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.quit.gameObject);
            }
        }

        private void ConfirmQuit()
        {
            if (Busy || !view.quitPopup.activeSelf)
            {
                return;
            }

            quitting = true;
            view.confirmQuit.interactable = view.cancelQuit.interactable = false;
            actions.QuitGame();
        }

        private void Unbind()
        {
            RestoreScale();

            if (!view)
            {
                return;
            }

            view.start.onClick.RemoveListener(StartGame);
            view.logo.onClick.RemoveListener(ToggleMenu);
            view.settings.onClick.RemoveListener(OpenSettings);
            view.chartEditor.onClick.RemoveListener(OpenChartEditor);
            if (view.chartWorkspace)
            {
                view.chartWorkspace.onClick.RemoveListener(OpenChartWorkspace);
            }
            view.quit.onClick.RemoveListener(ShowQuit);
            view.confirmQuit.onClick.RemoveListener(ConfirmQuit);
            view.cancelQuit.onClick.RemoveListener(CancelQuit);
        }

        private void OnDestroy() => Unbind();
    }
}
