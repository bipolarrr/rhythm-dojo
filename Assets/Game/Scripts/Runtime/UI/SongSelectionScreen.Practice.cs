using System.Collections;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed partial class SongSelectionScreen
    {
        private int practiceHeldDirection;
        private double practiceRepeatAt;

        public void PrepareEntryPresentation()
        {
            if (lifetime == null)
            {
                return;
            }
            state = SongSelectionState.Main;
            practiceHeldDirection = 0;
            view.practice.ResetPresentation();
            // Preparation can precede the user's mode choice. Read services again at activation.
            RefreshLibrary(true);
        }

        private void BindPractice()
        {
            var practice = view.practice;
            practice.Turned += TurnPracticeSong;
            practice.Settled += PracticeSettled;
            practice.previous.onClick.AddListener(StrikePrevious);
            practice.next.onClick.AddListener(StrikeNext);
            practice.library.onClick.AddListener(OpenPracticeLibrary);
        }

        private void UpdatePracticeInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool confirm = keyboard?.enterKey.wasPressedThisFrame == true;
            confirm |= gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (confirm)
            {
                // The focused utility button owns Submit; normal selection and striking own Start.
                var focused = EventSystem.current?.currentSelectedGameObject;
                bool utility = focused == view.settingsButton.gameObject || focused == view.practice.library.gameObject;
                utility |= focused == view.previewButton.gameObject || focused == view.back.gameObject;
                if (!utility)
                {
                    Play();
                }
                return;
            }
            if (state == SongSelectionState.StartPending)
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
                if (list.Entries.Count > 0)
                {
                    view.practice.BeginBarrage(direction);
                }
            }
            practiceHeldDirection = direction;
            bool up = keyboard?.upArrowKey.wasPressedThisFrame == true || gamepad?.dpad.up.wasPressedThisFrame == true;
            bool down = keyboard?.downArrowKey.wasPressedThisFrame == true || gamepad?.dpad.down.wasPressedThisFrame == true;
            if (up || down)
            {
                int step = -1;
                if (up)
                {
                    step = 1;
                }
                int index = presentation.SelectedDifficulty + step;
                while (index >= 0 && index < difficultyOptions.Length)
                {
                    if (presentation.Difficulties[index])
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
            if (Busy || state != SongSelectionState.Main || list.Entries.Count == 0)
            {
                return;
            }
            StopPreview();
            view.practice.Strike(direction);
        }

        private void TurnPracticeSong(int direction)
        {
            if (state != SongSelectionState.Main && state != SongSelectionState.StartPending)
            {
                return;
            }
            string songId = list.Move(selection.SelectedSongId, direction);
            if (songId != null)
            {
                SelectSong(songId);
            }
        }

        private void PracticeSettled()
        {
            if (state == SongSelectionState.StartPending)
            {
                state = SongSelectionState.Main;
                Play();
            }
            RefreshPracticeFeedback();
        }

        private void RefreshPracticeFeedback()
        {
            if (presentation == null)
            {
                return;
            }
            var practice = view.practice;
            bool available = !Busy && state == SongSelectionState.Main;
            bool settled = available && !practice.IsStriking;
            practice.interfaceGroup.interactable = available;
            practice.interfaceGroup.blocksRaycasts = available;
            practice.play.interactable = available && CanPlay;
            practice.previous.interactable = practice.next.interactable = available && list.Entries.Count > 0;
            practice.back.interactable = available;
            practice.library.interactable = practice.settings.interactable = settled;
            practice.preview.interactable = settled && CanPlay && selection.Library is ISongChartLoader;
            for (int i = 0; i < practice.difficulties.Length; i++)
            {
                practice.difficulties[i].interactable = settled && presentation.Difficulties[i];
            }
            bool panel = state == SongSelectionState.Library || state == SongSelectionState.ModePopup;
            view.libraryPanel.SetActive(panel);
            view.libraryGroup.interactable = !Busy && state == SongSelectionState.Library;
            view.libraryGroup.blocksRaycasts = state == SongSelectionState.Library;
            view.modePopup.SetActive(ModalOpen);
            view.allTab.GetComponent<Image>().color = new Color(.35f, .25f, .18f);
            view.favoritesTab.GetComponent<Image>().color = new Color(.15f, .12f, .10f);
            if (list.FavoritesOnly)
            {
                view.allTab.GetComponent<Image>().color = new Color(.15f, .12f, .10f);
                view.favoritesTab.GetComponent<Image>().color = new Color(.35f, .25f, .18f);
            }
            string message = presentation.Message;
            if (previewFailure != null)
            {
                message = previewFailure;
            }
            if (playbackFailure.HasValue)
            {
                message = playbackFailure.Value.Message;
            }
            if (state == SongSelectionState.Loading)
            {
                int frame = (int)((Time.unscaledTimeAsDouble - loadingStartedAt) / .35) % LoadingCaptions.Length;
                message = LoadingCaptions[frame];
            }
            else if (state == SongSelectionState.StartPending)
            {
                message = "마지막 타격 후 수련 시작";
            }
            practice.status.text = message;
            practice.preview.GetComponentInChildren<Text>().text = previewCaption;
            string caption = "수련 시작";
            if (HardModeSelected)
            {
                caption = "검정 시작";
            }
            if (playbackFailure.HasValue && playbackFailure.Value.CanRetry)
            {
                caption = "다시 시도";
            }
            practice.play.GetComponentInChildren<Text>().text = caption;
        }

        private void OpenPracticeLibrary()
        {
            if (Busy || state != SongSelectionState.Main || view.practice.IsStriking)
            {
                return;
            }
            practiceHeldDirection = 0;
            view.practice.StopBarrage();
            state = SongSelectionState.Library;
            RefreshPracticeFeedback();
            FocusSelected();
        }

        private void ClosePracticeLibrary()
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            state = SongSelectionState.Main;
            RefreshPracticeFeedback();
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.practice.library.gameObject);
            }
        }

        private void PreviewPractice()
        {
            if (state == SongSelectionState.Main && !view.practice.IsStriking)
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
            if (Busy || ModalOpen)
            {
                return;
            }
            if (state == SongSelectionState.Library)
            {
                ClosePracticeLibrary();
                return;
            }
            StopPreview();
            practiceHeldDirection = 0;
            state = SongSelectionState.Leaving;
            RefreshPracticeFeedback();
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
            practiceHeldDirection = 0;
            if (!view || !view.practice)
            {
                return;
            }
            var practice = view.practice;
            practice.Turned -= TurnPracticeSong;
            practice.Settled -= PracticeSettled;
            practice.previous.onClick.RemoveListener(StrikePrevious);
            practice.next.onClick.RemoveListener(StrikeNext);
            practice.library.onClick.RemoveListener(OpenPracticeLibrary);
        }
    }
}
