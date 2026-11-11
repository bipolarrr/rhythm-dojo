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
        [SerializeField]
        private AudioSource effects;
        private float knockTime;
        private int modalClosedFrame = -1;
        private bool doorSoundPlayed;
        private Coroutine focusRoutine;
        public const float MenuExpansionSeconds = 1.35f;

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
            if (view.modeSelection)
            {
                view.modeSelection.ResetPresentation();
                view.modeSelection.previous.onClick.AddListener(PreviousMode);
                view.modeSelection.next.onClick.AddListener(NextMode);
                view.modeSelection.practiceTarget.onClick.AddListener(view.modeSelection.PracticeTarget);
            }
            view.logoArtwork.localRotation = Quaternion.identity;
            titleScale = view.logoArtwork.localScale;
            promptScale = view.startPrompt.rectTransform.localScale;
            beatStartTime = AudioSettings.dspTime;
            bounceInitialized = true;
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            beatClock = menuBeatClock;
            starting = quitting = false;
            modalClosedFrame = -1;
            menuExpanded = view.alwaysVisibleMenu;
            expansion = 0f;
            doorSoundPlayed = false;
            if (view.alwaysVisibleMenu)
            {
                expansion = 1f;
            }
            view.SetExpansion(0f);
            view.menuPanel.gameObject.SetActive(view.alwaysVisibleMenu);
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            view.confirmQuit.interactable = view.cancelQuit.interactable = true;
            if (view.editor)
            {
                view.editor.onClick.AddListener(OpenEditor);
            }

            view.start.onClick.AddListener(StartGame);
            view.logo.onClick.AddListener(ToggleMenu);
            if (view.entranceTrigger)
            {
                view.entranceTrigger.onClick.AddListener(ToggleMenu);
            }
            view.settings.onClick.AddListener(OpenSettings);
            view.quit.onClick.AddListener(ShowQuit);
            view.confirmQuit.onClick.AddListener(ConfirmQuit);
            view.cancelQuit.onClick.AddListener(CancelQuit);
            if (!effects)
            {
                throw new InvalidOperationException("Main menu effect audio source missing.");
            }

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.logo.gameObject);
            }
        }

        private bool Busy
        {
            get
            {
                if (actions == null || starting || quitting)
                {
                    return true;
                }
                if (view.modeSelection && view.modeSelection.IsMoving)
                {
                    return true;
                }
                return actions.Transitioning;
            }
        }

        private void Update()
        {
            if (!bounceInitialized)
            {
                return;
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            bool cancel = keyboard?.escapeKey.wasPressedThisFrame == true;
            cancel = cancel || gamepad?.buttonEast.wasPressedThisFrame == true;
            if (cancel && Time.frameCount != modalClosedFrame)
            {
                if (TryReturnToEntrance())
                {
                    return;
                }
            }
            if (starting && actions is RhythmDojo.Core.MainMenuActions mainMenuActions)
            {
                if (mainMenuActions.RecoverSelectionFailure())
                {
                    starting = false;
                    view.SetMenuInteractable(true);
                    view.modeSelection.detail.text = "곡 목록을 열지 못했습니다. 다시 수련 시작을 눌러 주세요.";
                }
            }
            if (!menuExpanded && view.ambientLights != null)
            {
                float glow = .045f + Mathf.Sin(Time.unscaledTime * 2.3f) * .012f;
                foreach (var light in view.ambientLights)
                {
                    light.color = new Color(1, .68f, .23f, glow);
                }
            }
            if (view.modeSelection)
            {
                bool available = !Busy && !view.quitPopup.activeSelf;
                available = available && menuExpanded && expansion >= .99f;
                view.modeSelection.InputEnabled = available;
                view.modeSelection.practiceTarget.interactable = available && !view.modeSelection.IsArriving;
                if (available)
                {
                    view.start.interactable = view.modeSelection.CanEnter;
                }
                if (available && !view.modeSelection.IsMoving)
                {
                    if (keyboard?.leftArrowKey.wasPressedThisFrame == true || gamepad?.dpad.left.wasPressedThisFrame == true)
                    {
                        PreviousMode();
                    }
                    else if (keyboard?.rightArrowKey.wasPressedThisFrame == true || gamepad?.dpad.right.wasPressedThisFrame == true)
                    {
                        NextMode();
                    }
                    if (keyboard?.spaceKey.wasPressedThisFrame == true)
                    {
                        if (view.modeSelection.SelectedIndex == 0)
                        {
                            view.modeSelection.Practice();
                        }
                        else if (view.modeSelection.SelectedIndex == 1)
                        {
                            view.modeSelection.PracticeTarget();
                        }
                    }
                }
            }
            if (!Busy && Time.frameCount != modalClosedFrame && cancel)
            {
                if (view.quitPopup.activeSelf)
                {
                    CancelQuit();
                }
                else if (menuExpanded)
                {
                    ToggleMenu();
                }

                return;
            }

            if (starting)
            {
                RestoreScale();
                return;
            }

            if (menuExpanded)
            {
                expansion = Mathf.MoveTowards(expansion, 1f, Time.unscaledDeltaTime / MenuExpansionSeconds);
                if (!doorSoundPlayed && expansion >= .14f)
                {
                    doorSoundPlayed = true;
                    if (view.doorSlide)
                    {
                        effects.PlayOneShot(view.doorSlide, .42f);
                    }
                }
            }
            else
            {
                expansion = Mathf.MoveTowards(expansion, 0f, Time.unscaledDeltaTime / MenuExpansionSeconds);
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
            view.entrance.SetLogoSeparation(view.entrance.MenuLogoSeparation);
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

            if (view.entrance)
            {
                view.entrance.SetLogoSeparation(view.entrance.MenuLogoSeparation);
            }
        }

        private void OnEnable() => beatStartTime = AudioSettings.dspTime;

        private void OnDisable()
        {
            RestoreScale();
            if (view && view.modeSelection)
            {
                view.modeSelection.InputEnabled = false;
            }
        }

        private bool TryReturnToEntrance()
        {
            if (actions == null || starting || quitting)
            {
                return false;
            }
            if (actions.Transitioning || view.quitPopup.activeSelf)
            {
                return false;
            }
            if (!view.modeSelection || !menuExpanded)
            {
                return false;
            }

            view.modeSelection.CancelMotion();
            view.modeSelection.InputEnabled = false;
            menuExpanded = false;
            RestartFocusRoutine();
            return true;
        }

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

            if (view.modeSelection)
            {
                if (view.modeSelection.IsMoving)
                {
                    return;
                }
                if (view.modeSelection.SelectedIndex == 0)
                {
                    view.modeSelection.Practice();
                    return;
                }
                if (!view.modeSelection.CanEnter)
                {
                    return;
                }
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
            else if (!leaving)
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

        private void OpenEditor()
        {
            if (Busy || !menuExpanded || view.quitPopup.activeSelf)
            {
                return;
            }

            if (expansion < .99f)
            {
                return;
            }

            if (!(actions is RhythmDojo.Core.MainMenuActions mainMenuActions))
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);

            try
            {
                mainMenuActions.ShowEditor();
            }
            catch (Exception error)
            {
                starting = false;
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

            if (view.modeSelection)
            {
                if (view.modeSelection.IsMoving || (expansion > 0 && expansion < 1))
                {
                    return;
                }
            }
            menuExpanded = !menuExpanded;
            doorSoundPlayed = false;
            if (!menuExpanded && view.modeSelection)
            {
                view.modeSelection.CancelMotion();
            }
            knockTime = .18f;
            if (view.knock)
            {
                effects.PlayOneShot(view.knock, .6f);
            }

            RestartFocusRoutine();
        }

        private void RestartFocusRoutine()
        {
            if (focusRoutine != null)
            {
                StopCoroutine(focusRoutine);
            }
            focusRoutine = StartCoroutine(FocusMenuAfterExpansion());
        }

        private IEnumerator FocusMenuAfterExpansion()
        {
            // Reversing the doors must also cancel the previous focus destination.
            while ((menuExpanded && expansion < 1f) || (!menuExpanded && expansion > 0f))
            {
                yield return null;
            }
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

        public void ResumeAfterModal()
        {
            modalClosedFrame = Time.frameCount;
            ResumeFromSettings();
        }

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

        private void PreviousMode()
        {
            ChangeMode(-1);
        }

        private void NextMode()
        {
            ChangeMode(1);
        }

        private void ChangeMode(int direction)
        {
            if (Busy || view.quitPopup.activeSelf || expansion < .99f)
            {
                return;
            }
            view.modeSelection.Move(direction);
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.start.gameObject);
            }
        }

        private void Unbind()
        {
            RestoreScale();

            if (!view)
            {
                return;
            }

            if (view.modeSelection)
            {
                view.modeSelection.previous.onClick.RemoveListener(PreviousMode);
                view.modeSelection.next.onClick.RemoveListener(NextMode);
                if (view.modeSelection.practiceTarget)
                {
                    view.modeSelection.practiceTarget.onClick.RemoveListener(view.modeSelection.PracticeTarget);
                }
                view.modeSelection.InputEnabled = false;
            }
            if (view.editor)
            {
                view.editor.onClick.RemoveListener(OpenEditor);
            }

            view.start.onClick.RemoveListener(StartGame);
            view.logo.onClick.RemoveListener(ToggleMenu);
            if (view.entranceTrigger)
            {
                view.entranceTrigger.onClick.RemoveListener(ToggleMenu);
            }
            view.settings.onClick.RemoveListener(OpenSettings);
            view.quit.onClick.RemoveListener(ShowQuit);
            view.confirmQuit.onClick.RemoveListener(ConfirmQuit);
            view.cancelQuit.onClick.RemoveListener(CancelQuit);
        }

        private void OnDestroy() => Unbind();
    }
}
