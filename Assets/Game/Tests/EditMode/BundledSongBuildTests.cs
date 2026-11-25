using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RhythmDojo.Authoring;
using RhythmDojo.EditorTools;
using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEditor.Build;

namespace RhythmDojo.Tests
{
    [Category("Fast")]
    public sealed class BundledSongBuildTests
    {
        private string root;
        private string folder;
        private string audio;
        private string destination;
        private GameModeDefinition mode;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "bundled-song-" + Guid.NewGuid());
            folder = Path.Combine(root, "song");
            var source = Path.GetFullPath("Content/GaemunIlgyeok");
            Directory.CreateDirectory(Path.Combine(folder, "audio"));
            string chart = Path.Combine(source, FileSongProjectStore.FileName);
            File.Copy(chart, Path.Combine(folder, FileSongProjectStore.FileName));
            var project = new SongProjectJson().Deserialize(File.ReadAllText(chart));
            audio = project.Audio;
            destination = "Songs/" + project.Id.ToString("D") + "/";
            File.Copy(Path.Combine(source, audio), Path.Combine(folder, audio));
            mode = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath).catalog[0].Mode;
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(root, true);
        }

        [Test]
        public void OnlyChartAndReferencedAudioArePackagedUnderSongId()
        {
            File.WriteAllText(Path.Combine(folder, "workspace.json"), "{}");
            var files = new Dictionary<string, string>();
            BundledSongBuildProcessor.AddSongs(root, mode, (source, target) => files.Add(target, source));
            Assert.That(files.Count, Is.EqualTo(2));
            Assert.That(files[destination + FileSongProjectStore.FileName],
                Is.EqualTo(Path.Combine(folder, FileSongProjectStore.FileName)));
            Assert.That(files[destination + audio], Is.EqualTo(Path.GetFullPath(Path.Combine(folder, audio))));
        }

        [TestCase("missing", "Audio file missing")]
        [TestCase("modified", "SHA-256")]
        [TestCase("duplicate", "Duplicate song ID")]
        public void InvalidContentStopsBuild(string fault, string expectedError)
        {
            switch (fault)
            {
                case "missing":
                    File.Delete(Path.Combine(folder, audio));
                    break;
                case "modified":
                    File.WriteAllText(Path.Combine(folder, audio), "modified audio");
                    break;
                case "duplicate":
                    var second = Directory.CreateDirectory(Path.Combine(root, "song2"));
                    File.Copy(Path.Combine(folder, FileSongProjectStore.FileName),
                        Path.Combine(second.FullName, FileSongProjectStore.FileName));
                    break;
            }

            var error = Assert.Throws<BuildFailedException>(
                () => BundledSongBuildProcessor.AddSongs(root, mode, (source, target) => { }));
            Assert.That(error.Message, Does.Contain(expectedError));
            Assert.That(error.Message, Does.Contain(root));
        }
    }
}
