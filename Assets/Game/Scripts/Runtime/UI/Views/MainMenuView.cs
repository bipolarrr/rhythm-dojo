using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public Text title, startPrompt, quitQuestion;
        public Button logo, start, settings, quit, confirmQuit, cancelQuit;
        public CanvasGroup menuPanel;
        public Vector2 logoOpenPosition = new Vector2(-330, 0);
        public Vector2 menuOpenPosition = new Vector2(200, 0);
        public GameObject quitPopup;

        public void Validate()
        {
            if (!title || !startPrompt || !quitQuestion || !logo || !menuPanel || !start || !settings || !quit ||
                !confirmQuit || !cancelQuit || !quitPopup)
                throw new InvalidOperationException("Main menu view references missing.");
        }

        public void SetMenuInteractable(bool value)
        {
            logo.interactable = start.interactable = settings.interactable = quit.interactable = value;
        }

        public void SetExpansion(float amount)
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            ((RectTransform)logo.transform).anchoredPosition = Vector2.Lerp(Vector2.zero, logoOpenPosition, eased);
            ((RectTransform)menuPanel.transform).anchoredPosition = menuOpenPosition + new Vector2(30f * (1f - eased), 0);
            menuPanel.alpha = eased;
            menuPanel.interactable = menuPanel.blocksRaycasts = amount >= .99f;
            startPrompt.gameObject.SetActive(amount < .01f);
        }
    }
}
