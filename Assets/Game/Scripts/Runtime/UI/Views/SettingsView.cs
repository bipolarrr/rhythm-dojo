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
        public void Validate()
        {
            if (!bufferList || !actualState || !status || !apply || !back)
                throw new InvalidOperationException("Settings view references missing.");
        }
    }
}
