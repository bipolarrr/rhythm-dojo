using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Game Settings")]
    public sealed class GameSettings : ScriptableObject
    {
        public SongCatalog catalog;
        public DifficultyProfile[] difficulties;
        public DifficultyProfile defaultDifficulty;
        public ScrollSettings scroll;
        public AudioPlaybackSettings audio;
        public GameplayPresentationSettings presentation;
        public void Validate()
        {
            ValidateOptions(); catalog.Validate();
        }
        public void ValidateOptions()
        {
            if (!catalog || catalog.Count == 0 || difficulties == null || difficulties.Length == 0 ||
                !defaultDifficulty || !scroll || !audio || !presentation) throw new InvalidOperationException("Game settings references missing.");
            var ids = new System.Collections.Generic.HashSet<string>(); bool foundDefault = false;
            foreach (var profile in difficulties)
            {
                if (!profile) throw new InvalidOperationException("Missing difficulty profile.");
                profile.Validate();
                if (!ids.Add(profile.Id)) throw new InvalidOperationException("Duplicate difficulty ID.");
                foundDefault |= profile == defaultDifficulty;
            }
            if (!foundDefault) throw new InvalidOperationException("Default difficulty must be in the list.");
            scroll.Validate(); audio.Validate(); presentation.Validate();
        }
    }
}
