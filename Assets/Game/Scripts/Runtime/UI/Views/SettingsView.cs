using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SettingsView : MonoBehaviour
    {
        public Dropdown bufferList;
        public Text actualState, status;
        public Button apply, back;
        public Button soundTab, graphicsTab;
        public Button gameplayTab;
        public GameObject gameplayPanel;
        public Slider speedSlider, timingOffset;
        public Text speedLabel, timingLabel;
        public Dropdown[] hardRuleLists;
        public InputField[] hardLossCounts;
        public Text hardModeHint;
        public GameObject soundPanel, graphicsPanel;
        public Slider masterVolume, backgroundVolume, gameplayVolume;
        public Text masterLabel, backgroundLabel, gameplayLabel, displayState;
        public Dropdown aspectList, resolutionList, fpsList;
        public Button[] qualityButtons;
        public Text[] qualityLabels;
        public void Validate()
        {
            if (!bufferList || !actualState || !status || !apply || !back || !soundTab || !graphicsTab || !soundPanel || !graphicsPanel ||
                !masterVolume || !backgroundVolume || !gameplayVolume || !masterLabel || !backgroundLabel || !gameplayLabel ||
                !aspectList || !resolutionList || !fpsList || !displayState || !gameplayTab || !gameplayPanel ||
                !speedSlider || !timingOffset || !speedLabel || !timingLabel || !hardModeHint ||
                hardRuleLists == null || hardRuleLists.Length != 3 || Array.Exists(hardRuleLists, value => !value) ||
                hardLossCounts == null || hardLossCounts.Length != 3 || Array.Exists(hardLossCounts, value => !value))
                throw new InvalidOperationException("Settings view references missing.");
        }
    }
}
