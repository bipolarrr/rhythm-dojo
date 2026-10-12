using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RhythmDojo.Services;
using RhythmDojo.Gameplay;
using RhythmDojo.Application;

namespace RhythmDojo.UI
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private SettingsView view;
        private IAudioSettingsService audio;
        private IAppNavigation navigation;
        private PresentationPreferences presentation;
        private GameSettings settings;
        private ISongSelectionService selection;
        private ISelectionPreferences preferences;
        private HardModePreferences hardMode;
        private UnityEngine.Events.UnityAction<int>[] hardRuleActions;
        private UnityEngine.Events.UnityAction<string>[] hardCountActions;
        private float frameSampleSeconds, measuredFrameRate;
        private int frameSampleCount;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Settings view missing.");
            view.Validate();
        }
        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation)
        {
            var flow = appNavigation as RhythmDojo.Core.AppFlowController;
            Initialize(audioSettings, appNavigation, flow ? flow.Settings : null, flow?.Selection, flow?.Preferences);
        }
        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation,
            GameSettings gameSettings, ISongSelectionService songs, ISelectionPreferences savedPreferences)
        {
            Unbind(); Validate(); audio = audioSettings; navigation = appNavigation;
            settings = gameSettings; selection = songs; preferences = savedPreferences ?? new SelectionPreferences();
            hardMode = (appNavigation as RhythmDojo.Core.AppFlowController)?.HardMode ?? HardModePreferences.Load();
            hardRuleActions = new UnityEngine.Events.UnityAction<int>[3];
            hardCountActions = new UnityEngine.Events.UnityAction<string>[3];
            for (int i=0; i<3; i++)
            {
                int index = i;
                var rule = hardMode.GetRule((Judgment)i);
                Configure(view.hardRuleLists[i],new[] { "체력 유지", "체력 감소", "즉시 게임 오버" },(int)rule.Action);
                hardRuleActions[i] = value => HardRuleChanged(index);
                hardCountActions[i] = value => HardCountChanged(index,value);
                view.hardRuleLists[i].onValueChanged.AddListener(hardRuleActions[i]);
                view.hardLossCounts[i].onEndEdit.AddListener(hardCountActions[i]);
            }
            RefreshHardModeRows();
            view.speedSlider.SetValueWithoutNotify((float)(selection?.SelectedMultiplier ?? preferences.Multiplier));
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            view.speedSlider.onValueChanged.AddListener(SpeedChanged);
            view.timingOffset.onValueChanged.AddListener(TimingChanged);
            view.gameplayTab.onClick.AddListener(ShowGameplay);
            RefreshGameplay();
            var options = new List<string> { $"Default ({audio.DefaultBufferSize} samples)" };
            for (int i = 1; i < audio.OptionCount; i++) options.Add($"{audio.GetBufferSize(i)} samples");
            view.bufferList.ClearOptions(); view.bufferList.AddOptions(options);
            view.bufferList.SetValueWithoutNotify(audio.SelectedBufferOption);
            view.bufferList.onValueChanged.AddListener(Changed);
            view.apply.onClick.AddListener(Apply); view.back.onClick.AddListener(Back);
            presentation = PresentationPreferences.Current ?? PresentationPreferences.Load();
            view.masterVolume.SetValueWithoutNotify(presentation.masterVolume * 100);
            view.backgroundVolume.SetValueWithoutNotify(presentation.backgroundVolume * 100);
            view.gameplayVolume.SetValueWithoutNotify(presentation.gameplayVolume * 100);
            Configure(view.aspectList, new[] { "16:9", "16:10" }, presentation.aspect);
            Configure(view.resolutionList, new[] { "FHD (1920 × 1080)", "QHD (2560 × 1440)" }, presentation.resolution);
            Configure(view.fpsList, new[] { "30", "60", "120", "144", "240", "제한 없음" }, presentation.frameRate);
            view.masterVolume.onValueChanged.AddListener(MasterChanged);
            view.backgroundVolume.onValueChanged.AddListener(BackgroundChanged);
            view.gameplayVolume.onValueChanged.AddListener(GameplayChanged);
            view.aspectList.onValueChanged.AddListener(DisplayChanged);
            view.resolutionList.onValueChanged.AddListener(DisplayChanged);
            view.fpsList.onValueChanged.AddListener(DisplayChanged);
            view.soundTab.onClick.AddListener(ShowSound); view.graphicsTab.onClick.AddListener(ShowGraphics);
            frameSampleSeconds = measuredFrameRate = 0; frameSampleCount = 0;
            ShowSound(); RefreshVolumes(); RefreshDisplay();
            audio.Changed += Refresh; Refresh();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.soundTab.gameObject);
        }
        private void Changed(int option) { audio.SelectBufferOption(option); Refresh(); }
        private void Apply()
        {
            if (view.graphicsPanel.activeSelf)
            {
                presentation.aspect = view.aspectList.value; presentation.resolution = view.resolutionList.value;
                presentation.frameRate = view.fpsList.value; presentation.ApplyGraphics();
                audio.ApplyBufferSize();
            }
            Refresh(); RefreshDisplay();
        }
        private static void Configure(Dropdown dropdown, string[] options, int value)
        { dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(options)); dropdown.SetValueWithoutNotify(value); }
        private void ShowSound() => SelectTab(false);
        private void ShowGraphics() => SelectTab(true);
        private void ShowGameplay() => SelectTab(false, true);
        private void SelectTab(bool graphics, bool gameplay = false)
        {
            view.soundPanel.SetActive(!graphics && !gameplay); view.graphicsPanel.SetActive(graphics);
            view.gameplayPanel.SetActive(gameplay);
            view.soundTab.image.color = !graphics && !gameplay ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            view.graphicsTab.image.color = graphics ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            view.gameplayTab.image.color = gameplay ? new Color(.15f,.46f,.5f) : new Color(.13f,.17f,.2f);
            var nav = view.apply.navigation; nav.selectOnUp = gameplay ? HardModeLastControl() : graphics ? view.bufferList : view.gameplayVolume; view.apply.navigation = nav;
        }
        private Selectable HardModeLastControl() => view.hardLossCounts[2].interactable ?
            (Selectable)view.hardLossCounts[2] : view.hardRuleLists[2];
        private void HardRuleChanged(int index)
        {
            var rule = hardMode.GetRule((Judgment)index);
            hardMode.SetRule((Judgment)index,(HealthRuleAction)view.hardRuleLists[index].value,rule.LossCount);
            hardMode.Save(); RefreshHardModeRows();
        }
        private void HardCountChanged(int index, string value)
        {
            var rule = hardMode.GetRule((Judgment)index);
            if (!int.TryParse(value,out int count)) count=rule.LossCount;
            hardMode.SetRule((Judgment)index,rule.Action,count);
            hardMode.Save(); RefreshHardModeRows();
        }
        private void RefreshHardModeRows()
        {
            for (int i=0; i<3; i++)
            {
                var rule = hardMode.GetRule((Judgment)i);
                view.hardRuleLists[i].SetValueWithoutNotify((int)rule.Action);
                view.hardLossCounts[i].SetTextWithoutNotify(rule.LossCount.ToString());
                bool damage = rule.Action == HealthRuleAction.Damage;
                view.hardLossCounts[i].interactable = damage;
                Selectable next = i<2 ? view.hardRuleLists[i+1] : view.apply;
                var nav = view.hardRuleLists[i].navigation; nav.mode=Navigation.Mode.Explicit;
                nav.selectOnRight=damage ? view.hardLossCounts[i] : null; nav.selectOnDown=next;
                nav.selectOnUp=i>0 ? view.hardRuleLists[i-1] : view.timingOffset; view.hardRuleLists[i].navigation=nav;
                nav=view.hardLossCounts[i].navigation; nav.mode=Navigation.Mode.Explicit;
                nav.selectOnLeft=view.hardRuleLists[i]; nav.selectOnDown=next; nav.selectOnUp=view.hardRuleLists[i];
                view.hardLossCounts[i].navigation=nav;
            }
            var timingNavigation=view.timingOffset.navigation;
            timingNavigation.selectOnDown=view.hardRuleLists[0]; view.timingOffset.navigation=timingNavigation;
            if (view.gameplayPanel.activeSelf)
            { var nav=view.apply.navigation; nav.selectOnUp=HardModeLastControl(); view.apply.navigation=nav; }
        }
        private void SpeedChanged(float value)
        {
            double rounded = Math.Round(Math.Clamp((double)value, 1, 10), 1, MidpointRounding.AwayFromZero);
            if (settings != null)
            {
                bool known = false;
                for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                    known |= Math.Abs(settings.scroll.GetMultiplier(i) - rounded) < .0001;
                if (!known) return;
            }
            view.speedSlider.SetValueWithoutNotify((float)rounded);
            preferences.Multiplier = rounded; preferences.ScrollMode = ScrollMode.Constant;
            if (selection != null && selection.Library.Find(selection.SelectedSongId) != null)
                selection.UpdateSelection(selection.SelectedSongId, selection.SelectedDifficulty, ScrollMode.Constant, rounded);
            preferences.Save(); RefreshGameplay();
        }
        private void TimingChanged(float value)
        {
            preferences.TimingOffsetMs = Mathf.RoundToInt(value); preferences.Save();
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs); RefreshGameplay();
        }
        private void RefreshGameplay()
        {
            view.speedLabel.text = $"속도  {view.speedSlider.value:0.0}x";
            view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";
        }
        private void MasterChanged(float value) { presentation.masterVolume=value/100f; presentation.SaveAudio(); RefreshVolumes(); }
        private void BackgroundChanged(float value) { presentation.backgroundVolume=value/100f; presentation.SaveAudio(); RefreshVolumes(); }
        private void GameplayChanged(float value) { presentation.gameplayVolume=value/100f; presentation.SaveAudio(); RefreshVolumes(); }
        private void RefreshVolumes()
        {
            view.masterLabel.text=$"전체 소리 {presentation.masterVolume*100:0}%";
            view.backgroundLabel.text=$"배경음악 {presentation.backgroundVolume*100:0}%";
            view.gameplayLabel.text=$"플레이 음악 {presentation.gameplayVolume*100:0}%";
        }
        private void DisplayChanged(int option) => RefreshDisplay();
        private void RefreshDisplay()
        {
            int height = view.resolutionList.value == 0 ? 1080 : 1440;
            int width = Mathf.RoundToInt(height*(view.aspectList.value==0 ? 16f/9f : 16f/10f));
            int targetFrameRate = UnityEngine.Application.targetFrameRate;
            string frameLimit = targetFrameRate > 0 ? $"{targetFrameRate} FPS" : "제한 없음";
            string measured = measuredFrameRate > 0 ? $"{measuredFrameRate:0.0} FPS" : "측정 중";
            view.displayState.text=$"선택 {width} × {height} · 현재 {Screen.width} × {Screen.height}\nFPS 제한 {frameLimit} · 현재 {measured}";
        }
        private void Update()
        {
            // Use elapsed real time: this remains accurate when timeScale changes.
            frameSampleSeconds += Time.unscaledDeltaTime; frameSampleCount++;
            if (frameSampleSeconds >= .5f)
            {
                measuredFrameRate = frameSampleCount / frameSampleSeconds;
                frameSampleSeconds = 0; frameSampleCount = 0;
                RefreshDisplay();
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                view.GetComponentsInChildren<Canvas>().Length == 1 && !navigation.Transitioning) Back();
        }
        private void Back()
        {
            if (navigation is ISettingsReturnNavigation returning) returning.ReturnFromSettings();
            else navigation.ShowSelection();
        }
        private void Refresh()
        {
            var actual = audio.Actual;
            view.actualState.text = $"현재 DSP: {actual.BufferSize} samples × {actual.BufferCount} · {actual.SampleRate} Hz\n요청 버퍼: {audio.RequestedBufferSize} samples";
            view.status.text = audio.BufferStatus;
            view.apply.interactable = view.back.interactable = !navigation.Transitioning;
        }
        private void Unbind()
        {
            if (audio != null) audio.Changed -= Refresh;
            if (!view) return;
            if (hardRuleActions != null)
                for (int i=0; i<hardRuleActions.Length; i++) view.hardRuleLists[i].onValueChanged.RemoveListener(hardRuleActions[i]);
            if (hardCountActions != null)
                for (int i=0; i<hardCountActions.Length; i++) view.hardLossCounts[i].onEndEdit.RemoveListener(hardCountActions[i]);
            hardRuleActions=null; hardCountActions=null;
            view.speedSlider.onValueChanged.RemoveListener(SpeedChanged);
            view.timingOffset.onValueChanged.RemoveListener(TimingChanged);
            view.gameplayTab.onClick.RemoveListener(ShowGameplay);
            view.bufferList.onValueChanged.RemoveListener(Changed);
            view.apply.onClick.RemoveListener(Apply); view.back.onClick.RemoveListener(Back);
            view.masterVolume.onValueChanged.RemoveListener(MasterChanged);
            view.backgroundVolume.onValueChanged.RemoveListener(BackgroundChanged);
            view.gameplayVolume.onValueChanged.RemoveListener(GameplayChanged);
            view.aspectList.onValueChanged.RemoveListener(DisplayChanged); view.resolutionList.onValueChanged.RemoveListener(DisplayChanged);
            view.fpsList.onValueChanged.RemoveListener(DisplayChanged);
            view.soundTab.onClick.RemoveListener(ShowSound); view.graphicsTab.onClick.RemoveListener(ShowGraphics);
        }
        private void OnDestroy() => Unbind();
    }
}
