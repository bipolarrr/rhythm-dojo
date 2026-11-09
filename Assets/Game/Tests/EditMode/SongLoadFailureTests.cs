using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;

namespace RhythmDojo.Tests
{
    public sealed class SongLoadFailureTests
    {
        [Test]
        public async Task DeletedJsonAndDeletedAudioKeepTheirOriginThroughLibraryAndRefresh()
        {
            string root = Path.Combine(Path.GetTempPath(), "SongLoad-" + Guid.NewGuid());
            var store = new FileSongProjectStore(root);
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Game/Settings/GameSettings.asset");
            var mode = settings.catalog[0].Mode;
            var project = new SongProject
            {
                Title = "Failure fixture",
                Charts = new[] { new ChartDocument
                {
                    Slot = ChartSlot.Standard, ModeId = mode.name, LaneCount = 4,
                    CompletionTimeSeconds = 3, Notes = new[] { NoteData.Tap(0, 1) }
                } }
            };
            try
            {
                project.Audio = await store.ImportAsync(project.Id,
                    Path.GetFullPath("Assets/Game/Audio/TestPulse.wav"), default);
                await store.SaveAsync(project, default);
                var library = new SongLibrary();
                library.Register(new ProjectSongProvider(store, mode));
                string id = project.Id.ToString("D");
                string path = Path.Combine(store.GetSongFolder(project.Id), FileSongProjectStore.FileName);
                string json = File.ReadAllText(path);
                string audio = store.ResolveAudioPath(project.Id, project.Audio);
                byte[] audioBytes = File.ReadAllBytes(audio);

                File.Delete(path);
                var missingChart = await LoadFailure(library, id);
                Assert.That(missingChart.Failure.Kind, Is.EqualTo(SongLoadFailureKind.MissingChart));
                Assert.That(missingChart.InnerException, Is.InstanceOf<FileNotFoundException>());
                Assert.That(File.Exists(audio), Is.True);
                Assert.That(SongLoadErrorDisplay.FromException(missingChart).CanRetry, Is.False);

                File.WriteAllText(path, json);
                File.Delete(audio);
                var missingAudio = await LoadFailure(library, id);
                Assert.That(missingAudio.Failure.Kind, Is.EqualTo(SongLoadFailureKind.MissingAudio));
                library.Refresh();
                Assert.That(library.Find(id).Charts.Single().Failure.Kind,
                    Is.EqualTo(SongLoadFailureKind.MissingAudio));
                var cached = await LoadFailure(library, id);
                Assert.That(cached.Failure.Kind, Is.EqualTo(SongLoadFailureKind.MissingAudio));

                File.WriteAllBytes(audio, audioBytes);
                File.WriteAllText(path, "{broken json");
                library.Refresh();
                Assert.That(library.Find(id).Failure.Kind, Is.EqualTo(SongLoadFailureKind.InvalidChart));
                File.WriteAllText(path, json);
                library.Refresh();
                Assert.That(library.Find(id).Error, Is.Null);
                Assert.That(library.Find(id).Charts.Single().Error, Is.Null);
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>(async () =>
                    await library.LoadChartAsync(id, ChartSlot.Standard, canceled.Token));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static async Task<SongLoadException> LoadFailure(SongLibrary library, string id)
        {
            try
            {
                using var song = await library.LoadChartAsync(id, ChartSlot.Standard, default);
                Assert.Fail("Expected a structured loading failure.");
                return null;
            }
            catch (SongLoadException error)
            {
                return error;
            }
        }

        [Test]
        public void AudioBoundaryPreservesDecodeFailureAndCancellation()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Game/Settings/GameSettings.asset");
            var song = settings.catalog[0];
            var document = SongAssetAdapter.ToDocument(song);
            var cause = new InvalidOperationException("decode failed");
            var loader = new DocumentSongLoader(_ => song.Mode, (_, token) => throw cause);
            var error = Assert.ThrowsAsync<SongLoadException>(async () => await loader.LoadAsync(document, default));
            Assert.That(error.Failure.Kind, Is.EqualTo(SongLoadFailureKind.AudioLoadFailed));
            Assert.That(error.InnerException, Is.SameAs(cause));
            Assert.That(SongLoadErrorDisplay.FromException(error).CanRetry, Is.True);
            loader = new DocumentSongLoader(_ => song.Mode,
                (_, token) => throw new FileNotFoundException("audio"));
            error = Assert.ThrowsAsync<SongLoadException>(async () => await loader.LoadAsync(document, default));
            Assert.That(error.Failure.Kind, Is.EqualTo(SongLoadFailureKind.MissingAudio));
            loader = new DocumentSongLoader(_ => song.Mode, (_, token) => throw new OperationCanceledException());
            Assert.ThrowsAsync<OperationCanceledException>(async () => await loader.LoadAsync(document, default));
        }

        [Test]
        public void UnattributedFileExceptionsRemainOtherFailures()
        {
            var failure = SongLoadFailure.FromException(new FileNotFoundException("song.audio: missing"));
            Assert.That(failure.Kind, Is.EqualTo(SongLoadFailureKind.Other));
            Assert.That(new SongLoadErrorDisplay(failure).CanRetry, Is.True);
        }
    }
}
