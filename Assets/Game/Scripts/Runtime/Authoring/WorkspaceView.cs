using System;
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
        SaveNew, DiscardNew, ConfirmSaveAs, CancelSaveAs
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
        public Slider volumeControl;
        public Dropdown recent, quantization;
        public Button quantizationCycle;
        public Text quantizationCycleValue;
        public Text status, selection, playback;
        public RawImage jacket;
        public GameObject exitPopup;
        public GameObject newPopup;
        public GameObject saveAsPopup;
        public Text saveAsError;
        public WorkspaceMenuPopup[] menus;
        public Button[] difficulty;
        public GameObject menuDismissArea;

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
            if (exitPopup.activeSelf || newPopup.activeSelf)
            {
                return;
            }

            if (saveAsPopup.activeSelf)
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
