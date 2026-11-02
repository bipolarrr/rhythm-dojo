using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class GameplayPauseMenu : MonoBehaviour, IModalInputParticipant
    {
        public GameObject panel;
        public Button resumeButton;
        public Button settingsButton;
        public Button restartButton;
        public Button exitButton;
        public GameObject countdown;
        public Text countdownText;
        private RhythmGameController controller;
        private AppFlowController flow;
        private GameplayCompositionRoot root;
        private double resumeAt = -1;

        public bool CountingDown => resumeAt >= 0;

        public void Validate()
        {
            if (!panel || !resumeButton || !settingsButton)
            {
                throw new System.InvalidOperationException("Gameplay pause menu references missing.");
            }
            if (!restartButton || !exitButton)
            {
                throw new System.InvalidOperationException("Gameplay pause navigation references missing.");
            }
            if (!countdown || !countdownText)
            {
                throw new System.InvalidOperationException("Gameplay resume countdown references missing.");
            }
        }

        public void Initialize(RhythmGameController controller, AppFlowController flow, GameplayCompositionRoot root)
        {
            Validate();
            this.controller = controller;
            this.flow = flow;
            this.root = root;
            resumeButton.onClick.AddListener(BeginResume);
            settingsButton.onClick.AddListener(OpenSettings);
            restartButton.onClick.AddListener(Restart);
            exitButton.onClick.AddListener(Exit);
        }

        public void Open()
        {
            if (!isActiveAndEnabled || !controller)
            {
                return;
            }

            controller.PauseSession();
            if (!controller.IsPaused)
            {
                return;
            }

            resumeAt = -1;
            countdown.SetActive(false);
            panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void BeginResume()
        {
            if (!panel.activeSelf || !controller.IsPaused)
            {
                return;
            }

            panel.SetActive(false);
            countdown.SetActive(true);
            resumeAt = Time.realtimeSinceStartupAsDouble + 3;
            countdownText.text = "3";
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OpenSettings() => flow.ShowGameplaySettings();

        private void Restart()
        {
            resumeAt = -1;
            panel.SetActive(false);
            countdown.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            controller.RestartSession();
        }

        private void Exit()
        {
            resumeAt = -1;
            root.ReturnToSelection();
        }

        private void Update()
        {
            if (!CountingDown)
            {
                return;
            }

            double remaining = resumeAt - Time.realtimeSinceStartupAsDouble;
            if (remaining <= 0)
            {
                resumeAt = -1;
                countdown.SetActive(false);
                controller.ResumeSession();
                return;
            }

            countdownText.text = Mathf.CeilToInt((float)remaining).ToString();
            float phase = 1 - ((float)remaining - Mathf.Floor((float)remaining));
            countdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.16f, 1, phase);
        }

        public void ResumeAfterModal()
        {
            if (panel.activeSelf)
            {
                EventSystem.current?.SetSelectedGameObject(settingsButton.gameObject);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && CountingDown)
            {
                Open();
            }
        }

        private void OnDestroy()
        {
            resumeButton.onClick.RemoveListener(BeginResume);
            settingsButton.onClick.RemoveListener(OpenSettings);
            restartButton.onClick.RemoveListener(Restart);
            exitButton.onClick.RemoveListener(Exit);
        }
    }
}
