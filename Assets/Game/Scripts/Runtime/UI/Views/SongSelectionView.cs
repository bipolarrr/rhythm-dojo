using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class SongSelectionView : MonoBehaviour
    {
        public Dropdown difficultyList,
            scrollModeList,
            multiplierList,
            sortList;
        public Text details,
            songTitle,
            artistBpm,
            record,
            timingLabel,
            emptyMessage;
        public Image album;
        public Sprite fallbackCover;
        public Slider timingOffset;
        public Slider speedSlider;
        public Text speedLabel;
        public Button play,
            settingsButton,
            back,
            allTab,
            favoritesTab,
            confirmBack,
            cancelBack;
        public Button modeButton,
            normalModeButton,
            hardModeButton,
            cancelMode;
        public Text modeLabel;
        public GameObject modePopup;
        public Button[] difficultyButtons;
        public GameObject[] difficultyIndicators;
        public GameObject backPopup;
        public RectTransform rowContent;
        public SongRowView rowTemplate;
        public ScrollRect scroll;
        public readonly List<SongRowView> Rows = new List<SongRowView>();

        public void Validate()
        {
            if ((!difficultyList || !scrollModeList) || !multiplierList)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!sortList || !details) || !play)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!settingsButton || !album) || !songTitle)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!artistBpm || !record) || !timingOffset)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!timingLabel || !speedSlider) || !speedLabel)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!rowContent || !rowTemplate) || !back)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!allTab || !favoritesTab) || !backPopup)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!confirmBack || !cancelBack) || !emptyMessage)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!scroll || !modeButton) || !normalModeButton)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!hardModeButton || !cancelMode) || !modeLabel)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if ((!modePopup || difficultyButtons == null) || difficultyButtons.Length == 0)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if (Array.Exists(difficultyButtons, button => !button) || difficultyIndicators == null)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if (difficultyIndicators.Length != difficultyButtons.Length)
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }

            if (Array.Exists(difficultyIndicators, indicator => !indicator))
            {
                throw new InvalidOperationException("Song selection view references missing.");
            }
        }

        public static Color DifficultyColor(int index)
        {
            if (index == 0)
            {
                return new Color(.86f, .87f, .80f);
            }
            else if (index == 1)
            {
                return new Color(.95f, .29f, .28f);
            }
            else
            {
                return new Color(.46f, .78f, .76f);
            }
        }

        public void RefreshDifficultyButtons()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                var button = difficultyButtons[i];
                bool selected = i == difficultyList.value;
                var color = DifficultyColor(i);

                if (selected)
                {
                    button.GetComponent<Image>().color = Color.Lerp(
                        new Color(.04f, .055f, .065f),
                        color,
                        .55f
                    );
                }
                else
                {
                    button.GetComponent<Image>().color = new Color(.075f, .09f, .105f);
                }

                button.GetComponent<Outline>().enabled = selected;

                if (i < difficultyList.options.Count)
                {
                    button.GetComponentInChildren<Text>().text = difficultyList.options[i].text;
                }
                else
                {
                    button.GetComponentInChildren<Text>().text = "";
                }

                difficultyIndicators[i].SetActive(selected);
            }
        }

        public void ClearRows()
        {
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

        public void SetInteractable(bool value)
        {
            difficultyList.interactable =
                scrollModeList.interactable =
                multiplierList.interactable =
                sortList.interactable =
                speedSlider.interactable =
                timingOffset.interactable =
                settingsButton.interactable =
                modeButton.interactable =
                back.interactable =
                allTab.interactable =
                favoritesTab.interactable =
                    value;

            foreach (var button in difficultyButtons)
            {
                button.interactable = value;
            }

            foreach (var row in Rows)
            {
                row.select.interactable = row.favorite.interactable = value;
            }
        }
    }
}
