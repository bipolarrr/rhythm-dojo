using System;
using RhythmDojo.Application;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    public sealed class RhythmGameController : MonoBehaviour
    {
        private RhythmSessionCoordinator coordinator;

        public RhythmSession Session => coordinator?.Session;

        public IGameplayReadModel ReadModel => coordinator;

        public ChartData Chart { get; private set; }
        public ISongClock Clock { get; private set; }
        public ILaneInput Input { get; private set; }

        public void Initialize(
            ChartData chart,
            JudgmentSettings settings,
            GameModeRules mode,
            TempoMap tempo,
            string title,
            string difficulty,
            ScrollMode scrollMode,
            double multiplier,
            IScrollTimeline scroll,
            ILaneInput input,
            ISongClock clock,
            INotePresenter presenter
        ) =>
            Initialize(
                chart,
                settings,
                mode,
                tempo,
                title,
                difficulty,
                scrollMode,
                multiplier,
                scroll,
                input,
                clock,
                presenter,
                null
            );

        public void Initialize(
            ChartData chart,
            JudgmentSettings settings,
            GameModeRules mode,
            TempoMap tempo,
            string title,
            string difficulty,
            ScrollMode scrollMode,
            double multiplier,
            IScrollTimeline scroll,
            ILaneInput input,
            ISongClock clock,
            INotePresenter presenter,
            HardModeHealthRules healthRules,
            PlayRules rules = null
        )
        {
            Shutdown();
            Chart = chart;
            Clock = clock;
            Input = input;
            coordinator = new RhythmSessionCoordinator(
                chart,
                settings,
                mode,
                tempo,
                title,
                difficulty,
                scrollMode,
                multiplier,
                scroll,
                input,
                clock,
                presenter,
                healthRules,
                rules
            );
        }

        public void StartSession() => coordinator?.Start();

        private void Update() => coordinator?.Tick();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                coordinator?.LoseFocus();
            }
        }

        public void Shutdown()
        {
            coordinator?.Dispose();
            coordinator = null;
        }

        private void OnDisable() => Shutdown();

        private void OnDestroy() => Shutdown();
    }
}
