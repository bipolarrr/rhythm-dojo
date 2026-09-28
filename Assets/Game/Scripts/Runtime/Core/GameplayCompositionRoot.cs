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
        private AppFlowController flow;
        private bool initialized;
        private bool returnPending;
        private PlayableSong ownedSong;
        private GameModeDefinition runtimeMode;
        private GameplayPresentationSettings runtimePresentation;
        public void Validate(bool validateGeneratedViews = false)
        {
            if (!settings || !controller || !clock || !input || !notes || !playfield || !hud || !ui)
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
            clock.Initialize(song.AudioClip, song.AudioOffset, settings.audio);
            input.Initialize(runtimeMode); notes.Configure(runtimeMode, runtimePresentation);
            playfield.Build(runtimeMode, runtimePresentation);
            controller.Initialize(chart, judgments, mode, tempo, song.Title, request.Difficulty.DisplayName,
                request.ScrollMode, request.Multiplier, scroll, input, clock, notes);
            hud.Initialize(controller.ReadModel, runtimeMode, runtimePresentation);
            input.ReturnRequested += ReturnToSelection; ui.Initialize(controller.ReadModel, ReturnToSelection);
            initialized = true; input.Activate();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        private void Update()
        {
            if (returnPending) { CompleteReturn(); return; }

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
            controller.Shutdown(); input.Shutdown(); hud.Unbind(); initialized = false;
            input.ReturnRequested -= ReturnToSelection; ui.Unbind();
            flow.ReturnFromGameplay();
        }
        private void OnDestroy()
        {
            if (input) input.ReturnRequested -= ReturnToSelection;
            if (ui) ui.Unbind();
            if (controller) controller.Shutdown();
            ownedSong?.Dispose();
            if (runtimeMode) Destroy(runtimeMode);
            if (runtimePresentation) Destroy(runtimePresentation);
        }
    }
}

