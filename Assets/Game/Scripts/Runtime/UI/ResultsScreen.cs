using System;
using System.Globalization;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.UI
{
    public sealed class ResultsScreen : MonoBehaviour
    {
        [SerializeField]
        private ResultsView view;
        [SerializeField]
        private ResultsChartReview review;
        private Action flow;
        private PlayResult result;

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
            Validate();
            OnDestroy();
            flow = onReturn;
            view.backButton.onClick.AddListener(Back);
            view.moreButton.onClick.AddListener(OpenReview);
            review.Closed += ReviewClosed;
            Display(result);
        }

        public void Display(PlayResult result)
        {
            this.result = result;
            review.Close();
            view.moreButton.interactable = result?.Chart != null;
            view.cover.enabled = false;
            view.coverPlaceholder.gameObject.SetActive(true);

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
            view.artist.text = result.Artist;
            view.difficulty.text = result.Difficulty;
            view.outcome.text = "CLEAR";

            if (result.Session.Failure.Failed)
            {
                view.outcome.text = "FAILED";
            }

            view.perfect.text = result.Session.Perfect.ToString(CultureInfo.InvariantCulture);
            view.good.text = result.Session.Good.ToString(CultureInfo.InvariantCulture);
            view.miss.text = result.Session.Miss.ToString(CultureInfo.InvariantCulture);
            view.accuracy.text = "정확도  " + result.Session.AccuracyRate.ToString("F2", CultureInfo.InvariantCulture) + "%";
            view.maxCombo.text = "최대 콤보  " + result.MaxCombo.ToString(CultureInfo.InvariantCulture);
            view.cover.sprite = result.Cover;
            view.cover.enabled = result.Cover;
            view.coverPlaceholder.gameObject.SetActive(!result.Cover);
        }

        private void Back()
        {
            flow?.Invoke();
        }

        private void OpenReview()
        {
            review.Show(result);
            view.backButton.interactable = false;
            view.moreButton.interactable = false;
        }

        private void ReviewClosed()
        {
            view.backButton.interactable = true;
            view.moreButton.interactable = result?.Chart != null;

            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(view.moreButton.gameObject);
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (flow == null || keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (view.review.gameObject.activeSelf)
                {
                    review.Close();
                }
                else
                {
                    Back();
                }
            }
        }

        private void OnDestroy()
        {
            if (review)
            {
                review.Closed -= ReviewClosed;
            }

            if (view && view.backButton)
            {
                view.backButton.onClick.RemoveListener(Back);
                view.moreButton.onClick.RemoveListener(OpenReview);
            }
        }
    }
}
