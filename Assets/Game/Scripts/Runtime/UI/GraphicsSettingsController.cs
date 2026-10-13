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
    public sealed class GraphicsSettingsController : IDisposable
    {
        private readonly SettingsView view;
        private readonly PresentationPreferences presentation;
        private readonly IAudioSettingsService audio;
        private readonly IAppNavigation navigation;
        private float frameSampleSeconds, measuredFrameRate;
        private int frameSampleCount;
        public GraphicsSettingsController(SettingsView view, PresentationPreferences presentation, IAudioSettingsService audio, IAppNavigation navigation)
        {
            this.view = view; this.presentation = presentation; this.audio = audio; this.navigation = navigation;
            var options = new List<string> { $"Default ({audio.DefaultBufferSize} samples)" };
            for (int i = 1; i < audio.OptionCount; i++) options.Add($"{audio.GetBufferSize(i)} samples");
            view.bufferList.ClearOptions(); view.bufferList.AddOptions(options);
            view.bufferList.SetValueWithoutNotify(audio.SelectedBufferOption);
            Configure(view.aspectList, new[] { "16:9", "16:10" }, presentation.aspect);
            Configure(view.resolutionList, new[] { "FHD (1920 × 1080)", "QHD (2560 × 1440)" }, presentation.resolution);
            Configure(view.fpsList, new[] { "30", "60", "120", "144", "240", "제한 없음" }, presentation.frameRate);
            view.bufferList.onValueChanged.AddListener(Changed);
            view.apply.onClick.AddListener(Apply);
            view.aspectList.onValueChanged.AddListener(DisplayChanged);
            view.resolutionList.onValueChanged.AddListener(DisplayChanged);
            view.fpsList.onValueChanged.AddListener(DisplayChanged);
            audio.Changed += Refresh; Refresh(); RefreshDisplay();
        }
        public void Tick()
        {
            frameSampleSeconds += Time.unscaledDeltaTime; frameSampleCount++;
            if (frameSampleSeconds < .5f) return;
            measuredFrameRate = frameSampleCount / frameSampleSeconds;
            frameSampleSeconds = 0; frameSampleCount = 0; RefreshDisplay();
        }
        private void Changed(int option) { audio.SelectBufferOption(option); Refresh(); }
        private void Apply()
        {
            if (view.graphicsPanel.activeSelf)
            {
                presentation.aspect = view.aspectList.value; presentation.resolution = view.resolutionList.value;
                presentation.frameRate = view.fpsList.value; presentation.ApplyGraphicsValues(); presentation.SaveValues();
                audio.ApplyBufferSize();
            }
            Refresh(); RefreshDisplay();
        }
        private static void Configure(Dropdown dropdown, string[] options, int value)
        { dropdown.ClearOptions(); dropdown.AddOptions(new List<string>(options)); dropdown.SetValueWithoutNotify(value); }
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
        private void Refresh()
        {
            var actual = audio.Actual;
            view.actualState.text = $"현재 DSP: {actual.BufferSize} samples × {actual.BufferCount} · {actual.SampleRate} Hz\n요청 버퍼: {audio.RequestedBufferSize} samples";
            view.status.text = audio.BufferStatus;
            view.apply.interactable = view.back.interactable = !navigation.Transitioning;
        }

        public void Dispose()
        {
            audio.Changed -= Refresh;
            view.bufferList.onValueChanged.RemoveListener(Changed); view.apply.onClick.RemoveListener(Apply);
            view.aspectList.onValueChanged.RemoveListener(DisplayChanged);
            view.resolutionList.onValueChanged.RemoveListener(DisplayChanged); view.fpsList.onValueChanged.RemoveListener(DisplayChanged);
        }
    }
}
