using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;

namespace RhythmDojo.UI
{
    public sealed class SongSelectionScreen : MonoBehaviour
    {
        [SerializeField] private SongSelectionView view;
        private GameSettings settings;
        private ISongSelectionService selection;
        private IAppNavigation navigation;
        private CancellationTokenSource lifetime;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Song selection view missing.");
            view.Validate();
        }
        public void Initialize(GameSettings gameSettings, ISongSelectionService songs, IAppNavigation appNavigation)
        {
            Unbind(); Validate(); settings = gameSettings; selection = songs; navigation = appNavigation;
            lifetime = new CancellationTokenSource();
            var difficulties = new List<string>();
            foreach (var profile in settings.difficulties) difficulties.Add(profile.DisplayName);
            view.difficultyList.ClearOptions(); view.difficultyList.AddOptions(difficulties);
            view.scrollModeList.ClearOptions(); view.scrollModeList.AddOptions(new List<string> { "Constant", "BPM changes" });
            var multipliers = new List<string>();
            for (int i = 0; i < settings.scroll.MultiplierCount; i++) multipliers.Add($"{settings.scroll.GetMultiplier(i):0.##}x");
            view.multiplierList.ClearOptions(); view.multiplierList.AddOptions(multipliers);
            view.difficultyList.SetValueWithoutNotify(Array.IndexOf(settings.difficulties, selection.SelectedDifficulty));
            view.scrollModeList.SetValueWithoutNotify((int)selection.SelectedScrollMode);
            for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                if (settings.scroll.GetMultiplier(i) == selection.SelectedMultiplier) view.multiplierList.SetValueWithoutNotify(i);
            view.songList.onValueChanged.AddListener(Changed); view.difficultyList.onValueChanged.AddListener(Changed);
            view.scrollModeList.onValueChanged.AddListener(Changed); view.multiplierList.onValueChanged.AddListener(Changed);
            view.play.onClick.AddListener(Play); view.settingsButton.onClick.AddListener(ShowSettings);
            selection.Library.Changed += RefreshLibrary; RefreshLibrary();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.songList.gameObject);
        }
        private void RefreshLibrary()
        {
            var labels = new List<string>(); int index = 0;
            for (int i = 0; i < selection.Library.Entries.Count; i++)
            {
                var entry = selection.Library.Entries[i]; labels.Add(entry.Title);
                if (entry.Id == selection.SelectedSongId) index = i;
            }
            view.songList.ClearOptions(); view.songList.AddOptions(labels);
            view.songList.SetValueWithoutNotify(index); Changed(0);
        }
        private void Changed(int value)
        {
            if (selection.Library.Entries.Count == 0)
            { view.play.interactable = false; view.details.text = "No songs available."; return; }
            var song = selection.Library.Entries[view.songList.value];
            selection.UpdateSelection(song.Id, settings.difficulties[view.difficultyList.value],
                (ScrollMode)view.scrollModeList.value, settings.scroll.GetMultiplier(view.multiplierList.value));
            view.play.interactable = song.Error == null && !navigation.Transitioning;
            if (song.Error != null) { view.details.text = "Cannot play: " + song.Error; return; }
            string bpm = song.MinBpm == song.MaxBpm ? $"{song.MinBpm:0.##}" : $"{song.MinBpm:0.##}–{song.MaxBpm:0.##}";
            var windows = settings.difficulties[view.difficultyList.value].ToSettings();
            view.details.text = $"{song.Title}\n{song.Artist}\n\nBPM {bpm}    Length {song.Duration:0.0}s\n" +
                $"{song.NoteCount} notes / {song.LaneCount} lanes\n\n" +
                $"Perfect ±{windows.PerfectWindow * 1000:0}ms / Good ±{windows.GoodWindow * 1000:0}ms\n\n" +
                "Difficulty changes judgment windows.\nScroll mode and multiplier change visual movement.\n\n" +
                "Use mouse or keyboard navigation to select.\nSPACE starts / restarts after loading. ESC returns here.";
        }
        private async void Play()
        {
            if (!view.play.interactable || navigation.Transitioning) return;
            SetLoading(true);
            try { await navigation.PlaySelectedAsync(lifetime.Token); }
            catch (OperationCanceledException) { if (this && lifetime != null) { SetLoading(false); Changed(0); } }
            catch (Exception e)
            {
                if (!this || lifetime == null) return;
                SetLoading(false); view.details.text = "Cannot play: " + e.Message; view.play.interactable = true;
            }
        }
        private void SetLoading(bool loading)
        {
            view.songList.interactable = view.difficultyList.interactable = view.scrollModeList.interactable =
                view.multiplierList.interactable = view.settingsButton.interactable = !loading;
            view.play.interactable = !loading;
        }
        private void ShowSettings() => navigation.ShowSettings();
        private void Unbind()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            if (selection != null) selection.Library.Changed -= RefreshLibrary;
            if (!view) return;
            view.songList.onValueChanged.RemoveListener(Changed); view.difficultyList.onValueChanged.RemoveListener(Changed);
            view.scrollModeList.onValueChanged.RemoveListener(Changed); view.multiplierList.onValueChanged.RemoveListener(Changed);
            view.play.onClick.RemoveListener(Play); view.settingsButton.onClick.RemoveListener(ShowSettings);
        }
        private void OnDestroy() => Unbind();
    }
}
