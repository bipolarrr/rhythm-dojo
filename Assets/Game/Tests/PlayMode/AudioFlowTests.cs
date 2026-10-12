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
        public IEnumerator TitleStartupRestoresSavedMasterAndBackgroundVolume()
        {
            var presentation = RhythmDojo.Services.PresentationPreferences.Current;
            presentation.masterVolume = .25f; presentation.backgroundVolume = .4f; presentation.gameplayVolume = .6f;
            presentation.SaveAudio();
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            UnityEngine.Object.Destroy(flow.gameObject);
            yield return null;
            AudioListener.volume = 1f; // Simulate a fresh process before the title scene starts.
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            var music = UnityEngine.Object.FindFirstObjectByType<TitleMusicPlayer>();
            Assert.That(music, Is.Not.Null);
            Assert.That(AudioListener.volume, Is.EqualTo(.25f).Within(.001f));
            Assert.That(music.GetComponent<AudioSource>().volume, Is.EqualTo(.7f * .4f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator SoundSlidersChangeMixedOutputMuteAndRestoreSavedValues()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Game/Scenes/Title.unity");
            yield return null;
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowTitleSettings();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            var music = UnityEngine.Object.FindFirstObjectByType<TitleMusicPlayer>();
            var source = music.GetComponent<AudioSource>();
            // Title music is optional in the existing asset. Use a controlled tone
            // for output measurements without choosing a new production music track.
            if (source.clip)
            {
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(source.isPlaying, Is.True, "Opening settings must preserve title playback.");
            }
            float baseVolume = source.volume / RhythmDojo.Services.PresentationPreferences.Current.backgroundVolume;
            var originalClip = source.clip;
            var tone = AudioClip.Create("Sound settings verification tone", 48000, 1, 48000, false);
            var data = new float[48000];
            for (int i=0; i<data.Length; i++) data[i] = .2f * Mathf.Sin(2*Mathf.PI*440*i/48000);
            tone.SetData(data,0);
            var report = new System.Text.StringBuilder("Master,Background,SourceVolume,OutputRMS\n");
            try
            {
                source.Stop(); source.clip = tone; source.loop = true; source.Play();
                var master = new[] {100,100,50,0,100};
                var background = new[] {100,50,100,100,0};
                var levels = new double[master.Length];
                for (int i=0; i<master.Length; i++)
                {
                    view.masterVolume.value = master[i]; view.backgroundVolume.value = background[i];
                    Assert.That(AudioListener.volume, Is.EqualTo(master[i]/100f).Within(.001f));
                    Assert.That(source.volume, Is.EqualTo(baseVolume*background[i]/100f).Within(.001f));
                    yield return MeasureOutputRms(value => levels[i] = value);
                    report.AppendLine($"{master[i]},{background[i]},{source.volume:F4},{levels[i]:F6}");
                    Assert.That(source.isPlaying, Is.True, "Changing volume must not stop music or its beat clock.");
                }
                Assert.That(levels[0], Is.GreaterThan(.001), "The listener must receive actual audio samples.");
                Assert.That(levels[1]/levels[0], Is.EqualTo(.5).Within(.1));
                Assert.That(levels[2]/levels[0], Is.EqualTo(.5).Within(.1));
                Assert.That(levels[3], Is.LessThan(.0001), "Master zero must mute the mixed output.");
                Assert.That(levels[4], Is.LessThan(.0001), "Background zero must mute title music.");
                view.masterVolume.value=65; view.backgroundVolume.value=35; view.gameplayVolume.value=55;
                view.back.onClick.Invoke();
                yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
                var saved = RhythmDojo.Services.PresentationPreferences.Load();
                Assert.That(saved.masterVolume, Is.EqualTo(.65f).Within(.001f));
                Assert.That(saved.backgroundVolume, Is.EqualTo(.35f).Within(.001f));
                Assert.That(saved.gameplayVolume, Is.EqualTo(.55f).Within(.001f));
                flow.ShowTitleSettings();
                yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
                yield return null;
                view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
                Assert.That(view.masterVolume.value, Is.EqualTo(65));
                Assert.That(view.backgroundVolume.value, Is.EqualTo(35));
                Assert.That(view.gameplayVolume.value, Is.EqualTo(55));
            }
            finally
            {
                source.Stop(); source.clip = originalClip;
                UnityEngine.Object.Destroy(tone);
                System.IO.File.WriteAllText("Logs/sound-volume-measurements.csv", report.ToString());
            }
        }

        [UnityTest]
        public IEnumerator GameplayMusicUsesSelectedVolumeAndRespondsToLiveChanges()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.ShowSettings();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            yield return null;
            var view = UnityEngine.Object.FindFirstObjectByType<SettingsView>();
            view.masterVolume.value=50; view.backgroundVolume.value=30; view.gameplayVolume.value=35;
            view.back.onClick.Invoke();
            yield return Await(() => !UnityEngine.Object.FindFirstObjectByType<SettingsScreen>());
            flow.PlaySelected();
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<RhythmGameController>());
            yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            var source = UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>();
            Assert.That(source.volume, Is.EqualTo(flow.Settings.audio.Volume*.35f).Within(.001f));
            game.StartSession();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(source.isPlaying, Is.True);
            var presentation = RhythmDojo.Services.PresentationPreferences.Current;
            presentation.backgroundVolume=0; presentation.SaveAudio();
            Assert.That(source.volume, Is.EqualTo(flow.Settings.audio.Volume*.35f).Within(.001f), "Background control must not change gameplay music.");
            presentation.gameplayVolume=0; presentation.SaveAudio();
            Assert.That(source.volume, Is.Zero, "Live gameplay volume must mute the existing source.");
            Assert.That(source.isPlaying, Is.True);
            presentation.gameplayVolume=1; presentation.SaveAudio();
            Assert.That(source.volume, Is.EqualTo(flow.Settings.audio.Volume).Within(.001f));
            game.StartSession();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.volume, Is.EqualTo(flow.Settings.audio.Volume).Within(.001f));
        }

        private static IEnumerator MeasureOutputRms(System.Action<double> measured)
        {
            var samples = new float[2048];
            AudioListener.GetOutputData(samples,0); // Allocate Unity's history buffer before measuring.
            yield return new WaitForSecondsRealtime(.3f);
            double sum = 0; int count = 0;
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble-start < .35)
            {
                AudioListener.GetOutputData(samples,0);
                foreach (float sample in samples) { sum += sample*sample; count++; }
                yield return null;
            }
            measured(System.Math.Sqrt(sum/count));
        }

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
