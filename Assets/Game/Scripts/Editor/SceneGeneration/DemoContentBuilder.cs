using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.EditorTools
{
    public static class DemoContentBuilder
    {
        public const string SongPath = "Assets/Game/Settings/DemoSong.asset";
        public const string ChartPath = "Assets/Game/Settings/DemoChart.asset";
        public const string AudioPath = "Assets/Game/Audio/DemoMelody.wav";
        public const string CoverPath = "Assets/Game/Art/DemoCover.png";

        [MenuItem("Game Tools/Content/Add Demo Melody")]
        public static void Prepare()
        {
            var settings = SceneResources.Require<GameSettings>(TestContentBuilder.SettingsPath);
            if (!AssetDatabase.IsValidFolder("Assets/Game/Art")) AssetDatabase.CreateFolder("Assets/Game", "Art");
            if (!File.Exists(AudioPath)) GenerateAudio();
            if (!File.Exists(CoverPath)) GenerateCover();
            AssetDatabase.ImportAsset(AudioPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(CoverPath, ImportAssetOptions.ForceSynchronousImport);
            var audioImporter = (AudioImporter)AssetImporter.GetAtPath(AudioPath);
            var audioSettings = audioImporter.defaultSampleSettings;
            if (audioSettings.compressionFormat != AudioCompressionFormat.PCM || audioSettings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                audioSettings.compressionFormat = AudioCompressionFormat.PCM;
                audioSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                audioImporter.defaultSampleSettings = audioSettings; audioImporter.SaveAndReimport();
            }
            var imageImporter = (TextureImporter)AssetImporter.GetAtPath(CoverPath);
            if (imageImporter.textureType != TextureImporterType.Sprite || imageImporter.spriteImportMode != SpriteImportMode.Single)
            {
                imageImporter.textureType = TextureImporterType.Sprite;
                imageImporter.spriteImportMode = SpriteImportMode.Single; imageImporter.SaveAndReimport();
            }
            var chart = AssetDatabase.LoadAssetAtPath<RhythmChart>(ChartPath);
            if (!chart)
            {
                chart = ScriptableObject.CreateInstance<RhythmChart>();
                var notes = new List<NoteData>();
                for (int beat = 0; beat < 54; beat++)
                {
                    double time = 1.5 + beat * .5; int lane = beat % 4;
                    notes.Add(beat % 8 == 4 ? NoteData.Hold(lane, time, time + .75) : NoteData.Tap(lane, time));
                }
                chart.SetGeneratedContent(notes.ToArray(), 30);
                AssetDatabase.CreateAsset(chart, ChartPath);
            }
            var song = AssetDatabase.LoadAssetAtPath<SongDefinition>(SongPath);
            if (!song)
            {
                song = ScriptableObject.CreateInstance<SongDefinition>();
                song.SetGeneratedDefaults("demo-melody", "임시 곡 / Demo Melody",
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath), chart, settings.catalog[0].Mode, new SongTiming(), "Yuna");
                song.SetCover(AssetDatabase.LoadAssetAtPath<Sprite>(CoverPath));
                AssetDatabase.CreateAsset(song, SongPath);
            }
            if (!Enumerable.Range(0, settings.catalog.Count).Any(i => settings.catalog[i] && settings.catalog[i].SongId == song.SongId))
            {
                var songs = Enumerable.Range(0, settings.catalog.Count).Select(i => settings.catalog[i]).ToList();
                songs.Add(song); settings.catalog.SetGeneratedDefaults(songs.ToArray()); EditorUtility.SetDirty(settings.catalog);
            }
            AssetDatabase.SaveAssets(); settings.Validate();
        }

        public static void PrepareAndBuildSelection()
        {
            string backup = "Logs/selection-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach (var path in new[] { SceneBuilder.SelectionPath, SceneBuilder.BootstrapPath, "Assets/Game/Settings/SongCatalog.asset" })
                if (File.Exists(path)) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
            Prepare(); SceneBuilder.BuildSelection();
            EditorPrefs.SetBool("RhythmDojo.MainMenu.Start:" + UnityEngine.Application.dataPath, false);
            MainMenuEditorStartup.Apply();
            EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath, OpenSceneMode.Single);
            Debug.Log("Demo Melody ready; selection scene generated. Previous assets saved to " + backup);
        }
        private static void GenerateAudio()
        {
            const int rate = 44100, count = rate * 30;
            int[] melody = { 60, 64, 67, 72, 71, 67, 64, 62, 60, 64, 69, 72, 74, 72, 69, 64 };
            using var writer = new BinaryWriter(File.Create(AudioPath));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                double time = (double)i / rate, age = time % .5;
                int beat = (int)(time * 2);
                double frequency = 440 * Math.Pow(2, (melody[beat % melody.Length] - 69) / 12.0);
                double envelope = Math.Min(1, age * 100) * Math.Exp(-age * 6);
                double lead = (Math.Sin(2 * Math.PI * frequency * time) + .2 * Math.Sin(4 * Math.PI * frequency * time)) * envelope;
                double bassFrequency = 440 * Math.Pow(2, ((beat / 8 % 2 == 0 ? 36 : 41) - 69) / 12.0);
                double bass = Math.Sin(2 * Math.PI * bassFrequency * time) * Math.Exp(-age * 5);
                double kick = Math.Sin(2 * Math.PI * (65 * age + 2 * (1 - Math.Exp(-age * 35)))) * Math.Exp(-age * 30);
                double fade = Math.Min(1, Math.Min(time * 4, (30 - time) * 2));
                writer.Write((short)((lead * .18 + bass * .1 + kick * .18) * fade * 32767));
            }
        }
        private static void GenerateCover()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 256, dy = y - 256, distance = Mathf.Sqrt(dx * dx + dy * dy);
                    Color color = Color.Lerp(new Color(.03f,.065f,.12f), new Color(.07f,.2f,.26f), y / 511f);
                    if (Mathf.Abs(distance - 155) < 4 || Mathf.Abs(distance - 115) < 2) color = new Color(.22f,.85f,.8f);
                    if (distance < 28) color = new Color(.8f,.95f,.92f);
                    if (y > 80 && y < 105 && x > 64 && x < 448) color = new Color(.2f,.65f,.7f);
                    if (x > 100 && x < 410 && y > 370 && y < 382) color = new Color(.8f,.95f,.92f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply(); File.WriteAllBytes(CoverPath, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}