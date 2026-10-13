using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Services
{
    [Serializable]
    public sealed class SongRecord
    {
        public string songId,
            difficulty;
        public int score,
            maxCombo;
        public double rate;
        public bool failed,
            gaugeClear;
        public int challengeAchievements;

        public string Rank
        {
            get
            {
                if (rate >= 95)
                {
                    return "S";
                }
                else if (rate >= 90)
                {
                    return "A";
                }
                else if (rate >= 80)
                {
                    return "B";
                }
                else if (rate >= 70)
                {
                    return "C";
                }
                else
                {
                    return "D";
                }
            }
        }
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
        void SaveCompleted(
            string songId,
            string difficulty,
            SessionSnapshot snapshot,
            int maxCombo
        );
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
            public int recordVersion;
        }

        private readonly string key;
        private Data data;

        public SelectionPreferences(string storageKey = StorageKey)
        {
            key = storageKey;

            try
            {
                if (key == null)
                {
                    data = new Data();
                }
                else
                {
                    data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key, ""));
                }
            }
            catch (ArgumentException)
            {
                data = null;
            }

            data ??= new Data();
            data.favorites ??= new List<string>();
            data.records ??= new List<SongRecord>();

            if (data.recordVersion != 2)
            {
                data.records.Clear();
                data.recordVersion = 2;
                Save();
            }

            if (!Enum.IsDefined(typeof(ScrollMode), data.scrollMode))
            {
                data.scrollMode = ScrollMode.Constant;
            }

            if (!double.IsFinite(data.multiplier) || data.multiplier <= 0)
            {
                data.multiplier = 1;
            }

            data.timingOffsetMs = Mathf.Clamp(data.timingOffsetMs, -200, 200);
        }

        public string Difficulty
        {
            get => data.difficulty;
            set => data.difficulty = value;
        }

        public ScrollMode ScrollMode
        {
            get => data.scrollMode;
            set => data.scrollMode = value;
        }

        public double Multiplier
        {
            get => data.multiplier;
            set => data.multiplier = value;
        }

        public int TimingOffsetMs
        {
            get => data.timingOffsetMs;
            set => data.timingOffsetMs = Mathf.Clamp(value, -200, 200);
        }

        public bool IsFavorite(string id) => id != null && data.favorites.Contains(id);

        public void ToggleFavorite(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!data.favorites.Remove(id))
            {
                data.favorites.Add(id);
            }

            Save();
        }

        public SongRecord FindRecord(string id, string difficulty) =>
            data.records.Find(r =>
            {
                if ((!(r != null) || !(r.songId == id)) || !(r.difficulty == difficulty))
                {
                    return false;
                }

                return !r.failed;
            });

        public SongRecord FindFailedRecord(string id, string difficulty) =>
            data.records.Find(r =>
            {
                if ((!(r != null) || !(r.songId == id)) || !(r.difficulty == difficulty))
                {
                    return false;
                }

                return r.failed;
            });

        public void SaveCompleted(
            string id,
            string difficulty,
            SessionSnapshot snapshot,
            int maxCombo
        )
        {
            if (
                snapshot.State != SessionState.Completed
                || snapshot.TotalNotes <= 0
                || string.IsNullOrEmpty(id)
            )
            {
                return;
            }

            double rate = snapshot.AccuracyRate;
            int score = ScoreRules.Score(rate);
            SongRecord record;

            if (snapshot.Failure.Failed)
            {
                record = FindFailedRecord(id, difficulty);
            }
            else
            {
                record = FindRecord(id, difficulty);
            }

            if (record != null)
            {
                if (record.score > score || record.score == score && record.maxCombo >= maxCombo)
                {
                    return;
                }
            }

            if (record == null)
            {
                record = new SongRecord
                {
                    songId = id,
                    difficulty = difficulty,
                    failed = snapshot.Failure.Failed,
                };
                data.records.Add(record);
            }

            record.score = score;
            record.rate = rate;
            record.maxCombo = maxCombo;
            Save();
        }

        public void Save()
        {
            if (key == null)
            {
                return;
            }

            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void SaveCompleted(
            string id,
            string difficulty,
            SessionSnapshot snapshot,
            int maxCombo,
            PlayRules rules
        )
        {
            SaveCompleted(id, difficulty, snapshot, maxCombo);

            if (snapshot.State != SessionState.Completed || snapshot.Failure.Failed)
            {
                return;
            }

            var record = FindRecord(id, difficulty);

            if (record == null)
            {
                return;
            }

            if (rules.Mode == RhythmDojo.Gameplay.PlayMode.Gauge)
            {
                record.gaugeClear = true;
            }
            else
            {
                record.challengeAchievements |= 1 << (rules.ChallengePreset - 1);
            }

            Save();
        }
    }

    public interface ITitleNavigation
    {
        void ShowTitle();
    }
}
