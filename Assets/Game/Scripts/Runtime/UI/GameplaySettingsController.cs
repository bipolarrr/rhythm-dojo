using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class GameplaySettingsController : IDisposable
    {
        private static readonly JudgmentPosition[] JudgmentPositions =
        {
            JudgmentPosition.Upper,
            JudgmentPosition.Middle,
            JudgmentPosition.Lower,
        };

        private readonly SettingsView view;
        private readonly GameSettings settings;
        private readonly ISongSelectionService selection;
        private readonly ISelectionPreferences preferences;
        private readonly float speedSliderMinimum;
        private readonly TimingCalibrationDialog calibration;
        private readonly PresentationPreferences presentation;
        private GameplayRulesSettings gameplayRules;

        private ScrollMode speedScrollMode = ScrollMode.Constant;
        private Action<double> gameplaySpeedChanged;
        private Action<int> calibrationApplied;

        public GameplaySettingsController(
            SettingsView view,
            GameSettings settings,
            ISongSelectionService selection,
            ISelectionPreferences preferences,
            HardModePreferences hardMode,
            IPlayRulesService rules
        )
            : this(view, settings, selection, preferences, hardMode, rules, new PresentationPreferences())
        {
        }

        public GameplaySettingsController(
            SettingsView view,
            GameSettings settings,
            ISongSelectionService selection,
            ISelectionPreferences preferences,
            HardModePreferences hardMode,
            IPlayRulesService rules,
            PresentationPreferences presentation,
            TitleMusicPlayer titleMusic = null
        )
        {
            this.view = view;
            this.settings = settings;
            this.selection = selection;
            this.preferences = preferences;
            this.presentation = presentation ?? new PresentationPreferences();
            speedScrollMode = selection?.SelectedScrollMode ?? preferences.ScrollMode;
            speedSliderMinimum = view.speedSlider.minValue;
            calibration = new TimingCalibrationDialog(
                view,
                this.presentation,
                preferences,
                ApplyCalibration,
                CreateSpeedTestTimeline,
                () => rules?.Snapshot.Timing ?? new PlayRules().Timing,
                titleMusic
            );

            if (rules != null && view.playRuleLists != null && view.playRuleLists.Length == 5)
            {
                gameplayRules = new GameplayRulesSettings(view.playRuleLists, rules);
            }

            view.speedSlider.SetValueWithoutNotify(
                (float)(selection?.SelectedMultiplier ?? preferences.Multiplier)
            );
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            view.speedSlider.onValueChanged.AddListener(SpeedChanged);
            view.timingOffset.onValueChanged.AddListener(TimingChanged);
            if (view.judgmentPositionList)
            {
                int index = Array.IndexOf(JudgmentPositions, this.presentation.judgmentPosition);
                if (index < 0)
                {
                    index = Array.IndexOf(JudgmentPositions, JudgmentPosition.Middle);
                }

                Configure(view.judgmentPositionList, new[] { "상단", "중앙", "하단" }, index);
                view.judgmentPositionList.onValueChanged.AddListener(JudgmentPositionChanged);
            }

            view.focusHudButton.onClick.AddListener(ToggleFocusHud);
            view.railOpacity.SetValueWithoutNotify(this.presentation.railOpacity * 100);
            view.railOpacity.onValueChanged.AddListener(RailOpacityChanged);
            RefreshGameplay();
        }

        public Selectable HardModeLastControl() => view.playRuleLists[4];

        public bool TickCalibration() => calibration.Tick();

        internal void OpenTimingCalibration(Action<int> onApplied)
        {
            if (!view.CanEdit)
            {
                return;
            }

            calibrationApplied = onApplied;
            calibration.Open(false);
        }

        private void ApplyCalibration(int value)
        {
            if (!view.CanEdit)
            {
                return;
            }

            TimingChanged(value);
            calibrationApplied?.Invoke(value);
        }

        private IScrollTimeline CreateSpeedTestTimeline()
        {
            if (!settings || !settings.scroll)
            {
                return new ConstantScrollTimeline(8, view.speedSlider.value);
            }

            var tempo = new TempoMap(new[] { new TempoPoint(0, TimingCalibrationDialog.TestBpm) });
            return settings.scroll.CreateTimeline(speedScrollMode, tempo, view.speedSlider.value);
        }

        public void BindGameplaySpeed(ScrollMode mode, double multiplier, Action<double> onSpeedChanged)
        {
            speedScrollMode = mode;
            gameplaySpeedChanged = onSpeedChanged;

            view.speedSlider.minValue = Mathf.Min(speedSliderMinimum, (float)multiplier);
            view.speedSlider.SetValueWithoutNotify((float)multiplier);
            RefreshGameplay();
        }

        private void SpeedChanged(float value)
        {
            if (!view.CanEdit)
            {
                return;
            }

            double rounded = Math.Round(
                Math.Clamp((double)value, 1, 10),
                1,
                MidpointRounding.AwayFromZero
            );

            if (settings != null)
            {
                bool known = false;

                for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                {
                    known |= Math.Abs(settings.scroll.GetMultiplier(i) - rounded) < .0001;
                }

                if (!known)
                {
                    return;
                }
            }

            view.speedSlider.SetValueWithoutNotify((float)rounded);
            preferences.Multiplier = rounded;
            preferences.ScrollMode = speedScrollMode;

            if (selection != null && selection.Library.Find(selection.SelectedSongId) != null)
            {
                selection.UpdateSelection(
                    selection.SelectedSongId,
                    selection.SelectedDifficulty,
                    speedScrollMode,
                    rounded
                );
            }

            preferences.Save();

            gameplaySpeedChanged?.Invoke(rounded);
            RefreshGameplay();
        }

        private void TimingChanged(float value)
        {
            if (!view.CanEdit)
            {
                return;
            }

            preferences.TimingOffsetMs = Mathf.RoundToInt(value);
            preferences.Save();
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            RefreshGameplay();
        }

        private void JudgmentPositionChanged(int value)
        {
            if (!view.CanEdit)
            {
                return;
            }

            if (value < 0 || value >= JudgmentPositions.Length)
            {
                return;
            }

            presentation.judgmentPosition = JudgmentPositions[value];
            presentation.SaveValues();
        }

        private void RefreshGameplay()
        {
            string caption = "HUD · 기본";
            if (presentation.focusHud)
            {
                caption = "HUD · 집중";
            }
            view.focusHudButton.GetComponentInChildren<Text>().text = caption;
            view.speedLabel.text = $"속도  {view.speedSlider.value:0.0#}x";
            view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";
        }

        private void ToggleFocusHud()
        {
            if (!view.CanEdit)
            {
                return;
            }
            presentation.focusHud = !presentation.focusHud;
            presentation.SaveValues();
            RefreshGameplay();
        }

        private void RailOpacityChanged(float value)
        {
            if (!view.CanEdit)
            {
                return;
            }
            presentation.railOpacity = value / 100;
            presentation.SaveValues();
        }

        private static void Configure(Dropdown dropdown, string[] options, int value)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(value);
        }

        public void Dispose()
        {
            view.focusHudButton.onClick.RemoveListener(ToggleFocusHud);
            view.railOpacity.onValueChanged.RemoveListener(RailOpacityChanged);
            calibration.Dispose();
            gameplayRules?.Dispose();

            view.speedSlider.onValueChanged.RemoveListener(SpeedChanged);
            view.speedSlider.minValue = speedSliderMinimum;
            view.timingOffset.onValueChanged.RemoveListener(TimingChanged);
            if (view.judgmentPositionList)
            {
                view.judgmentPositionList.onValueChanged.RemoveListener(JudgmentPositionChanged);
            }
        }
    }
}
