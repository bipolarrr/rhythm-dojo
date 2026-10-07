using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    public sealed class GeneratedContent
    {
        public GameSettings Settings;
        public NoteView TapTemplate, HoldTemplate;
        public Material NoteMaterial, HighwayMaterial, RailMaterial, JudgmentMaterial;
        public void ReloadAfterSceneChange()
        {
            Settings = SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath);
            NoteMaterial = SceneResources.Require<Material>("Assets/Game/Materials/Note Base.mat");
            HighwayMaterial = SceneResources.Require<Material>("Assets/Game/Materials/Highway.mat");
            RailMaterial = SceneResources.Require<Material>("Assets/Game/Materials/Rails.mat");
            JudgmentMaterial = SceneResources.Require<Material>("Assets/Game/Materials/Judgement.mat");
            TapTemplate = SceneResources.Require<GameObject>("Assets/Game/Prefabs/TapNote.prefab").GetComponent<NoteView>();
            HoldTemplate = SceneResources.Require<GameObject>("Assets/Game/Prefabs/HoldNote.prefab").GetComponent<NoteView>();
        }
    }
    public static class TestContentBuilder
    {
        public const string SettingsPath = "Assets/Game/Settings/GameSettings.asset";
        public const string VariableChartPath = "Assets/Game/Settings/TempoChart.asset";
        [MenuItem("Game Tools/Content/Prepare Default and Verification Content")]
        public static void PrepareMenu() => Prepare();
        public static GeneratedContent Prepare()
        {
            foreach (string folder in new[] { "Scenes", "Settings", "Materials", "Audio", "Prefabs" })
                Directory.CreateDirectory("Assets/Game/" + folder);
            AssetDatabase.Refresh();
            bool needsMigration = !File.Exists("Assets/Game/Settings/Standard.asset") ||
                !File.Exists("Assets/Game/Settings/ScrollSettings.asset") ||
                !File.Exists("Assets/Game/Settings/AudioPlaybackSettings.asset") ||
                !File.Exists("Assets/Game/Settings/TestSong.asset");
            RhythmGameConfig legacy = null;
            if (needsMigration && File.Exists(SceneBuilder.ConfigPath))
            {
                legacy = AssetDatabase.LoadAssetAtPath<RhythmGameConfig>(SceneBuilder.ConfigPath);
                if (!legacy) throw new InvalidOperationException("Cannot load legacy configuration. Repair its script reference.");
                legacy.Validate();
            }
            var defaults = DefaultSettingsBuilder.Prepare(legacy);
            var mode = defaults.Mode;
            var chart = Asset<RhythmChart>(SceneBuilder.ChartPath, out _);
            chart.SetGeneratedContent(new[] {
                NoteData.Tap(0,1.5), NoteData.Tap(1,2), NoteData.Tap(2,2.5), NoteData.Tap(3,3),
                NoteData.Hold(0,3.8,5.3), NoteData.Tap(2,4.25), NoteData.Tap(3,4.7),
                NoteData.Tap(1,5.8), NoteData.Tap(2,5.8), NoteData.Tap(0,6.5), NoteData.Tap(0,6.68),
                NoteData.Hold(3,7.3,9), NoteData.Tap(0,7.75), NoteData.Tap(1,8.15), NoteData.Tap(2,8.55),
                NoteData.Hold(1,9.6,10.6), NoteData.Tap(0,10.9), NoteData.Tap(3,10.9) }, 12); Dirty(chart);
            var tempoChart = Asset<RhythmChart>(VariableChartPath, out _);
            tempoChart.SetGeneratedContent(new[] {
                NoteData.Tap(0,1.5), NoteData.Tap(1,2), NoteData.Tap(2,2.5), NoteData.Tap(3,3),
                NoteData.Hold(0,5.2,6.8), NoteData.Tap(2,6), NoteData.Tap(1,7.5), NoteData.Tap(3,9) }, 12); Dirty(tempoChart);
            var fixedTiming = new SongTiming { tempoPoints = new[] { new SerializedTempoPoint(0,100) } };
            var variableTiming = new SongTiming { tempoPoints = new[] { new SerializedTempoPoint(0,150), new SerializedTempoPoint(6,180) } };
            var pulse = Audio("Assets/Game/Audio/TestPulse.wav", fixedTiming.ToTempoMap());
            var variablePulse = Audio("Assets/Game/Audio/TempoPulse.wav", variableTiming.ToTempoMap());
            var song = Asset<SongDefinition>("Assets/Game/Settings/TestSong.asset", out bool newSong);
            if (newSong)
            {
                if (legacy) fixedTiming.chartAudioOffsetSeconds = legacy.AudioOffset;
                song.SetGeneratedDefaults("test-pulse", "Foundation Pulse", pulse, chart, mode, fixedTiming, "Mira"); Dirty(song);
            }
            var tempoSong = Asset<SongDefinition>("Assets/Game/Settings/TempoSong.asset", out bool newTempoSong);
            if (newTempoSong) { tempoSong.SetGeneratedDefaults("tempo-pulse", "Tempo Shift", variablePulse, tempoChart, mode, variableTiming, "Aster"); Dirty(tempoSong); }
            var catalog = Asset<SongCatalog>("Assets/Game/Settings/SongCatalog.asset", out bool newCatalog);
            if (newCatalog) { catalog.SetGeneratedDefaults(new[] { song, tempoSong }); Dirty(catalog); }
            var settings = Asset<GameSettings>(SettingsPath, out bool newSettings);
            if (newSettings)
            {
                settings.catalog = catalog; settings.difficulties = new[] { defaults.Easy, defaults.Standard, defaults.Hard }; settings.defaultDifficulty = defaults.Standard;
                settings.scroll = defaults.Scroll; settings.audio = defaults.Audio; settings.presentation = defaults.Presentation; Dirty(settings);
            }
            GameContentValidator.Validate(settings);
            return PresentationAssetBuilder.Prepare(settings);
        }
        private static AudioClip Audio(string path, TempoMap map)
        {
            if (!File.Exists(path)) GeneratePulse(path, map);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); var sample = importer.defaultSampleSettings;
            if (sample.loadType != AudioClipLoadType.DecompressOnLoad || sample.compressionFormat != AudioCompressionFormat.PCM || !sample.preloadAudioData)
            {
                sample.loadType = AudioClipLoadType.DecompressOnLoad; sample.compressionFormat = AudioCompressionFormat.PCM;
                sample.preloadAudioData = true; importer.defaultSampleSettings = sample; importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (!clip || clip.length < 12) throw new InvalidOperationException("Generated test audio must last 12 seconds.");
            return clip;
        }
        private static void GeneratePulse(string path, TempoMap map)
        {
            const int rate = 44100, samples = rate * 12;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1);
            writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            double nextBeat = 0, lastBeat = 0; int beatIndex = -1;
            for (int i = 0; i < samples; i++)
            {
                double time = (double)i / rate;
                if (time + 1e-9 >= nextBeat)
                {
                    lastBeat = nextBeat; beatIndex++;
                    int segment = map.FindSegment(lastBeat); nextBeat = lastBeat + 60 / map[segment].Bpm;
                    if (segment + 1 < map.Count) nextBeat = Math.Min(nextBeat, map[segment + 1].StartTimeSeconds);
                }
                double age = time - lastBeat, frequency = beatIndex % 4 == 0 ? 880 : 440;
                writer.Write((short)(Math.Sin(2 * Math.PI * frequency * age) * Math.Exp(-age * 75) * 6500));
            }
        }
    }
}

