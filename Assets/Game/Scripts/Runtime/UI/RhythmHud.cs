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
        private double[] flashUntil;
        private Color[] flashColor;
        private double nextTextRefresh;
        private string recent = "Get ready";
        public void Validate()
        { if (!view) throw new InvalidOperationException("HUD view missing."); view.Validate(); }
        public void Initialize(IGameplayReadModel readModel, GameModeDefinition mode, GameplayPresentationSettings presentation)
        {
            Unbind(); Validate(); settings = presentation; model = readModel;
            indicators = view.lanes.Build(mode, presentation);
            flashUntil = new double[indicators.Length]; flashColor = new Color[indicators.Length];
            model.Judged += Judged; model.HoldStarted += HoldStarted; model.ResetOccurred += ResetDisplay;
            ResetDisplay();
        }
        private void Judged(JudgmentEvent result)
        { recent = $"{result.Grade} {result.ErrorSeconds * 1000:+0;-0;0} ms"; Flash(result); }
        private void HoldStarted(HoldStartedEvent result)
        { recent = $"HOLD {result.Head.Grade} {result.Head.ErrorSeconds * 1000:+0;-0;0} ms"; Flash(result.Head); }
        private void Flash(JudgmentEvent result)
        {
            flashUntil[result.Lane] = Time.unscaledTimeAsDouble + settings.flashDuration;
            flashColor[result.Lane] = result.Grade == Judgment.Miss ? settings.missColor :
                result.Grade == Judgment.Perfect ? settings.perfectColor : settings.goodColor;
        }
        private void ResetDisplay()
        {
            Array.Clear(flashUntil, 0, flashUntil.Length); recent = "Get ready"; nextTextRefresh = 0;
        }
        private void LateUpdate()
        {
            if (model == null) return;
            double now = Time.unscaledTimeAsDouble;
            for (int i = 0; i < indicators.Length; i++)
                indicators[i].color = now < flashUntil[i] ? flashColor[i] : model.IsHeld(i) ? settings.heldColor : settings.idleColor;
            if (now < nextTextRefresh) return;
            nextTextRefresh = now + settings.hudRefreshInterval;
            view.status.text = RhythmHudFormatter.Format(model.Snapshot, recent);
        }
        public void Unbind()
        {
            if (model == null) return;
            model.Judged -= Judged; model.HoldStarted -= HoldStarted; model.ResetOccurred -= ResetDisplay; model = null;
        }
        private void OnDestroy() => Unbind();
    }
}

