using System;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        public Text title, startPrompt, quitQuestion;
        public Button start, settings, quit, confirmQuit, cancelQuit;
        public GameObject quitPopup;

        public void Validate()
        {
            if (!title || !startPrompt || !quitQuestion || !start || !settings || !quit ||
                !confirmQuit || !cancelQuit || !quitPopup)
                throw new InvalidOperationException("Main menu view references missing.");
        }

        public void SetMenuInteractable(bool value)
        {
            start.interactable = settings.interactable = quit.interactable = value;
        }
    }
}