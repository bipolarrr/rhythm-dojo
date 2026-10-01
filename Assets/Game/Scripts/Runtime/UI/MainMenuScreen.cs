using System;
using UnityEngine;
using UnityEngine.EventSystems;
using RhythmDojo.Services;

namespace RhythmDojo.UI
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        private IMainMenuActions actions;
        private bool starting, quitting;

        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Main menu view missing.");
            view.Validate();
        }

        public void Initialize(IMainMenuActions menuActions)
        {
            Unbind();
            Validate();
            actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
            starting = quitting = false;
            view.quitPopup.SetActive(false);
            view.SetMenuInteractable(true);
            view.confirmQuit.interactable = view.cancelQuit.interactable = true;
            view.start.onClick.AddListener(StartGame);
            view.quit.onClick.AddListener(ShowQuit);
            view.confirmQuit.onClick.AddListener(ConfirmQuit);
            view.cancelQuit.onClick.AddListener(CancelQuit);
        }

        private bool Busy => actions == null || starting || quitting || actions.Transitioning;

        private void StartGame()
        {
            if (Busy || view.quitPopup.activeSelf) return;
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

        private void ShowQuit()
        {
            if (Busy) return;
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
            if (!view) return;
            view.start.onClick.RemoveListener(StartGame);
            view.quit.onClick.RemoveListener(ShowQuit);
            view.confirmQuit.onClick.RemoveListener(ConfirmQuit);
            view.cancelQuit.onClick.RemoveListener(CancelQuit);
        }

        private void OnDestroy() => Unbind();
    }
}