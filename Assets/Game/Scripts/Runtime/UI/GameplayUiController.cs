using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.UI
{
    public sealed class GameplayUiController : MonoBehaviour
    {
        [SerializeField] private GameplayHudView view;
        private IGameplayReadModel model;
        private Action returnRequested;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Gameplay UI view missing.");
            view.Validate();
        }
        public void Initialize(IGameplayReadModel readModel, Action onReturn)
        {
            Unbind(); Validate(); model = readModel; returnRequested = onReturn;
            view.returnButton.onClick.AddListener(Return); Refresh();
        }
        private void Return() => returnRequested?.Invoke();
        private void Update() => Refresh();
        private void Refresh()
        {
            if (model != null) view.returnButton.gameObject.SetActive(model.Snapshot.Session.State != SessionState.Playing);
        }
        public void Unbind()
        {
            if (view && view.returnButton) view.returnButton.onClick.RemoveListener(Return);
            model = null; returnRequested = null;
        }
        private void OnDestroy() => Unbind();
    }
}
