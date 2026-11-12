using System.Collections;
using RhythmDojo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed partial class SongSelectionScreen
    {
        private bool practiceSelected, practicePlayQueued;
        private int practiceHeldDirection;
        private double practiceRepeatAt;
        private bool PracticeActive => practiceSelected && view.practice.gameObject.activeSelf;

        public void PrepareEntryPresentation()
        {
            ConfigurePractice();
        }

        private void BindPractice()
        {
            if (!view.practice)
            {
                return;
            }
            var practice = view.practice;
            practice.Turned += TurnPracticeSong;
            practice.Settled += PracticeSettled;
            practice.previous.onClick.AddListener(StrikePrevious);
            practice.next.onClick.AddListener(StrikeNext);
            practice.play.onClick.AddListener(Play);
            practice.back.onClick.AddListener(ShowBack);
            practice.library.onClick.AddListener(OpenPracticeLibrary);
            practice.preview.onClick.AddListener(PreviewPractice);
            practice.settings.onClick.AddListener(PracticeSettings);
            for (int i = 0; i < difficultyActions.Length; i++)
            {
                practice.difficulties[i].onClick.AddListener(difficultyActions[i]);
            }
        }

        private void ConfigurePractice()
        {
            if (!view.practice)
            {
                return;
            }
            bool selected = false;
            if (navigation is AppFlowController flow)
            {
                selected = flow.PracticeSelection;
            }
            if (practiceSelected == selected)
            {
                return;
            }
            practiceSelected = selected;
            practicePlayQueued = false;
            view.practice.gameObject.SetActive(selected);
            view.practice.browser.SetActive(!selected);
            if (selected)
            {
                view.practice.ResetPresentation();
                RefreshPracticeDetails();
            }
        }

        private void UpdatePracticeInput()
        {
            if (Busy || ModalOpen || view.practice.IsLeaving)
            {
                practiceHeldDirection = 0;
                view.practice.StopBarrage();
                return;
            }
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool cancel = keyboard?.escapeKey.wasPressedThisFrame == true;
            cancel |= gamepad?.buttonEast.wasPressedThisFrame == true;
            if (cancel)
            {
                ShowBack();
                return;
            }
            bool confirm = keyboard?.enterKey.wasPressedThisFrame == true;
            confirm |= gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (confirm)
            {
                Play();
                return;
            }
            if (practicePlayQueued)
            {
                view.practice.StopBarrage();
                return;
            }
            int direction = 0;
            bool left = keyboard?.leftArrowKey.isPressed == true || gamepad?.dpad.left.isPressed == true;
            bool right = keyboard?.rightArrowKey.isPressed == true || gamepad?.dpad.right.isPressed == true;
            if (left != right)
            {
                direction = 1;
                if (left)
                {
                    direction = -1;
                }
            }
            double now = Time.unscaledTimeAsDouble;
            if (direction != practiceHeldDirection)
            {
                view.practice.StopBarrage();
                if (direction != 0)
                {
                    StrikePractice(direction);
                    practiceRepeatAt = now + view.practice.holdSeconds;
                }
            }
            else if (direction != 0 && now >= practiceRepeatAt)
            {
                if (view.Rows.Count > 0)
                {
                    view.practice.BeginBarrage(direction);
                }
            }
            practiceHeldDirection = direction;

            bool up = keyboard?.upArrowKey.wasPressedThisFrame == true || gamepad?.dpad.up.wasPressedThisFrame == true;
            bool down = keyboard?.downArrowKey.wasPressedThisFrame == true || gamepad?.dpad.down.wasPressedThisFrame == true;
            if (up || down)
            {
                int step = 1;
                if (up)
                {
                    step = -1;
                }
                int index = view.difficultyList.value + step;
                while (index >= 0 && index < view.difficultyButtons.Length)
                {
                    if (view.difficultyButtons[index].interactable)
                    {
                        SelectDifficulty(index);
                        break;
                    }
                    index += step;
                }
            }
        }

        private void StrikePrevious() => StrikePractice(-1);

        private void StrikeNext() => StrikePractice(1);

        private void StrikePractice(int direction)
        {
            if (Busy || ModalOpen || practicePlayQueued)
            {
                return;
            }
            if (view.Rows.Count == 0 || view.practice.IsLeaving)
            {
                return;
            }
            StopPreview();
            view.practice.Strike(direction);
        }

        private void TurnPracticeSong(int direction)
        {
            if (view.Rows.Count == 0)
            {
                return;
            }
            int index = -1;
            for (int i = 0; i < view.Rows.Count; i++)
            {
                if (view.Rows[i].Entry.Id == selection.SelectedSongId)
                {
                    index = i;
                    break;
                }
            }
            index = (index + direction + view.Rows.Count) % view.Rows.Count;
            SelectSong(view.Rows[index].Entry.Id);
        }

        private void PracticeSettled()
        {
            RefreshPracticeDetails();
            if (practicePlayQueued)
            {
                practicePlayQueued = false;
                Play();
            }
        }

        private void RefreshPracticeDetails()
        {
            if (!view.practice || !practiceSelected)
            {
                return;
            }
            var practice = view.practice;
            string details = view.details.text;
            int lineEnd = details.IndexOf('\n');
            if (lineEnd >= 0)
            {
                details = details.Substring(0, lineEnd);
            }
            string songArtist = view.artistBpm.text;
            int artistLineEnd = songArtist.IndexOf('\n');
            if (artistLineEnd >= 0)
            {
                songArtist = songArtist.Substring(0, artistLineEnd);
            }
            practice.rig.SetSong(view.album.sprite, view.songTitle.text, songArtist);
            int index = -1;
            for (int i = 0; i < view.Rows.Count; i++)
            {
                if (view.Rows[i].Entry.Id == selection.SelectedSongId)
                {
                    index = i;
                    break;
                }
            }
            practice.SetSongInformation(view.songTitle.text, view.artistBpm.text, details,
                view.record.text, $"수련곡  {index + 1:00} / {view.Rows.Count:00}");
            for (int i = 0; i < practice.difficulties.Length; i++)
            {
                var button = practice.difficulties[i];
                button.GetComponentInChildren<Text>().text = view.difficultyList.options[i].text;
                Color color = new Color(1f, .91f, .72f);
                if (view.difficultyList.value == i)
                {
                    color = new Color(1f, .55f, .25f);
                }
                button.GetComponentInChildren<Text>().color = color;
            }
            RefreshPracticeFeedback();
        }

        private void RefreshPracticeFeedback()
        {
            if (!PracticeActive)
            {
                return;
            }
            var practice = view.practice;
            bool available = !Busy && !ModalOpen && !practice.IsLeaving;
            practice.play.interactable = available && view.play.interactable;
            practice.previous.interactable = practice.next.interactable = available && view.Rows.Count > 0;
            practice.back.interactable = available;
            bool settled = available && !practice.IsStriking;
            practice.library.interactable = practice.settings.interactable = settled;
            practice.preview.interactable = settled && view.previewButton.interactable;
            for (int i = 0; i < practice.difficulties.Length; i++)
            {
                practice.difficulties[i].interactable = settled && view.difficultyButtons[i].interactable;
            }
            practice.status.text = "";
            if (view.playErrorPanel.activeSelf)
            {
                practice.status.text = view.playErrorLabel.text;
            }
            else if (loading)
            {
                practice.status.text = view.playStatusLabel.text;
            }
            else if (practicePlayQueued)
            {
                practice.status.text = "마지막 타격 후 수련 시작";
            }
            practice.preview.GetComponentInChildren<Text>().text =
                view.previewButton.GetComponentInChildren<Text>(true).text;
        }

        private void OpenPracticeLibrary()
        {
            if (Busy || view.practice.IsStriking || view.practice.IsLeaving)
            {
                return;
            }
            practiceHeldDirection = 0;
            view.practice.gameObject.SetActive(false);
            view.practice.browser.SetActive(true);
            FocusSelected();
        }

        private void PreviewPractice()
        {
            if (!view.practice.IsStriking)
            {
                TogglePreview();
            }
        }

        private void PracticeSettings()
        {
            if (!view.practice.IsStriking)
            {
                ShowSettings();
            }
        }

        private void ReturnFromPractice()
        {
            if (Busy || ModalOpen || view.practice.IsLeaving)
            {
                return;
            }
            StopPreview();
            practicePlayQueued = false;
            practiceHeldDirection = 0;
            if (!PracticeActive)
            {
                view.practice.browser.SetActive(false);
                view.practice.gameObject.SetActive(true);
                view.practice.ResetPresentation();
                RefreshPracticeDetails();
                FocusSelected();
                return;
            }
            StartCoroutine(LeavePractice());
        }

        private IEnumerator LeavePractice()
        {
            yield return view.practice.Leave();
            if (navigation is AppFlowController flow)
            {
                flow.ShowPracticeModes();
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && view && view.practice)
            {
                practiceHeldDirection = 0;
                view.practice.StopBarrage();
            }
        }

        private void UnbindPractice()
        {
            practicePlayQueued = practiceSelected = false;
            practiceHeldDirection = 0;
            if (!view || !view.practice)
            {
                return;
            }
            var practice = view.practice;
            practice.gameObject.SetActive(false);
            practice.browser.SetActive(true);
            practice.Turned -= TurnPracticeSong;
            practice.Settled -= PracticeSettled;
            practice.previous.onClick.RemoveListener(StrikePrevious);
            practice.next.onClick.RemoveListener(StrikeNext);
            practice.play.onClick.RemoveListener(Play);
            practice.back.onClick.RemoveListener(ShowBack);
            practice.library.onClick.RemoveListener(OpenPracticeLibrary);
            practice.preview.onClick.RemoveListener(PreviewPractice);
            practice.settings.onClick.RemoveListener(PracticeSettings);
            if (difficultyActions != null)
            {
                for (int i = 0; i < difficultyActions.Length; i++)
                {
                    practice.difficulties[i].onClick.RemoveListener(difficultyActions[i]);
                }
            }
        }
    }
}
