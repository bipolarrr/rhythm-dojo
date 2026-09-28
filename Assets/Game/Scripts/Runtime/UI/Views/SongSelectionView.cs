using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SongSelectionView : MonoBehaviour
    {
        public Dropdown songList, difficultyList, scrollModeList, multiplierList;
        public Text details;
        public Button play, settingsButton;
        public void Validate()
        {
            if (!songList || !difficultyList || !scrollModeList || !multiplierList || !details || !play || !settingsButton)
                throw new InvalidOperationException("Song selection view references missing.");
        }
    }
}
