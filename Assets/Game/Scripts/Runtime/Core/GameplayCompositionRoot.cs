using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Core
{
    public sealed class GameplayCompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private RhythmGameController controller;
        [SerializeField] private SongClock clock;
        [SerializeField] private LaneKeyboardInput input;
        [SerializeField] private NotePresenter notes;
        [SerializeField] private PlayfieldPresenter playfield;
        [SerializeField] private RhythmHud hud;
        [SerializeField] private GameplayUiController ui;
        [SerializeField] private GameplayHitFeedback hits;
        private AppFlowController flow;
        private bool initialized;
        private bool returnPending;
        private bool recordSaved;
        private int maxCombo;
        private PlayRequest playRequest;
        private PlayableSong ownedSong;
        private GameModeDefinition runtimeMode;
        private GameplayPresentationSettings runtimePresentation;
        public void Validate(bool validateGeneratedViews = false)
        {
            if (!settings || !controller || !clock || !input || !notes || !playfield || !hud || !ui || !hits)
                throw new InvalidOperationException("Gameplay composition references missing. Rebuild Gameplay.");
            settings.ValidateOptions(); clock.Validate(); playfield.Validate(); hud.Validate(); ui.Validate();
            notes.Validate(validateGeneratedViews ? settings.catalog[0].Chart.ToChartData() : null);
        }
        private void Start()
        {
            if (!initialized)
            {
                flow = AppFlowController.Create(settings);
                ownedSong = SongAssetAdapter.Load(settings.catalog[0]);
                Initialize(new PlayRequest(ownedSong, settings.defaultDifficulty, ScrollMode.Constant, 1), settings, flow);
            }
        }
        public void Initialize(PlayRequest request, GameSettings gameSettings, AppFlowController appFlow)
        {
            if (initialized) return;
            settings = gameSettings; Validate(); flow = appFlow;
            playRequest = request;
            var song = request.Song; song.Validate();
            runtimeMode = Instantiate(song.Mode);
            runtimePresentation = Instantiate(settings.presentation);
            var mode = runtimeMode.ToRules(); var chart = song.Chart; var judgments = request.Difficulty.ToSettings();
            double lastEnd = 0;
            for (int i = 0; i < chart.Count; i++) lastEnd = Math.Max(lastEnd, chart[i].EndTime);
            chart = chart.WithCompletionTime(Math.Max(chart.CompletionTime, Math.Max(song.AudioClip.length + song.AudioOffset,
                lastEnd + judgments.GoodWindow + RhythmSession.TimingTolerance * 2)));
            var tempo = song.Tempo;
            var scroll = settings.scroll.CreateTimeline(request.ScrollMode, tempo, request.Multiplier);
            clock.Initialize(song.AudioClip, song.AudioOffset, settings.audio, request.TimingOffsetMs);
            input.Initialize(runtimeMode); notes.Configure(runtimeMode, runtimePresentation);
            playfield.Build(runtimeMode, runtimePresentation);
            controller.Initialize(chart, judgments, mode, tempo, song.Title, request.Difficulty.DisplayName,
                request.ScrollMode, request.Multiplier, scroll, input, clock, notes);
            hud.Initialize(controller.ReadModel, runtimeMode, runtimePresentation);
            hits.Initialize(controller.ReadModel, runtimeMode, Camera.main, hud.GetComponent<Canvas>());
            controller.ReadModel.Judged += TrackCombo;
            controller.ReadModel.ResetOccurred += ResetRecord;
            input.ReturnRequested += ReturnToSelection; ui.Initialize(controller.ReadModel, ReturnToSelection);
            initialized = true; input.Activate();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        private void Update()
        {
            if (returnPending) { CompleteReturn(); return; }

        }
        private void TrackCombo(JudgmentEvent result) => maxCombo = Math.Max(maxCombo, controller.ReadModel.Snapshot.Session.Combo);
        private void ResetRecord() { maxCombo = 0; recordSaved = false; }
        private void LateUpdate() => SaveRecord();
        private void SaveRecord()
        {
            if (!initialized || recordSaved || !flow || flow.IsEditorPlaytest || playRequest == null) return;
            var snapshot = controller.ReadModel.Snapshot.Session;
            if (snapshot.State != SessionState.Completed) return;
            flow.Preferences.SaveCompleted(playRequest.Song.SongId, playRequest.Difficulty.name, snapshot, maxCombo);
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
            if (!initialized || (flow && flow.Transitioning)) return;
            // Escape is delivered inside an InputAction callback. Dispose its actions only
            // after Dynamic dispatch has completed, in the composition root's Update.
            returnPending = true;
        }
        private void CompleteReturn()
        {
            returnPending = false;
            SaveRecord(); UnbindRecord();
            hits.Unbind(); controller.Shutdown(); input.Shutdown(); hud.Unbind(); initialized = false;
            input.ReturnRequested -= ReturnToSelection; ui.Unbind();
            flow.ReturnFromGameplay();
        }
        private void OnDestroy()
        {
            if (input) input.ReturnRequested -= ReturnToSelection;
            if (ui) ui.Unbind();
            UnbindRecord();
            if (hits) hits.Unbind();
            if (controller) controller.Shutdown();
            ownedSong?.Dispose();
            if (runtimeMode) Destroy(runtimeMode);
            if (runtimePresentation) Destroy(runtimePresentation);
        }
    }
}

