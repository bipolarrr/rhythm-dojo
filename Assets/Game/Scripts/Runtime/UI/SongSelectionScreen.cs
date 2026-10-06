using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RhythmDojo.Application;
using RhythmDojo.Content;
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
        private ISelectionPreferences preferences;
        private CancellationTokenSource lifetime;
        private bool favoritesOnly, loading;
        private UnityEngine.Events.UnityAction[] difficultyActions;
        public void Validate()
        {
            if (!view) throw new InvalidOperationException("Song selection view missing.");
            view.Validate();
        }
        public void Initialize(GameSettings gameSettings, ISongSelectionService songs, IAppNavigation appNavigation,
            ISelectionPreferences savedPreferences = null)
        {
            Unbind(); Validate(); settings = gameSettings; selection = songs; navigation = appNavigation;
            preferences = savedPreferences ?? new SelectionPreferences();
            lifetime = new CancellationTokenSource(); loading = false; favoritesOnly = false;
            view.backPopup.SetActive(false); view.SetInteractable(true);
            view.difficultyList.ClearOptions();
            view.difficultyList.AddOptions(settings.difficulties.Select(p => p.DisplayName).ToList());
            view.scrollModeList.ClearOptions(); view.scrollModeList.AddOptions(new List<string> { "고정 속도", "BPM 연동" });
            view.multiplierList.ClearOptions();
            view.multiplierList.AddOptions(Enumerable.Range(0, settings.scroll.MultiplierCount)
                .Select(i => $"{settings.scroll.GetMultiplier(i):0.##}x").ToList());
            view.sortList.ClearOptions(); view.sortList.AddOptions(new List<string> { "정렬 : 제목", "정렬 : 작곡가", "정렬 : BPM" });
            view.sortList.SetValueWithoutNotify(0);
            view.difficultyList.SetValueWithoutNotify(Array.IndexOf(settings.difficulties, selection.SelectedDifficulty));
            view.scrollModeList.SetValueWithoutNotify((int)ScrollMode.Constant);
            for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                if (settings.scroll.GetMultiplier(i) == selection.SelectedMultiplier) view.multiplierList.SetValueWithoutNotify(i);
            RefreshSpeed();
            view.speedSlider.onValueChanged.AddListener(SpeedChanged);
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs); RefreshTiming();
            view.RefreshDifficultyButtons();
            difficultyActions = new UnityEngine.Events.UnityAction[view.difficultyButtons.Length];
            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                int index = i;
                difficultyActions[i] = () => SelectDifficulty(index);
                view.difficultyButtons[i].onClick.AddListener(difficultyActions[i]);
                var nav = view.difficultyButtons[i].navigation; nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                nav.selectOnDown = view.speedSlider; nav.selectOnUp = view.allTab;
                view.difficultyButtons[i].navigation = nav;
            }
            view.difficultyList.onValueChanged.AddListener(Changed);
            view.scrollModeList.onValueChanged.AddListener(Changed);
            view.multiplierList.onValueChanged.AddListener(Changed);
            view.sortList.onValueChanged.AddListener(SortChanged);
            view.timingOffset.onValueChanged.AddListener(TimingChanged);
            view.play.onClick.AddListener(Play); view.settingsButton.onClick.AddListener(ShowSettings);
            view.back.onClick.AddListener(ShowBack); view.confirmBack.onClick.AddListener(ConfirmBack);
            view.cancelBack.onClick.AddListener(CancelBack);
            view.allTab.onClick.AddListener(ShowAll); view.favoritesTab.onClick.AddListener(ShowFavorites);
            selection.Library.Changed += RefreshLibrary; RefreshLibrary(); FocusSelected();
        }
        private static bool Expanded(Dropdown dropdown) => dropdown.GetComponentInChildren<Canvas>() != null;
        private bool Busy => loading || navigation == null || navigation.Transitioning;
        private void Update()
        {
            if (Busy || Keyboard.current == null) return;
            if (Expanded(view.difficultyList) || Expanded(view.multiplierList) ||
                Expanded(view.scrollModeList) || Expanded(view.sortList)) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (view.backPopup.activeSelf) CancelBack(); else ShowBack();
                return;
            }
            if (view.backPopup.activeSelf) return;
            var focused = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != view.speedSlider.gameObject && focused != view.timingOffset.gameObject && focused != view.sortList.gameObject)
            {
                int direction = Keyboard.current.rightArrowKey.wasPressedThisFrame ? 1 :
                    Keyboard.current.leftArrowKey.wasPressedThisFrame ? -1 : 0;
                if (direction != 0)
                {
                    SelectDifficulty(Mathf.Clamp(view.difficultyList.value + direction, 0, view.difficultyButtons.Length - 1));
                    if (EventSystem.current && view.difficultyButtons.Any(button => button.gameObject == focused))
                        EventSystem.current.SetSelectedGameObject(view.difficultyButtons[view.difficultyList.value].gameObject);
                }
            }
            if (Keyboard.current.enterKey.wasPressedThisFrame && view.Rows.Any(r => r.select.gameObject == focused)) Play();
        }
        private void RefreshLibrary()
        {
            if (Busy) return;
            IEnumerable<SongEntry> entries = selection.Library.Entries;
            if (favoritesOnly) entries = entries.Where(s => preferences.IsFavorite(s.Id));
            entries = view.sortList.value == 1 ? entries.OrderBy(s => s.Artist, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Title) :
                view.sortList.value == 2 ? entries.OrderBy(s => s.MinBpm).ThenBy(s => s.Title) :
                entries.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);
            var visible = entries.ToList(); view.ClearRows();
            foreach (var entry in visible)
            {
                var row = Instantiate(view.rowTemplate, view.rowContent);
                row.name = "Song Row " + entry.Id;
                row.Bind(entry, view.fallbackCover, selection.SelectedDifficulty.DisplayName,
                    preferences.IsFavorite(entry.Id), SelectSong, ToggleFavorite);
                row.gameObject.SetActive(true); view.Rows.Add(row);
            }
            for (int i = 0; i < view.Rows.Count; i++)
            {
                var nav = view.Rows[i].select.navigation; nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? view.Rows[i - 1].select : view.allTab;
                nav.selectOnDown = i + 1 < view.Rows.Count ? view.Rows[i + 1].select : view.play;
                nav.selectOnLeft = null; nav.selectOnRight = null;
                view.Rows[i].select.navigation = nav;
            }
            view.allTab.GetComponent<Image>().color = favoritesOnly ? new Color(.14f,.22f,.28f) : new Color(.1f,.4f,.45f);
            view.favoritesTab.GetComponent<Image>().color = favoritesOnly ? new Color(.1f,.4f,.45f) : new Color(.14f,.22f,.28f);
            view.emptyMessage.gameObject.SetActive(visible.Count == 0);
            view.emptyMessage.text = favoritesOnly ? "즐겨찾기한 곡이 없습니다." : "등록된 곡이 없습니다.";
            if (visible.Count == 0)
            {
                view.songTitle.text = "곡을 선택하세요"; view.artistBpm.text = "";
                view.album.sprite = view.fallbackCover; view.record.text = "기록 없음";
                view.details.text = "전체 탭에서 곡을 선택하세요."; view.play.interactable = false; return;
            }
            var selected = visible.FirstOrDefault(s => s.Id == selection.SelectedSongId) ?? visible[0];
            SelectSong(selected.Id);
        }
        public void SelectSong(string songId)
        {
            if (Busy || view.backPopup.activeSelf) return;
            var song = selection.Library.Find(songId); if (song == null) return;
            selection.UpdateSelection(songId, settings.difficulties[view.difficultyList.value],
                ScrollMode.Constant, settings.scroll.GetMultiplier(view.multiplierList.value));
            RefreshDetails(song);
            int index = view.Rows.FindIndex(r => r.Entry.Id == songId);
            foreach (var row in view.Rows) row.SetSelected(row.Entry.Id == songId);
            if (index >= 0 && EventSystem.current && EventSystem.current.currentSelectedGameObject == view.Rows[index].gameObject)
            {
                Canvas.ForceUpdateCanvases();
                float contentHeight = view.rowContent.rect.height, viewportHeight = view.scroll.viewport.rect.height;
                float top = index * 172f, bottom = top + 170f;
                float current = view.rowContent.anchoredPosition.y;
                if (top < current) current = top;
                else if (bottom > current + viewportHeight) current = bottom - viewportHeight;
                var position = view.rowContent.anchoredPosition;
                position.y = Mathf.Clamp(current, 0, Mathf.Max(0, contentHeight - viewportHeight));
                view.rowContent.anchoredPosition = position;
            }
        }
        private void RefreshDetails(SongEntry song)
        {
            view.songTitle.text = song.Title;
            string bpm = song.MinBpm == song.MaxBpm ? $"{song.MinBpm:0.##}" : $"{song.MinBpm:0.##}–{song.MaxBpm:0.##}";
            view.artistBpm.text = $"{song.Artist}  /  BPM {bpm}";
            view.album.sprite = song.Cover ? song.Cover : view.fallbackCover;
            if (view.album.sprite) view.album.GetComponent<AspectRatioFitter>().aspectRatio =
                view.album.sprite.rect.width / view.album.sprite.rect.height;
            var best = preferences.FindRecord(song.Id, selection.SelectedDifficulty.name);
            view.record.text = best == null ? "스코어     —             랭크  —\n레이트     —\n최대 콤보  —\n기록 없음" :
                $"스코어     {best.score:N0}       랭크  {best.Rank}\n레이트     {best.rate:0.00}%\n최대 콤보  {best.maxCombo}";
            view.details.text = song.Error == null ?
                $"{song.Duration:0}s · {song.NoteCount} 노트 · {song.LaneCount} 레인\n↑↓ 곡 선택 · ←→ 난이도 · Enter 플레이 · Esc 뒤로가기" :
                "재생 불가: " + song.Error;
            view.play.interactable = song.Error == null && !Busy && !view.backPopup.activeSelf;
            foreach (var row in view.Rows) row.difficulty.text = row.Entry.Error == null ? selection.SelectedDifficulty.DisplayName : "재생 불가";
        }
        private void SelectDifficulty(int value)
        {
            if (Busy || view.backPopup.activeSelf) return;
            view.difficultyList.value = value;
        }
        private void Changed(int value)
        {
            if (Busy || view.backPopup.activeSelf) return;
            RefreshSpeed();
            view.scrollModeList.SetValueWithoutNotify((int)ScrollMode.Constant);
            view.RefreshDifficultyButtons();
            preferences.Difficulty = settings.difficulties[view.difficultyList.value].name;
            preferences.ScrollMode = ScrollMode.Constant;
            preferences.Multiplier = settings.scroll.GetMultiplier(view.multiplierList.value);
            preferences.Save();
            if (view.Rows.Count != 0) SelectSong(selection.SelectedSongId);
        }
        private void RefreshSpeed()
        {
            double value = Math.Round(Math.Clamp(settings.scroll.GetMultiplier(view.multiplierList.value), 1, 10), 1, MidpointRounding.AwayFromZero);
            for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                if (Math.Abs(settings.scroll.GetMultiplier(i) - value) < .0001) view.multiplierList.SetValueWithoutNotify(i);
            view.speedSlider.SetValueWithoutNotify((float)value);
            view.speedLabel.text = $"속도  {value:0.0}x";
        }
        private void SpeedChanged(float value)
        {
            if (Busy || view.backPopup.activeSelf) return;
            double rounded = Math.Round(Math.Clamp((double)value, 1, 10), 1, MidpointRounding.AwayFromZero);
            view.speedSlider.SetValueWithoutNotify((float)rounded);
            for (int i = 0; i < settings.scroll.MultiplierCount; i++)
                if (Math.Abs(settings.scroll.GetMultiplier(i) - rounded) < .0001)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                    Changed(i);
                    return;
                }
        }
        private void TimingChanged(float value)
        {
            preferences.TimingOffsetMs = Mathf.RoundToInt(value); preferences.Save(); RefreshTiming();
        }
        private void RefreshTiming() => view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";
        private void SortChanged(int value) { RefreshLibrary(); FocusSelected(); }
        private void ShowAll() { favoritesOnly = false; RefreshLibrary(); FocusSelected(); }
        private void ShowFavorites() { favoritesOnly = true; RefreshLibrary(); FocusSelected(); }
        private void ToggleFavorite(string id)
        {
            if (Busy || view.backPopup.activeSelf) return;
            preferences.ToggleFavorite(id); RefreshLibrary(); FocusSelected();
        }
        private void FocusSelected()
        {
            if (!EventSystem.current) return;
            var row = view.Rows.Find(r => r.Entry.Id == selection.SelectedSongId);
            EventSystem.current.SetSelectedGameObject(row ? row.select.gameObject : view.allTab.gameObject);
        }
        private async void Play()
        {
            if (Busy || !view.play.interactable || view.backPopup.activeSelf) return;
            SetLoading(true);
            try { await navigation.PlaySelectedAsync(lifetime.Token); }
            catch (OperationCanceledException) { if (this && lifetime != null) { SetLoading(false); RefreshLibrary(); } }
            catch (Exception e)
            {
                if (!this || lifetime == null) return;
                SetLoading(false); RefreshLibrary(); view.details.text = "재생 불가: " + e.Message;
            }
        }
        private void SetLoading(bool value)
        {
            loading = value; view.SetInteractable(!value); view.play.interactable = !value;
        }
        private void ShowSettings() { if (!Busy && !view.backPopup.activeSelf) navigation.ShowSettings(); }
        private void ShowBack()
        {
            if (Busy) return;
            view.SetInteractable(false); view.play.interactable = false; view.backPopup.SetActive(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(view.cancelBack.gameObject);
        }
        private void CancelBack()
        {
            if (Busy) return;
            view.backPopup.SetActive(false); view.SetInteractable(true); RefreshLibrary(); FocusSelected();
        }
        private void ConfirmBack()
        {
            if (Busy) return;
            if (navigation is ITitleNavigation titleNavigation) titleNavigation.ShowTitle();
        }
        private void Unbind()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            if (selection != null) selection.Library.Changed -= RefreshLibrary;
            if (!view) return;
            if (difficultyActions != null)
                for (int i = 0; i < difficultyActions.Length; i++)
                    view.difficultyButtons[i].onClick.RemoveListener(difficultyActions[i]);
            difficultyActions = null;
            view.difficultyList.onValueChanged.RemoveListener(Changed);
            view.scrollModeList.onValueChanged.RemoveListener(Changed);
            view.multiplierList.onValueChanged.RemoveListener(Changed);
            view.sortList.onValueChanged.RemoveListener(SortChanged);
            view.speedSlider.onValueChanged.RemoveListener(SpeedChanged);
            view.timingOffset.onValueChanged.RemoveListener(TimingChanged);
            view.play.onClick.RemoveListener(Play); view.settingsButton.onClick.RemoveListener(ShowSettings);
            view.back.onClick.RemoveListener(ShowBack); view.confirmBack.onClick.RemoveListener(ConfirmBack);
            view.cancelBack.onClick.RemoveListener(CancelBack); view.allTab.onClick.RemoveListener(ShowAll);
            view.favoritesTab.onClick.RemoveListener(ShowFavorites); view.ClearRows();
        }
        private void OnDestroy() => Unbind();
    }
}