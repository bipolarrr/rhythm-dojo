using System;
using System.Collections.Generic;
using System.Linq;
using RhythmDojo.Content;

namespace RhythmDojo.Services
{
    // Session state belongs to the selection service, independently of scene instances and rows.
    public sealed class SongSelectionList
    {
        private readonly List<SongEntry> entries = new List<SongEntry>();

        public int Sort { get; set; }
        public bool FavoritesOnly { get; set; }
        public IReadOnlyList<SongEntry> Entries => entries;

        public string Refresh(ISongLibrary library, ISelectionPreferences preferences, string selectedSongId)
        {
            IEnumerable<SongEntry> visible = library.Entries;
            if (FavoritesOnly)
            {
                visible = visible.Where(song => preferences.IsFavorite(song.Id));
            }

            switch (Sort)
            {
                case 1:
                    visible = visible.OrderBy(song => song.Artist, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(song => song.Title, StringComparer.OrdinalIgnoreCase);
                    break;
                case 2:
                    visible = visible.OrderBy(song => song.MinBpm)
                        .ThenBy(song => song.Title, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    visible = visible.OrderBy(song => song.Title, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            entries.Clear();
            entries.AddRange(visible);
            if (IndexOf(selectedSongId) >= 0)
            {
                return selectedSongId;
            }
            if (entries.Count == 0)
            {
                return null;
            }
            return entries[0].Id;
        }

        public int IndexOf(string songId) => entries.FindIndex(song => song.Id == songId);

        public string Move(string songId, int direction)
        {
            if (entries.Count == 0)
            {
                return null;
            }
            int index = (IndexOf(songId) + direction + entries.Count) % entries.Count;
            return entries[index].Id;
        }
    }
}
