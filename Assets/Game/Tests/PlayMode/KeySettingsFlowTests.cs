using System.Collections;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RhythmDojo.Tests
{
    public sealed class KeySettingsFlowTests : GameplayTestContext
    {
        [UnityTest]
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
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(game.IsPaused, Is.False);
            pressed = -1;
            yield return Press(Key.A);
            Assert.That(pressed, Is.EqualTo(-1));
            yield return Press(Key.Q);
            Assert.That(pressed, Is.EqualTo(0));
        }
    }
}
