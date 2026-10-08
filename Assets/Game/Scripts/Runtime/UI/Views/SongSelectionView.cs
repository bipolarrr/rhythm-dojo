using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SongSelectionView : MonoBehaviour
    {
        public Dropdown difficultyList, scrollModeList, multiplierList, sortList;
        public Text details, songTitle, artistBpm, record, timingLabel, emptyMessage;
        public Image album;
        public Sprite fallbackCover;
        public Slider timingOffset;
        public Slider speedSlider;
        public Text speedLabel;
        public Button play, settingsButton, back, allTab, favoritesTab, confirmBack, cancelBack;
        public Button[] difficultyButtons;
        public GameObject[] difficultyIndicators;
        public GameObject backPopup;
        public RectTransform rowContent;
        public SongRowView rowTemplate;
        public ScrollRect scroll;
        public readonly List<SongRowView> Rows = new List<SongRowView>();
        public void Validate()
        {
            if (!difficultyList || !scrollModeList || !multiplierList || !sortList || !details || !play ||
                !settingsButton || !album || !songTitle || !artistBpm || !record || !timingOffset ||
                !timingLabel || !speedSlider || !speedLabel || !rowContent || !rowTemplate || !back || !allTab || !favoritesTab ||
                !backPopup || !confirmBack || !cancelBack || !emptyMessage || !scroll ||
                difficultyButtons == null || difficultyButtons.Length == 0 || Array.Exists(difficultyButtons, button => !button) ||
                difficultyIndicators == null || difficultyIndicators.Length != difficultyButtons.Length || Array.Exists(difficultyIndicators, indicator => !indicator))
                throw new InvalidOperationException("Song selection view references missing.");
        }
        public static Color DifficultyColor(int index)
        {
            return index == 0 ? new Color(.86f, .87f, .80f) :
                index == 1 ? new Color(.95f, .29f, .28f) : new Color(.46f, .78f, .76f);
        }
        public void RefreshDifficultyButtons()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                var button = difficultyButtons[i];
                bool selected = i == difficultyList.value;
                var color = DifficultyColor(i);
                button.GetComponent<Image>().color = selected ? Color.Lerp(new Color(.04f, .055f, .065f), color, .55f) : new Color(.075f, .09f, .105f);
                button.GetComponent<Outline>().enabled = selected;
                button.GetComponentInChildren<Text>().text = i < difficultyList.options.Count ? difficultyList.options[i].text : "";
                difficultyIndicators[i].SetActive(selected);
            }
        }
        public void ClearRows()
        {
            foreach (var row in Rows)
            {
                row.gameObject.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(row.gameObject); else DestroyImmediate(row.gameObject);
            }
            Rows.Clear();
        }
        public void SetInteractable(bool value)
        {
            difficultyList.interactable = scrollModeList.interactable = multiplierList.interactable =
                sortList.interactable = speedSlider.interactable = timingOffset.interactable = settingsButton.interactable =
                back.interactable = allTab.interactable = favoritesTab.interactable = value;
            foreach (var button in difficultyButtons) button.interactable = value;
            foreach (var row in Rows) row.select.interactable = row.favorite.interactable = value;
        }
    }
}