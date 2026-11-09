using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Presentation;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.Core
{
    public sealed class GameplayCompositionRoot : MonoBehaviour
    {
        private const double ResultTransitionDelaySeconds = 1;
        private const double FullComboResultTransitionDelaySeconds = ResultTransitionDelaySeconds * 2;

        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private RhythmGameController controller;

        [SerializeField]
        private SongClock clock;

        [SerializeField]
        private LaneKeyboardInput input;

        [SerializeField]
        private NotePresenter notes;

        [SerializeField]
        private PlayfieldPresenter playfield;

        [SerializeField]
        private RhythmHud hud;

        [SerializeField]
        private GameplayUiController ui;

        [SerializeField]
        private GameplayHitFeedback hits;
        private AppFlowController flow;
        private bool initialized;
        private bool returnPending;
        private bool recordSaved;
        private int? previousBestScore;
        private double resultTransitionTime = -1;
        private int maxCombo;
        private PlayRequest playRequest;
        private PlayableSong ownedSong;
        private GameModeDefinition runtimeMode;
        private GameplayPresentationSettings runtimePresentation;
        private GameplayPauseMenu pauseMenu;
        private CancellationTokenSource playbackLifetime;

        public bool NeedsAudioRecovery => playRequest?.Song.NeedsAudioRecovery ?? false;

        public void Validate(bool validateGeneratedViews = false)
        {
            if ((!settings || !controller) || !clock)
            {
                throw new InvalidOperationException(
                    "Gameplay composition references missing. Rebuild Gameplay."
                );
            }

            if ((!input || !notes) || !playfield)
            {
                throw new InvalidOperationException(
                    "Gameplay composition references missing. Rebuild Gameplay."
                );
            }

            if ((!hud || !ui) || !hits)
            {
                throw new InvalidOperationException(
                    "Gameplay composition references missing. Rebuild Gameplay."
                );
            }

            settings.ValidateOptions();
            clock.Validate();
            playfield.Validate();
            hud.Validate();
            ui.Validate();

            if (validateGeneratedViews)
            {
                notes.Validate(settings.catalog[0].Chart.ToChartData());
            }
            else
            {
                notes.Validate(null);
            }
        }

        private void Start()
        {
            if (!initialized)
            {
                flow = AppFlowController.Create(settings);
                ownedSong = SongAssetAdapter.Load(settings.catalog[0]);
                Initialize(
                    new PlayRequest(
                        ownedSong,
                        settings.defaultDifficulty,
                        ScrollMode.Constant,
                        1,
                        flow.Preferences.TimingOffsetMs,
                        null,
                        flow.Rules.Snapshot
                    ),
                    settings,
                    flow
                );
            }
        }

        public void Initialize(
            PlayRequest request,
            GameSettings gameSettings,
            AppFlowController appFlow
        )
        {
            if (initialized)
            {
                return;
            }

            settings = gameSettings;
            Validate();
            flow = appFlow;
            playRequest = request;

            var song = request.Song;
            song.Validate();
            playbackLifetime = new CancellationTokenSource();
            runtimeMode = Instantiate(song.Mode);
            runtimePresentation = Instantiate(settings.presentation);

            var mode = runtimeMode.ToRules();
            var chart = song.Chart;
            var judgments = request.Rules?.Timing ?? request.Difficulty.ToSettings();
            double lastEnd = 0;

            for (int i = 0; i < chart.Count; i++)
            {
                lastEnd = Math.Max(lastEnd, chart[i].EndTime);
            }

            // Keep every head/tail judgment window, but cap empty outros after the last note.
            double finalJudgmentTime =
                lastEnd + judgments.GoodWindow + RhythmSession.TimingTolerance * 2;
            double completionTime = Math.Max(chart.CompletionTime, song.AudioClip.length + song.AudioOffset);
            completionTime = Math.Min(completionTime, finalJudgmentTime + ResultTransitionDelaySeconds);
            chart = chart.WithCompletionTime(Math.Max(finalJudgmentTime, completionTime));

            var tempo = song.Tempo;
            var scroll = settings.scroll.CreateTimeline(
                request.ScrollMode,
                tempo,
                request.Multiplier
            );
            clock.Initialize(
                song.AudioClip,
                song.AudioOffset,
                settings.audio,
                request.TimingOffsetMs
            );
            clock.SetPlaybackReadiness(() => !song.NeedsAudioRecovery);
            song.AudioChanged += ReplaceAudio;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            input.Initialize(runtimeMode, song.ModeId);
            notes.Configure(runtimeMode, runtimePresentation);
            playfield.Build(runtimeMode, runtimePresentation);
            playfield.ConfigureMeasureLines(tempo, scroll, chart);

            string ruleLabel;

            if (request.Rules == null)
            {
                ruleLabel = request.Difficulty.DisplayName;
            }
            else
            {
                ruleLabel =
                    request.Difficulty.DisplayName
                    + " · "
                    + (
                        request.Rules.Profile switch
                        {
                            JudgmentProfile.Warmup => "준비운동",
                            _ => "대련",
                        }
                    )
                    + " · "
                    + (
                        request.Rules.Mode switch
                        {
                            RhythmDojo.Gameplay.PlayMode.Gauge => "Gauge",
                            _ => "Challenge " + request.Rules.ChallengePreset,
                        }
                    )
                    + " · "
                    + (
                        request.Rules.HoldEnd switch
                        {
                            HoldEndMode.Automatic => "롱노트 자동",
                            _ => "끝 입력 판정",
                        }
                    );
            }

            controller.Initialize(
                chart,
                judgments,
                mode,
                tempo,
                song.Title,
                ruleLabel,
                request.ScrollMode,
                request.Multiplier,
                scroll,
                input,
                clock,
                notes,
                request.HardModeRules,
                request.Rules
            );
            hud.Initialize(controller.ReadModel, runtimeMode, runtimePresentation, song.ModeId);
            hits.Initialize(
                controller.ReadModel,
                runtimeMode,
                Camera.main,
                hud.GetComponent<Canvas>()
            );
            controller.ReadModel.Judged += TrackCombo;
            controller.ReadModel.ResetOccurred += ResetRecord;
            pauseMenu = hud.GetComponent<GameplayPauseMenu>();
            if (pauseMenu)
            {
                pauseMenu.Initialize(controller, flow, this);
                controller.FocusLost = pauseMenu.PauseForFocusLoss;
            }
            input.ReturnRequested += HandleGameplayEscape;
            ui.Initialize(controller.ReadModel, ReturnToSelection, playfield.SetRailOpacity);
            initialized = true;
            input.Activate();

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            // A reset can occur after loading the song but before this scene is initialized.
            if (song.NeedsAudioRecovery)
            {
                AudioConfigurationChanged(false);
            }
        }

        private void Update()
        {
            if (returnPending)
            {
                CompleteReturn();

                return;
            }
        }

        private void AudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!initialized || playRequest == null)
            {
                return;
            }

            if (controller.IsPaused)
            {
                return;
            }

            if (!playRequest.Song.NeedsAudioRecovery)
            {
                return;
            }

            if (controller.Session.State == SessionState.Playing || controller.Session.State == SessionState.Ready)
            {
                controller.PauseSession();
                if (pauseMenu)
                {
                    pauseMenu.Open();
                }
            }
        }

        private void ReplaceAudio(AudioClip clip)
        {
            clock.ReplaceAudioClip(clip);
        }

        public void SetScrollMultiplier(double multiplier)
        {
            if (!initialized || !controller.IsPaused)
            {
                return;
            }

            var tempo = playRequest.Song.Tempo;
            var scroll = settings.scroll.CreateTimeline(playRequest.ScrollMode, tempo, multiplier);

            notes.SetScrollTimeline(scroll);
            playfield.ConfigureMeasureLines(tempo, scroll, controller.Chart);
            controller.RefreshScrollMultiplier(multiplier);
        }

        public async Task PreparePlaybackAsync(CancellationToken cancellationToken)
        {
            if (!initialized || returnPending)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, playbackLifetime.Token);
            while (playRequest.Song.NeedsAudioRecovery)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                await playRequest.Song.RecoverAudioAsync(cancellation.Token);
                if (playRequest.Song.NeedsAudioRecovery)
                {
                    // A device may reset again while decoding. Yield so Exit can cancel retries.
                    await Task.Yield();
                }
            }
            cancellation.Token.ThrowIfCancellationRequested();
        }

        private void TrackCombo(JudgmentEvent result)
        {
            maxCombo = Math.Max(maxCombo, controller.ReadModel.Snapshot.Session.Combo);
        }

        private void ResetRecord()
        {
            resultTransitionTime = -1;
            maxCombo = 0;
            recordSaved = false;
            previousBestScore = null;
        }

        private void LateUpdate()
        {
            SaveRecord();

            if (!initialized)
            {
                return;
            }

            var readModel = controller.ReadModel;
            if (readModel == null)
            {
                return;
            }

            var gameplay = readModel.Snapshot;
            playfield.RenderMeasureLines(gameplay.Session.State, gameplay.SongTime);

            if (!flow || flow.Transitioning)
            {
                return;
            }

            if (playRequest == null || controller.IsPaused)
            {
                return;
            }

            var snapshot = gameplay.Session;

            if (snapshot.State != SessionState.Completed && snapshot.State != SessionState.GameOver)
            {
                resultTransitionTime = -1;
                return;
            }

            // Let the HUD display the outcome before unloading Gameplay.
            // Use real time because failure pauses the song clock.
            if (resultTransitionTime < 0)
            {
                double delay = ResultTransitionDelaySeconds;
                if (snapshot.IsFullCombo)
                {
                    delay = FullComboResultTransitionDelaySeconds;
                }

                resultTransitionTime = Time.realtimeSinceStartupAsDouble + delay;
                return;
            }

            if (Time.realtimeSinceStartupAsDouble < resultTransitionTime)
            {
                return;
            }

            var laneLabels = new string[runtimeMode.LaneCount];

            for (int i = 0; i < laneLabels.Length; i++)
            {
                laneLabels[i] = PlayKeyBindings.GetLabel(runtimeMode, playRequest.Song.ModeId, i);
            }

            var result = new PlayResult(
                playRequest.Song.Title,
                playRequest.Song.Artist,
                playRequest.Difficulty.DisplayName,
                flow.Library.Find(playRequest.Song.SongId)?.Cover,
                snapshot,
                maxCombo,
                controller.Chart,
                playRequest.Song.Tempo,
                laneLabels,
                controller.Session
            )
            {
                PreviousBestScore = previousBestScore,
                CanCompareScore = recordSaved && snapshot.TotalNotes > 0,
            };
            ShutdownGameplay();
            flow.ShowResults(result, gameplay.ScrollMultiplier);
        }

        private void SaveRecord()
        {
            if ((!initialized || recordSaved) || !flow)
            {
                return;
            }

            if (flow.IsEditorPlaytest || playRequest == null)
            {
                return;
            }

            var snapshot = controller.ReadModel.Snapshot.Session;

            if (snapshot.State != SessionState.Completed)
            {
                return;
            }

            var rules = playRequest.Rules;
            var songId = playRequest.Song.SongId;
            var recordKey = playRequest.Difficulty.name;
            if (rules != null && flow.Preferences is SelectionPreferences records)
            {
                recordKey = RecordIdentity.Rules(playRequest.Song.ChartId, playRequest.Song.Revision, rules);
                if (snapshot.Failure.Failed)
                {
                    previousBestScore = records.FindFailedRecord(songId, recordKey)?.score;
                }
                else
                {
                    previousBestScore = records.FindRecord(songId, recordKey)?.score;
                }

                records.SaveCompleted(
                    songId,
                    recordKey,
                    snapshot,
                    maxCombo,
                    rules
                );
            }
            else
            {
                if (!snapshot.Failure.Failed)
                {
                    previousBestScore = flow.Preferences.FindRecord(songId, recordKey)?.score;
                }
                else if (flow.Preferences is SelectionPreferences failedRecords)
                {
                    previousBestScore = failedRecords.FindFailedRecord(songId, recordKey)?.score;
                }

                flow.Preferences.SaveCompleted(
                    songId,
                    recordKey,
                    snapshot,
                    maxCombo
                );
            }

            recordSaved = true;
        }

        private void UnbindRecord()
        {
            if (controller && controller.ReadModel != null)
            {
                controller.ReadModel.Judged -= TrackCombo;
                controller.ReadModel.ResetOccurred -= ResetRecord;
            }
        }

        private void HandleGameplayEscape()
        {
            if (pauseMenu && pauseMenu.HandleEscape())
            {
                return;
            }

            ReturnToSelection();
        }

        public void ReturnToSelection()
        {
            if (!initialized || flow && flow.Transitioning)
            {
                return;
            }

            // Escape is delivered inside an InputAction callback. Dispose its actions only
            // after Dynamic dispatch has completed, in the composition root's Update.
            returnPending = true;
        }

        private void CompleteReturn()
        {
            returnPending = false;
            SaveRecord();
            ShutdownGameplay();
            flow.ReturnFromGameplay();
        }

        private void ShutdownGameplay()
        {
            ReleasePlaybackRecovery();
            UnbindRecord();
            hits.Unbind();
            controller.Shutdown();
            input.Shutdown();
            hud.Unbind();
            initialized = false;
            input.ReturnRequested -= HandleGameplayEscape;
            ui.Unbind();
        }

        private void OnDestroy()
        {
            ReleasePlaybackRecovery();
            if (input)
            {
                input.ReturnRequested -= HandleGameplayEscape;
            }

            if (ui)
            {
                ui.Unbind();
            }

            UnbindRecord();

            if (hits)
            {
                hits.Unbind();
            }

            if (controller)
            {
                controller.Shutdown();
            }

            ownedSong?.Dispose();

            if (runtimeMode)
            {
                Destroy(runtimeMode);
            }

            if (runtimePresentation)
            {
                Destroy(runtimePresentation);
            }
        }

        private void ReleasePlaybackRecovery()
        {
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;
            if (playRequest != null)
            {
                playRequest.Song.AudioChanged -= ReplaceAudio;
            }
            playbackLifetime?.Cancel();
            playbackLifetime?.Dispose();
            playbackLifetime = null;
            if (clock)
            {
                clock.SetPlaybackReadiness(null);
            }
        }
    }
}
