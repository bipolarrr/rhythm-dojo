using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    // Only the explicit development diagnostic scene contains this component.
    public sealed partial class RemediationPlayerProbe : MonoBehaviour
    {
        private const string TitlePath = "Assets/Game/Scenes/Title.unity";
        private const string SecondModeId = "RemediationSecondFourLane";
        private const string SongId = "remediation-external-wave";

        [SerializeField] private GameSettings settings;
        private AppFlowController flow;
        private GameModeDefinition mode;
        private TemporaryWaveFile wave;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior previousBackground;
        private AudioConfiguration originalAudio;
        private string phase;
        private string output;
        private string logFailure;
        private Report report;
        private PreferenceBackup backup;
        private bool initializedInput;
        private double started;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            UnityEngine.Application.runInBackground = true;
            started = Time.realtimeSinceStartupAsDouble;
            phase = Argument("-remediationPhase", "exercise");
            output = Path.GetFullPath(Argument("-remediationOutput",
                Path.Combine(UnityEngine.Application.dataPath, "../remediation-evidence")));
            Directory.CreateDirectory(output);
            report = new Report
            {
                phase = phase,
                unity = UnityEngine.Application.unityVersion,
                platform = UnityEngine.Application.platform.ToString(),
                editor = UnityEngine.Application.isEditor,
                startedUtc = DateTime.UtcNow.ToString("O"),
            };
            originalAudio = AudioSettings.GetConfiguration();
            UnityEngine.Application.logMessageReceived += ReceiveLog;
            yield return RunChecked(Execute());

            try
            {
                Cleanup();
            }
            catch (Exception exception)
            {
                report.success = false;
                report.error += "\nCleanup: " + exception;
            }
            // Destroy callbacks run at the end of the frame and remain part of validation.
            yield return null;
            if (logFailure != null)
            {
                report.success = false;
                report.error += "\nUnity error during execution or cleanup: " + logFailure;
            }
            try
            {
                if (!report.success || phase != "exercise")
                {
                    RestorePreferences();
                }
            }
            catch (Exception exception)
            {
                report.success = false;
                report.error += "\nPreference restoration: " + exception;
            }
            UnityEngine.Application.logMessageReceived -= ReceiveLog;
            report.finishedUtc = DateTime.UtcNow.ToString("O");
            report.duration = Time.realtimeSinceStartupAsDouble - started;
            report.step = "finished";
            Flush();
            int exitCode = 1;
            if (report.success)
            {
                exitCode = 0;
            }
            UnityEngine.Application.Quit(exitCode);
        }

        private IEnumerator Execute()
        {
            Require(!UnityEngine.Application.isEditor, "Diagnostic must run in a standalone player.");
            if (phase == "restore")
            {
                LoadBackup();
                Step("restore", "Restore saved preferences and probe-owned editor draft only.");
                yield break;
            }
            Require(phase == "exercise" || phase == "persistence", "Unknown diagnostic phase.");
            if (phase == "exercise")
            {
                BackupPreferences();
            }
            else
            {
                LoadBackup();
                Require(!backup.restored, "Persistence requires an unfinished exercise backup.");
            }

            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            initializedInput = true;
            mode = Object.Instantiate(settings.catalog[0].Mode);
            mode.name = SecondModeId;
            if (phase == "persistence")
            {
                Require(PlayKeyBindings.GetKey(settings.catalog[0].Mode,
                    settings.catalog[0].Mode.name, 0) == Key.A, "Default mode key persisted across processes.");
                Require(PlayKeyBindings.GetKey(mode, SecondModeId, 0) == Key.Q,
                    "Second mode key persisted independently across processes.");
                Step("persisted-keys", "Default=A; second four-lane mode=Q, before clearing any preferences.");
            }

            SceneManager.LoadSceneAsync(TitlePath);
            yield return Wait(() => SceneManager.GetActiveScene().path == TitlePath, "Title load");
            yield return null;
            flow = Object.FindFirstObjectByType<AppFlowController>();
            Require(flow, "Title creates AppFlow.");
            yield return Capture("title");
            yield return OpenTitleMenu();
            yield return Click(MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View.settings);
            yield return Wait(() => Object.FindFirstObjectByType<SettingsView>(), "Title settings");
            var view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.keySettingsTab);
            if (phase == "exercise")
            {
                yield return ChangeKey(view, Key.A);
            }
            Require(view.keyBindingButtons[0].GetComponentInChildren<Text>().text == "A",
                "Title settings display default mode A.");
            yield return Click(view.back);
            Step("title-settings", "Opened settings with queued pointer input; key persisted and Back restored title.");
            if (phase == "persistence")
            {
                yield return EnterSelection();
                RegisterWave();
                yield return SelectWave();
                yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
                yield return Wait(() => Object.FindFirstObjectByType<SettingsView>(), "Selected mode settings");
                view = Object.FindFirstObjectByType<SettingsView>();
                yield return Click(view.keySettingsTab);
                Require(view.keyBindingButtons[0].GetComponentInChildren<Text>().text == "Q",
                    "Reloaded selected mode UI displays Q.");
                yield return Click(view.back);
                CheckSelectionCleanup();
                Step("persistence-ui", "Both mode settings survived a separate process launch.");
                yield break;
            }

            yield return VerifyTrainingModes();
            yield return EditorRoundTrip();
            yield return EnterSelection();
            RegisterWave();
            yield return SelectWave();
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Wait(() => Object.FindFirstObjectByType<SettingsView>(), "Selected mode settings");
            view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.keySettingsTab);
            yield return ChangeKey(view, Key.Q);
            Require(PlayKeyBindings.GetKey(settings.catalog[0].Mode,
                settings.catalog[0].Mode.name, 0) == Key.A, "Second four-lane mode leaves default mode A unchanged.");
            yield return Capture("mode-settings");
            yield return Click(view.back);
            yield return ExercisePlayback();
            yield return ReturnToTitle();
            Step("exercise-complete", "Mode keys remain saved for the next process; original preferences stay in backup.");
        }

        private void RegisterWave()
        {
            wave = new TemporaryWaveFile(90, 0);
            var document = SongAssetAdapter.ToDocument(settings.catalog[0]);
            document.SongId = SongId;
            document.Title = "Remediation external WAV";
            document.ModeId = SecondModeId;
            document.AudioId = wave.Path;
            document.AudioOffset = .125;
            document.Notes = new[] { NoteData.Tap(0, 60), NoteData.Tap(1, 70) };
            document.CompletionTime = 74;
            var loader = new DocumentSongLoader(_ => mode, ChartEditorCompositionRoot.LoadAudio);
            flow.Library.Register(new WaveProvider(document, mode, loader));
        }

        private IEnumerator SelectWave()
        {
            yield return OpenSelectionLibrary();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            var row = view.Rows.Single(item => item.Entry.Id == SongId);
            EventSystem.current.SetSelectedGameObject(row.select.gameObject);
            yield return Click(row.select);
            Require(flow.Selection.SelectedSongId == SongId, "Generated external WAV selected.");
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().closeLibrary);
        }

        private IEnumerator ExercisePlayback()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Wait(() => Object.FindFirstObjectByType<RhythmGameController>(), "External WAV gameplay");
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var menu = Object.FindFirstObjectByType<GameplayPauseMenu>();
            Require(flow.CurrentRequest.Song.ModeId == SecondModeId, "Original mode ID reaches gameplay.");
            Require(Object.FindFirstObjectByType<GameplayHudView>().lanes.laneRoot.GetChild(0)
                .GetComponentInChildren<Text>().text == "Q", "HUD uses the selected mode key.");
            var originalClip = flow.CurrentRequest.Song.AudioClip;
            yield return Press(Key.Space);
            yield return Wait(() => game.Clock.Running && game.Clock.SongTime > .5, "Playback start");
            yield return Press(Key.Escape);
            Require(game.IsPaused, "Esc pauses gameplay.");
            double frozen = game.Clock.SongTime;
            flow.ShowGameplaySettings();
            Require(!Object.FindFirstObjectByType<SettingsView>(), "Paused settings are blocked.");
            Require(!flow.ApplyBufferSize(), "Gameplay cannot apply audio settings.");
            // Device recovery is independent of settings access and remains supported during pause.
            Require(AudioSettings.Reset(AudioSettings.GetConfiguration()), "Simulated device reset.");
            report.requestedBuffer = flow.Audio.RequestedBufferSize;
            var configuration = AudioSettings.GetConfiguration();
            report.appliedBuffer = configuration.dspBufferSize;
            report.sampleRate = configuration.sampleRate;
            foreach (var measurement in flow.Audio.ClockRateMeasurements)
            {
                report.clockMeasurements.Add(new Measurement
                {
                    elapsed = measurement.ElapsedSeconds,
                    lower = measurement.LowerBound,
                    upper = measurement.UpperBound,
                    buffer = measurement.BufferSize,
                    rate = measurement.SampleRate,
                    state = measurement.State.ToString(),
                });
            }
            yield return Capture("paused-audio-settings");
            yield return Click(menu.resumeButton);
            yield return Wait(() => !game.IsPaused && game.Clock.Running,
                "Recovered scheduled resume", 15);
            Require(flow.CurrentRequest.Song.AudioClip != originalClip, "External clip decoded again after reset.");
            Require(!originalClip, "Previous external lease released after replacement.");
            var samples = new float[1024];
            Require(flow.CurrentRequest.Song.AudioClip.GetData(samples, 0), "Recovered clip sample data readable.");
            report.clipPeak = Peak(samples);
            Require(report.clipPeak > .1, "Recovered decoded samples retain the generated tone.");
            var source = ((Component)game.Clock).GetComponent<AudioSource>();
            Require(source.isPlaying, "Recovered AudioSource is playing.");
            for (int i = 0; i < 20; i++)
            {
                AudioListener.GetOutputData(samples, 0);
                report.mixerPeak = Math.Max(report.mixerPeak, Peak(samples));
                yield return null;
            }
            Require(report.mixerPeak > .001, "Recovered tone reaches the mixer.");
            yield return MeasureAlignment(game, source);
            yield return Capture("recovered-gameplay");
            Step("loopback-ready", "Recovered external 440 Hz tone plays for 20 seconds for independent OS loopback capture.");
            yield return Delay(20);
            Require(game.Clock.Running && source.isPlaying, "Tone remained active throughout loopback window.");
            yield return Press(Key.Escape);
            Require(game.IsPaused, "Second pause accepted.");
            yield return Click(menu.restartButton);
            yield return Wait(() => !game.IsPaused && game.Clock.Running, "Restart");
            Require(game.Clock.SongTime < 1, "Restart resets song time.");
            yield return Press(Key.Escape);
            yield return Click(menu.exitButton);
            yield return Wait(() => Object.FindFirstObjectByType<SongSelectionScreen>(), "Pause exit");
            Require(flow.LastResult == null, "Pause exit creates no Results.");
            CheckSelectionCleanup();
            Step("pause-resume-restart-exit", "Real external loader, reset/recovery, scheduled resume, restart and exit passed.");

            yield return SelectWave();
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Wait(() => Object.FindFirstObjectByType<RhythmGameController>(), "Results gameplay");
            yield return null;
            game = Object.FindFirstObjectByType<RhythmGameController>();
            yield return Press(Key.Space);
            yield return Wait(() => game.Clock.Running, "Results playback start");
            // This is a navigation/build-scene check; full real-time completion is covered by PupaResultsFlowTests.
            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return Wait(() => Object.FindFirstObjectByType<ResultsScreen>(), "Built Results scene");
            Require(flow.LastResult.LaneLabels[0] == "Q", "Results retains selected mode label.");
            yield return Capture("results");
            yield return Click(Object.FindFirstObjectByType<ResultsView>().backButton);
            yield return Wait(() => Object.FindFirstObjectByType<SongSelectionScreen>(), "Results Back");
            CheckSelectionCleanup();
            Step("results-navigation", "Explicit session advance used only to exercise the built Results scene and Back.");
        }

        private IEnumerator MeasureAlignment(RhythmGameController game, AudioSource source)
        {
            double previous = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                double expected = game.Clock.SongTime - flow.CurrentRequest.Song.AudioOffset;
                double actual = source.timeSamples / (double)source.clip.frequency;
                AudioSettings.GetDSPBufferSize(out int block, out int count);
                double uncertainty = 2.0 * block / AudioSettings.outputSampleRate
                    + now - previous + 1.0 / source.clip.frequency;
                report.alignment.Add(new Alignment
                {
                    audio = actual,
                    logicalAudio = expected,
                    error = actual - expected,
                    uncertainty = uncertainty,
                    block = block,
                    buffers = count,
                });
                Require(Math.Abs(actual - expected) <= uncertainty,
                    "Audio/song alignment remains inside DSP-block plus observation-frame uncertainty.");
                previous = now;
            }
        }

        private IEnumerator SubmitMode()
        {
            var screen = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).GetComponent<MainMenuScreen>();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(screen.gameObject);
            screen.OnSubmit(null);
            yield return null;
        }

        private IEnumerator EditorRoundTrip()
        {
            var view = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View;
            yield return Click(view.modeSelection.next);
            yield return Wait(() => !view.modeSelection.IsMoving, "Travel to Challenge");
            yield return Click(view.modeSelection.next);
            yield return Wait(() => !view.modeSelection.IsMoving, "Travel to scribing room");
            Require(view.modeSelection.SelectedIndex == 3, "Fourth mode is the scribing room.");
            yield return Capture("scribing-room");
            yield return SubmitMode();
            yield return Wait(() => Object.FindFirstObjectByType<ChartEditorMenu>(), "Editor load");
            yield return Wait(() => Object.FindFirstObjectByType<ChartEditorMenu>().IsInitialized, "Editor initialize");
            var screen = Object.FindFirstObjectByType<ChartEditorScreen>();
            string id = screen.Session.Snapshot.Id.ToString("D");
            string folder = OwnedProjectFolder(id);
            Require(!Directory.Exists(folder), "Generated editor project must not already exist.");
            backup.ownedProjectIds.Add(id);
            SaveBackup();
            var workspace = Object.FindFirstObjectByType<WorkspaceEditor>();
            Require(workspace && workspace.view, "Scribing opens the current authored workspace.");
            yield return Capture("chart-editor");
            yield return Click(workspace.view.menus[1].trigger);
            workspace.view.title.text = "Scene route verification";
            yield return Click(workspace.view.actions.Single(binding => binding.action == WorkspaceAction.ApplySettings).button);
            yield return Wait(() => workspace.HasUnsavedChanges, "Editor draft modification");
            yield return Click(workspace.view.menus[0].trigger);
            yield return Click(workspace.view.actions.Single(binding => binding.action == WorkspaceAction.Exit).button);
            yield return Wait(() => workspace.view.exitPopup.activeSelf, "Editor exit confirmation");
            yield return Click(workspace.view.actions.Single(binding => binding.action == WorkspaceAction.SaveExit).button);
            yield return Wait(() => SceneManager.GetActiveScene().path == TitlePath, "Editor save and exit");
            Require(File.Exists(Path.Combine(folder, FileSongProjectStore.FileName)),
                "Editor Back saved its new draft through the real store.");
            CleanupProjects();
            Step("editor-round-trip", "File menu save and exit verified; only the new probe-owned project/workspace files and empty folder removed.");
            yield return OpenTitleMenu();
        }

        private IEnumerator OpenTitleMenu()
        {
            yield return Wait(() => MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()), "Title view");
            yield return null;
            var view = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View;
            if (!view.modeSelection)
            {
                yield return Wait(() => view.logo.IsInteractable(), "Title return animation completion");
                yield return Click(view.logo);
                yield return Wait(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath,
                    "Mode selection entry");
                yield return Wait(() => !SceneManager.GetSceneByPath(TitlePath).isLoaded, "Title cleanup");
                yield return null;
                view = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View;
            }
            Require(view.menuPanel.blocksRaycasts, "Expanded title menu receives pointer input.");
        }

        private IEnumerator EnterSelection()
        {
            yield return Capture("practice-mode");
            yield return SubmitMode();
            yield return Wait(() => SceneManager.GetActiveScene().path == AppFlowController.SelectionPath,
                "Title entrance");
            yield return Wait(() => !DojoEntranceTransition.Active, "Door completion");
            yield return Wait(() => !flow.Transitioning, "Selection activation cleanup");
            CheckSelectionCleanup();
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            Require(view.practice.gameObject.activeInHierarchy, "Free practice opens the authored wooden dummy.");
            Require(!view.libraryPanel.activeSelf, "The library opens only when requested.");
            yield return Capture("practice-selection");
            yield return OpenSelectionLibrary();
            Step("selection-ready", "Title unloaded, door complete, one input system and listener.");
        }

        private IEnumerator OpenSelectionLibrary()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            if (!view.libraryPanel.activeSelf)
            {
                yield return Click(view.practice.library);
            }
            Require(view.libraryPanel.activeInHierarchy, "The shared song library is visible.");
        }

        private IEnumerator VerifyTrainingModes()
        {
            var view = MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View;
            yield return Click(view.modeSelection.previous);
            yield return Wait(() => !view.modeSelection.IsMoving, "Travel to lesson");
            yield return SubmitMode();
            Require(view.modeSelection.lessonCue.gameObject.activeInHierarchy, "Lesson begins inside ModeSelection.");
            yield return Capture("lesson");
            for (int i = 0; i < 2; i++)
            {
                yield return Click(view.modeSelection.next);
                yield return Wait(() => !view.modeSelection.IsMoving, "Travel to Challenge");
            }
            yield return Capture("challenge-mode");
            yield return SubmitMode();
            yield return Wait(() => SceneManager.GetActiveScene().path == AppFlowController.SelectionPath,
                "Challenge selection");
            yield return Wait(() => !flow.Transitioning, "Challenge activation cleanup");
            Require(flow.Rules.Snapshot.Mode == RhythmDojo.Gameplay.PlayMode.Challenge,
                "Challenge selection preserves the requested rules.");
            Require(Object.FindFirstObjectByType<SongSelectionView>().practice.gameObject.activeInHierarchy, "Challenge uses the wooden dummy selection.");
            CheckSelectionCleanup();
            yield return Capture("challenge-selection");
            yield return ReturnToTitle();
            Step("training-modes", "Lesson, practice and Challenge use the authored mode rooms.");
        }

        private IEnumerator ReturnToTitle()
        {
            var view = Object.FindFirstObjectByType<SongSelectionView>();
            if (view.libraryPanel.activeSelf)
            {
                yield return Click(view.closeLibrary);
            }
            yield return Click(view.back);
            yield return Wait(() => SceneManager.GetActiveScene().path == MainMenuActions.ModeSelectionScenePath,
                "Selection return to modes");
            yield return Wait(() => !flow.Transitioning, "Mode return cleanup");
            yield return Press(Key.Escape);
            yield return Wait(() => SceneManager.GetActiveScene().path == TitlePath, "Selection Back to Title");
            yield return OpenTitleMenu();
            yield return Click(MainMenuCompositionRoot.InScene(SceneManager.GetActiveScene()).View.settings);
            yield return Wait(() => Object.FindFirstObjectByType<SettingsView>(), "Reentered title settings");
            yield return Click(Object.FindFirstObjectByType<SettingsView>().back);
            Require(Object.FindFirstObjectByType<MainMenuScreen>().enabled, "Reentered title input restored.");
        }

        private IEnumerator ChangeKey(SettingsView view, Key key)
        {
            yield return Click(view.keyBindingButtons[0]);
            yield return Press(key);
            Require(view.keyBindingStatus.text.Contains("저장"), "Key change reports successful save.");
        }

        private void CheckSelectionCleanup()
        {
            Require(SceneManager.sceneCount == 1, "Only Selection remains loaded.");
            Require(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled) == 1, "Exactly one active EventSystem.");
            Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Count(value => value.isActiveAndEnabled) == 1, "Exactly one active AudioListener.");
            var screen = Object.FindFirstObjectByType<SongSelectionScreen>();
            Require(screen && screen.isActiveAndEnabled, "Selection input restored.");
            var group = screen.GetComponent<CanvasGroup>();
            if (group)
            {
                Require(group.interactable && group.blocksRaycasts, "Selection CanvasGroup restored.");
            }
        }

        private sealed class WaveProvider : ISongProvider
        {
            private readonly SongDocument document;
            private readonly GameModeDefinition mode;
            private readonly ISongDocumentLoader loader;

            public WaveProvider(SongDocument document, GameModeDefinition mode, ISongDocumentLoader loader)
            {
                this.document = document;
                this.mode = mode;
                this.loader = loader;
            }

            public IReadOnlyList<SongEntry> GetEntries() => new[]
            {
                new SongEntry(document.SongId, document.Title, "Generated fixture", 90, 2, mode.LaneCount,
                    120, 120, mode: mode, modeId: document.ModeId),
            };

            public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken) =>
                loader.LoadAsync(document, cancellationToken);
        }
    }
}




