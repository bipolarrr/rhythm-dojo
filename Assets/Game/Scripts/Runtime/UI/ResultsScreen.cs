using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class ResultsScreen : MonoBehaviour, IModalInputParticipant
    {
        [SerializeField]
        private ResultsView view;
        [SerializeField]
        private ResultsChartReview review;
        [SerializeField]
        private ResultsSoundPlayer sounds;
        private Action flow;
        private Func<CancellationToken, Task> retry;
        private CancellationTokenSource retryLifetime;
        private SongLoadErrorDisplay retryFailure;
        private bool retrying;
        private PlayResult result;
        private int displayedScore;
        private double displayedAccuracyRate;
        private ISelectionPreferences preferences;
        private Button timingRecommendation;
        private Text timingRecommendationLabel;
        private Action openTimingCalibration;
        private int modalClosedFrame = -1;
        private bool inputReady;
        private int readyAfterFrame = -1;
        private int displayFrame;
        private GameObject lastSelection;
        private bool replayOnEnable;
        private readonly System.Collections.Generic.List<Selectable> menuControls =
            new System.Collections.Generic.List<Selectable>();

        public void Validate()
        {
            if (!view || !review)
            {
                throw new System.InvalidOperationException("Results screen references missing.");
            }

            view.Validate();
            if (!sounds)
            {
                throw new InvalidOperationException("Recorded results audio pool missing.");
            }
            sounds.Validate();
        }

        public void Initialize(PlayResult result, Action onReturn)
        {
            Initialize(result, onReturn, null);
        }

        // Presentation values can be supplied independently of the gameplay calculation.
        public void Initialize(PlayResult result, Action onReturn, int score, double accuracyRate)
        {
            Initialize(result, onReturn, null, null, null, score, accuracyRate);
        }

        public void Initialize(PlayResult result, Action onReturn, Func<CancellationToken, Task> onRetry)
        {
            Initialize(result, onReturn, onRetry, null);
        }

        public void Initialize(
            PlayResult result,
            Action onReturn,
            Func<CancellationToken, Task> onRetry,
            ISelectionPreferences savedPreferences
        )
        {
            Initialize(result, onReturn, onRetry, savedPreferences, null);
        }

        internal void Initialize(
            PlayResult result,
            Action onReturn,
            Func<CancellationToken, Task> onRetry,
            ISelectionPreferences savedPreferences,
            Action openTimingCalibration,
            int? score = null,
            double? accuracyRate = null
        )
        {
            Unbind();
            Validate();

            flow = onReturn;
            retry = onRetry;
            preferences = savedPreferences;
            this.openTimingCalibration = openTimingCalibration;
            retryLifetime = new CancellationTokenSource();
            retryFailure = null;
            retrying = false;
            modalClosedFrame = -1;

            view.moreButton.onClick.AddListener(OpenReview);
            view.retryButton.onClick.AddListener(Retry);

            if (preferences != null)
            {
                BindTimingRecommendation();
            }

            review.Closed += ReviewClosed;
            Display(result, score ?? result?.Score ?? 0, accuracyRate ?? result?.Session.AccuracyRate ?? 0);
        }

        public void Display(PlayResult result)
        {
            Display(result, result?.Score ?? 0, result?.Session.AccuracyRate ?? 0);
        }

        public void Display(PlayResult result, int score, double accuracyRate)
        {
            this.result = result;
            displayedScore = score;
            displayedAccuracyRate = accuracyRate;
            review.Close();
            inputReady = false;
            readyAfterFrame = -1;
            displayFrame = Time.frameCount;
            lastSelection = null;
            EventSystem.current?.SetSelectedGameObject(null);
            view.moreButton.interactable = result?.Chart != null;
            view.cover.enabled = false;
            view.coverPlaceholder.gameObject.SetActive(true);
            view.retryButton.gameObject.SetActive(result != null && retry != null);
            view.retryButton.interactable = !retrying && (retryFailure?.CanRetry ?? true);
            view.retryStatus.text = "";
            view.scoreValue.text = "—";
            view.scoreSummary.text = "";
            view.newRecordBadge.SetActive(false);
            if (view.progressNote)
            {
                view.progressNote.text = "";
            }

            RefreshTimingRecommendation();

            if (result == null)
            {
                view.title.text = "플레이 결과가 없습니다";
                view.artist.text = "곡을 선택하고 플레이해 주세요";
                view.difficulty.text = "";
                view.outcome.text = "";
                view.perfect.text = "—";
                view.good.text = "—";
                view.miss.text = "—";
                view.accuracy.text = "정확도  —";
                view.maxCombo.text = "최대 콤보  —";
                BeginPresentation();

                return;
            }

            view.title.text = result.Title;
            RefreshScoreSummary(result);
            view.artist.text = result.Artist;
            view.difficulty.text = result.Difficulty;
            view.outcome.text = new SongRecord { rate = displayedAccuracyRate }.Rank;

            if (view.progressNote && result.Session.State == SessionState.Completed)
            {
                view.progressNote.text = "수련 완료";
            }

            if (result.Session.Failure.Failed)
            {
                view.outcome.text = "FAILED";
                if (view.progressNote)
                {
                    double seconds = Math.Max(0, result.Session.Failure.Time);
                    var elapsed = TimeSpan.FromSeconds(seconds);
                    view.progressNote.text = $"수련 중단  ·  {(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
                    if (result.Session.State == SessionState.Completed)
                    {
                        view.progressNote.text = $"완주  ·  실패 시점 {(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
                    }
                }
            }
            view.perfect.text = result.Session.Perfect.ToString(CultureInfo.InvariantCulture);
            view.good.text = result.Session.Good.ToString(CultureInfo.InvariantCulture);
            view.miss.text = result.Session.Miss.ToString(CultureInfo.InvariantCulture);
            view.accuracy.text = "정확도  " + displayedAccuracyRate.ToString("F2", CultureInfo.InvariantCulture) + "%";
            view.maxCombo.text = "최대 콤보  " + result.MaxCombo.ToString(CultureInfo.InvariantCulture);
            view.cover.sprite = result.Cover;
            if (!result.Cover || result.Cover == view.legacyFallbackCover)
            {
                view.cover.sprite = view.fallbackCover;
            }
            view.cover.enabled = view.cover.sprite;
            view.coverPlaceholder.gameObject.SetActive(!view.cover.sprite);
            if (view.cover.sprite)
            {
                var fit = view.cover.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                if (fit)
                {
                    fit.aspectRatio = view.cover.sprite.rect.width / view.cover.sprite.rect.height;
                }
            }
            BeginPresentation();
        }

        private void BeginPresentation()
        {
            // Capture display values; the animation never owns or modifies PlayResult.
            view.presentation.Begin(displayedScore, result?.Session.Failure.Failed ?? false, result != null);
            if (!UnityEngine.Application.isPlaying)
            {
                // Static editor consumers have no Update loop to advance the entrance.
                view.presentation.Finish();
                inputReady = true;
                view.presentation.inputGate.interactable = true;
                view.presentation.inputGate.blocksRaycasts = true;
            }
        }

        private void BindTimingRecommendation()
        {
            timingRecommendation = view.recommendationButton;
            timingRecommendationLabel = timingRecommendation.GetComponentInChildren<Text>(true);
            timingRecommendation.onClick.AddListener(ApplyTimingRecommendation);
        }

        private void RefreshTimingRecommendation()
        {
            if (!timingRecommendation)
            {
                return;
            }

            int? recommendation = result?.TimingOffsetRecommendationMs;
            bool available = recommendation.HasValue || openTimingCalibration != null;
            timingRecommendation.gameObject.SetActive(result != null && preferences != null && available);

            if (!timingRecommendation.gameObject.activeSelf)
            {
                return;
            }

            bool canUseTimingControl = !retrying && !view.review.gameObject.activeSelf;
            if (!recommendation.HasValue)
            {
                timingRecommendationLabel.text = "박자 보정 테스트";
                timingRecommendation.interactable = canUseTimingControl;
                return;
            }

            int value = recommendation.Value;
            bool applied = result.AppliedTimingOffsetMs.HasValue || preferences.TimingOffsetMs == value;

            if (applied)
            {
                timingRecommendationLabel.text = $"현재 보정 {value:+0;-0;0} ms";
            }
            else
            {
                timingRecommendationLabel.text = $"보정 추천 {value:+0;-0;0} ms 적용";
            }

            timingRecommendation.interactable = !applied && canUseTimingControl;
        }

        private void ApplyTimingRecommendation()
        {
            if (!inputReady)
            {
                return;
            }
            if (!timingRecommendation.interactable || result == null)
            {
                return;
            }
            PlayConfirmation();

            if (result.TimingOffsetRecommendationMs is not { } value)
            {
                openTimingCalibration?.Invoke();
                return;
            }

            preferences.TimingOffsetMs = value;
            preferences.Save();

            result.AppliedTimingOffsetMs = value;
            RefreshTimingRecommendation();
        }

        private void RefreshScoreSummary(PlayResult result)
        {
            view.scoreValue.text = displayedScore.ToString("N0", CultureInfo.CurrentCulture);
            if (!result.CanCompareScore)
            {
                return;
            }

            if (!result.PreviousBestScore.HasValue)
            {
                view.scoreSummary.text = "첫 기록";
                return;
            }

            int best = result.PreviousBestScore.Value;
            int difference = displayedScore - best;
            string bestLabel = "이전 최고";
            if (result.Session.Failure.Failed)
            {
                bestLabel = "이전 실패 완주 최고";
            }

            view.scoreSummary.text = $"{bestLabel}  {best:N0}\n최고점 대비  {difference:+#,0;-#,0;0}";
            view.newRecordBadge.SetActive(difference > 0);
        }

        private async void Retry()
        {
            if (!inputReady)
            {
                return;
            }
            if (retry == null || retrying || view.review.gameObject.activeSelf)
            {
                return;
            }

            if (retryFailure != null && !retryFailure.CanRetry)
            {
                return;
            }

            var currentLifetime = retryLifetime;
            sounds.StopAll();
            PlayConfirmation();
            retrying = true;
            view.retryButton.interactable = false;
            view.moreButton.interactable = false;
            view.retryStatus.text = "곡 불러오는 중…";
            RefreshTimingRecommendation();
            try
            {
                await retry(currentLifetime.Token);
            }
            catch (OperationCanceledException)
            {
                if (!this || retryLifetime != currentLifetime)
                {
                    return;
                }

                if (currentLifetime.IsCancellationRequested)
                {
                    return;
                }

                retrying = false;
                view.retryButton.interactable = true;
                view.moreButton.interactable = result?.Chart != null;
                view.retryStatus.text = "";
                RefreshTimingRecommendation();
            }
            catch (Exception e)
            {
                if (!this || retryLifetime != currentLifetime)
                {
                    return;
                }

                if (currentLifetime.IsCancellationRequested)
                {
                    return;
                }

                retrying = false;
                view.retryButton.interactable = true;
                view.moreButton.interactable = result?.Chart != null;
                var display = SongLoadErrorDisplay.FromException(e);
                retryFailure = display;
                view.retryButton.interactable = display.CanRetry;
                view.retryStatus.text = display.Message;
                RefreshTimingRecommendation();
            }
        }

        private void Back()
        {
            if (!retrying)
            {
                sounds.StopAll();
                flow?.Invoke();
            }
        }

        private void OpenReview()
        {
            if (!inputReady || retrying)
            {
                return;
            }
            PlayConfirmation();
            review.Show(result);
            view.moreButton.interactable = false;
            view.retryButton.interactable = false;
            RefreshTimingRecommendation();
        }

        private void ReviewClosed()
        {
            view.moreButton.interactable = result?.Chart != null;
            view.retryButton.interactable = !retrying && (retryFailure?.CanRetry ?? true);
            RefreshTimingRecommendation();

            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.moreButton.gameObject);
            }
        }

        private void Update()
        {
            if (!view || !RhythmDojo.Input.ApplicationFocusState.IsFocused)
            {
                return;
            }
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (Time.frameCount == modalClosedFrame)
            {
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (view.review.gameObject.activeSelf)
                {
                    if (view.review.zoom.GetComponentInChildren<Canvas>())
                    {
                        return;
                    }

                    review.Close();
                }
                else
                {
                    Back();
                }
                return;
            }

            if (!inputReady)
            {
                if (view.presentation.IsRevealing)
                {
                    if (Time.frameCount > displayFrame && SubmitPressed())
                    {
                        view.presentation.Finish();
                        readyAfterFrame = Time.frameCount;
                    }
                    return;
                }
                if (readyAfterFrame < 0)
                {
                    readyAfterFrame = Time.frameCount;
                }
                // Wait for release AND a later frame. EventSystem Update may run before
                // or after this component; the skip must never also submit a menu action.
                if (Time.frameCount <= readyAfterFrame + 1 || SubmitHeld())
                {
                    return;
                }
                inputReady = true;
                view.presentation.inputGate.interactable = true;
                view.presentation.inputGate.blocksRaycasts = true;
            }

            RefreshNavigation();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != lastSelection)
            {
                if (lastSelection && selected)
                {
                    sounds.Menu(false);
                }
                lastSelection = selected;
            }
        }

        private static bool SubmitPressed()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                {
                    return true;
                }
                if (keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool SubmitHeld()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed)
                {
                    return true;
                }
                if (keyboard.numpadEnterKey.isPressed)
                {
                    return true;
                }
            }
            var gamepad = UnityEngine.InputSystem.Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.isPressed)
            {
                return true;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private void PlayConfirmation()
        {
            // Pointer clicks already use the shared DojoClickFeedback source.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
            {
                return;
            }
            sounds.Menu(true);
        }

        private void RefreshNavigation()
        {
            if (!view || !inputReady)
            {
                return;
            }
            if (view.review.gameObject.activeSelf)
            {
                return;
            }

            menuControls.Clear();
            AddControl(view.retryButton);
            AddControl(view.recommendationButton);
            AddControl(view.moreButton);
            DojoMenuNavigation.Chain(menuControls, true);
            var system = EventSystem.current;
            if (!system || menuControls.Count == 0)
            {
                return;
            }

            var selected = system.currentSelectedGameObject;
            if (!selected || !selected.activeInHierarchy)
            {
                menuControls[0].Select();
            }
            else if (selected.TryGetComponent<Selectable>(out var control) && !control.IsInteractable())
            {
                menuControls[0].Select();
            }
        }

        private void AddControl(Selectable control)
        {
            if (control.gameObject.activeInHierarchy && control.IsInteractable())
            {
                menuControls.Add(control);
            }
        }

        public void ResumeAfterModal()
        {
            modalClosedFrame = Time.frameCount;
            RefreshTimingRecommendation();
        }

        private void Unbind()
        {
            retryLifetime?.Cancel();
            retryLifetime?.Dispose();
            retryLifetime = null;
            openTimingCalibration = null;
            if (timingRecommendation)
            {
                timingRecommendation.onClick.RemoveListener(ApplyTimingRecommendation);
            }
            if (review)
            {
                review.Closed -= ReviewClosed;
            }

            if (sounds)
            {
                sounds.StopAll();
            }
            if (view)
            {
                if (view.moreButton)
                {
                    view.moreButton.onClick.RemoveListener(OpenReview);
                }
                if (view.retryButton)
                {
                    view.retryButton.onClick.RemoveListener(Retry);
                }
            }
        }

        private void OnDisable()
        {
            // ModalInputScope disables this controller alone. Only reopening the
            // actual Results object should replay its presentation.
            replayOnEnable = !gameObject.activeInHierarchy;
            if (sounds)
            {
                sounds.StopAll();
            }
        }

        private void OnEnable()
        {
            if (!replayOnEnable)
            {
                return;
            }
            replayOnEnable = false;
            if (result != null && view)
            {
                Display(result, displayedScore, displayedAccuracyRate);
            }
        }

        private void OnDestroy() => Unbind();
    }
}
