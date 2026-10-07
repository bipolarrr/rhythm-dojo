using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.UI
{
    public sealed class RhythmHud : MonoBehaviour
    {
        [SerializeField] private GameplayHudView view;
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
        private static readonly Color Cream = new Color(.94f, .93f, .85f);
        private static readonly Color Cyan = new Color(.46f, .84f, .81f);
        private static readonly Color Gold = new Color(1f, .77f, .42f);
        private static readonly Color Red = new Color(.96f, .29f, .31f);
        public void Validate()
        { if (!view) throw new InvalidOperationException("HUD view missing."); view.Validate(); }
        public void Initialize(IGameplayReadModel readModel, GameModeDefinition mode, GameplayPresentationSettings presentation)
        {
            Unbind(); Validate(); settings = presentation; model = readModel;
            indicators = view.lanes.Build(mode, presentation);
            keyLabels = new Text[indicators.Length];
            for (int i = 0; i < indicators.Length; i++) keyLabels[i] = indicators[i].GetComponentInChildren<Text>();
            flashUntil = new double[indicators.Length]; flashColor = new Color[indicators.Length];
            model.Judged += Judged; model.HoldStarted += HoldStarted; model.ResetOccurred += ResetDisplay;
            ResetDisplay();
        }
        private void Judged(JudgmentEvent result)
        { recent = $"{result.Grade} {result.ErrorSeconds * 1000:+0;-0;0} ms"; Flash(result); ShowJudgment(result, false); }
        private void HoldStarted(HoldStartedEvent result)
        { recent = $"HOLD {result.Head.Grade} {result.Head.ErrorSeconds * 1000:+0;-0;0} ms"; Flash(result.Head); ShowJudgment(result.Head, true); }
        private void ShowJudgment(JudgmentEvent result, bool hold)
        {
            judgmentAt = Time.unscaledTimeAsDouble; showingJudgment = true; nextTextRefresh = 0;
            maximumCombo = Math.Max(maximumCombo, model.Snapshot.Session.Combo);
            view.judgment.text = (hold ? "HOLD · " : "") + result.Grade.ToString().ToUpperInvariant();
            view.judgment.fontSize = hold ? 23 : 31;
            view.judgment.color = result.Grade == Judgment.Miss ? Red : result.Grade == Judgment.Perfect ? Cyan : Gold;
            double milliseconds = result.ErrorSeconds * 1000;
            view.timing.text = result.Grade == Judgment.Miss ? "COMBO BREAK" :
                Math.Abs(milliseconds) < .5 ? "ON TIME" : $"{(milliseconds < 0 ? "EARLY" : "LATE")}  {milliseconds:+0;-0;0} ms";
        }
        private void Flash(JudgmentEvent result)
        {
            flashUntil[result.Lane] = Time.unscaledTimeAsDouble + settings.flashDuration;
            flashColor[result.Lane] = result.Grade == Judgment.Miss ? settings.missColor :
                result.Grade == Judgment.Perfect ? settings.perfectColor : settings.goodColor;
        }
        private void ResetDisplay()
        {
            Array.Clear(flashUntil, 0, flashUntil.Length); recent = "Get ready"; nextTextRefresh = 0;
            showingJudgment = false; judgmentAt = comboAt = -10; displayedCombo = -1; maximumCombo = 0;
            view.combo.color = Cream; view.combo.rectTransform.localScale = Vector3.one;
            view.judgmentGroup.alpha = 1; view.judgmentGroup.transform.localScale = Vector3.one;
            view.judgment.fontSize = 31; view.judgment.text = "READY"; view.judgment.color = Cyan;
            view.timing.text = "박자에 집중하세요";
        }
        private void LateUpdate()
        {
            if (model == null) return;
            double now = Time.unscaledTimeAsDouble;
            for (int i = 0; i < indicators.Length; i++)
            {
                indicators[i].color = now < flashUntil[i] ? flashColor[i] : model.IsHeld(i) ? settings.heldColor : settings.idleColor;
                var tint = indicators[i].color;
                keyLabels[i].color = tint.r * .2126f + tint.g * .7152f + tint.b * .0722f > .55f ? new Color(.025f,.035f,.045f) : Cream;
            }
            AnimateFeedback(now);
            if (now < nextTextRefresh) return;
            nextTextRefresh = now + settings.hudRefreshInterval;
            view.status.text = RhythmHudFormatter.Format(model.Snapshot, recent);
            RefreshTrainingDisplay(model.Snapshot, now);
        }
        private void AnimateFeedback(double now)
        {
            float comboPulse = Mathf.Clamp01(1 - (float)(now - comboAt) / .20f);
            view.combo.rectTransform.localScale = Vector3.one * (1 + .08f * comboPulse * comboPulse);
            if (!showingJudgment) return;
            float age = (float)(now - judgmentAt);
            view.judgmentGroup.alpha = 1 - Mathf.Clamp01((age - .65f) / .35f);
            view.judgmentGroup.transform.localScale = Vector3.one * (1 + .08f * Mathf.Clamp01(1 - age / .16f));
        }
        private void RefreshTrainingDisplay(GameplaySnapshot snapshot, double now)
        {
            var s = snapshot.Session;
            view.songTitle.text = snapshot.SongTitle;
            view.songDetails.text = $"{snapshot.DifficultyName}  /  {model.LaneCount} KEY\nBPM {snapshot.CurrentBpm:0.##}  ·  {(snapshot.ScrollMode == ScrollMode.Bpm ? "BPM" : "고정")} {snapshot.ScrollMultiplier:0.##}x";
            view.sessionState.text = s.State == SessionState.Ready ?
                snapshot.ReadyReason == ReadyReason.FocusLost ? "SPACE로 다시 시작" : "SPACE로 수련 시작" :
                s.State == SessionState.Completed ? "수련 완료 · 다시 도전!" : "수련 중";
            if (displayedCombo != s.Combo)
            {
                displayedCombo = s.Combo; comboAt = now;
                maximumCombo = Math.Max(maximumCombo, s.Combo);
                view.combo.text = s.Combo.ToString();
            }
            view.combo.color = showingJudgment && view.judgment.text == "MISS" && now - judgmentAt < .35 ? Red : Cream;
            view.bestCombo.text = $"BEST  {maximumCombo}";
            view.counts.text = $"PERFECT    {s.Perfect}\nGOOD         {s.Good}\nMISS            {s.Miss}";
            view.accuracy.text = s.Resolved == 0 ? "정확도  —" : $"정확도  {(s.Perfect + s.Good * .5) / s.Resolved * 100:0.0}%";
            view.progress.text = $"처리한 노트  {s.Resolved} / {s.TotalNotes}";
            view.progressFill.rectTransform.anchorMax = new Vector2(s.TotalNotes > 0 ? Mathf.Clamp01((float)s.Resolved / s.TotalNotes) : 0, 1);
            if (s.State == SessionState.Completed && now - judgmentAt > 1)
            {
                showingJudgment = false; view.judgmentGroup.alpha = 1;
                view.judgmentGroup.transform.localScale = Vector3.one;
                view.judgment.fontSize = 31; view.judgment.color = Cyan;
            }
            if (!showingJudgment)
            {
                view.judgment.text = s.State == SessionState.Ready ? "READY" : s.State == SessionState.Completed ? "FINISH" : "";
                view.timing.text = s.State == SessionState.Ready ? snapshot.ReadyReason == ReadyReason.FocusLost ?
                    "창 전환으로 수련 중단" : "박자에 집중하세요" : "";
            }
        }
        public void Unbind()
        {
            if (model == null) return;
            model.Judged -= Judged; model.HoldStarted -= HoldStarted; model.ResetOccurred -= ResetDisplay; model = null;
        }
        private void OnDestroy() => Unbind();
    }
}

