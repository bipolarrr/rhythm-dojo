using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Core
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        public void Validate()
        {
            if (!settings) throw new System.InvalidOperationException("Bootstrap settings missing.");
            settings.ValidateOptions();
        }
        private void Start() { Validate(); AppFlowController.Create(settings).ShowSelection(); }
    }
}
