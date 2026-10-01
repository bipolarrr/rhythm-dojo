using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class SettingsFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator SettingsButtonOpensSettingsAndBackPreservesSongOptions()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var selection = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            selection.songList.value = 1;
            selection.multiplierList.value = 4;
            yield return Click(selection.settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SettingsView>().back);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
            yield return null;
            Assert.That(flow.SelectedSongIndex, Is.EqualTo(1));
            Assert.That(flow.SelectedMultiplier, Is.EqualTo(1.5));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().songList.value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BufferSelectionWithoutApplyLeavesActualAudioUnchanged()
        {
            yield return Click(UnityEngine.Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            var before = AudioSettings.GetConfiguration();
            view.bufferList.value = 1;
            yield return null;
            Assert.That(flow.SelectedBufferOption, Is.EqualTo(1));
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(before.dspBufferSize));
            Assert.That(view.actualState.text, Does.Contain($"{before.dspBufferSize} samples"));
        }

        [UnityTest]
        public IEnumerator SettingsMouseAndKeyboardApplyActualBufferAndKeepSelection()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(flow.DefaultBufferSize, Is.EqualTo(originalAudio.dspBufferSize));
            var before = AudioSettings.GetConfiguration();
            Assert.That(flow.ApplyBufferSize(), Is.False);
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(before.dspBufferSize));
            yield return Click(ButtonNamed("Settings"));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            CaptureSelection("settings");
            var dropdown = UnityEngine.Object.FindFirstObjectByType<Dropdown>();
            Assert.That(dropdown.options.Count, Is.EqualTo(8));
            yield return Press(Key.Enter); yield return Press(Key.DownArrow); yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(.25f); // uGUI restores selection after closing its popup.
            Assert.That(flow.SelectedBufferOption, Is.EqualTo(1));
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(before.dspBufferSize), "Selection alone must not reset audio.");
            yield return Press(Key.DownArrow);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonNamed("Apply").gameObject));
            yield return Press(Key.Enter);
            Assert.That(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(t => t.name == "Actual Audio State").text,
                Does.Contain($"{AudioSettings.GetConfiguration().dspBufferSize} samples"));
            Assert.That(flow.BufferStatus, Does.Contain("Actual:"));
            yield return Press(Key.RightArrow); yield return Press(Key.Enter);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
            EventSystem.current.SetSelectedGameObject(ButtonNamed("Settings").gameObject);
            yield return Press(Key.Enter);
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            dropdown = UnityEngine.Object.FindFirstObjectByType<Dropdown>();
            Assert.That(dropdown.value, Is.EqualTo(1));
            dropdown.value = 0; yield return Click(ButtonNamed("Apply"));
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(flow.DefaultBufferSize));
            yield return Click(ButtonNamed("Back"));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
