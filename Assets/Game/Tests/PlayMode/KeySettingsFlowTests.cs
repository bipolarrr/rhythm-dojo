using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class KeySettingsFlowTests : GameplayTestContext
    {
        private sealed class ModeProvider : ISongProvider
        {
            private readonly SongDocument document;
            private readonly SongDefinition source;
            private readonly GameModeDefinition mode;

            public ModeProvider(SongDefinition source, GameModeDefinition mode)
            {
                this.source = source;
                this.mode = mode;
                document = SongAssetAdapter.ToDocument(source);
                document.SongId = "mode-key-regression";
                document.ModeId = mode.name;
                document.Notes = new[] { new NoteData(0, NoteKind.Tap, 5, 5) };
                document.CompletionTime = 8;
            }

            public IReadOnlyList<SongEntry> GetEntries() => new[]
            {
                new SongEntry(document.SongId, "Mode keys", "Test", 8, 1, mode.LaneCount,
                    120, 120, mode: mode, modeId: document.ModeId),
            };

            public Task<PlayableSong> LoadAsync(string songId, CancellationToken cancellationToken) =>
                Task.FromResult(new PlayableSong(document, source.AudioClip, mode));
        }

        private static GameModeDefinition SixLaneMode()
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            mode.name = "SixLaneKeys";
            mode.SetGeneratedDefaults(new[]
            {
                new LaneDefinition("<Keyboard>/a", "Left A", Color.white),
                new LaneDefinition("<Keyboard>/s", "Left S", Color.white),
                new LaneDefinition("<Keyboard>/d", "Left D", Color.white),
                new LaneDefinition("<Keyboard>/j", "Right J", Color.white),
                new LaneDefinition("<Keyboard>/k", "Right K", Color.white),
                new LaneDefinition("<Gamepad>/buttonSouth", "Pad South", Color.white),
            });
            return mode;
        }

        private static void SelectModeSong(AppFlowController flow, GameModeDefinition mode)
        {
            flow.Library.Register(new ModeProvider(flow.Settings.catalog[0], mode));
            flow.Selection.UpdateSelection("mode-key-regression", flow.Settings.defaultDifficulty,
                ScrollMode.Constant, 1);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator KeyTabCapturesPersistsCancelsRejectsDuplicatesAndResets()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            var view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.keySettingsTab);
            Assert.That(view.gameplayPanel.activeSelf, Is.False);
            Assert.That(view.keySettingsPanel.activeSelf, Is.True);
            yield return Click(view.keyBindingButtons[0]);
            yield return Press(Key.F);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.D));
            Assert.That(view.keyBindingStatus.text, Does.Contain("사용 중"));
            yield return Press(Key.Escape);
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.keyBindingButtons[0].GetComponentInChildren<Text>().text, Is.EqualTo("D"));
            yield return Click(view.keyBindingButtons[0]);
            yield return Press(Key.Space);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.D));
            yield return Press(Key.A);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.A));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents, Is.True);
            yield return Click(view.keyBindingButtons[1]);
            yield return Press(Key.Enter);
            Assert.That(PlayKeyBindings.GetKey(1), Is.EqualTo(Key.Enter));
            Assert.That(view.keyBindingStatus.text, Does.Contain("저장"));
            yield return Click(view.keyBindingButtons[2]);
            yield return Press(Key.LeftArrow);
            Assert.That(PlayKeyBindings.GetKey(2), Is.EqualTo(Key.LeftArrow));
            yield return Click(view.back);
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.keySettingsTab);
            Assert.That(view.keyBindingButtons[0].GetComponentInChildren<Text>().text, Is.EqualTo("A"));
            yield return Click(view.resetKeys);
            Assert.That(Enumerable.Range(0, 4).Select(PlayKeyBindings.GetKey),
                Is.EqualTo(new[] { Key.D, Key.F, Key.J, Key.K }));
            yield return Click(view.back);
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator CustomKeysDriveGameplayAndPausedChangesRefreshHudImmediately()
        {
            var keys = new[] { Key.A, Key.S, Key.L, Key.Semicolon };
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(PlayKeyBindings.TrySetKey(i, keys[i], out _), Is.True);
            }
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var hud = Object.FindFirstObjectByType<GameplayHudView>();
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(hud.lanes.laneRoot.GetChild(i).GetComponentInChildren<Text>().text,
                    Is.EqualTo(PlayKeyBindings.GetLabel(i)));
            }
            int pressed = -1;
            game.Input.Pressed += (lane, time) => pressed = lane;
            yield return Press(Key.D);
            Assert.That(pressed, Is.EqualTo(-1));
            for (int i = 0; i < keys.Length; i++)
            {
                yield return Press(keys[i]);
                Assert.That(pressed, Is.EqualTo(i));
                Assert.That(((RhythmDojo.Input.LaneKeyboardInput)game.Input).IsHeld(i), Is.False);
            }
            yield return Press(Key.Space);
            yield return Press(Key.Escape);
            var pause = Object.FindFirstObjectByType<GameplayPauseMenu>();
            Assert.That(game.IsPaused, Is.True);
            yield return Click(pause.settingsButton);
            var view = Object.FindFirstObjectByType<SettingsView>();
            yield return Click(view.keySettingsTab);
            yield return Click(view.keyBindingButtons[0]);
            yield return Press(Key.Q);
            Assert.That(hud.lanes.laneRoot.GetChild(0).GetComponentInChildren<Text>().text, Is.EqualTo("Q"));
            yield return Click(view.back);
            yield return Click(pause.resumeButton);
            yield return Await(
                () => !game.IsPaused && !pause.countdown.activeSelf,
                "Gameplay did not finish scheduled resume.");
            Assert.That(game.IsPaused, Is.False);
            Assert.That(game.Clock.Running, Is.True);
            pressed = -1;
            yield return Press(Key.A);
            Assert.That(pressed, Is.EqualTo(-1));
            yield return Press(Key.Q);
            Assert.That(pressed, Is.EqualTo(0));
            game.Session.Advance(game.Chart.CompletionTime + 1);
            yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
            Assert.That(Object.FindFirstObjectByType<AppFlowController>().LastResult.LaneLabels[0], Is.EqualTo("Q"));
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator SelectedSixLaneModeBuildsScrollableRowsAndTitleKeepsCatalogDefault()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var mode = SixLaneMode();
            try
            {
                SelectModeSong(flow, mode);
                yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
                var view = Object.FindFirstObjectByType<SettingsView>();
                yield return Click(view.keySettingsTab);
                Assert.That(view.keyBindingButtons.Length, Is.EqualTo(6));
                Assert.That(view.keyRowsContainer.rect.height, Is.GreaterThan(view.keyRowsScroll.viewport.rect.height));
                Assert.That(view.keyBindingButtons[5].interactable, Is.False);
                Assert.That(view.keyBindingButtons[0].GetComponentInChildren<Text>().text, Is.EqualTo("Left A"));
                CaptureSelection("pr-quality-remediation/task5-six-lanes-top");
                view.keyRowsScroll.verticalNormalizedPosition = 0;
                yield return null;
                CaptureSelection("pr-quality-remediation/task5-six-lanes-bottom");
                yield return Click(view.keyBindingButtons[4]);
                yield return Press(Key.O);
                Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 4), Is.EqualTo("<Keyboard>/o"));
                yield return Click(view.back);

                flow.ShowTitle();
                yield return Await(() => Object.FindFirstObjectByType<MainMenuEntranceScreen>() && !flow.Transitioning);
                flow.ShowTitleSettings();
                yield return null;
                view = Object.FindFirstObjectByType<SettingsView>();
                yield return Click(view.keySettingsTab);
                Assert.That(view.keyBindingButtons.Length, Is.EqualTo(flow.Settings.catalog[0].Mode.LaneCount));
                Assert.That(view.keyBindingButtons[0].GetComponentInChildren<Text>().text,
                    Is.EqualTo(flow.Settings.catalog[0].Mode.GetLane(0).displayName));
                yield return Click(view.back);
            }
            finally
            {
                Object.Destroy(mode);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator PausedSettingsUseOriginalPlayModeWhenSelectionChangesAndResultsKeepLabels()
        {
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            var mode = SixLaneMode();
            try
            {
                SelectModeSong(flow, mode);
                flow.PlaySelected();
                yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
                yield return null;
                var game = Object.FindFirstObjectByType<RhythmGameController>();
                Assert.That(flow.CurrentRequest.Song.ModeId, Is.EqualTo(mode.name));
                Assert.That(flow.CurrentRequest.Song.Mode.name, Is.Not.EqualTo(mode.name));
                game.StartSession();
                yield return Press(Key.Escape);
                flow.Selection.UpdateSelection("test-pulse", flow.Settings.defaultDifficulty, ScrollMode.Constant, 1);
                var pause = Object.FindFirstObjectByType<GameplayPauseMenu>();
                yield return Click(pause.settingsButton);
                var view = Object.FindFirstObjectByType<SettingsView>();
                yield return Click(view.keySettingsTab);
                Assert.That(view.keyBindingButtons.Length, Is.EqualTo(6));
                yield return Click(view.keyBindingButtons[0]);
                yield return Press(Key.Q);
                var hud = Object.FindFirstObjectByType<GameplayHudView>();
                Assert.That(hud.lanes.laneRoot.GetChild(0).GetComponentInChildren<Text>().text, Is.EqualTo("Q"));
                yield return Click(view.back);
                yield return Click(pause.resumeButton);
                yield return Await(() => !game.IsPaused && !pause.countdown.activeSelf);
                int pressed = -1;
                game.Input.Pressed += (lane, time) => pressed = lane;
                yield return Press(Key.A);
                Assert.That(pressed, Is.EqualTo(-1));
                yield return Press(Key.Q);
                Assert.That(pressed, Is.EqualTo(0));
                game.Session.Advance(game.Chart.CompletionTime + 1);
                yield return Await(() => Object.FindFirstObjectByType<ResultsScreen>());
                Assert.That(flow.LastResult.LaneLabels.Count, Is.EqualTo(6));
                Assert.That(flow.LastResult.LaneLabels[0], Is.EqualTo("Q"));
                Assert.That(flow.LastResult.LaneLabels[5], Is.EqualTo("Pad South"));
            }
            finally
            {
                Object.Destroy(mode);
            }
        }

        [UnityTest]
        [Category("SceneIntegration")]
        public IEnumerator ChangingAnotherModeDoesNotReleaseCurrentHeldInput()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = Object.FindFirstObjectByType<RhythmGameController>();
            var input = (RhythmDojo.Input.LaneKeyboardInput)game.Input;
            var mode = SixLaneMode();
            int released = 0;
            game.Input.Released += (lane, time) => released++;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return null;
                Assert.That(input.IsHeld(0), Is.True);
                Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, Key.Q, out _), Is.True);
                yield return null;
                Assert.That(input.IsHeld(0), Is.True);
                Assert.That(released, Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(input.IsHeld(0), Is.False);
                Assert.That(released, Is.EqualTo(1));
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Object.Destroy(mode);
            }
        }
    }
}
