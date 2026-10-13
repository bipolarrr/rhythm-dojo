using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SettingsView : MonoBehaviour
    {
        public Dropdown bufferList;
        public Text actualState,
            status;
        public Button apply,
            back;
        public Button soundTab,
            graphicsTab;
        public Button gameplayTab;
        public GameObject gameplayPanel;
        public Slider speedSlider,
            timingOffset;
        public Text speedLabel,
            timingLabel;
        public Dropdown[] hardRuleLists;
        public Dropdown[] playRuleLists;
        public InputField[] hardLossCounts;
        public Text hardModeHint;
        public GameObject soundPanel,
            graphicsPanel;
        public Slider masterVolume,
            backgroundVolume,
            gameplayVolume;
        public Text masterLabel,
            backgroundLabel,
            gameplayLabel,
            displayState;
        public Dropdown aspectList,
            resolutionList,
            fpsList;
        public Button[] qualityButtons;
        public Text[] qualityLabels;

        public void Validate()
        {
            if ((!bufferList || !actualState) || !status)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!apply || !back) || !soundTab)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!graphicsTab || !soundPanel) || !graphicsPanel)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!masterVolume || !backgroundVolume) || !gameplayVolume)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!masterLabel || !backgroundLabel) || !gameplayLabel)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!aspectList || !resolutionList) || !fpsList)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!displayState || !gameplayTab) || !gameplayPanel)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!speedSlider || !timingOffset) || !speedLabel)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if ((!timingLabel || !hardModeHint) || playRuleLists == null)
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if (
                (playRuleLists.Length != 5 || Array.Exists(playRuleLists, value => !value))
                || hardRuleLists == null
            )
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if (
                (hardRuleLists.Length != 3 || Array.Exists(hardRuleLists, value => !value))
                || hardLossCounts == null
            )
            {
                throw new InvalidOperationException("Settings view references missing.");
            }

            if (hardLossCounts.Length != 3 || Array.Exists(hardLossCounts, value => !value))
            {
                throw new InvalidOperationException("Settings view references missing.");
            }
        }
    }
}
