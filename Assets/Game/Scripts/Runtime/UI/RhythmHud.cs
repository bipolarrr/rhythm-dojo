using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class RhythmHud : MonoBehaviour
    {
        [SerializeField]
        private GameplayHudView view;
        private IGameplayReadModel model;
        private GameplayPresentationSettings settings;
        private Image[] indicators;
        private Text[] keyLabels;
        private GameModeDefinition bindingMode;
        private string bindingModeId;
        private double[] flashUntil;
        private Color[] flashColor;
        private double nextTextRefresh;
        private double judgmentAt = -10;
        private double comboAt = -10;
        private int displayedCombo = -1;
        private bool showingJudgment;
        private string difficulty;
        private float previousHealth = 1;
        private double damagedAt = -10;

        public void SetSongInformation(string title, string difficulty, Sprite cover)
        {
            this.difficulty = difficulty;
            view.songTitle.text = title;
            view.songDetails.text = difficulty;
            view.album.sprite = cover;
            if (!cover)
            {
                view.album.sprite = view.fallbackCover;
            }
        }

        public void SetIntroductionVisible(bool visible)
        {
            view.introduction.SetActive(visible);
            view.lanes.laneRoot.gameObject.SetActive(visible);
        }
        private RectTransform judgmentRect;
        private Vector2 judgmentAnchorMin;
        private Vector2 judgmentAnchorMax;
        private Color Cream = new Color(.94f, .93f, .85f);
        private Color Cyan = new Color(.46f, .84f, .81f);
        private Color Gold = new Color(1f, .77f, .42f);
        private Color Red = new Color(.96f, .29f, .31f);

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("HUD view missing.");
            }

            view.Validate();
        }

        public void Initialize(
            IGameplayReadModel readModel,
            GameModeDefinition mode,
            GameplayPresentationSettings presentation,
            string modeId = null
        )
        {
            Unbind();
            Validate();
            if (view.pixelTheme)
            {
                Cream = new Color(.98f, .86f, .65f);
                Cyan = new Color(.67f, .90f, .83f);
                Gold = new Color(1f, .87f, .57f);
                Red = new Color(.99f, .52f, .40f);
            }

            settings = presentation;
            bindingMode = mode;
            bindingModeId = modeId ?? mode.name;
            model = readModel;
            indicators = view.lanes.Build(mode, presentation, bindingModeId);
            keyLabels = new Text[indicators.Length];
            if (!judgmentRect)
            {
                judgmentRect = (RectTransform)view.judgmentGroup.transform;
                judgmentAnchorMin = judgmentRect.anchorMin;
                judgmentAnchorMax = judgmentRect.anchorMax;
            }

            for (int i = 0; i < indicators.Length; i++)
            {
                keyLabels[i] = indicators[i].GetComponentInChildren<Text>();
            }

            flashUntil = new double[indicators.Length];
            PlayKeyBindings.Changed += RefreshKeyLabels;
            flashColor = new Color[indicators.Length];
            model.Judged += Judged;
            model.HoldStarted += HoldStarted;
            model.ResetOccurred += ResetDisplay;
            ResetDisplay();
        }

        private void RefreshKeyLabels()
        {
            for (int i = 0; i < keyLabels.Length; i++)
            {
                keyLabels[i].text = PlayKeyBindings.GetLabel(bindingMode, bindingModeId, i);
            }
        }

        private void Judged(JudgmentEvent result)
        {
            Flash(result);
            ShowJudgment(result, false);
        }

        private void HoldStarted(HoldStartedEvent result)
        {
            Flash(result.Head);
            ShowJudgment(result.Head, true);
        }

        private void ShowJudgment(JudgmentEvent result, bool hold)
        {
            judgmentAt = Time.unscaledTimeAsDouble;
            showingJudgment = true;
            SetJudgmentPosition((PresentationPreferences.Current ?? PresentationPreferences.Load()).judgmentPosition);
            nextTextRefresh = 0;

            view.judgment.text = JudgmentDisplay.Label(result.Grade);
            view.judgment.fontSize = 52;
            if (hold)
            {
                view.judgment.fontSize = 44;
            }

            view.timing.fontSize = 14;

            if (result.Grade == Judgment.Miss)
            {
                view.judgment.color = Red;
            }
            else if (result.Grade == Judgment.Perfect)
            {
                view.judgment.color = Gold;
            }
            else
            {
                view.judgment.color = Cyan;
            }

            view.judgmentMotion.Play(result.Grade, hold);

            double milliseconds = result.ErrorSeconds * 1000;

            if (result.Grade == Judgment.Miss)
            {
                view.timing.text = "연격 끊김";
            }
            else if (Math.Abs(milliseconds) < .5)
            {
                view.timing.text = "정박";
            }
            else
            {
                if (milliseconds < 0)
                {
                    view.timing.text = $"빠름  {milliseconds:+0;-0;0} ms";
                }
                else
                {
                    view.timing.text = $"늦음  {milliseconds:+0;-0;0} ms";
                }
            }

            if (hold && result.Grade != Judgment.Miss)
            {
                view.timing.text = "유지 · " + view.timing.text;
            }
        }

        private void SetJudgmentPosition(JudgmentPosition position)
        {
            float offset = 0;
            switch (position)
            {
                case JudgmentPosition.Upper:
                    // Keep the upper stamp above the point where incoming notes enter.
                    offset = .997f - judgmentAnchorMax.y;
                    break;
                case JudgmentPosition.Lower:
                    offset = -.024f;
                    break;
            }

            var displacement = new Vector2(0, offset);
            judgmentRect.anchorMin = judgmentAnchorMin + displacement;
            judgmentRect.anchorMax = judgmentAnchorMax + displacement;
        }

        private void Flash(JudgmentEvent result)
        {
            flashUntil[result.Lane] = Time.unscaledTimeAsDouble + settings.flashDuration;

            if (result.Grade == Judgment.Miss)
            {
                flashColor[result.Lane] = settings.missColor;
            }
            else if (result.Grade == Judgment.Perfect)
            {
                flashColor[result.Lane] = settings.perfectColor;
            }
            else
            {
                flashColor[result.Lane] = settings.goodColor;
            }
        }

        private void ResetDisplay()
        {
            previousHealth = (float)model.Snapshot.Session.HealthRatio;
            damagedAt = -10;
            Array.Clear(flashUntil, 0, flashUntil.Length);
            nextTextRefresh = 0;
            showingJudgment = false;
            judgmentAt = comboAt = -10;
            displayedCombo = -1;
            view.combo.text = "";
            view.combo.color = Cream;
            view.combo.rectTransform.localScale = Vector3.one;
            SetJudgmentPosition(JudgmentPosition.Middle);
            view.judgmentGroup.alpha = 0;
            view.judgmentGroup.transform.localScale = Vector3.one;
            view.judgmentMotion.ResetPresentation();
            view.judgment.fontSize = 36;
            view.judgment.text = "";
            view.judgment.color = Cyan;
            view.timing.fontSize = 18;
            view.timing.text = "";
            RefreshHealth(model.Snapshot.Session);
        }

        private void LateUpdate()
        {
            if (model == null)
            {
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            // Lane count and display aspect are runtime data. Keep the authored gauge
            // adjacent to the widest visible part of the field without changing projection.
            float right = view.gameplayCamera.WorldToViewportPoint(new Vector3(
                bindingMode.LaneCount * bindingMode.LaneSpacing * .5f,
                0,
                settings.highwayCenter - settings.highwayLength * .5f)).x;
            var minimum = view.healthGauge.anchorMin;
            var maximum = view.healthGauge.anchorMax;
            float width = maximum.x - minimum.x;
            minimum.x = right + .012f;
            maximum.x = minimum.x + width;
            view.healthGauge.anchorMin = minimum;
            view.healthGauge.anchorMax = maximum;

            for (int i = 0; i < indicators.Length; i++)
            {
                if (now < flashUntil[i])
                {
                    indicators[i].color = flashColor[i];
                }
                else if (model.IsHeld(i))
                {
                    indicators[i].color = settings.heldColor;
                }
                else
                {
                    indicators[i].color = settings.idleColor;
                    if (view.pixelTheme)
                    {
                        indicators[i].color = Color.white;
                    }
                }

                var tint = indicators[i].color;

                if (tint.r * .2126f + tint.g * .7152f + tint.b * .0722f > .55f)
                {
                    keyLabels[i].color = new Color(.025f, .035f, .045f);
                }
                else
                {
                    keyLabels[i].color = Cream;
                }
            }

            var snapshot = model.Snapshot;
            RefreshHealth(snapshot.Session);
            RefreshCombo(snapshot.Session, now);
            AnimateFeedback(now);

            if (now < nextTextRefresh)
            {
                return;
            }

            nextTextRefresh = now + settings.hudRefreshInterval;
            RefreshTrainingDisplay(snapshot);
        }

        private void RefreshHealth(SessionSnapshot snapshot)
        {
            float fraction = Mathf.Clamp01((float)snapshot.HealthRatio);
            if (fraction < previousHealth)
            {
                damagedAt = Time.unscaledTimeAsDouble;
                view.healthDamage.rectTransform.anchorMax = new Vector2(1, previousHealth);
            }
            previousHealth = fraction;
            var damage = view.healthDamage.color;
            damage.a = Mathf.Clamp01(1 - (float)(Time.unscaledTimeAsDouble - damagedAt) / .24f);
            view.healthDamage.color = damage;
            view.healthFill.rectTransform.anchorMax = new Vector2(1, fraction);

            if (fraction <= .25f)
            {
                view.healthFill.color = Red;
            }
            else if (fraction <= .5f)
            {
                view.healthFill.color = Gold;
            }
            else
            {
                view.healthFill.color = Cyan;
            }

            view.healthLabel.color = view.healthFill.color;

            if (snapshot.Mode == RhythmDojo.Gameplay.PlayMode.Gauge)
            {
                view.healthLabel.text =
                    $"체력  {snapshot.HealthRatio * snapshot.MaximumHealth:0.##} / {snapshot.MaximumHealth}";
            }
            else
            {
                view.healthLabel.text =
                    $"Challenge · Good {snapshot.Good} / Miss {snapshot.Miss}";
            }

            RefreshOutcome(snapshot);

            if (snapshot.Failure.Failed && snapshot.State == SessionState.Playing)
            {
                view.healthLabel.text = "FAILED · 끝까지 진행";
            }
        }

        private void RefreshOutcome(SessionSnapshot snapshot)
        {
            bool completed = snapshot.State == SessionState.Completed;
            bool showOutcome = completed || snapshot.State == SessionState.GameOver;
            view.gameOverPanel.SetActive(showOutcome);

            if (!showOutcome)
            {
                return;
            }

            bool cleared = completed && !snapshot.Failure.Failed;
            var outcomeRect = view.gameOverText.rectTransform;
            float outcomeScale = 1f;
            if (cleared && !snapshot.IsFullCombo)
            {
                outcomeScale = 1.16f;
            }
            outcomeRect.localScale = Vector3.one * outcomeScale;

            if (cleared)
            {
                view.gameOverText.text = "GAME CLEAR";
                view.gameOverText.color = Cyan;

                if (snapshot.IsFullCombo)
                {
                    view.gameOverText.text = "Full Combo";
                }

                return;
            }

            view.gameOverText.text = "GAME OVER";
            view.gameOverText.color = Red;
        }

        private void AnimateFeedback(double now)
        {
            float comboPulse = Mathf.Clamp01(1 - (float)(now - comboAt) / .20f);
            float accent = .055f;
            if (displayedCombo >= 50 && displayedCombo % 10 == 0)
            {
                accent = .13f;
            }
            view.combo.rectTransform.localScale = Vector3.one * (1 + accent * comboPulse * comboPulse);
            view.combo.rectTransform.anchoredPosition = Vector2.up * (comboPulse * comboPulse * 3);

            if (!showingJudgment)
            {
                return;
            }

            float age = (float)(now - judgmentAt);
            view.judgmentMotion.Render(age);
        }

        private void RefreshCombo(SessionSnapshot snapshot, double now)
        {
            int value = 0;
            if (snapshot.State == SessionState.Playing)
            {
                value = snapshot.Combo;
            }
            if (displayedCombo == value)
            {
                return;
            }

            displayedCombo = value;
            if (value == 0)
            {
                view.combo.text = "";
                return;
            }

            view.combo.text = value.ToString();
            view.combo.color = Cream;
            if (value >= 50)
            {
                view.combo.color = Gold;
            }
            comboAt = now;
        }

        private void RefreshTrainingDisplay(GameplaySnapshot snapshot)
        {
            var s = snapshot.Session;
            view.songTitle.text = snapshot.SongTitle;
            view.songDetails.text = difficulty ?? snapshot.DifficultyName;

            if (s.State == SessionState.Ready)
            {
                view.sessionState.text = "수련 준비";
            }
            else if (s.State == SessionState.GameOver || s.State == SessionState.Completed)
            {
                view.sessionState.text = "결과 화면으로 이동";
            }
            else if (s.Failure.Failed)
            {
                view.sessionState.text = "FAILED · 끝까지 진행";
            }
            else
            {
                view.sessionState.text = "수련 중";
            }

            if (s.State == SessionState.Completed || s.State == SessionState.GameOver)
            {
                showingJudgment = false;
            }

            if (!showingJudgment)
            {
                view.judgmentMotion.ResetPresentation();
                view.judgment.text = "";
                view.timing.text = "";
                if (s.State == SessionState.Ready && snapshot.ReadyReason == ReadyReason.FocusLost)
                {
                    view.timing.text = "창 전환으로 수련 중단";
                }
            }
        }

        public void Unbind()
        {
            PlayKeyBindings.Changed -= RefreshKeyLabels;
            if (model == null)
            {
                return;
            }

            model.Judged -= Judged;
            model.HoldStarted -= HoldStarted;
            model.ResetOccurred -= ResetDisplay;
            model = null;
        }

        private void OnDestroy() => Unbind();
    }
}
