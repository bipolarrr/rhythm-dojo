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
        public DojoModeView modeSelection;
        public Image[] ambientLights;
        public Image backdrop;
        public GameObject entranceInvitation;
        public Button editor;
        public Button entranceTrigger;
        public bool alwaysVisibleMenu;
        public CanvasGroup menuPanel;
        public Vector2 logoOpenPosition = new Vector2(-330, 0);
        public Vector2 menuOpenPosition = new Vector2(200, 0);
        public GameObject quitPopup;
        public DojoEntranceView entrance;
        public CanvasGroup logoGroup;
        public RectTransform logoArtwork;
        public RectTransform trainingInterior;
        public DojoSlidingImage slidingPlaque;
        public DojoSlidingImage slidingLogo;
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
            if (!backdrop)
            {
                throw new InvalidOperationException("Main menu backdrop missing.");
            }
            if (modeSelection)
            {
                modeSelection.Validate();
                if (!menuPanel || !start || !settings)
                {
                    throw new InvalidOperationException("Mode selection controls missing.");
                }
                if (!quit || !editor || !quitPopup)
                {
                    throw new InvalidOperationException("Mode selection menu missing.");
                }
                if (!confirmQuit || !cancelQuit || !quitQuestion)
                {
                    throw new InvalidOperationException("Mode selection quit dialog missing.");
                }
                return;
            }
            if (!title || !startPrompt || !logo)
            {
                throw new InvalidOperationException("Main menu view references missing.");
            }

            if (!entranceTrigger || !entranceInvitation)
            {
                throw new InvalidOperationException("Main menu view references missing.");
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
            if (entranceTrigger)
            {
                entranceTrigger.interactable = value;
            }

            if (logo)
            {
                logo.interactable = value;
            }
            if (modeSelection)
            {
                start.interactable = settings.interactable = quit.interactable = value;
            }

            if (editor)
            {
                editor.interactable = value;
            }
        }

        public void SetExpansion(float amount)
        {
            if (modeSelection)
            {
                amount = Mathf.Clamp01(amount);
                menuPanel.alpha = 1f;
                modeSelection.SetEntranceProgress(amount);
                menuPanel.interactable = menuPanel.blocksRaycasts = amount >= 1f;
                return;
            }

            if (!menuPanel)
            {
                amount = Mathf.Clamp01(amount);
                entrance.SetEntrySequence(amount);
                entrance.interiorBackdrop.alpha = 0f;
                logoGroup.blocksRaycasts = amount < .01f;
                startPrompt.gameObject.SetActive(amount < .01f);
                entranceInvitation.SetActive(amount < .01f);
                return;
            }

            if (alwaysVisibleMenu)
            {
                menuPanel.alpha = 1f;
                menuPanel.interactable = true;
                menuPanel.blocksRaycasts = true;
                startPrompt.gameObject.SetActive(false);
                return;
            }

            amount = Mathf.Clamp01(amount);
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            entrance.SetOpening(0f);
            logo.targetGraphic.raycastTarget = amount < .01f;
            ((RectTransform)logo.transform).anchoredPosition = Vector2.zero;
            float separation = Mathf.Min(220f, ((RectTransform)entrance.transform).rect.width * .18f) * eased;
            entrance.SetLogoSeparation(separation);

            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, 1f, amount));
            ((RectTransform)menuPanel.transform).anchoredPosition =
                menuOpenPosition + Vector2.up * (36f * (1f - reveal));
            menuPanel.transform.localScale = Vector3.one * Mathf.Lerp(.72f, 1f, reveal);
            menuPanel.alpha = reveal;
            if (trainingInterior)
            {
                trainingInterior.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, eased);
            }

            menuPanel.interactable = menuPanel.blocksRaycasts = amount >= .99f;
            startPrompt.gameObject.SetActive(amount < .01f);
        }
    }
}
