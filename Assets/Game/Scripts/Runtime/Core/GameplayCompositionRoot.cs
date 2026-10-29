using System;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmDojo.Core
{
    public sealed class GameplayCompositionRoot : MonoBehaviour
    {
        private const double ResultTransitionDelaySeconds = 1;

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
        private double resultTransitionTime = -1;
        private int maxCombo;
        private PlayRequest playRequest;
        private PlayableSong ownedSong;
        private GameModeDefinition runtimeMode;
        private GameplayPresentationSettings runtimePresentation;

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
            input.Initialize(runtimeMode);
            notes.Configure(runtimeMode, runtimePresentation);
            playfield.Build(runtimeMode, runtimePresentation);

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
            hud.Initialize(controller.ReadModel, runtimeMode, runtimePresentation);
            hits.Initialize(
                controller.ReadModel,
                runtimeMode,
                Camera.main,
                hud.GetComponent<Canvas>()
            );
            controller.ReadModel.Judged += TrackCombo;
            controller.ReadModel.ResetOccurred += ResetRecord;
            input.ReturnRequested += ReturnToSelection;
            ui.Initialize(controller.ReadModel, ReturnToSelection);
            initialized = true;
            input.Activate();

            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
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

        private void TrackCombo(JudgmentEvent result)
        {
            maxCombo = Math.Max(maxCombo, controller.ReadModel.Snapshot.Session.Combo);
        }

        private void ResetRecord()
        {
            resultTransitionTime = -1;
            maxCombo = 0;
            recordSaved = false;
        }

        private void LateUpdate()
        {
            SaveRecord();

            if (!initialized || !flow || flow.Transitioning)
            {
                return;
            }

            if (playRequest == null)
            {
                return;
            }

            var snapshot = controller.ReadModel.Snapshot.Session;

            if (snapshot.State != SessionState.Completed && snapshot.State != SessionState.GameOver)
            {
                resultTransitionTime = -1;
                return;
            }

            // Let the HUD display GAME CLEAR / GAME OVER before unloading Gameplay.
            // Use real time because failure pauses the song clock.
            if (resultTransitionTime < 0)
            {
                resultTransitionTime = Time.realtimeSinceStartupAsDouble + ResultTransitionDelaySeconds;
                return;
            }

            if (Time.realtimeSinceStartupAsDouble < resultTransitionTime)
            {
                return;
            }

            var laneLabels = new string[runtimeMode.LaneCount];

            for (int i = 0; i < laneLabels.Length; i++)
            {
                laneLabels[i] = runtimeMode.GetLane(i).displayName;
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
            );
            ShutdownGameplay();
            flow.ShowResults(result);
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

            if (
                playRequest.Rules != null
                && flow.Preferences is RhythmDojo.Services.SelectionPreferences records
            )
            {
                records.SaveCompleted(
                    playRequest.Song.SongId,
                    RecordIdentity.Rules(
                        playRequest.Song.ChartId,
                        playRequest.Song.Revision,
                        playRequest.Rules
                    ),
                    snapshot,
                    maxCombo,
                    playRequest.Rules
                );
            }
            else
            {
                flow.Preferences.SaveCompleted(
                    playRequest.Song.SongId,
                    playRequest.Difficulty.name,
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
            UnbindRecord();
            hits.Unbind();
            controller.Shutdown();
            input.Shutdown();
            hud.Unbind();
            initialized = false;
            input.ReturnRequested -= ReturnToSelection;
            ui.Unbind();
        }

        private void OnDestroy()
        {
            if (input)
            {
                input.ReturnRequested -= ReturnToSelection;
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
    }
}
