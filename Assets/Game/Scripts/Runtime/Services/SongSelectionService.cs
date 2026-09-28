using System;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Services
{
    public sealed class SongSelectionService : ISongSelectionService
    {
        private readonly GameSettings settings;
        public ISongLibrary Library { get; }
        public string SelectedSongId { get; private set; }
        public DifficultyProfile SelectedDifficulty { get; private set; }
        public ScrollMode SelectedScrollMode { get; private set; }
        public double SelectedMultiplier { get; private set; } = 1;
        public SongSelectionService(GameSettings settings, ISongLibrary library)
        {
            this.settings = settings; Library = library; SelectedDifficulty = settings.defaultDifficulty;
            SelectedSongId = library.Entries.Count > 0 ? library.Entries[0].Id : null;
        }
        public void UpdateSelection(string songId, DifficultyProfile difficulty, ScrollMode mode, double multiplier)
        {
            if (Library.Find(songId) == null) throw new ArgumentException("Song not registered.");
            if (Array.IndexOf(settings.difficulties, difficulty) < 0) throw new ArgumentException("Unregistered difficulty.");
            bool known = false;
            for (int i = 0; i < settings.scroll.MultiplierCount; i++) known |= settings.scroll.GetMultiplier(i) == multiplier;
            if (!known || !Enum.IsDefined(typeof(ScrollMode), mode)) throw new ArgumentException("Invalid scroll selection.");
            SelectedSongId = songId; SelectedDifficulty = difficulty; SelectedScrollMode = mode; SelectedMultiplier = multiplier;
        }
    }
}
