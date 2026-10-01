using System;
using UnityEngine;
using RhythmDojo.UI;

namespace RhythmDojo.Core
{
    public sealed class MainMenuCompositionRoot : MonoBehaviour
    {
        [SerializeField] private MainMenuScreen screen;

        public void Validate()
        {
            if (!screen) throw new InvalidOperationException("Main menu screen missing.");
            screen.Validate();
        }

        private void Start()
        {
            Validate();
            screen.Initialize(new MainMenuActions());
        }
    }
}