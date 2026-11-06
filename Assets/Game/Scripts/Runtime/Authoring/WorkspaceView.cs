using System;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    public enum WorkspaceAction
    {
        New, Open, Save, SaveAs, Exit, ApplySettings, EstimateBpm, ImportAudio, ImportJacket,
        Undo, Redo, Copy, Paste, Delete, SelectAll, SelectTaps, SelectHolds, Deselect,
        Mirror, Randomize, PlayPause, Stop, Loop, Slow, Test, TestHere, AutoTest, AutoTestHere,
        SaveExit, DiscardExit, CancelExit, Pencil, Select, Eraser, Position, Tap, Hold, ImportPupa,
        SaveNew, DiscardNew, ConfirmSaveAs, CancelSaveAs, ProjectFiles, ChartSettings, ResetView,
        PlayReturn, Scroll, ApplyEstimatedBpm, CloseNoteSpeedWarning, OnAir, ConfirmOnAir, CancelOnAir,
        OpenByProjectId, ShowProjectList, CloseProjectList, OpenListedProject
    }

    [Serializable]
    public sealed class WorkspaceActionBinding
    {
        public WorkspaceAction action;
        public Button button;
    }

    public sealed class WorkspaceView : MonoBehaviour
    {
        public WorkspaceActionBinding[] actions;
        public WorkspaceActionBinding[] toolActions;
        public InputField title, artist, audioPath, jacketPath, soundPath, bpm, offset, volume,
            saveAsName, position, loopStart, loopEnd;
        public InputField noteSpeed;
        public InputField openProjectId;
        public Slider volumeControl;
        public Dropdown recent, quantization;
        public Button quantizationCycle;
        public Text quantizationCycleValue;
        public Text status, selection, playback;
        public Text bpmSuggestion;
        public Button applyEstimatedBpm;
        public RawImage jacket;
        public GameObject exitPopup;
        public GameObject newPopup;
        public GameObject saveAsPopup;
        public GameObject projectListPopup;
        public ScrollRect projectListScroll;
        public WorkspaceProjectRow projectListRow;
        public Text projectListStatus;
        public GameObject noteSpeedWarning;
        public GameObject onAirPopup;
        public Text onAirCountdown;
        public WorkspaceToolIcon onAirIcon;
        public Text saveAsError;
        public WorkspaceMenuPopup[] menus;
        public Button[] difficulty;
        public GameObject menuDismissArea;

        private string onAirPromptText;

        public void SetOnAirQuantizing(bool quantizing)
        {
            var heading = onAirPopup.transform.Find("Question").GetComponent<Text>();
            if (quantizing)
            {
                onAirPromptText = heading.text;
                heading.text = "로딩 중…\nON AIR 노트를 퀀타이즈에 맞춰 조정하고 있습니다.";
                onAirPopup.transform.SetAsLastSibling();
            }
            else
            {
                heading.text = onAirPromptText;
            }

            foreach (var binding in actions)
            {
                if (binding.action == WorkspaceAction.ConfirmOnAir || binding.action == WorkspaceAction.CancelOnAir)
                {
                    binding.button.gameObject.SetActive(!quantizing);
                }
            }

            onAirPopup.SetActive(quantizing);
        }

        public bool IsNoteSpeedWarningOpen => noteSpeedWarning && noteSpeedWarning.activeSelf;
        public bool IsProjectListOpen => projectListPopup && projectListPopup.activeSelf;

        public bool HasOpenMenu
        {
            get
            {
                foreach (var menu in menus)
                {
                    if (menu.panel.activeSelf)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void OpenMenu(WorkspaceMenuPopup menu)
        {
            if (IsProjectListOpen)
            {
                return;
            }

            if (exitPopup.activeSelf || newPopup.activeSelf)
            {
                return;
            }

            if (saveAsPopup.activeSelf || (onAirPopup && onAirPopup.activeSelf))
            {
                return;
            }

            if (menu.panel.activeSelf)
            {
                return;
            }

            CloseMenus();
            menuDismissArea.SetActive(true);
            menuDismissArea.transform.SetAsLastSibling();
            foreach (var item in menus)
            {
                item.transform.SetAsLastSibling();
            }

            menu.Open();
        }

        public void CloseMenus()
        {
            foreach (var menu in menus)
            {
                menu.Close();
            }

            menuDismissArea.SetActive(false);
        }
    }
}
