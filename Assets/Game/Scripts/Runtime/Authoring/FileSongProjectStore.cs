using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;

namespace RhythmDojo.Authoring
{
    public sealed class FileSongProjectStore : ISongProjectStore, IProjectAudioImporter
    {
        public const string FileName = "project.rdchart.json";
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private readonly string root;
        private readonly SongProjectJson json = new SongProjectJson();
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        public FileSongProjectStore(string songsRoot) { root = Path.GetFullPath(songsRoot); }
        public string GetSongFolder(Guid songId)
        {
            ProjectValidation.Require(songId != Guid.Empty, "song.id", "Nonempty UUID required.");
            return Path.Combine(root, songId.ToString("D"));
        }
        public string ResolveAudioPath(Guid songId, string relativePath)
        {
            ProjectValidation.AudioPath(relativePath);
            if (relativePath == null) throw new ProjectValidationException("song.audio", "Audio not assigned.");
            var folder = GetSongFolder(songId);
            var path = Path.GetFullPath(Path.Combine(folder, relativePath));
            if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ProjectValidationException("song.audio", "Path escapes song folder.");
            RejectLinks(path);
            return path;
        }
        // Refuse junctions/symlinks as well as lexical traversal, including in ancestor directories.
        private static void RejectLinks(string path)
        {
            for (string p = path; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Project paths cannot traverse a symbolic link: " + p);
        }
        public bool AudioExists(Guid songId, string relativePath) => relativePath != null && File.Exists(ResolveAudioPath(songId, relativePath));
        public Task SaveAsync(SongProject project, CancellationToken cancellationToken)
        {
            // Capture synchronously: callers may mutate their detached snapshot immediately after this call.
            string text = json.Serialize(project);
            string folder = GetSongFolder(project.Id);
            return SaveSnapshotAsync(folder, text, cancellationToken);
        }
        private async Task SaveSnapshotAsync(string folder, string text, CancellationToken token)
        {
            var gate = Gates.GetOrAdd(folder, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await Task.Run(() => {
                    token.ThrowIfCancellationRequested(); RejectLinks(folder); Directory.CreateDirectory(folder);
                    string target = Path.Combine(folder, FileName), temporary = Path.Combine(folder, "." + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        RejectLinks(target);
                        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] bytes = Utf8.GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                        }
                        token.ThrowIfCancellationRequested();
                        // The commit point: never report cancellation after this succeeds.
                        if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }, token).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        public async Task<SongProject> LoadAsync(Guid songId, CancellationToken cancellationToken)
        {
            string path = Path.Combine(GetSongFolder(songId), FileName);
            return await Task.Run(() => {
                cancellationToken.ThrowIfCancellationRequested(); RejectLinks(path);
                var project = json.Deserialize(File.ReadAllText(path, Utf8));
                ProjectValidation.Require(project.Id == songId, "song.id", "Document UUID does not match folder UUID.");
                cancellationToken.ThrowIfCancellationRequested(); return project;
            }, cancellationToken).ConfigureAwait(false);
        }
        public async Task<string> ImportAsync(Guid songId, string sourcePath, CancellationToken cancellationToken)
        {
            string folder = GetSongFolder(songId);
            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            ProjectValidation.Require(extension.Length > 1 && extension.Length <= 16 &&
                System.Linq.Enumerable.All(extension.Substring(1), char.IsLetterOrDigit), "song.audio", "Audio extension required.");
            var gate = Gates.GetOrAdd(folder, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => {
                    cancellationToken.ThrowIfCancellationRequested();
                    string audioFolder = Path.Combine(folder, "audio"); RejectLinks(audioFolder); Directory.CreateDirectory(audioFolder);
                    string temporary = Path.Combine(audioFolder, "." + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        using var hash = SHA256.Create();
                        using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            var buffer = new byte[81920]; int count;
                            while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
                            { cancellationToken.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); hash.TransformBlock(buffer, 0, count, buffer, 0); }
                            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); output.Flush(true);
                        }
                        string relative = "audio/" + BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant() + extension;
                        string destination = ResolveAudioPath(songId, relative);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
                        return relative;
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
    }
}
