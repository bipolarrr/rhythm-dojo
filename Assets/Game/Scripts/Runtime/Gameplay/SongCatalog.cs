using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Song Catalog")]
    public sealed class SongCatalog : ScriptableObject
    {
        [SerializeField] private SongDefinition[] songs;
        public int Count => songs?.Length ?? 0;
        public SongDefinition this[int index] => songs[index];
        public void Validate()
        {
            if (Count == 0) throw new InvalidOperationException("Song catalog is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var song in songs)
            {
                if (!song) throw new InvalidOperationException("Catalog contains a missing song.");
                song.Validate();
                if (!ids.Add(song.SongId)) throw new InvalidOperationException("Duplicate song ID: " + song.SongId);
            }
        }
        public string GetEntryError(int index)
        {
            try
            {
                if (!songs[index]) return "Missing song";
                songs[index].Validate();
                for (int i = 0; i < Count; i++)
                    if (i != index && songs[i] && songs[i].SongId == songs[index].SongId) return "Duplicate song ID";
                return null;
            }
            catch (Exception e) { return e.Message; }
        }
        public void SetGeneratedDefaults(SongDefinition[] definitions) { songs = (SongDefinition[])definitions.Clone(); }
    }
}
