using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public Text title,
            startPrompt,
            quitQuestion;
        public Button logo,
            start,
            settings,
            quit,
            confirmQuit,
            cancelQuit;
        public bool alwaysVisibleMenu;
        public Button chartEditor;
        public Button chartWorkspace;
        public CanvasGroup menuPanel;
        public Vector2 logoOpenPosition = new Vector2(-330, 0);
        public Vector2 menuOpenPosition = new Vector2(200, 0);
        public GameObject quitPopup;
        public DojoEntranceView entrance;
        public CanvasGroup logoGroup;
        public RectTransform logoArtwork;
        public AudioClip knock,
            boardCrack,
            doorSlide;

        public void RestoreBoards()
        {
            foreach (var board in GetComponentsInChildren<DojoBoardBreak>(true))
            {
                board.Restore();
            }

            if (logoGroup)
            {
                logoGroup.alpha = 1f;
            }
        }

        public void Validate()
        {
            if ((!title || !startPrompt) || !quitQuestion)
            {
                throw new InvalidOperationException("Main menu view references missing.");
            }

            if ((!logo || !menuPanel) || !start)
            {
                throw new InvalidOperationException("Main menu view references missing.");
            }

            if ((!settings || !quit) || !confirmQuit)
            {
                throw new InvalidOperationException("Main menu view references missing.");
            }

            if (!cancelQuit || !quitPopup)
            {
                throw new InvalidOperationException("Main menu view references missing.");
            }

            if (!chartEditor)
            {
                throw new InvalidOperationException("Chart editor button missing.");
            }

            if (!entrance || !logoGroup || !logoArtwork)
            {
                throw new InvalidOperationException("Dojo entrance references missing.");
            }

            if (!entrance.leftDoor || !entrance.rightDoor || !entrance.frame)
            {
                throw new InvalidOperationException("Dojo door references missing.");
            }
        }

        public void SetMenuInteractable(bool value)
        {
            chartEditor.interactable = value;
            if (chartWorkspace)
            {
                chartWorkspace.interactable = value;
            }
            logo.interactable =
                start.interactable =
                settings.interactable =
                quit.interactable =
                    value;
        }

        public void SetExpansion(float amount)
        {
            if (alwaysVisibleMenu)
            {
                menuPanel.alpha = 1f;
                menuPanel.interactable = true;
                menuPanel.blocksRaycasts = true;
                startPrompt.gameObject.SetActive(false);
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, amount);
            ((RectTransform)logo.transform).anchoredPosition = Vector2.Lerp(
                Vector2.zero,
                logoOpenPosition,
                eased
            );
            ((RectTransform)menuPanel.transform).anchoredPosition =
                menuOpenPosition + new Vector2(30f * (1f - eased), 0);
            menuPanel.alpha = eased;
            menuPanel.interactable = menuPanel.blocksRaycasts = amount >= .99f;
            startPrompt.gameObject.SetActive(amount < .01f);
        }
    }
}
