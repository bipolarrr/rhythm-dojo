using System;
using System.Collections.Generic;
using RhythmDojo.Application;
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
        private readonly HardModePreferences hardMode;
        private readonly float speedSliderMinimum;
        private readonly TimingCalibrationDialog calibration;
        private readonly PresentationPreferences presentation;
        private GameplayRulesSettings gameplayRules;
        private UnityEngine.Events.UnityAction<int>[] hardRuleActions;
        private UnityEngine.Events.UnityAction<string>[] hardCountActions;

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
            PresentationPreferences presentation
        )
        {
            this.view = view;
            this.settings = settings;
            this.selection = selection;
            this.preferences = preferences;
            this.hardMode = hardMode;
            this.presentation = presentation ?? new PresentationPreferences();
            speedScrollMode = selection?.SelectedScrollMode ?? preferences.ScrollMode;
            speedSliderMinimum = view.speedSlider.minValue;
            calibration = new TimingCalibrationDialog(
                view,
                this.presentation,
                preferences,
                ApplyCalibration,
                CreateSpeedTestTimeline,
                () => rules?.Snapshot.Timing ?? new PlayRules().Timing
            );

            if (rules != null && view.playRuleLists != null && view.playRuleLists.Length == 5)
            {
                gameplayRules = new GameplayRulesSettings(view.playRuleLists, rules);
            }

            hardRuleActions = new UnityEngine.Events.UnityAction<int>[3];
            hardCountActions = new UnityEngine.Events.UnityAction<string>[3];

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var rule = hardMode.GetRule((Judgment)i);
                Configure(
                    view.hardRuleLists[i],
                    new[] { "체력 유지", "체력 감소", "즉시 게임 오버" },
                    (int)rule.Action
                );
                hardRuleActions[i] = value => HardRuleChanged(index);
                hardCountActions[i] = value => HardCountChanged(index, value);
                view.hardRuleLists[i].onValueChanged.AddListener(hardRuleActions[i]);
                view.hardLossCounts[i].onEndEdit.AddListener(hardCountActions[i]);
            }

            RefreshHardModeRows();
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

            RefreshGameplay();
        }

        public Selectable HardModeLastControl()
        {
            if (gameplayRules != null)
            {
                return view.playRuleLists[4];
            }
            else if (view.hardLossCounts[2].interactable)
            {
                return (Selectable)view.hardLossCounts[2];
            }
            else
            {
                return view.hardRuleLists[2];
            }
        }

        private void HardRuleChanged(int index)
        {
            var rule = hardMode.GetRule((Judgment)index);
            hardMode.SetRule(
                (Judgment)index,
                (HealthRuleAction)view.hardRuleLists[index].value,
                rule.LossCount
            );
            hardMode.Save();
            RefreshHardModeRows();
        }

        private void HardCountChanged(int index, string value)
        {
            var rule = hardMode.GetRule((Judgment)index);

            if (!int.TryParse(value, out int count))
            {
                count = rule.LossCount;
            }

            hardMode.SetRule((Judgment)index, rule.Action, count);
            hardMode.Save();
            RefreshHardModeRows();
        }

        private void RefreshHardModeRows()
        {
            for (int i = 0; i < 3; i++)
            {
                var rule = hardMode.GetRule((Judgment)i);
                view.hardRuleLists[i].SetValueWithoutNotify((int)rule.Action);
                view.hardLossCounts[i].SetTextWithoutNotify(rule.LossCount.ToString());

                bool damage = rule.Action == HealthRuleAction.Damage;
                view.hardLossCounts[i].interactable = damage;

                Selectable next;

                if (i < 2)
                {
                    next = view.hardRuleLists[i + 1];
                }
                else
                {
                    next = view.apply;
                }

                var nav = view.hardRuleLists[i].navigation;
                nav.mode = Navigation.Mode.Explicit;

                if (damage)
                {
                    nav.selectOnRight = view.hardLossCounts[i];
                }
                else
                {
                    nav.selectOnRight = null;
                }

                nav.selectOnDown = next;

                if (i > 0)
                {
                    nav.selectOnUp = view.hardRuleLists[i - 1];
                }
                else
                {
                    nav.selectOnUp = view.timingOffset;
                }

                view.hardRuleLists[i].navigation = nav;
                nav = view.hardLossCounts[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = view.hardRuleLists[i];
                nav.selectOnDown = next;
                nav.selectOnUp = view.hardRuleLists[i];
                view.hardLossCounts[i].navigation = nav;
            }

            var timingNavigation = view.timingOffset.navigation;

            if (gameplayRules != null)
            {
                timingNavigation.selectOnDown = view.playRuleLists[0];
            }
            else
            {
                timingNavigation.selectOnDown = view.hardRuleLists[0];
            }

            var nextControl = timingNavigation.selectOnDown;
            var nextNavigation = nextControl.navigation;
            nextNavigation.selectOnUp = calibration.Entry;
            nextControl.navigation = nextNavigation;

            calibration.Entry.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = view.timingOffset,
                selectOnDown = nextControl,
                selectOnRight = calibration.SpeedEntry,
            };
            calibration.SpeedEntry.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = view.timingOffset,
                selectOnDown = nextControl,
                selectOnLeft = calibration.Entry,
                selectOnRight = view.judgmentPositionList,
            };

            if (view.judgmentPositionList)
            {
                view.judgmentPositionList.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = view.gameplayTab,
                    selectOnLeft = calibration.SpeedEntry,
                    selectOnDown = view.timingOffset,
                };
            }

            timingNavigation.selectOnDown = calibration.Entry;
            view.timingOffset.navigation = timingNavigation;

            if (view.gameplayPanel.activeSelf)
            {
                var nav = view.apply.navigation;
                nav.selectOnUp = HardModeLastControl();
                view.apply.navigation = nav;
            }
        }

        public bool TickCalibration() => calibration.Tick();

        internal void OpenTimingCalibration(Action<int> onApplied)
        {
            calibrationApplied = onApplied;
            calibration.Open(false);
        }

        private void ApplyCalibration(int value)
        {
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
            preferences.TimingOffsetMs = Mathf.RoundToInt(value);
            preferences.Save();
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            RefreshGameplay();
        }

        private void JudgmentPositionChanged(int value)
        {
            if (value < 0 || value >= JudgmentPositions.Length)
            {
                return;
            }

            presentation.judgmentPosition = JudgmentPositions[value];
            presentation.SaveValues();
        }

        private void RefreshGameplay()
        {
            view.speedLabel.text = $"속도  {view.speedSlider.value:0.0#}x";
            view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";
        }

        private static void Configure(Dropdown dropdown, string[] options, int value)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.SetValueWithoutNotify(value);
        }

        public void Dispose()
        {
            calibration.Dispose();
            gameplayRules?.Dispose();

            for (int i = 0; i < hardRuleActions.Length; i++)
            {
                view.hardRuleLists[i].onValueChanged.RemoveListener(hardRuleActions[i]);
            }

            for (int i = 0; i < hardCountActions.Length; i++)
            {
                view.hardLossCounts[i].onEndEdit.RemoveListener(hardCountActions[i]);
            }

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
