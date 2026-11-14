using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
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
        public Text recoveryStatus;
        private RhythmGameController controller;
        private AppFlowController flow;
        private GameplayCompositionRoot root;
        private double resumeAt = -1;
        private bool waitingForResume;
        private CancellationTokenSource recoveryCancellation;
        private int recoveryGeneration;

        public bool CountingDown => resumeAt >= 0 || waitingForResume;
        public bool RecoveringAudio => recoveryCancellation != null;
        public string RecoveryError { get; private set; }

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
            if (!recoveryStatus)
            {
                throw new System.InvalidOperationException("Gameplay audio recovery status missing.");
            }
        }

        public void Initialize(RhythmGameController controller, AppFlowController flow, GameplayCompositionRoot root)
        {
            Validate();
            this.controller = controller;
            this.flow = flow;
            this.root = root;
            resumeButton.onClick.AddListener(BeginResume);
            settingsButton.interactable = false;
            settingsButton.gameObject.SetActive(false);
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
            if (!controller.IsPaused && !root.NeedsAudioRecovery)
            {
                return;
            }

            CancelRecovery();
            resumeAt = -1;
            countdown.SetActive(false);
            panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
        }

        internal bool HandleEscape()
        {
            if (!isActiveAndEnabled)
            {
                return true;
            }

            if (CountingDown)
            {
                Open();
                return true;
            }

            if (panel.activeSelf)
            {
                if (RecoveringAudio)
                {
                    Open();
                }
                else
                {
                    BeginResume();
                }

                return true;
            }

            if (controller.Session.State != SessionState.Playing)
            {
                return false;
            }

            Open();
            return true;
        }

        internal void PauseForFocusLoss()
        {
            if (!CountingDown && !RecoveringAudio)
            {
                if (panel.activeSelf || controller.IsPaused)
                {
                    return;
                }
            }

            Open();
            if (panel.activeSelf)
            {
                SetRecoveryStatus("창이 전환되어 수련을 멈췄습니다.");
            }
        }

        public async void BeginResume()
        {
            if (!panel.activeSelf)
            {
                return;
            }

            if (!controller.IsPaused && !root.NeedsAudioRecovery)
            {
                return;
            }

            if (RecoveringAudio)
            {
                return;
            }

            int generation = recoveryGeneration;
            if (!await PreparePlayback())
            {
                return;
            }

            if (!this || generation != recoveryGeneration)
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

        private async void Restart()
        {
            if (RecoveringAudio)
            {
                return;
            }

            int generation = recoveryGeneration;
            if (!await PreparePlayback())
            {
                return;
            }

            if (!this || generation != recoveryGeneration)
            {
                return;
            }

            resumeAt = -1;
            panel.SetActive(false);
            countdown.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            controller.RestartSession();
        }

        private void Exit()
        {
            CancelRecovery();
            resumeAt = -1;
            root.ReturnToSelection();
        }

        private void Update()
        {
            if (waitingForResume)
            {
                if (!controller.IsPaused)
                {
                    waitingForResume = false;
                    countdown.SetActive(false);
                }
                else if (controller.Clock is IScheduledResumeClock scheduled && !scheduled.ResumePending)
                {
                    if (!controller.Clock.Running)
                    {
                        Open();
                    }
                }
                return;
            }

            if (!CountingDown)
            {
                return;
            }

            double remaining = resumeAt - Time.realtimeSinceStartupAsDouble;
            if (remaining <= 0)
            {
                resumeAt = -1;
                CompleteResume();
                return;
            }

            countdownText.text = Mathf.CeilToInt((float)remaining).ToString();
            float phase = 1 - ((float)remaining - Mathf.Floor((float)remaining));
            countdownText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.16f, 1, phase);
        }

        private async void CompleteResume()
        {
            int generation = recoveryGeneration;
            if (!await PreparePlayback())
            {
                return;
            }

            if (!this || generation != recoveryGeneration)
            {
                return;
            }

            panel.SetActive(false);
            controller.ResumeSession();
            waitingForResume = controller.IsPaused;
            countdown.SetActive(waitingForResume);
            countdownText.text = "1";
        }

        private async Task<bool> PreparePlayback()
        {
            if (!root.NeedsAudioRecovery)
            {
                SetRecoveryStatus("");
                return true;
            }

            var cancellation = new CancellationTokenSource();
            recoveryCancellation = cancellation;
            RecoveryError = null;
            SetRecoveryControls(false);
            panel.SetActive(true);
            countdown.SetActive(false);
            SetRecoveryStatus("음원 복구 중…");
            try
            {
                await root.PreparePlaybackAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                return this && ReferenceEquals(recoveryCancellation, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception error)
            {
                if (this && ReferenceEquals(recoveryCancellation, cancellation))
                {
                    RecoveryError = error.Message;
                    panel.SetActive(true);
                    countdown.SetActive(false);
                    SetRecoveryStatus("음원을 복구하지 못했습니다. 이어하기로 다시 시도하세요.");
                    EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
                }
                return false;
            }
            finally
            {
                if (ReferenceEquals(recoveryCancellation, cancellation))
                {
                    recoveryCancellation = null;
                    if (this)
                    {
                        SetRecoveryControls(true);
                    }
                }
                cancellation.Dispose();
            }
        }

        private void CancelRecovery()
        {
            waitingForResume = false;
            recoveryGeneration++;
            recoveryCancellation?.Cancel();
            recoveryCancellation = null;
            RecoveryError = null;
            SetRecoveryStatus("");
            SetRecoveryControls(true);
        }

        private void SetRecoveryStatus(string text)
        {
            if (recoveryStatus)
            {
                recoveryStatus.text = text;
                recoveryStatus.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        private void SetRecoveryControls(bool enabled)
        {
            resumeButton.interactable = enabled;
            settingsButton.interactable = false;
            restartButton.interactable = enabled;
            exitButton.interactable = true;
        }

        public void ResumeAfterModal()
        {
            if (panel.activeSelf)
            {
                EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
            }
        }

        private void OnDestroy()
        {
            CancelRecovery();
            resumeButton.onClick.RemoveListener(BeginResume);
            settingsButton.onClick.RemoveListener(OpenSettings);
            restartButton.onClick.RemoveListener(Restart);
            exitButton.onClick.RemoveListener(Exit);
        }
    }
}
