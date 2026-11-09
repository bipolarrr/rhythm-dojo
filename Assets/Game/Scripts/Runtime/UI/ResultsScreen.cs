using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
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
        private Action flow;
        private Func<CancellationToken, Task> retry;
        private CancellationTokenSource retryLifetime;
        private SongLoadErrorDisplay retryFailure;
        private bool retrying;
        private PlayResult result;
        private ISelectionPreferences preferences;
        private Button timingRecommendation;
        private Text timingRecommendationLabel;
        private Action openTimingCalibration;
        private int modalClosedFrame = -1;

        public void Validate()
        {
            if (!view || !review)
            {
                throw new System.InvalidOperationException("Results screen references missing.");
            }

            view.Validate();
        }

        public void Initialize(PlayResult result, Action onReturn)
        {
            Initialize(result, onReturn, null);
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
            Action openTimingCalibration
        )
        {
            Unbind();
            Validate();
            view.EnsureScoreActions();

            flow = onReturn;
            retry = onRetry;
            preferences = savedPreferences;
            this.openTimingCalibration = openTimingCalibration;
            retryLifetime = new CancellationTokenSource();
            retryFailure = null;
            retrying = false;
            modalClosedFrame = -1;

            view.backButton.onClick.AddListener(Back);
            view.moreButton.onClick.AddListener(OpenReview);
            view.retryButton.onClick.AddListener(Retry);

            if (preferences != null)
            {
                BindTimingRecommendation();
            }

            review.Closed += ReviewClosed;
            Display(result);

            var primary = view.backButton;
            if (view.retryButton.gameObject.activeInHierarchy && view.retryButton.interactable)
            {
                primary = view.retryButton;
            }

            EventSystem.current?.SetSelectedGameObject(primary.gameObject);
        }

        public void Display(PlayResult result)
        {
            this.result = result;
            review.Close();
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
                view.progressNote.text = "이번 수련의 결과";
            }

            RefreshTimingRecommendation();

            if (result == null)
            {
                view.title.text = "플레이 결과가 없습니다";
                view.artist.text = "곡을 선택하고 플레이해 주세요";
                view.difficulty.text = "";
                view.outcome.text = "플레이 결과";
                view.perfect.text = "—";
                view.good.text = "—";
                view.miss.text = "—";
                view.accuracy.text = "정확도  —";
                view.maxCombo.text = "최대 콤보  —";

                return;
            }

            view.title.text = result.Title;
            RefreshScoreSummary(result);
            view.artist.text = result.Artist;
            view.difficulty.text = result.Difficulty;
            view.outcome.text = new SongRecord { rate = result.Session.AccuracyRate }.Rank;

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
            view.accuracy.text = "정확도  " + result.Session.AccuracyRate.ToString("F2", CultureInfo.InvariantCulture) + "%";
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
            if (!timingRecommendation.interactable || result == null)
            {
                return;
            }

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
            view.scoreValue.text = result.Score.ToString("N0", CultureInfo.CurrentCulture);
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
            int difference = result.Score - best;
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
            if (retry == null || retrying || view.review.gameObject.activeSelf)
            {
                return;
            }

            if (retryFailure != null && !retryFailure.CanRetry)
            {
                return;
            }

            var currentLifetime = retryLifetime;
            retrying = true;
            view.retryButton.interactable = false;
            view.backButton.interactable = false;
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
                view.backButton.interactable = true;
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
                view.backButton.interactable = true;
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
                flow?.Invoke();
            }
        }

        private void OpenReview()
        {
            review.Show(result);
            view.backButton.interactable = false;
            view.moreButton.interactable = false;
            view.retryButton.interactable = false;
            RefreshTimingRecommendation();
        }

        private void ReviewClosed()
        {
            view.backButton.interactable = true;
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
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (flow == null || keyboard == null || Time.frameCount == modalClosedFrame)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
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

            if (view && view.backButton)
            {
                view.backButton.onClick.RemoveListener(Back);
                view.moreButton.onClick.RemoveListener(OpenReview);
                if (view.retryButton)
                {
                    view.retryButton.onClick.RemoveListener(Retry);
                }
            }
        }

        private void OnDestroy() => Unbind();
    }
}
