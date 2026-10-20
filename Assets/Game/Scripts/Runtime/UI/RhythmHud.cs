using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
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
        private double[] flashUntil;
        private Color[] flashColor;
        private double nextTextRefresh;
        private string recent = "Get ready";
        private double judgmentAt = -10;
        private double comboAt = -10;
        private int displayedCombo = -1;
        private int maximumCombo;
        private bool showingJudgment;
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
            GameplayPresentationSettings presentation
        )
        {
            Unbind();
            Validate();
            if (view.pixelTheme)
            {
                Cream = new Color(.98f, .86f, .65f);
                Cyan = new Color(.70f, .86f, .61f);
                Gold = new Color(1f, .74f, .36f);
                Red = new Color(1f, .40f, .32f);
            }

            settings = presentation;
            model = readModel;
            indicators = view.lanes.Build(mode, presentation);
            keyLabels = new Text[indicators.Length];

            for (int i = 0; i < indicators.Length; i++)
            {
                keyLabels[i] = indicators[i].GetComponentInChildren<Text>();
            }

            flashUntil = new double[indicators.Length];
            flashColor = new Color[indicators.Length];
            model.Judged += Judged;
            model.HoldStarted += HoldStarted;
            model.ResetOccurred += ResetDisplay;
            ResetDisplay();
        }

        private void Judged(JudgmentEvent result)
        {
            recent = $"{result.Grade} {result.ErrorSeconds * 1000:+0;-0;0} ms";
            Flash(result);
            ShowJudgment(result, false);
        }

        private void HoldStarted(HoldStartedEvent result)
        {
            recent = $"HOLD {result.Head.Grade} {result.Head.ErrorSeconds * 1000:+0;-0;0} ms";
            Flash(result.Head);
            ShowJudgment(result.Head, true);
        }

        private void ShowJudgment(JudgmentEvent result, bool hold)
        {
            judgmentAt = Time.unscaledTimeAsDouble;
            showingJudgment = true;
            nextTextRefresh = 0;
            maximumCombo = Math.Max(maximumCombo, model.Snapshot.Session.Combo);

            if (hold)
            {
                view.judgment.text = "HOLD · " + JudgmentDisplay.Label(result.Grade);
            }
            else
            {
                view.judgment.text = "" + JudgmentDisplay.Label(result.Grade);
            }

            if (hold)
            {
                view.judgment.fontSize = 23;
            }
            else
            {
                view.judgment.fontSize = 31;
            }

            if (result.Grade == Judgment.Miss)
            {
                view.judgment.color = Red;
            }
            else if (result.Grade == Judgment.Perfect)
            {
                view.judgment.color = Cyan;
            }
            else
            {
                view.judgment.color = Gold;
            }

            double milliseconds = result.ErrorSeconds * 1000;

            if (result.Grade == Judgment.Miss)
            {
                view.timing.text = "COMBO BREAK";
            }
            else if (Math.Abs(milliseconds) < .5)
            {
                view.timing.text = "ON TIME";
            }
            else
            {
                if (milliseconds < 0)
                {
                    view.timing.text = $"EARLY  {milliseconds:+0;-0;0} ms";
                }
                else
                {
                    view.timing.text = $"LATE  {milliseconds:+0;-0;0} ms";
                }
            }
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
            Array.Clear(flashUntil, 0, flashUntil.Length);
            recent = "Get ready";
            nextTextRefresh = 0;
            showingJudgment = false;
            judgmentAt = comboAt = -10;
            displayedCombo = -1;
            maximumCombo = 0;
            view.combo.color = Cream;
            view.combo.rectTransform.localScale = Vector3.one;
            view.judgmentGroup.alpha = 1;
            view.judgmentGroup.transform.localScale = Vector3.one;
            view.judgment.fontSize = 31;
            view.judgment.text = "READY";
            view.judgment.color = Cyan;
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

            RefreshHealth(model.Snapshot.Session);
            AnimateFeedback(now);

            if (now < nextTextRefresh)
            {
                return;
            }

            nextTextRefresh = now + settings.hudRefreshInterval;
            view.status.text = RhythmHudFormatter.Format(model.Snapshot, recent);
            RefreshTrainingDisplay(model.Snapshot, now);
        }

        private void RefreshHealth(SessionSnapshot snapshot)
        {
            float fraction = Mathf.Clamp01((float)snapshot.HealthRatio);
            view.healthFill.rectTransform.anchorMax = new Vector2(fraction, 1);

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
                    $"Challenge · Hmm... {snapshot.Good} / Nope. {snapshot.Miss}";
            }

            bool completed = snapshot.State == SessionState.Completed;
            view.gameOverPanel.SetActive(completed || snapshot.State == SessionState.GameOver);

            if (completed && !snapshot.Failure.Failed)
            {
                view.gameOverText.text = "GAME CLEAR";
            }
            else
            {
                view.gameOverText.text = "GAME OVER";
            }

            if (completed && !snapshot.Failure.Failed)
            {
                view.gameOverText.color = Cyan;
            }
            else
            {
                view.gameOverText.color = Red;
            }

            if (snapshot.Failure.Failed && snapshot.State == SessionState.Playing)
            {
                view.healthLabel.text = "FAILED · 끝까지 진행";
            }
        }

        private void AnimateFeedback(double now)
        {
            float comboPulse = Mathf.Clamp01(1 - (float)(now - comboAt) / .20f);
            view.combo.rectTransform.localScale =
                Vector3.one * (1 + .08f * comboPulse * comboPulse);

            if (!showingJudgment)
            {
                return;
            }

            float age = (float)(now - judgmentAt);
            view.judgmentGroup.alpha = 1 - Mathf.Clamp01((age - .65f) / .35f);
            view.judgmentGroup.transform.localScale =
                Vector3.one * (1 + .08f * Mathf.Clamp01(1 - age / .16f));
        }

        private void RefreshTrainingDisplay(GameplaySnapshot snapshot, double now)
        {
            var s = snapshot.Session;
            view.songTitle.text = snapshot.SongTitle;
            view.songDetails.text =
                $"{snapshot.DifficultyName}  /  {model.LaneCount} KEY\nBPM {snapshot.CurrentBpm:0.##}  ·  {(snapshot.ScrollMode switch { ScrollMode.Bpm => "BPM", _ => "고정" })} {snapshot.ScrollMultiplier:0.##}x";

            if (s.State == SessionState.Ready)
            {
                if (snapshot.ReadyReason == ReadyReason.FocusLost)
                {
                    view.sessionState.text = "SPACE로 다시 시작";
                }
                else
                {
                    view.sessionState.text = "SPACE로 수련 시작";
                }
            }
            else if (s.State == SessionState.GameOver)
            {
                view.sessionState.text = "GAME OVER · SPACE로 다시 도전";
            }
            else if (s.State == SessionState.Completed)
            {
                view.sessionState.text = "수련 완료 · 다시 도전!";
            }
            else if (s.Failure.Failed)
            {
                view.sessionState.text = "FAILED · 끝까지 진행";
            }
            else
            {
                view.sessionState.text = "수련 중";
            }

            if (displayedCombo != s.Combo)
            {
                displayedCombo = s.Combo;
                comboAt = now;
                maximumCombo = Math.Max(maximumCombo, s.Combo);
                view.combo.text = s.Combo.ToString();
            }

            if (
                showingJudgment
                && view.judgment.text == JudgmentDisplay.Label(Judgment.Miss)
                && now - judgmentAt < .35
            )
            {
                view.combo.color = Red;
            }
            else
            {
                view.combo.color = Cream;
            }

            view.bestCombo.text = $"BEST  {maximumCombo}";
            view.counts.text = $"Perfect    {s.Perfect}\nHmm...    {s.Good}\nNope.       {s.Miss}";

            if (s.TotalNotes == 0)
            {
                view.accuracy.text = "정확도  —";
            }
            else
            {
                view.accuracy.text = $"정확도  {s.AccuracyRate:0.0}%";
            }

            view.progress.text = $"처리한 노트  {s.Resolved} / {s.TotalNotes}";

            if (s.TotalNotes > 0)
            {
                view.progressFill.rectTransform.anchorMax = new Vector2(
                    Mathf.Clamp01((float)s.Resolved / s.TotalNotes),
                    1
                );
            }
            else
            {
                view.progressFill.rectTransform.anchorMax = new Vector2(0, 1);
            }

            if (s.State == SessionState.Completed && now - judgmentAt > 1)
            {
                showingJudgment = false;
                view.judgmentGroup.alpha = 1;
                view.judgmentGroup.transform.localScale = Vector3.one;
                view.judgment.fontSize = 31;
                view.judgment.color = Cyan;
            }

            if (!showingJudgment)
            {
                if (s.State == SessionState.Ready)
                {
                    view.judgment.text = "READY";
                }
                else if (s.State == SessionState.Completed)
                {
                    view.judgment.text = "FINISH";
                }
                else
                {
                    view.judgment.text = "";
                }

                if (s.State == SessionState.Ready)
                {
                    if (snapshot.ReadyReason == ReadyReason.FocusLost)
                    {
                        view.timing.text = "창 전환으로 수련 중단";
                    }
                    else
                    {
                        view.timing.text = "";
                    }
                }
                else
                {
                    view.timing.text = "";
                }
            }
        }

        public void Unbind()
        {
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
