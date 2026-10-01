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
    [Category("Gameplay")]
    public sealed class AudioFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator AllBufferRequestsReportActualConfigurationAndChangedAudioPlaysAndRestarts()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowSettings();
            Assert.That(flow.ApplyBufferSize(), Is.False, "Apply during transition must be rejected.");
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>()); yield return null;
            System.IO.File.WriteAllText("Logs/audio-buffers.txt", $"Startup: {originalAudio.dspBufferSize} samples / {originalAudio.sampleRate} Hz\n");
            for (int option = 1; option < AppFlowController.BufferOptionCount; option++)
            {
                flow.SelectBufferOption(option);
                bool accepted = flow.ApplyBufferSize();
                var actual = AudioSettings.GetConfiguration();
                AudioSettings.GetDSPBufferSize(out int size, out int count);
                yield return new WaitForSecondsRealtime(.3f);
                double start = AudioSettings.dspTime;
                double realStart = Time.realtimeSinceStartupAsDouble;
                yield return new WaitForSecondsRealtime(1);
                double elapsed = Time.realtimeSinceStartupAsDouble - realStart;
                double dspElapsed = AudioSettings.dspTime - start;
                System.IO.File.AppendAllText("Logs/audio-buffers.txt", $"Requested {flow.RequestedBufferSize}: accepted={accepted}, actual={size} x {count}, rate={actual.sampleRate}, real={elapsed:F4}s, DSP={dspElapsed:F4}s, ratio={dspElapsed/elapsed:F3}\n");
                Assert.That(size, Is.EqualTo(actual.dspBufferSize));
                Assert.That(size, Is.GreaterThan(0));
                Assert.That(AudioSettings.dspTime, Is.GreaterThan(start));
            }
            flow.SelectBufferOption(4); Assert.That(flow.ApplyBufferSize(), Is.True);
            flow.ShowSelection(); yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>()); yield return null;
            flow.PlaySelected(); yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var playingConfiguration = AudioSettings.GetConfiguration();
            Assert.That(flow.ApplyBufferSize(), Is.False, "Gameplay must reject buffer reset.");
            Assert.That(AudioSettings.GetConfiguration().dspBufferSize, Is.EqualTo(playingConfiguration.dspBufferSize));
            game.StartSession(); yield return new WaitForSecondsRealtime(2);
            Assert.That(game.Clock.SongTime, Is.GreaterThan(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying, Is.True);
            game.StartSession(); Assert.That(game.Session.Resolved, Is.Zero);
            yield return new WaitForSecondsRealtime(2);
            Assert.That(game.Clock.SongTime, Is.GreaterThan(1));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying, Is.True);
            System.IO.File.AppendAllText("Logs/audio-buffers.txt", "PASS: requested 256 samples; scheduled playback and restart advance song time and report AudioSource playing. Audible dropouts were not measured.\n");
        }
    }
}
