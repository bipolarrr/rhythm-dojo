using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using RhythmDojo.Services;

namespace RhythmDojo.UI
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private SettingsView view;
        private IAudioSettingsService audio;
        private IAppNavigation navigation;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Settings view missing.");
            view.Validate();
        }
        public void Initialize(IAudioSettingsService audioSettings, IAppNavigation appNavigation)
        {
            Unbind(); Validate(); audio = audioSettings; navigation = appNavigation;
            var options = new List<string> { $"Default ({audio.DefaultBufferSize} samples)" };
            for (int i = 1; i < audio.OptionCount; i++) options.Add($"{audio.GetBufferSize(i)} samples");
            view.bufferList.ClearOptions(); view.bufferList.AddOptions(options);
            view.bufferList.SetValueWithoutNotify(audio.SelectedBufferOption);
            view.bufferList.onValueChanged.AddListener(Changed);
            view.apply.onClick.AddListener(Apply); view.back.onClick.AddListener(Back);
            audio.Changed += Refresh; Refresh();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.bufferList.gameObject);
        }
        private void Changed(int option) { audio.SelectBufferOption(option); Refresh(); }
        private void Apply() { audio.ApplyBufferSize(); Refresh(); }
        private void Back() => navigation.ShowSelection();
        private void Refresh()
        {
            var actual = audio.Actual;
            view.actualState.text = $"Actual Unity DSP buffer: {actual.BufferSize} samples x {actual.BufferCount}\n" +
                $"Sample rate: {actual.SampleRate} Hz\nRequested: {audio.RequestedBufferSize} samples";
            view.status.text = audio.BufferStatus;
            view.apply.interactable = view.back.interactable = !navigation.Transitioning;
        }
        private void Unbind()
        {
            if (audio != null) audio.Changed -= Refresh;
            if (!view) return;
            view.bufferList.onValueChanged.RemoveListener(Changed);
            view.apply.onClick.RemoveListener(Apply); view.back.onClick.RemoveListener(Back);
        }
        private void OnDestroy() => Unbind();
    }
}
