using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmDojo.UI
{
    public sealed class GameplayUiController : MonoBehaviour
    {
        [SerializeField]
        private GameplayHudView view;
        private IGameplayReadModel model;
        private Action returnRequested;
        private UnityAction<float> railOpacityChanged;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Gameplay UI view missing.");
            }

            view.Validate();
            var pauseMenu = GetComponent<GameplayPauseMenu>();
            if (pauseMenu)
            {
                pauseMenu.Validate();
            }
        }

        public void Initialize(IGameplayReadModel readModel, Action onReturn)
        {
            Initialize(readModel, onReturn, null);
        }

        public void Initialize(
            IGameplayReadModel readModel,
            Action onReturn,
            UnityAction<float> applyOpacity
        )
        {
            Unbind();
            Validate();

            model = readModel;
            returnRequested = onReturn;
            railOpacityChanged = applyOpacity;
            view.returnButton.onClick.AddListener(Return);
            view.focusHudButton.onClick.AddListener(ToggleFocusHud);
            view.SetFocusHud((PresentationPreferences.Current ?? PresentationPreferences.Load()).focusHud);

            if (railOpacityChanged != null)
            {
                view.railOpacity.SetValueWithoutNotify(PresentationPreferences.Current.railOpacity);
                railOpacityChanged(view.railOpacity.value);
            }
            view.railOpacity.interactable = false;
            view.focusHudButton.interactable = false;

            Refresh();
        }

        private void Return() => returnRequested?.Invoke();

        private void ToggleFocusHud()
        {
            if (!SettingsAccessPolicy.CanChange)
            {
                return;
            }

            var preferences = PresentationPreferences.Current ?? PresentationPreferences.Load();
            preferences.focusHud = !preferences.focusHud;
            preferences.SaveValues();
            view.SetFocusHud(preferences.focusHud);
        }

        private void Update() => Refresh();

        private void Refresh()
        {
            if (model != null)
            {
                view.returnButton.gameObject.SetActive(
                    model.Snapshot.Session.State != SessionState.Playing
                );
            }
        }

        public void Unbind()
        {
            if (view && view.returnButton)
            {
                view.returnButton.onClick.RemoveListener(Return);
            }

            if (view && view.focusHudButton)
            {
                view.focusHudButton.onClick.RemoveListener(ToggleFocusHud);
            }

            if (view && view.railOpacity && railOpacityChanged != null)
            {
                view.railOpacity.onValueChanged.RemoveListener(railOpacityChanged);
            }

            railOpacityChanged = null;
            model = null;
            returnRequested = null;
        }

        private void OnDestroy() => Unbind();
    }
}
