using System;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Core
{
    // Scene services are composed here, including when a screen is opened directly.
    public sealed class ScreenCompositionRoot : MonoBehaviour
    {
        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private SongSelectionScreen selection;

        [SerializeField]
        private SettingsScreen audioSettings;

        [SerializeField]
        private ResultsScreen results;

        public void Validate()
        {
            int count = 0;
            if (selection)
            {
                count++;
            }

            if (audioSettings)
            {
                count++;
            }

            if (results)
            {
                count++;
            }

            if (!settings || count != 1)
            {
                throw new InvalidOperationException("Screen composition requires settings and exactly one screen.");
            }

            settings.ValidateOptions();

            if (results)
            {
                results.Validate();
            }

            if (selection)
            {
                selection.Validate();
            }

            if (audioSettings)
            {
                audioSettings.Validate();
            }
        }

        private void Start()
        {
            Validate();

            var flow = AppFlowController.Create(settings);

            if (results)
            {
                results.Initialize(flow.LastResult, flow.ReturnFromGameplay);
            }

            if (selection)
            {
                selection.Initialize(
                    settings,
                    flow.Selection,
                    flow,
                    flow.Preferences,
                    flow.HardMode,
                    flow.Rules
                );
                DojoEntranceTransition.SelectionReady(selection);
            }

            if (audioSettings)
            {
                audioSettings.Initialize(
                    flow.Audio,
                    flow,
                    settings,
                    flow.Selection,
                    flow.Preferences,
                    flow.HardMode,
                    flow.Rules,
                    RhythmDojo.Services.PresentationPreferences.Current
                );
            }
        }
    }
}
