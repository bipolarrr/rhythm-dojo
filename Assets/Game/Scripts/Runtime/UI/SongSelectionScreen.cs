using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class SongSelectionScreen : MonoBehaviour, IModalInputParticipant
    {
        [SerializeField]
        private SongSelectionView view;
        private GameSettings settings;
        private DifficultyProfile[] difficultyOptions;
        private double[] speedOptions;
        private ISongSelectionService selection;
        private IAppNavigation navigation;
        private ISelectionPreferences preferences;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource previewCancellation;
        private PlayableSong previewSong;
        private double previewEnd;
        private bool favoritesOnly,
            loading;
        private double loadingStartedAt;
        private (string SongId, DifficultyProfile Difficulty, string Message, bool CanRetry)? playbackFailure;

        private const string MissingAudioMessage = "음원 파일을 찾을 수 없어요.\n다른 곡을 선택해 주세요.";
        private const string InvalidChartMessage = "채보를 읽을 수 없어요.\n다른 곡을 선택해 주세요.";
        private static readonly string[] LoadingCaptions = { "불러오는 중.", "불러오는 중..", "불러오는 중..." };

        public bool HardModeSelected { get; private set; }

        private HardModePreferences hardMode;
        private IPlayRulesService rules;

        private bool ModalOpen => view.backPopup.activeSelf || view.modePopup.activeSelf;

        private UnityEngine.Events.UnityAction[] difficultyActions;

        public void Validate()
        {
            if (!view)
            {
                throw new InvalidOperationException("Song selection view missing.");
            }

            view.Validate();
        }

        public void Initialize(
            GameSettings gameSettings,
            ISongSelectionService songs,
            IAppNavigation appNavigation,
            ISelectionPreferences savedPreferences = null,
            HardModePreferences hardMode = null,
            IPlayRulesService rules = null
        )
        {
            Unbind();
            Validate();
            settings = gameSettings;
            selection = songs;
            navigation = appNavigation;

            if (songs is ISelectionOptions options)
            {
                difficultyOptions = options.DifficultyOptions.ToArray();
            }
            else
            {
                difficultyOptions = gameSettings.difficulties.ToArray();
            }

            if (songs is ISelectionOptions speeds)
            {
                speedOptions = speeds.SpeedOptions.ToArray();
            }
            else
            {
                speedOptions = Enumerable
                    .Range(0, gameSettings.scroll.MultiplierCount)
                    .Select(gameSettings.scroll.GetMultiplier)
                    .ToArray();
            }

            preferences = savedPreferences ?? new SelectionPreferences(null);
            this.hardMode = hardMode ?? new HardModePreferences(null);
            hardMode = this.hardMode;
            this.rules = rules;
            HardModeSelected = hardMode.Enabled;
            lifetime = new CancellationTokenSource();
            loading = false;
            playbackFailure = null;
            view.SetPlaybackStatus();
            favoritesOnly = false;
            view.backPopup.SetActive(false);
            view.modePopup.SetActive(false);
            view.SetInteractable(true);
            RefreshMode();
            view.modeButton.onClick.AddListener(ShowMode);
            view.normalModeButton.onClick.AddListener(SelectNormalMode);
            view.hardModeButton.onClick.AddListener(SelectHardMode);
            view.cancelMode.onClick.AddListener(CloseMode);
            view.difficultyList.ClearOptions();
            view.difficultyList.AddOptions(difficultyOptions.Select(p => p.DisplayName).ToList());
            view.scrollModeList.ClearOptions();
            view.scrollModeList.AddOptions(new List<string> { "고정 속도", "BPM 연동" });
            view.multiplierList.ClearOptions();
            view.multiplierList.AddOptions(
                Enumerable
                    .Range(0, speedOptions.Length)
                    .Select(i => $"{speedOptions[i]:0.##}x")
                    .ToList()
            );
            view.sortList.ClearOptions();
            view.sortList.AddOptions(
                new List<string> { "정렬 : 제목", "정렬 : 작곡가", "정렬 : BPM" }
            );
            view.sortList.SetValueWithoutNotify(0);
            view.difficultyList.SetValueWithoutNotify(
                Array.IndexOf(difficultyOptions, selection.SelectedDifficulty)
            );
            view.scrollModeList.SetValueWithoutNotify((int)ScrollMode.Constant);

            for (int i = 0; i < speedOptions.Length; i++)
            {
                if (speedOptions[i] == selection.SelectedMultiplier)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                }
            }

            RefreshSpeed();
            view.speedSlider.onValueChanged.AddListener(SpeedChanged);
            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            RefreshTiming();
            view.RefreshDifficultyButtons();
            difficultyActions = new UnityEngine.Events.UnityAction[view.difficultyButtons.Length];

            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                int index = i;
                difficultyActions[i] = () => SelectDifficulty(index);
                view.difficultyButtons[i].onClick.AddListener(difficultyActions[i]);

                var nav = view.difficultyButtons[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                nav.selectOnDown = view.settingsButton;
                nav.selectOnUp = view.allTab;
                view.difficultyButtons[i].navigation = nav;
            }

            view.difficultyList.onValueChanged.AddListener(Changed);
            view.scrollModeList.onValueChanged.AddListener(Changed);
            view.multiplierList.onValueChanged.AddListener(Changed);
            view.sortList.onValueChanged.AddListener(SortChanged);
            view.timingOffset.onValueChanged.AddListener(TimingChanged);
            view.play.onClick.AddListener(Play);
            view.previewButton.onClick.AddListener(TogglePreview);
            view.settingsButton.onClick.AddListener(ShowSettings);
            view.back.onClick.AddListener(ShowBack);
            view.confirmBack.onClick.AddListener(ConfirmBack);
            view.cancelBack.onClick.AddListener(CancelBack);
            view.allTab.onClick.AddListener(ShowAll);
            view.favoritesTab.onClick.AddListener(ShowFavorites);
            selection.Library.Changed += RefreshLibrary;
            RefreshLibrary();
            FocusSelected();
        }

        private static bool Expanded(Dropdown dropdown) =>
            dropdown.GetComponentInChildren<Canvas>() != null;

        private bool Busy => loading || navigation == null || navigation.Transitioning;

        private void Update()
        {
            if (previewSong != null)
            {
                if (AudioSettings.dspTime >= previewEnd || !view.previewSource.isPlaying || previewSong.NeedsAudioRecovery)
                {
                    StopPreview();
                }
            }

            if (loading)
            {
                int frame = (int)((Time.unscaledTimeAsDouble - loadingStartedAt) / .35) % LoadingCaptions.Length;
                view.playStatusLabel.text = LoadingCaptions[frame];
                return;
            }

            if (Busy || Keyboard.current == null || Expanded(view.difficultyList))
            {
                return;
            }

            if (
                Expanded(view.multiplierList)
                || Expanded(view.scrollModeList)
                || Expanded(view.sortList)
            )
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (view.modePopup.activeSelf)
                {
                    CloseMode();
                }
                else if (view.backPopup.activeSelf)
                {
                    CancelBack();
                }
                else
                {
                    ShowBack();
                }

                return;
            }

            if (ModalOpen)
            {
                return;
            }

            GameObject focused;

            if (EventSystem.current)
            {
                focused = EventSystem.current.currentSelectedGameObject;
            }
            else
            {
                focused = null;
            }

            if (focused != view.speedSlider.gameObject)
            {
                if (focused != view.timingOffset.gameObject && focused != view.sortList.gameObject)
                {
                    int direction;

                    if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                    {
                        direction = 1;
                    }
                    else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                    {
                        direction = -1;
                    }
                    else
                    {
                        direction = 0;
                    }

                    if (direction != 0)
                    {
                        SelectDifficulty(
                            Mathf.Clamp(
                                Array.IndexOf(difficultyOptions, selection.SelectedDifficulty)
                                    + direction,
                                0,
                                view.difficultyButtons.Length - 1
                            )
                        );

                        if (
                            EventSystem.current
                            && view.difficultyButtons.Any(button => button.gameObject == focused)
                        )
                        {
                            EventSystem.current.SetSelectedGameObject(
                                view.difficultyButtons[view.difficultyList.value].gameObject
                            );
                        }
                    }
                }
            }

            if (
                Keyboard.current.enterKey.wasPressedThisFrame
                && view.Rows.Any(r => r.select.gameObject == focused)
            )
            {
                Play();
            }
        }

        private void RefreshLibrary()
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            IEnumerable<SongEntry> entries = selection.Library.Entries;

            if (favoritesOnly)
            {
                entries = entries.Where(s => preferences.IsFavorite(s.Id));
            }

            if (view.sortList.value == 1)
            {
                entries = entries
                    .OrderBy(s => s.Artist, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.Title);
            }
            else if (view.sortList.value == 2)
            {
                entries = entries.OrderBy(s => s.MinBpm).ThenBy(s => s.Title);
            }
            else
            {
                entries = entries.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);
            }

            var visible = entries.ToList();
            view.ClearRows();

            foreach (var entry in visible)
            {
                var row = Instantiate(view.rowTemplate, view.rowContent);
                row.name = "Song Row " + entry.Id;
                row.Bind(
                    entry,
                    view.fallbackCover,
                    selection.SelectedDifficulty.DisplayName,
                    preferences.IsFavorite(entry.Id),
                    SelectSong,
                    ToggleFavorite
                );
                row.gameObject.SetActive(true);
                view.Rows.Add(row);
                if (row.indexLabel)
                {
                    row.indexLabel.text = view.Rows.Count.ToString("00");
                }
            }

            for (int i = 0; i < view.Rows.Count; i++)
            {
                var nav = view.Rows[i].select.navigation;
                nav.mode = Navigation.Mode.Explicit;

                if (i > 0)
                {
                    nav.selectOnUp = view.Rows[i - 1].select;
                }
                else
                {
                    nav.selectOnUp = view.allTab;
                }

                if (i + 1 < view.Rows.Count)
                {
                    nav.selectOnDown = view.Rows[i + 1].select;
                }
                else
                {
                    nav.selectOnDown = view.play;
                }

                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                view.Rows[i].select.navigation = nav;
            }

            if (favoritesOnly)
            {
                view.allTab.GetComponent<Image>().color = new Color(.09f, .115f, .13f);
            }
            else
            {
                view.allTab.GetComponent<Image>().color = new Color(.42f, .09f, .12f);
            }

            if (favoritesOnly)
            {
                view.favoritesTab.GetComponent<Image>().color = new Color(.42f, .09f, .12f);
            }
            else
            {
                view.favoritesTab.GetComponent<Image>().color = new Color(.09f, .115f, .13f);
            }

            if (view.pixelTheme)
            {
                var allFrame = view.allTab.GetComponentInChildren<PixelDojoFrameGraphic>();
                var favoriteFrame = view.favoritesTab.GetComponentInChildren<PixelDojoFrameGraphic>();
                var selectedFill = new Color(.98f, .85f, .62f);
                var normalFill = new Color(.78f, .62f, .42f);
                allFrame.color = selectedFill;
                favoriteFrame.color = normalFill;
                if (favoritesOnly)
                {
                    allFrame.color = normalFill;
                    favoriteFrame.color = selectedFill;
                }
            }
            view.emptyMessage.gameObject.SetActive(visible.Count == 0);

            if (favoritesOnly)
            {
                view.emptyMessage.text = "즐겨찾기한 곡이 없습니다.";
            }
            else
            {
                view.emptyMessage.text = "등록된 곡이 없습니다.";
            }

            if (visible.Count == 0)
            {
                view.songTitle.text = "곡을 선택하세요";
                view.artistBpm.text = "";
                view.album.sprite = view.fallbackCover;
                view.record.text = "기록 없음";
                if (view.comboRecord)
                {
                    view.record.text = "스코어  —\n랭크  —\n레이트  —";
                    view.comboRecord.text = "최대 콤보  —\n<size=30>기록</size>\n<size=28>기록 없음</size>";
                }
                view.details.text = "전체 탭에서 곡을 선택하세요.";
                view.play.interactable = false;
                view.previewButton.interactable = false;
                StopPreview();
                playbackFailure = null;
                view.SetPlaybackStatus();

                return;
            }

            var selected =
                visible.FirstOrDefault(s => s.Id == selection.SelectedSongId) ?? visible[0];
            SelectSong(selected.Id);
        }

        public void SelectSong(string songId)
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            var song = selection.Library.Find(songId);

            if (song == null)
            {
                return;
            }

            var difficulty = selection.SelectedDifficulty;
            int slot = Array.IndexOf(difficultyOptions, difficulty);

            if (!song.Charts.Any(c => (int)c.Slot == slot) && song.Charts.Count > 0)
            {
                difficulty = difficultyOptions[(int)song.Charts[0].Slot];
            }

            if (selection.SelectedSongId != songId || selection.SelectedDifficulty != difficulty)
            {
                StopPreview();
            }

            selection.UpdateSelection(
                songId,
                difficulty,
                ScrollMode.Constant,
                selection.SelectedMultiplier
            );
            view.difficultyList.SetValueWithoutNotify(Array.IndexOf(difficultyOptions, difficulty));
            view.RefreshDifficultyButtons();
            RefreshDetails(song);

            int index = view.Rows.FindIndex(r => r.Entry.Id == songId);

            foreach (var row in view.Rows)
            {
                row.SetSelected(row.Entry.Id == songId);
            }

            if (index >= 0)
            {
                if (
                    EventSystem.current
                    && EventSystem.current.currentSelectedGameObject == view.Rows[index].gameObject
                )
                {
                    Canvas.ForceUpdateCanvases();

                    float contentHeight = view.rowContent.rect.height,
                        viewportHeight = view.scroll.viewport.rect.height;
                    var rowRect = (RectTransform)view.Rows[index].transform;
                    float top =
                        view.rowContent.rect.yMax
                        - view.rowContent.InverseTransformPoint(
                            rowRect.TransformPoint(new Vector3(0, rowRect.rect.yMax, 0))
                        ).y;
                    float bottom =
                        view.rowContent.rect.yMax
                        - view.rowContent.InverseTransformPoint(
                            rowRect.TransformPoint(new Vector3(0, rowRect.rect.yMin, 0))
                        ).y;
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
                    position.y = Mathf.Clamp(
                        current,
                        0,
                        Mathf.Max(0, contentHeight - viewportHeight)
                    );
                    view.scroll.StopMovement();
                    view.rowContent.anchoredPosition = position;
                }
            }
        }

        private void RefreshDetails(SongEntry song)
        {
            var chart = song.Charts.FirstOrDefault(c =>
                (int)c.Slot == Array.IndexOf(difficultyOptions, selection.SelectedDifficulty)
            );
            view.songTitle.text = song.Title;

            string bpm;

            if (song.MinBpm == song.MaxBpm)
            {
                bpm = $"{song.MinBpm:0.##}";
            }
            else
            {
                bpm = $"{song.MinBpm:0.##}–{song.MaxBpm:0.##}";
            }

            view.artistBpm.text = $"{song.Artist}  /  BPM {bpm}";
            if (view.pixelTheme)
            {
                double duration = song.Duration;
                if (chart != null && chart.Duration.HasValue)
                {
                    duration = chart.Duration.Value;
                }
                var time = TimeSpan.FromSeconds(Math.Max(0, duration));
                view.artistBpm.text = $"아티스트  {song.Artist}\nBPM  {bpm}\nPLAY TIME  {(int)time.TotalMinutes}:{time.Seconds:00}";
            }

            if (song.Cover && song.Cover != view.legacyFallbackCover)
            {
                view.album.sprite = song.Cover;
            }
            else
            {
                view.album.sprite = view.fallbackCover;
            }

            if (view.album.sprite)
            {
                view.album.GetComponent<AspectRatioFitter>().aspectRatio =
                    view.album.sprite.rect.width / view.album.sprite.rect.height;
            }

            string recordKey;

            if (chart != null && rules != null)
            {
                recordKey = RecordIdentity.Rules(chart.Id, chart.Revision, rules.Snapshot);
            }
            else
            {
                recordKey = selection.SelectedDifficulty.name;
            }

            var best = preferences.FindRecord(song.Id, recordKey);

            if (best == null)
            {
                view.record.text =
                    "스코어  —\n랭크  —\n레이트  —\n최대 콤보  —\n기록 없음";
            }
            else
            {
                view.record.text =
                    $"스코어  {best.score:N0}\n랭크  {best.Rank}\n레이트  {best.rate:0.00}%\n최대 콤보  {best.maxCombo}";
            }

            var recordDetails = view.record;
            if (view.comboRecord)
            {
                recordDetails = view.comboRecord;
                if (best == null)
                {
                    view.record.text = "스코어  —\n랭크  —\n레이트  —";
                    recordDetails.text = "최대 콤보  —\n<size=30>기록</size>\n<size=28>기록 없음</size>";
                }
                else
                {
                    view.record.text = $"스코어  {best.score:N0}\n랭크  {best.Rank}\n레이트  {best.rate:0.00}%";
                    recordDetails.text = $"최대 콤보  {best.maxCombo:N0}\n<size=30>기록</size>";
                }
            }

            if (preferences is SelectionPreferences records)
            {
                if (best != null)
                {
                    string clearLabel = "Gauge —";
                    if (best.gaugeClear)
                    {
                        clearLabel = "Gauge CLEAR";
                    }

                    recordDetails.text +=
                        $"\n<size=26>{clearLabel}\nChallenge "
                        + string.Join(
                            " / ",
                            Enumerable
                                .Range(1, 3)
                                .Select(i =>
                                {
                                    if ((best.challengeAchievements & (1 << (i - 1))) != 0)
                                    {
                                        return i.ToString();
                                    }

                                    return "—";
                                })
                        )
                        + "</size>";
                }

                var failed = records.FindFailedRecord(song.Id, recordKey);

                if (failed != null)
                {
                    recordDetails.text +=
                        $"\n<size=26><color=#D6A873>실패 완주\n{failed.score:N0} · {failed.rate:0.00}%</color></size>";
                }
            }

            if (song.Error == null)
            {
                view.details.text =
                    $"{chart?.Duration ?? song.Duration:0}s · {chart?.NoteCount ?? song.NoteCount} 노트 · {song.LaneCount} 레인\n↑↓ 곡 선택 · ←→ 난이도 · Enter 플레이 · Esc 뒤로가기";
            }
            else
            {
                view.details.text = "재생 불가: " + song.Error;
            }

            view.play.interactable = song.Error == null && !Busy && !ModalOpen;
            view.play.interactable &= chart != null && chart.Error == null;

            if (chart?.Error != null)
            {
                view.details.text = "재생 불가: " + chart.Error;
            }

            RefreshPlaybackStatus(song, chart);
            view.previewButton.interactable = view.play.interactable && selection.Library is ISongChartLoader;

            for (int i = 0; i < view.difficultyButtons.Length; i++)
            {
                view.difficultyButtons[i].interactable =
                    !Busy && !ModalOpen && song.Charts.Any(c => (int)c.Slot == i);
            }

            foreach (var row in view.Rows)
            {
                var available = row.Entry.Charts.FirstOrDefault(c =>
                    (int)c.Slot == Array.IndexOf(difficultyOptions, selection.SelectedDifficulty)
                );

                if (row.Entry.Error != null || available?.Error != null)
                {
                    row.difficulty.text = "재생 불가";
                }
                else if (available == null)
                {
                    row.difficulty.text = "채보 없음";
                }
                else
                {
                    row.difficulty.text = selection.SelectedDifficulty.DisplayName;
                }
            }
        }

        private void SelectDifficulty(int value)
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            var song = selection.Library.Find(selection.SelectedSongId);

            if (song == null || !song.Charts.Any(c => (int)c.Slot == value))
            {
                return;
            }

            if (selection.SelectedDifficulty != difficultyOptions[value])
            {
                StopPreview();
            }

            selection.UpdateSelection(
                song.Id,
                difficultyOptions[value],
                selection.SelectedScrollMode,
                selection.SelectedMultiplier
            );
            view.difficultyList.SetValueWithoutNotify(value);
            view.RefreshDifficultyButtons();
            RefreshDetails(song);
        }

        private void Changed(int value)
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            RefreshSpeed();
            view.scrollModeList.SetValueWithoutNotify((int)ScrollMode.Constant);
            view.RefreshDifficultyButtons();
            preferences.Difficulty = selection.SelectedDifficulty.name;
            preferences.ScrollMode = ScrollMode.Constant;
            preferences.Multiplier = selection.SelectedMultiplier;
            preferences.Save();

            if (view.Rows.Count != 0)
            {
                SelectSong(selection.SelectedSongId);
            }
        }

        private void RefreshSpeed()
        {
            double value = selection.SelectedMultiplier;

            for (int i = 0; i < speedOptions.Length; i++)
            {
                if (Math.Abs(speedOptions[i] - value) < .0001)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                }
            }

            view.speedSlider.SetValueWithoutNotify((float)value);
            view.speedLabel.text = $"속도  {value:0.0}x";
        }

        private void SpeedChanged(float value)
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            double rounded = Math.Round(
                Math.Clamp((double)value, 1, 10),
                1,
                MidpointRounding.AwayFromZero
            );
            view.speedSlider.SetValueWithoutNotify((float)rounded);

            for (int i = 0; i < speedOptions.Length; i++)
            {
                if (Math.Abs(speedOptions[i] - rounded) < .0001)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                    selection.UpdateSelection(
                        selection.SelectedSongId,
                        selection.SelectedDifficulty,
                        ScrollMode.Constant,
                        rounded
                    );
                    Changed(i);

                    return;
                }
            }
        }

        private void TimingChanged(float value)
        {
            preferences.TimingOffsetMs = Mathf.RoundToInt(value);
            preferences.Save();
            RefreshTiming();
        }

        private void RefreshTiming() =>
            view.timingLabel.text = $"입력 타이밍 보정  {preferences.TimingOffsetMs:+0;-0;0} ms";

        private void SortChanged(int value)
        {
            RefreshLibrary();
            FocusSelected();
        }

        private void ShowAll()
        {
            favoritesOnly = false;
            RefreshLibrary();
            FocusSelected();
        }

        private void ShowFavorites()
        {
            favoritesOnly = true;
            RefreshLibrary();
            FocusSelected();
        }

        private void ToggleFavorite(string id)
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            preferences.ToggleFavorite(id);
            RefreshLibrary();
            FocusSelected();
        }

        private void FocusSelected()
        {
            if (!EventSystem.current)
            {
                return;
            }

            var row = view.Rows.Find(r => r.Entry.Id == selection.SelectedSongId);

            if (row)
            {
                EventSystem.current.SetSelectedGameObject(row.select.gameObject);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(view.allTab.gameObject);
            }
        }

        private async void TogglePreview()
        {
            if (Busy || ModalOpen || !view.previewButton.interactable)
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

            var request = new CancellationTokenSource();
            previewCancellation = request;
            view.previewButton.GetComponentInChildren<Text>().text = "불러오는 중…";
            PlayableSong song = null;
            try
            {
                var slot = (ChartSlot)Array.IndexOf(settings.difficulties, selection.SelectedDifficulty);
                song = await loader.LoadChartAsync(selection.SelectedSongId, slot, request.Token);
                if (previewCancellation != request)
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
                view.previewButton.GetComponentInChildren<Text>().text = "■ 멈추기";
            }
            catch (Exception)
            {
                if (previewCancellation == request)
                {
                    StopPreview();
                    view.previewButton.GetComponentInChildren<Text>().text = "미리듣기 실패 · 재시도";
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

            if (view && view.previewButton)
            {
                view.previewButton.GetComponentInChildren<Text>(true).text = "▶ 미리듣기";
            }

            previewSong?.Dispose();
            previewSong = null;
        }

        private async void Play()
        {
            if (Busy || !view.play.interactable || ModalOpen)
            {
                return;
            }

            var currentLifetime = lifetime;
            StopPreview();
            var songId = selection.SelectedSongId;
            var difficulty = selection.SelectedDifficulty;
            playbackFailure = null;
            SetLoading(true);

            try
            {
                await navigation.PlaySelectedAsync(currentLifetime.Token);
            }
            catch (OperationCanceledException)
            {
                if (this && lifetime == currentLifetime)
                {
                    SetLoading(false);
                    RefreshLibrary();
                    FocusSelected();
                }
            }
            catch (Exception e)
            {
                if (!this || lifetime != currentLifetime)
                {
                    return;
                }

                string message = "곡을 불러오지 못했어요.\n다시 시도해 주세요.";
                bool canRetry = true;
                switch (e)
                {
                    case FileNotFoundException:
                    case DirectoryNotFoundException:
                    case ProjectValidationException { FieldPath: "song.audio" }:
                        message = MissingAudioMessage;
                        canRetry = false;
                        break;
                    case ProjectValidationException:
                        message = InvalidChartMessage;
                        canRetry = false;
                        break;
                }

                playbackFailure = (songId, difficulty, message, canRetry);
                SetLoading(false);
                RefreshLibrary();
                if (view.play.interactable && EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(view.play.gameObject);
                }
                else
                {
                    FocusSelected();
                }
            }
        }

        private void SetLoading(bool value)
        {
            loading = value;
            view.SetInteractable(!value);
            view.play.interactable = !value;
            if (value)
            {
                loadingStartedAt = Time.unscaledTimeAsDouble;
                view.SetPlaybackStatus(LoadingCaptions[0]);
            }
        }

        private void RefreshPlaybackStatus(SongEntry song, ChartEntry chart)
        {
            if (playbackFailure is { } failure)
            {
                if (failure.SongId == song.Id && failure.Difficulty == selection.SelectedDifficulty)
                {
                    view.play.interactable &= failure.CanRetry;
                    string caption = null;
                    if (view.play.interactable)
                    {
                        caption = "다시 시도";
                    }

                    view.SetPlaybackStatus(caption, failure.Message);
                    return;
                }

                playbackFailure = null;
            }

            string error = chart?.Error ?? song.Error;
            if (error != null)
            {
                string message = "이 곡을 재생할 수 없어요.\n다른 곡을 선택해 주세요.";
                if (error.StartsWith("song.audio:", StringComparison.Ordinal))
                {
                    message = MissingAudioMessage;
                }

                view.SetPlaybackStatus(message: message);
                return;
            }

            view.SetPlaybackStatus();
        }

        private void ShowMode()
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            StopPreview();
            view.SetInteractable(false);
            view.play.interactable = false;
            RefreshMode();
            view.modePopup.SetActive(true);

            if (EventSystem.current)
            {
                if (HardModeSelected)
                {
                    EventSystem.current.SetSelectedGameObject(view.hardModeButton.gameObject);
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(view.normalModeButton.gameObject);
                }
            }
        }

        private void SelectNormalMode() => SelectMode(false);

        private void SelectHardMode() => SelectMode(true);

        private void SelectMode(bool hard)
        {
            if (Busy || !view.modePopup.activeSelf)
            {
                return;
            }

            HardModeSelected = hard;
            hardMode.Enabled = hard;
            hardMode.Save();
            RefreshMode();
            CloseMode();

            if (rules != null)
            {
                var snapshot = rules.Snapshot;

                if (hard)
                {
                    snapshot.Mode = RhythmDojo.Gameplay.PlayMode.Challenge;
                }
                else
                {
                    snapshot.Mode = RhythmDojo.Gameplay.PlayMode.Gauge;
                }

                rules.Update(snapshot);
                RefreshMode();
            }
        }

        private void RefreshMode()
        {
            if (HardModeSelected)
            {
                view.modeLabel.text = "모드: 하드";
            }
            else
            {
                view.modeLabel.text = "모드: 노말";
            }

            if (rules != null)
            {
                var snapshot = rules.Snapshot;
                HardModeSelected = snapshot.Mode == RhythmDojo.Gameplay.PlayMode.Challenge;

                if (HardModeSelected)
                {
                    view.modeLabel.text = "Challenge " + snapshot.ChallengePreset;
                }
                else
                {
                    view.modeLabel.text = "Gauge";
                }

                view.normalModeButton.GetComponentInChildren<Text>().text = "Gauge";
                view.hardModeButton.GetComponentInChildren<Text>().text = "Challenge";
            }

            if (!HardModeSelected)
            {
                view.normalModeButton.GetComponent<Image>().color = new Color(.48f, .32f, .18f);
            }
            else
            {
                view.normalModeButton.GetComponent<Image>().color = new Color(.27f, .15f, .10f);
            }

            if (HardModeSelected)
            {
                view.hardModeButton.GetComponent<Image>().color = new Color(.53f, .15f, .10f);
            }
            else
            {
                view.hardModeButton.GetComponent<Image>().color = new Color(.27f, .15f, .10f);
            }
            if (view.pixelTheme)
            {
                foreach (var button in new[] { view.normalModeButton, view.hardModeButton })
                {
                    var frame = button.GetComponentInChildren<PixelDojoFrameGraphic>();
                    frame.color = button.GetComponent<Image>().color;
                    button.GetComponent<Image>().color = Color.clear;
                }
            }
        }

        private void CloseMode()
        {
            if (Busy || !view.modePopup.activeSelf)
            {
                return;
            }

            view.modePopup.SetActive(false);
            view.SetInteractable(true);
            RefreshLibrary();

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.modeButton.gameObject);
            }
        }

        private void ShowSettings()
        {
            if (!Busy && !ModalOpen)
            {
                StopPreview();
                navigation.ShowSettings();
            }
        }

        private void ShowBack()
        {
            if (Busy || ModalOpen)
            {
                return;
            }

            StopPreview();
            view.SetInteractable(false);
            view.play.interactable = false;
            view.backPopup.SetActive(true);

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(view.cancelBack.gameObject);
            }
        }

        private void CancelBack()
        {
            if (Busy)
            {
                return;
            }

            view.backPopup.SetActive(false);
            view.SetInteractable(true);
            RefreshLibrary();
            FocusSelected();
        }

        private void ConfirmBack()
        {
            if (Busy)
            {
                return;
            }

            if (navigation is ITitleNavigation titleNavigation)
            {
                titleNavigation.ShowTitle();
            }
        }

        private void Unbind()
        {
            StopPreview();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;

            if (selection != null)
            {
                selection.Library.Changed -= RefreshLibrary;
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
            view.difficultyList.onValueChanged.RemoveListener(Changed);
            view.scrollModeList.onValueChanged.RemoveListener(Changed);
            view.multiplierList.onValueChanged.RemoveListener(Changed);
            view.sortList.onValueChanged.RemoveListener(SortChanged);
            view.speedSlider.onValueChanged.RemoveListener(SpeedChanged);
            view.timingOffset.onValueChanged.RemoveListener(TimingChanged);
            view.modeButton.onClick.RemoveListener(ShowMode);
            view.normalModeButton.onClick.RemoveListener(SelectNormalMode);
            view.hardModeButton.onClick.RemoveListener(SelectHardMode);
            view.cancelMode.onClick.RemoveListener(CloseMode);
            view.play.onClick.RemoveListener(Play);
            view.previewButton.onClick.RemoveListener(TogglePreview);
            view.settingsButton.onClick.RemoveListener(ShowSettings);
            view.back.onClick.RemoveListener(ShowBack);
            view.confirmBack.onClick.RemoveListener(ConfirmBack);
            view.cancelBack.onClick.RemoveListener(CancelBack);
            view.allTab.onClick.RemoveListener(ShowAll);
            view.favoritesTab.onClick.RemoveListener(ShowFavorites);
            view.ClearRows();
        }

        private void OnEnable()
        {
            if (selection == null || settings == null || preferences == null)
            {
                return;
            }

            for (int i = 0; i < speedOptions.Length; i++)
            {
                if (Math.Abs(speedOptions[i] - selection.SelectedMultiplier) < .0001)
                {
                    view.multiplierList.SetValueWithoutNotify(i);
                }
            }

            view.timingOffset.SetValueWithoutNotify(preferences.TimingOffsetMs);
            RefreshSpeed();
            RefreshTiming();
            RefreshMode();
        }

        private void OnDisable() => StopPreview();

        private void OnDestroy() => Unbind();

        public void ResumeAfterModal()
        {
            // Presentation can lease an unbound screen before composition initializes it.
            if (lifetime == null)
            {
                return;
            }

            RefreshMode();
            FocusSelected();

            var song = selection.Library.Find(selection.SelectedSongId);

            if (song != null)
            {
                RefreshDetails(song);
            }
        }
    }
}
