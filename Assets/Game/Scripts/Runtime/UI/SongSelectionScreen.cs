using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed partial class SongSelectionScreen : MonoBehaviour, IModalInputParticipant
    {
        [SerializeField] private SongSelectionView view;
        private DifficultyProfile[] difficultyOptions;
        private double[] speedOptions;
        private ISongSelectionService selection;
        private IAppNavigation navigation;
        private ISelectionPreferences preferences;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource previewCancellation;
        private CancellationTokenSource playCancellation;
        private PlayableSong previewSong;
        private double previewEnd;
        private double loadingStartedAt;
        private bool libraryRefreshPending;
        private (string SongId, DifficultyProfile Difficulty, string Message, bool CanRetry)? playbackFailure;
        private string previewCaption = "▶ 미리듣기";
        private string previewFailure;
        private SongSelectionList list;
        private SongSelectionPresentation presentation;
        private SongSelectionState state;
        private static readonly string[] LoadingCaptions = { "불러오는 중.", "불러오는 중..", "불러오는 중..." };
        public bool HardModeSelected { get; private set; }
        public SongSelectionState State => state;
        private HardModePreferences hardMode;
        private IPlayRulesService rules;
        private bool ModalOpen => state == SongSelectionState.ModePopup;
        private UnityEngine.Events.UnityAction[] difficultyActions;
        private bool Busy
        {
            get
            {
                if (state == SongSelectionState.Loading || state == SongSelectionState.Leaving)
                {
                    return true;
                }
                return navigation == null || navigation.Transitioning;
            }
        }

        private bool CanPlay
        {
            get
            {
                if (presentation == null || !presentation.CanPlay)
                {
                    return false;
                }
                return !playbackFailure.HasValue || playbackFailure.Value.CanRetry;
            }
        }

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Song selection view missing.");
            }
            view.Validate();
        }

        public void Initialize(GameSettings gameSettings, ISongSelectionService songs,
            IAppNavigation appNavigation, ISelectionPreferences savedPreferences = null,
            HardModePreferences hardMode = null, IPlayRulesService rules = null)
        {
            Unbind();
            Validate();
            if (selection != songs || list == null)
            {
                list = new SongSelectionList();
                if (songs is SongSelectionService service)
                {
                    list = service.List;
                }
            }
            selection = songs;
            navigation = appNavigation;
            difficultyOptions = gameSettings.difficulties.ToArray();
            speedOptions = Enumerable.Range(0, gameSettings.scroll.MultiplierCount)
                .Select(gameSettings.scroll.GetMultiplier).ToArray();
            if (songs is ISelectionOptions options)
            {
                difficultyOptions = options.DifficultyOptions.ToArray();
                speedOptions = options.SpeedOptions.ToArray();
            }
            if (view.difficultyButtons.Length != difficultyOptions.Length)
            {
                throw new InvalidOperationException("Authored difficulty controls do not match selection options.");
            }
            preferences = savedPreferences ?? new SelectionPreferences(null);
            this.hardMode = hardMode ?? new HardModePreferences(null);
            this.rules = rules;
            HardModeSelected = this.hardMode.Enabled;
            lifetime = new CancellationTokenSource();
            state = SongSelectionState.Main;
            playbackFailure = null;
            view.scrollModeList.ClearOptions();
            view.scrollModeList.AddOptions(new List<string> { "고정 속도", "BPM 연동" });
            view.multiplierList.ClearOptions();
            view.multiplierList.AddOptions(speedOptions.Select(value => $"{value:0.##}x").ToList());
            view.sortList.ClearOptions();
            view.sortList.AddOptions(new List<string> { "정렬 : 제목", "정렬 : 작곡가", "정렬 : BPM" });
            view.sortList.SetValueWithoutNotify(list.Sort);
            difficultyActions = new UnityEngine.Events.UnityAction[difficultyOptions.Length];
            for (int i = 0; i < difficultyActions.Length; i++)
            {
                int index = i;
                difficultyActions[i] = () => SelectDifficulty(index);
                view.difficultyButtons[i].GetComponentInChildren<Text>().text = difficultyOptions[i].DisplayName;
                view.difficultyButtons[i].onClick.AddListener(difficultyActions[i]);
            }
            view.scrollModeList.onValueChanged.AddListener(ScrollChanged);
            view.multiplierList.onValueChanged.AddListener(MultiplierChanged);
            view.sortList.onValueChanged.AddListener(SortChanged);
            view.speedSlider.onValueChanged.AddListener(SpeedChanged);
            view.timingOffset.onValueChanged.AddListener(TimingChanged);
            view.modeButton.onClick.AddListener(ShowMode);
            view.normalModeButton.onClick.AddListener(SelectNormalMode);
            view.hardModeButton.onClick.AddListener(SelectHardMode);
            view.cancelMode.onClick.AddListener(CloseMode);
            view.play.onClick.AddListener(Play);
            view.previewButton.onClick.AddListener(PreviewPractice);
            view.settingsButton.onClick.AddListener(PracticeSettings);
            view.back.onClick.AddListener(ShowBack);
            view.closeLibrary.onClick.AddListener(ClosePracticeLibrary);
            view.allTab.onClick.AddListener(ShowAll);
            view.favoritesTab.onClick.AddListener(ShowFavorites);
            selection.Library.Changed += OnLibraryChanged;
            BindPractice();
            PrepareEntryPresentation();
            FocusSelected();
        }

        private static bool Expanded(Dropdown dropdown) => dropdown.GetComponentInChildren<Canvas>() != null;

        private void Update()
        {
            if (lifetime == null)
            {
                return;
            }
            if (libraryRefreshPending && !Busy && !ModalOpen)
            {
                RefreshLibrary();
                FocusSelected();
            }
            if (previewSong != null)
            {
                bool finished = AudioSettings.dspTime >= previewEnd || !view.previewSource.isPlaying;
                if (finished || previewSong.NeedsAudioRecovery)
                {
                    StopPreview();
                }
            }
            RefreshPracticeFeedback();
            if (Busy)
            {
                return;
            }
            // UGUI owns the topmost dropdown, including Escape and selection restoration.
            if (Expanded(view.multiplierList) || Expanded(view.scrollModeList) || Expanded(view.sortList))
            {
                return;
            }
            bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
            cancel |= Gamepad.current?.buttonEast.wasPressedThisFrame == true;
            if (cancel)
            {
                if (ModalOpen)
                {
                    CloseMode();
                }
                else if (state == SongSelectionState.Library)
                {
                    ClosePracticeLibrary();
                }
                else
                {
                    ShowBack();
                }
                return;
            }
            if (ModalOpen || state == SongSelectionState.Library)
            {
                return;
            }
            UpdatePracticeInput();
        }

        private void OnLibraryChanged()
        {
            playbackFailure = null;
            StopPreview();
            RefreshLibrary();
        }

        private void RefreshLibrary(bool preparing = false)
        {
            if (!preparing && (Busy || ModalOpen))
            {
                libraryRefreshPending = true;
                return;
            }
            libraryRefreshPending = false;
            string selected = list.Refresh(selection.Library, preferences, selection.SelectedSongId);
            UpdateSelectedSong(selected);
            // Reuse the authored row instances by position; release any surplus after library changes.
            while (view.Rows.Count > list.Entries.Count)
            {
                var row = view.Rows[view.Rows.Count - 1];
                view.Rows.RemoveAt(view.Rows.Count - 1);
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            for (int i = 0; i < list.Entries.Count; i++)
            {
                if (i == view.Rows.Count)
                {
                    view.Rows.Add(Instantiate(view.rowTemplate, view.rowContent));
                }
                var row = view.Rows[i];
                row.Bind(list.Entries[i], view.fallbackCover, selection.SelectedDifficulty.DisplayName,
                    preferences.IsFavorite(list.Entries[i].Id), SelectSong, ToggleFavorite);
                row.gameObject.SetActive(true);
                var nav = row.select.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = view.allTab;
                if (i > 0)
                {
                    nav.selectOnUp = view.Rows[i - 1].select;
                }
                nav.selectOnDown = view.closeLibrary;
                nav.selectOnLeft = null;
                nav.selectOnRight = row.favorite;
                row.select.navigation = nav;
                if (i > 0)
                {
                    nav = view.Rows[i - 1].select.navigation;
                    nav.selectOnDown = row.select;
                    view.Rows[i - 1].select.navigation = nav;
                }
                if (row.indexLabel)
                {
                    row.indexLabel.text = (i + 1).ToString("00");
                }
            }
            RefreshDetails();
        }

        private void UpdateSelectedSong(string songId)
        {
            var song = selection.Library.Find(songId);
            var difficulty = selection.SelectedDifficulty;
            int slot = Array.IndexOf(difficultyOptions, difficulty);
            if (song != null && !song.Charts.Any(chart => (int)chart.Slot == slot))
            {
                var available = song.Charts.FirstOrDefault(chart => (int)chart.Slot < difficultyOptions.Length);
                if (available != null)
                {
                    difficulty = difficultyOptions[(int)available.Slot];
                }
            }
            if (selection.SelectedSongId != songId || selection.SelectedDifficulty != difficulty)
            {
                StopPreview();
                playbackFailure = null;
            }
            selection.UpdateSelection(songId, difficulty, selection.SelectedScrollMode, selection.SelectedMultiplier);
        }

        public void SelectSong(string songId)
        {
            if (Busy || ModalOpen || list.IndexOf(songId) < 0)
            {
                return;
            }
            UpdateSelectedSong(songId);
            RefreshDetails();
            EnsureSelectedVisible();
        }

        private void EnsureSelectedVisible()
        {
            if (state != SongSelectionState.Library)
            {
                return;
            }
            int index = list.IndexOf(selection.SelectedSongId);
            if (index < 0 || index >= view.Rows.Count)
            {
                return;
            }
            Canvas.ForceUpdateCanvases();
            float contentHeight = view.rowContent.rect.height;
            float viewportHeight = view.scroll.viewport.rect.height;
            var rowRect = (RectTransform)view.Rows[index].transform;
            float top = view.rowContent.rect.yMax - view.rowContent.InverseTransformPoint(
                rowRect.TransformPoint(new Vector3(0, rowRect.rect.yMax, 0))).y;
            float bottom = view.rowContent.rect.yMax - view.rowContent.InverseTransformPoint(
                rowRect.TransformPoint(new Vector3(0, rowRect.rect.yMin, 0))).y;
            float current = view.rowContent.anchoredPosition.y;
            if (top < current)
            {
                current = top;
            }
            else if (bottom > current + viewportHeight)
            {
                current = bottom - viewportHeight;
            }
            var position = view.rowContent.anchoredPosition;
            position.y = Mathf.Clamp(current, 0, Mathf.Max(0, contentHeight - viewportHeight));
            view.scroll.StopMovement();
            view.rowContent.anchoredPosition = position;
        }

        private void RefreshDetails()
        {
            if (playbackFailure is { } failure)
            {
                bool sameSelection = failure.SongId == selection.SelectedSongId
                    && failure.Difficulty == selection.SelectedDifficulty;
                if (!sameSelection)
                {
                    playbackFailure = null;
                }
            }
            RefreshMode();
            presentation = SongSelectionPresentation.Create(selection, list, difficultyOptions,
                preferences, rules, HardModeSelected, view.fallbackCover);
            view.Render(presentation);
            foreach (var row in view.Rows)
            {
                row.SetSelected(row.Entry.Id == selection.SelectedSongId);
                var available = row.Entry.Charts.FirstOrDefault(chart => (int)chart.Slot == presentation.SelectedDifficulty);
                row.difficulty.text = selection.SelectedDifficulty.DisplayName;
                if (row.Entry.Error != null || available?.Error != null)
                {
                    row.difficulty.text = "재생 불가";
                }
                else if (available == null)
                {
                    row.difficulty.text = "채보 없음";
                }
            }
            RefreshSpeed();
            RefreshTiming();
            RefreshPracticeFeedback();
        }

        private void SelectDifficulty(int value)
        {
            if (Busy || ModalOpen || view.practice.IsStriking)
            {
                return;
            }
            if (presentation == null || !presentation.Difficulties[value])
            {
                return;
            }
            if (selection.SelectedDifficulty != difficultyOptions[value])
            {
                StopPreview();
                playbackFailure = null;
            }
            selection.UpdateSelection(selection.SelectedSongId, difficultyOptions[value],
                selection.SelectedScrollMode, selection.SelectedMultiplier);
            RefreshDetails();
        }

        private void ScrollChanged(int value)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            selection.UpdateSelection(selection.SelectedSongId, selection.SelectedDifficulty,
                (ScrollMode)value, selection.SelectedMultiplier);
            Changed(value);
        }

        private void MultiplierChanged(int value)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            selection.UpdateSelection(selection.SelectedSongId, selection.SelectedDifficulty,
                selection.SelectedScrollMode, speedOptions[value]);
            Changed(value);
        }

        private void Changed(int value)
        {
            preferences.Difficulty = selection.SelectedDifficulty.name;
            preferences.ScrollMode = selection.SelectedScrollMode;
            preferences.Multiplier = selection.SelectedMultiplier;
            preferences.Save();
            RefreshSpeed();
        }

        private void RefreshSpeed()
        {
            view.scrollModeList.SetValueWithoutNotify((int)selection.SelectedScrollMode);
            for (int i = 0; i < speedOptions.Length; i++)
            {
                if (Math.Abs(speedOptions[i] - selection.SelectedMultiplier) < .0001)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                }
            }
            view.speedSlider.SetValueWithoutNotify((float)selection.SelectedMultiplier);
            view.speedLabel.text = $"속도  {selection.SelectedMultiplier:0.0}x";
        }

        private void SpeedChanged(float value)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            double rounded = Math.Round(Math.Clamp((double)value, 1, 10), 1, MidpointRounding.AwayFromZero);
            int index = Array.FindIndex(speedOptions, speed => Math.Abs(speed - rounded) < .0001);
            if (index >= 0)
            {
                MultiplierChanged(index);
            }
        }

        private void TimingChanged(float value)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            preferences.TimingOffsetMs = Mathf.RoundToInt(value);
            preferences.Save();
            RefreshTiming();
        }

        private void RefreshTiming()
        {
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";
        }

        private void SortChanged(int value)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }

            list.Sort = value;
            RefreshLibrary();
            FocusSelected();
        }

        private void ShowAll()
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }

            list.FavoritesOnly = false;
            RefreshLibrary();
            FocusSelected();
        }

        private void ShowFavorites()
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }

            list.FavoritesOnly = true;
            RefreshLibrary();
            FocusSelected();
        }

        private void ToggleFavorite(string id)
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            preferences.ToggleFavorite(id);
            RefreshLibrary();
            FocusSelected();
        }

        public void RestoreEntryFocus()
        {
            PrepareEntryPresentation();
            FocusSelected();
        }

        private void FocusSelected()
        {
            if (!EventSystem.current)
            {
                return;
            }
            GameObject target = view.practice.library.gameObject;
            if (state == SongSelectionState.Library)
            {
                target = view.allTab.gameObject;
                int index = list.IndexOf(selection.SelectedSongId);
                if (index >= 0 && index < view.Rows.Count)
                {
                    target = view.Rows[index].select.gameObject;
                }
            }
            else if (CanPlay)
            {
                target = view.play.gameObject;
            }
            EventSystem.current.SetSelectedGameObject(target);
            EnsureSelectedVisible();
        }

        private async void TogglePreview()
        {
            if (Busy || ModalOpen || !CanPlay)
            {
                return;
            }
            if (selection.Library is not ISongChartLoader loader)
            {
                return;
            }
            if (previewCancellation != null)
            {
                StopPreview();
                return;
            }
            var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            previewCancellation = request;
            previewFailure = null;
            previewCaption = "불러오는 중…";
            PlayableSong song = null;
            try
            {
                var slot = (ChartSlot)Array.IndexOf(difficultyOptions, selection.SelectedDifficulty);
                song = await loader.LoadChartAsync(selection.SelectedSongId, slot, request.Token);
                if (previewCancellation != request || !this || !isActiveAndEnabled)
                {
                    return;
                }
                const float previewDuration = 12;
                var source = view.previewSource;
                var clip = song.AudioClip;
                float start = Mathf.Min(clip.length * .35f, Mathf.Max(0, clip.length - previewDuration));
                source.clip = clip;
                source.time = start;
                source.volume = (PresentationPreferences.Current ?? PresentationPreferences.Load()).gameplayVolume;
                source.Play();
                previewEnd = AudioSettings.dspTime + Math.Min(previewDuration, clip.length - start);
                previewSong = song;
                song = null;
                previewCaption = "■ 멈추기";
            }
            catch (OperationCanceledException)
            {
                if (previewCancellation == request)
                {
                    StopPreview();
                }
            }
            catch (Exception e)
            {
                if (previewCancellation == request)
                {
                    StopPreview();
                    var display = SongLoadErrorDisplay.FromException(e);
                    previewFailure = display.Message;
                    previewCaption = "미리듣기 실패 · 재시도";
                    if (!display.CanRetry)
                    {
                        playbackFailure = (selection.SelectedSongId, selection.SelectedDifficulty,
                            display.Message, false);
                    }
                }
            }
            finally
            {
                song?.Dispose();
            }
        }

        private void StopPreview()
        {
            var request = previewCancellation;
            previewCancellation = null;
            request?.Cancel();
            request?.Dispose();
            if (view && view.previewSource)
            {
                view.previewSource.Stop();
                view.previewSource.clip = null;
            }
            previewCaption = "▶ 미리듣기";
            previewFailure = null;
            previewSong?.Dispose();
            previewSong = null;
        }

        private async void Play()
        {
            if (Busy || !CanPlay || state != SongSelectionState.Main)
            {
                return;
            }
            if (view.practice.IsStriking)
            {
                state = SongSelectionState.StartPending;
                view.practice.StopBarrage();
                return;
            }
            StopPreview();
            var songId = selection.SelectedSongId;
            var difficulty = selection.SelectedDifficulty;
            playbackFailure = null;
            state = SongSelectionState.Loading;
            loadingStartedAt = Time.unscaledTimeAsDouble;
            RefreshPracticeFeedback();
            var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            playCancellation = request;
            try
            {
                await navigation.PlaySelectedAsync(request.Token);
            }
            catch (OperationCanceledException)
            {
                if (this && playCancellation == request)
                {
                    state = SongSelectionState.Main;
                    RefreshLibrary();
                    FocusSelected();
                }
            }
            catch (Exception e)
            {
                if (!this || playCancellation != request)
                {
                    return;
                }
                var display = SongLoadErrorDisplay.FromException(e);
                playbackFailure = (songId, difficulty, display.Message, display.CanRetry);
                state = SongSelectionState.Main;
                RefreshLibrary();
                FocusSelected();
            }
            finally
            {
                if (playCancellation == request)
                {
                    playCancellation = null;
                }
                request.Dispose();
            }
        }

        private void ShowMode()
        {
            if (Busy || state != SongSelectionState.Library)
            {
                return;
            }
            state = SongSelectionState.ModePopup;
            RefreshPracticeFeedback();
            if (EventSystem.current)
            {
                var button = view.normalModeButton;
                if (HardModeSelected)
                {
                    button = view.hardModeButton;
                }
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }

        private void SelectNormalMode() => SelectMode(false);
        private void SelectHardMode() => SelectMode(true);

        private void SelectMode(bool hard)
        {
            if (Busy || !ModalOpen)
            {
                return;
            }
            HardModeSelected = hard;
            hardMode.Enabled = hard;
            hardMode.Save();
            if (rules != null)
            {
                var snapshot = rules.Snapshot;
                snapshot.Mode = RhythmDojo.Gameplay.PlayMode.Gauge;
                if (hard)
                {
                    snapshot.Mode = RhythmDojo.Gameplay.PlayMode.Challenge;
                }
                rules.Update(snapshot);
            }
            CloseMode();
        }

        private void RefreshMode()
        {
            if (rules != null)
            {
                HardModeSelected = rules.Snapshot.Mode == RhythmDojo.Gameplay.PlayMode.Challenge;
            }
            view.modeLabel.text = "일반 · Gauge";
            if (HardModeSelected)
            {
                view.modeLabel.text = "하드 · Challenge";
            }
        }

        private void CloseMode()
        {
            if (Busy || !ModalOpen)
            {
                return;
            }
            state = SongSelectionState.Library;
            RefreshDetails();
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.modeButton.gameObject);
            }
        }

        private void ShowSettings()
        {
            if (Busy || state != SongSelectionState.Main)
            {
                return;
            }
            StopPreview();
            navigation.ShowSettings();
        }

        private void ShowBack() => ReturnFromPractice();

        private void Unbind()
        {
            libraryRefreshPending = false;
            UnbindPractice();
            StopPreview();
            playCancellation?.Cancel();
            playCancellation = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            if (selection != null)
            {
                selection.Library.Changed -= OnLibraryChanged;
            }
            if (!view)
            {
                return;
            }
            if (difficultyActions != null)
            {
                for (int i = 0; i < difficultyActions.Length; i++)
                {
                    view.difficultyButtons[i].onClick.RemoveListener(difficultyActions[i]);
                }
            }
            difficultyActions = null;
            view.scrollModeList.onValueChanged.RemoveListener(ScrollChanged);
            view.multiplierList.onValueChanged.RemoveListener(MultiplierChanged);
            view.sortList.onValueChanged.RemoveListener(SortChanged);
            view.speedSlider.onValueChanged.RemoveListener(SpeedChanged);
            view.timingOffset.onValueChanged.RemoveListener(TimingChanged);
            view.modeButton.onClick.RemoveListener(ShowMode);
            view.normalModeButton.onClick.RemoveListener(SelectNormalMode);
            view.hardModeButton.onClick.RemoveListener(SelectHardMode);
            view.cancelMode.onClick.RemoveListener(CloseMode);
            view.play.onClick.RemoveListener(Play);
            view.previewButton.onClick.RemoveListener(PreviewPractice);
            view.settingsButton.onClick.RemoveListener(PracticeSettings);
            view.back.onClick.RemoveListener(ShowBack);
            view.closeLibrary.onClick.RemoveListener(ClosePracticeLibrary);
            view.allTab.onClick.RemoveListener(ShowAll);
            view.favoritesTab.onClick.RemoveListener(ShowFavorites);
            view.ClearRows();
        }

        private void OnEnable()
        {
            if (lifetime != null)
            {
                PrepareEntryPresentation();
            }
        }

        private void OnDisable()
        {
            StopPreview();
            var request = playCancellation;
            playCancellation = null;
            request?.Cancel();
            if (view && view.practice)
            {
                view.practice.StopBarrage();
                practiceHeldDirection = 0;
            }
        }

        private void OnDestroy() => Unbind();

        public void ResumeAfterModal()
        {
            if (lifetime == null)
            {
                return;
            }
            PrepareEntryPresentation();
            FocusSelected();
        }
    }
}
