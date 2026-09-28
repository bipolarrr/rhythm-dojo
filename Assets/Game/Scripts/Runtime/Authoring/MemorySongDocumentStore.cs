using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;

namespace RhythmDojo.Authoring
{
    // Reference implementation for authoring integration; no disk format is implied.
    public sealed class MemorySongDocumentStore : ISongDocumentStore
    {
        private readonly Dictionary<string, SongDocument> documents = new Dictionary<string, SongDocument>(StringComparer.Ordinal);
        public Task SaveAsync(SongDocument document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document == null || string.IsNullOrWhiteSpace(document.SongId)) throw new ArgumentException("SongId: required.");
            documents[document.SongId] = document.Copy(); return Task.CompletedTask;
        }
        public Task<SongDocument> LoadAsync(string songId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!documents.TryGetValue(songId, out var document)) throw new KeyNotFoundException(songId);
            return Task.FromResult(document.Copy());
        }
    }
}
