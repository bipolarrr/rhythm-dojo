using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Services
{
    [Serializable]
    public sealed class SongRecord
    {
        public string songId, difficulty;
        public int score, maxCombo;
        public double rate;
        public string Rank => rate >= 95 ? "S" : rate >= 90 ? "A" : rate >= 80 ? "B" : rate >= 70 ? "C" : "D";
    }

    public interface ISelectionPreferences
    {
        string Difficulty { get; set; }
        ScrollMode ScrollMode { get; set; }
        double Multiplier { get; set; }
        int TimingOffsetMs { get; set; }
        bool IsFavorite(string songId);
        void ToggleFavorite(string songId);
        SongRecord FindRecord(string songId, string difficulty);
        void SaveCompleted(string songId, string difficulty, SessionSnapshot snapshot, int maxCombo);
        void Save();
    }

    public sealed class SelectionPreferences : ISelectionPreferences
    {
        public const string StorageKey = "RhythmDojo.Selection.v1";
        [Serializable]
        private sealed class Data
        {
            public string difficulty;
            public ScrollMode scrollMode;
            public double multiplier = 1;
            public int timingOffsetMs;
            public List<string> favorites = new List<string>();
            public List<SongRecord> records = new List<SongRecord>();
        }
        private readonly string key;
        private Data data;
        public SelectionPreferences(string storageKey = StorageKey)
        {
            key = storageKey;
            try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key, "")); }
            catch (ArgumentException) { data = null; }
            data ??= new Data();
            data.favorites ??= new List<string>();
            data.records ??= new List<SongRecord>();
            if (!Enum.IsDefined(typeof(ScrollMode), data.scrollMode)) data.scrollMode = ScrollMode.Constant;
            if (!double.IsFinite(data.multiplier) || data.multiplier <= 0) data.multiplier = 1;
            data.timingOffsetMs = Mathf.Clamp(data.timingOffsetMs, -200, 200);
        }
        public string Difficulty { get => data.difficulty; set => data.difficulty = value; }
        public ScrollMode ScrollMode { get => data.scrollMode; set => data.scrollMode = value; }
        public double Multiplier { get => data.multiplier; set => data.multiplier = value; }
        public int TimingOffsetMs { get => data.timingOffsetMs; set => data.timingOffsetMs = Mathf.Clamp(value, -200, 200); }
        public bool IsFavorite(string id) => id != null && data.favorites.Contains(id);
        public void ToggleFavorite(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!data.favorites.Remove(id)) data.favorites.Add(id);
            Save();
        }
        public SongRecord FindRecord(string id, string difficulty) =>
            data.records.Find(r => r != null && r.songId == id && r.difficulty == difficulty);
        public void SaveCompleted(string id, string difficulty, SessionSnapshot snapshot, int maxCombo)
        {
            if (snapshot.State != SessionState.Completed || snapshot.TotalNotes <= 0 || string.IsNullOrEmpty(id)) return;
            double rate = (snapshot.Perfect + snapshot.Good * .5) / snapshot.TotalNotes * 100;
            int score = (int)Math.Round(rate * 10000, MidpointRounding.AwayFromZero);
            var record = FindRecord(id, difficulty);
            if (record != null && (record.score > score || (record.score == score && record.maxCombo >= maxCombo))) return;
            if (record == null) { record = new SongRecord { songId = id, difficulty = difficulty }; data.records.Add(record); }
            record.score = score; record.rate = rate; record.maxCombo = maxCombo; Save();
        }
        public void Save() { PlayerPrefs.SetString(key, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
    }

    public interface ITitleNavigation
    {
        void ShowTitle();
    }
}