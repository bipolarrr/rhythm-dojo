using System;
using System.Threading;
using System.Threading.Tasks;
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

        private bool initialized;

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

        internal static void EnableInput(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var system in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                {
                    system.enabled = true;
                }
            }
        }

        private void Start()
        {
            Initialize(AppFlowController.Create(settings));
        }

        public void Initialize(AppFlowController flow)
        {
            if (audioSettings && !RhythmDojo.Services.SettingsAccessPolicy.CanChange)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    root.SetActive(false);
                }
                return;
            }
            if (initialized)
            {
                return;
            }
            if (selection && !flow.CanInitializeSelection(gameObject.scene))
            {
                return;
            }
            Validate();
            initialized = true;

            if (results)
            {
                Func<CancellationToken, Task> retry = null;
                if (flow.CanRetryResult)
                {
                    retry = flow.RetryResultAsync;
                }

                results.Initialize(
                    flow.LastResult,
                    flow.ReturnFromGameplay,
                    retry,
                    flow.Preferences,
                    flow.ShowResultTimingCalibration
                );
            }

            if (selection)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() == gameObject.scene)
                {
                    EnableInput(gameObject.scene);
                }

                selection.Initialize(
                    settings,
                    flow.Selection,
                    flow,
                    flow.Preferences,
                    flow.HardMode,
                    flow.Rules
                );
                flow.SelectionInitialized(selection);
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
