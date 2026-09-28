using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Unity.Profiling;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    // Used only by the diagnostic player. This scene/component is never in the shipping build.
    public sealed class AudioBufferProbe : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        private AudioConfiguration original;
        private string output;
        private bool restored;
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); UnityEngine.Application.runInBackground = true;
            original = AudioSettings.GetConfiguration();
            output = Path.Combine(UnityEngine.Application.dataPath, "../audio-probe.csv");
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == "-audioProbeLog") output = arguments[i + 1];
            File.WriteAllText(output, $"# Unity={UnityEngine.Application.unityVersion}, editor={UnityEngine.Application.isEditor}, batch={UnityEngine.Application.isBatchMode}, startup={original.dspBufferSize}, rate={original.sampleRate}\n" +
                "phase,requested,actual,buffers,rate,real_seconds,dsp_seconds,ratio,frames,profiler_gc_bytes,gc_collections,source_playing\n");
            gameObject.AddComponent<AudioListener>();
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.volume = .02f;
            var samples = new float[original.sampleRate];
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / original.sampleRate);
            try
            {
                foreach (int size in new[] { 64, 128, 256, 1024 })
                {
                    var configuration = AudioSettings.GetConfiguration(); configuration.dspBufferSize = size;
                    if (!AudioSettings.Reset(configuration)) throw new InvalidOperationException("Probe audio reset failed.");
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Measure("empty", size, source);
                    // Reset discards dynamically created audio clips; generate after each reset.
                    var clip = AudioClip.Create("Probe tone", original.sampleRate, 1, original.sampleRate, false);
                    clip.SetData(samples, 0);
                    source.clip = clip; source.Play();
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Measure("native-tone", size, source);
                    source.Stop(); source.clip = null; Destroy(clip);
                    UnityEngine.Application.targetFrameRate = 120;
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Measure("empty-capped-120", size, source);
                    UnityEngine.Application.targetFrameRate = -1;
                }
                Destroy(source); Destroy(GetComponent<AudioListener>());
                var flow = AppFlowController.Create(settings); flow.ShowSelection();
                while (flow.Transitioning) yield return null;
                foreach (int option in new[] { 2, 4 })
                {
                    flow.ShowSettings(); while (flow.Transitioning) yield return null;
                    flow.SelectBufferOption(option);
                    if (!flow.ApplyBufferSize()) throw new InvalidOperationException(flow.BufferStatus);
                    flow.ShowSelection(); while (flow.Transitioning) yield return null;
                    flow.PlaySelected(); while (flow.Transitioning) yield return null;
                    yield return null;
                    var game = FindFirstObjectByType<RhythmGameController>(); game.StartSession();
                    var audio = FindFirstObjectByType<RhythmDojo.Audio.SongClock>().GetComponent<AudioSource>();
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Measure("gameplay", flow.RequestedBufferSize, audio);
                    game.StartSession(); yield return new WaitForSecondsRealtime(.5f);
                    yield return Measure("restart", flow.RequestedBufferSize, audio);
                    FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection();
                    yield return null;
                    while (flow.Transitioning) yield return null;
                }
            }
            finally { Restore(); }
            UnityEngine.Application.Quit(0);
        }
        private IEnumerator Measure(string phase, int requested, AudioSource source)
        {
            var configuration = AudioSettings.GetConfiguration();
            AudioSettings.GetDSPBufferSize(out int actual, out int buffers);
            double realStart = Time.realtimeSinceStartupAsDouble, dspStart = AudioSettings.dspTime;
            int frameStart = Time.frameCount, gcStart = GC.CollectionCount(0);
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            long allocated = 0;
            do
            {
                yield return null;
                if (allocations.Valid) allocated += allocations.LastValue;
            } while (Time.realtimeSinceStartupAsDouble - realStart < 3);
            if (!allocations.Valid) allocated = -1;
            double elapsed = Time.realtimeSinceStartupAsDouble - realStart, dspElapsed = AudioSettings.dspTime - dspStart;
            File.AppendAllText(output, FormattableString.Invariant($"{phase},{requested},{actual},{buffers},{configuration.sampleRate},{elapsed:F4},{dspElapsed:F4},{dspElapsed / elapsed:F4},{Time.frameCount - frameStart},{allocated},{GC.CollectionCount(0) - gcStart},{source && source.isPlaying}\n"));
        }
        private void Restore()
        {
            if (restored) return;
            restored = true;
            if (!AudioSettings.Reset(original)) Debug.LogError("Diagnostic player could not restore audio configuration.");
        }
        private void OnApplicationQuit() => Restore();
    }
}
