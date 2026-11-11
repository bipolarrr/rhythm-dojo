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

        private IMainMenuActions actions;
        private bool starting,
            quitting;
        [SerializeField]
        private AudioSource effects;
        private int modalClosedFrame = -1;
        public const float MenuExpansionSeconds = 1.35f;

        public void Validate()
        {
            if (!view || !view.modeSelection)
            {
                throw new InvalidOperationException("Mode selection view missing.");
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
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            starting = quitting = false;
            modalClosedFrame = -1;
            view.SetExpansion(1f);
            view.menuPanel.gameObject.SetActive(true);
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            view.confirmQuit.interactable = view.cancelQuit.interactable = true;
            if (view.editor)
            {
                view.editor.onClick.AddListener(OpenEditor);
            }

            view.start.onClick.AddListener(StartGame);
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
                EventSystem.current.SetSelectedGameObject(view.start.gameObject);
            }
        }

        private bool Busy
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return true;
                }
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
                    view.menuPanel.alpha = 1f;
                    view.SetMenuInteractable(true);
                    view.modeSelection.detail.text = "곡 목록을 열지 못했습니다. 다시 수련 시작을 눌러 주세요.";
                }
            }
            if (view.modeSelection)
            {
                bool available = !Busy && !view.quitPopup.activeSelf;
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
            if (cancel && Time.frameCount != modalClosedFrame && !Busy)
            {
                if (view.quitPopup.activeSelf)
                {
                    CancelQuit();
                }
            }
        }

        private void OnDisable()
        {
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
            if (!(actions is RhythmDojo.Core.MainMenuActions mainMenuActions))
            {
                return false;
            }
            view.modeSelection.CancelMotion();
            view.modeSelection.InputEnabled = false;
            mainMenuActions.ReturnToTitle();
            return true;
        }

        private void StartGame()
        {
            if (Busy || view.quitPopup.activeSelf)
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
                view.menuPanel.alpha = 1f;
                view.SetMenuInteractable(true);
                Debug.LogException(error, this);
            }
        }

        private void OpenEditor()
        {
            if (Busy || view.quitPopup.activeSelf)
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
            if (Busy || view.quitPopup.activeSelf)
            {
                return;
            }

            starting = true;
            view.SetMenuInteractable(false);
            StartCoroutine(BreakAndInvoke(view.settings, actions.ShowSettings, false));
        }

        private void ShowQuit()
        {
            if (Busy)
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
            if (Busy || view.quitPopup.activeSelf)
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
            view.settings.onClick.RemoveListener(OpenSettings);
            view.quit.onClick.RemoveListener(ShowQuit);
            view.confirmQuit.onClick.RemoveListener(ConfirmQuit);
            view.cancelQuit.onClick.RemoveListener(CancelQuit);
        }

        private void OnDestroy() => Unbind();
    }
}
