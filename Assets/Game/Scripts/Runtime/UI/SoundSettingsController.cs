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
    public sealed class SoundSettingsController : IDisposable
    {
        private readonly SettingsView view;
        private readonly PresentationPreferences presentation;
        private double saveAudioAt = double.PositiveInfinity;

        public SoundSettingsController(SettingsView view, PresentationPreferences presentation)
        {
            this.view = view;
            this.presentation = presentation;
            view.masterVolume.SetValueWithoutNotify(presentation.masterVolume * 100);
            view.backgroundVolume.SetValueWithoutNotify(presentation.backgroundVolume * 100);
            view.gameplayVolume.SetValueWithoutNotify(presentation.gameplayVolume * 100);
            view.masterVolume.onValueChanged.AddListener(MasterChanged);
            view.backgroundVolume.onValueChanged.AddListener(BackgroundChanged);
            view.gameplayVolume.onValueChanged.AddListener(GameplayChanged);
            RefreshVolumes();
        }

        public void Tick()
        {
            if (Time.unscaledTimeAsDouble >= saveAudioAt)
            {
                FlushAudio();
            }
        }

        private void MasterChanged(float value)
        {
            presentation.masterVolume = value / 100f;
            ApplyAudio();
        }

        private void BackgroundChanged(float value)
        {
            presentation.backgroundVolume = value / 100f;
            ApplyAudio();
        }

        private void GameplayChanged(float value)
        {
            presentation.gameplayVolume = value / 100f;
            ApplyAudio();
        }

        private void ApplyAudio()
        {
            presentation.ApplyAudio();
            saveAudioAt = Time.unscaledTimeAsDouble + .3;
            RefreshVolumes();
        }

        private void FlushAudio()
        {
            if (double.IsPositiveInfinity(saveAudioAt))
            {
                return;
            }

            presentation.SaveValues();
            saveAudioAt = double.PositiveInfinity;
        }

        private void RefreshVolumes()
        {
            view.masterLabel.text = $"전체 소리 {presentation.masterVolume * 100:0}%";
            view.backgroundLabel.text = $"배경음악 {presentation.backgroundVolume * 100:0}%";
            view.gameplayLabel.text = $"플레이 음악 {presentation.gameplayVolume * 100:0}%";
        }

        public void Dispose()
        {
            FlushAudio();
            view.masterVolume.onValueChanged.RemoveListener(MasterChanged);
            view.backgroundVolume.onValueChanged.RemoveListener(BackgroundChanged);
            view.gameplayVolume.onValueChanged.RemoveListener(GameplayChanged);
        }
    }
}
