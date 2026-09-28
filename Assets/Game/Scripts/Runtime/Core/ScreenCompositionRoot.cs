using System;
using UnityEngine;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;

namespace RhythmDojo.Core
{
    // Scene services are composed here, including when a screen is opened directly.
    public sealed class ScreenCompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private SongSelectionScreen selection;
        [SerializeField] private SettingsScreen audioSettings;
        public void Validate()
        {
            if (!settings || (!selection && !audioSettings) || (selection && audioSettings))
                throw new InvalidOperationException("Screen composition requires settings and exactly one screen.");
            settings.ValidateOptions();
            if (selection) selection.Validate();
            if (audioSettings) audioSettings.Validate();
        }
        private void Start()
        {
            Validate(); var flow = AppFlowController.Create(settings);
            if (selection) selection.Initialize(settings, flow.Selection, flow);
            if (audioSettings) audioSettings.Initialize(flow.Audio, flow);
        }
    }
}
