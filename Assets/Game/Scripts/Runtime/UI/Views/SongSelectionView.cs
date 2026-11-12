using System;
using System.Collections.Generic;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SongSelectionView : MonoBehaviour
    {
        public PracticeSelectionView practice;
        public CanvasGroup inputGroup;
        public GameObject libraryPanel;
        public Button closeLibrary;
        public CanvasGroup libraryGroup;
        public Dropdown scrollModeList, multiplierList, sortList;
        public Text details, songTitle, artistBpm, record, comboRecord, timingLabel, emptyMessage;
        public RectTransform[] entrancePanels;
        public Image album;
        public Button previewButton;
        public AudioSource previewSource;
        public Sprite fallbackCover;
        public Slider timingOffset, speedSlider;
        public Text speedLabel;
        public Button play, settingsButton, back, allTab, favoritesTab;
        public Button modeButton, normalModeButton, hardModeButton, cancelMode;
        public Text modeLabel;
        public GameObject modePopup;
        public Button[] difficultyButtons;
        public RectTransform rowContent;
        public SongRowView rowTemplate;
        public ScrollRect scroll;
        public readonly List<SongRowView> Rows = new List<SongRowView>();

        public void Validate()
        {
            if (!practice || !libraryPanel || !libraryGroup)
            {
                throw new InvalidOperationException("Song selection composition missing. Rebuild Song Selection.");
            }
            practice.Validate();
            UnityEngine.Object[] required = { inputGroup, closeLibrary, scrollModeList, multiplierList, sortList,
                details, songTitle, artistBpm, record, comboRecord, timingLabel, emptyMessage,
                album, previewButton, previewSource, fallbackCover, timingOffset, speedSlider, speedLabel,
                play, settingsButton, back, allTab, favoritesTab, modeButton, normalModeButton,
                hardModeButton, cancelMode, modeLabel, modePopup, rowContent, rowTemplate, scroll };
            if (Array.Exists(required, item => !item))
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }
            if (difficultyButtons == null || difficultyButtons.Length == 0)
            {
                throw new InvalidOperationException("Song selection difficulties missing.");
            }
            if (Array.Exists(difficultyButtons, button => !button))
            {
                throw new InvalidOperationException("Song selection difficulty references missing.");
            }
        }

        public void Render(SongSelectionPresentation presentation)
        {
            practice.Render(presentation);
            emptyMessage.text = presentation.Message;
            emptyMessage.gameObject.SetActive(presentation.Count == 0);
        }

        public void ClearRows()
        {
            // Only row count is dynamic. This view owns every instantiated row.
            foreach (var row in Rows)
            {
                row.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(row.gameObject);
                }
                else
                {
                    DestroyImmediate(row.gameObject);
                }
            }
            Rows.Clear();
        }
    }
}
