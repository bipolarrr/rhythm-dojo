using System;
using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Services
{
    public sealed class HardModePreferences
    {
        public const string StorageKey = "RhythmDojo.HardMode.v1";
        [Serializable]
        private sealed class Data
        {
            public bool enabled;
            public int perfectAction;
            public int goodAction = (int)HealthRuleAction.GameOver;
            public int missAction = (int)HealthRuleAction.GameOver;
            public int perfectCount = 10, goodCount = 10, missCount = 10;
        }
        private readonly string key;
        private readonly Data data;
        public bool Enabled { get => data.enabled; set => data.enabled = value; }
        public HardModePreferences(string storageKey = StorageKey)
        {
            key = storageKey;
            try { data = key == null ? new Data() : JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key, "")); }
            catch (ArgumentException) { data = null; }
            data ??= new Data();
            for (int i = 0; i < 3; i++)
            {
                var grade = (Judgment)i;
                int action = i == 0 ? data.perfectAction : i == 1 ? data.goodAction : data.missAction;
                int count = i == 0 ? data.perfectCount : i == 1 ? data.goodCount : data.missCount;
                SetRule(grade, Enum.IsDefined(typeof(HealthRuleAction), action) ? (HealthRuleAction)action :
                    i == 0 ? HealthRuleAction.Preserve : HealthRuleAction.GameOver, count);
            }
        }
        public static HardModePreferences Load() => new HardModePreferences();
        public HardModeHealthRule GetRule(Judgment grade) => grade switch
        {
            Judgment.Perfect => new HardModeHealthRule((HealthRuleAction)data.perfectAction, data.perfectCount),
            Judgment.Good => new HardModeHealthRule((HealthRuleAction)data.goodAction, data.goodCount),
            Judgment.Miss => new HardModeHealthRule((HealthRuleAction)data.missAction, data.missCount),
            _ => throw new ArgumentOutOfRangeException(nameof(grade))
        };
        public void SetRule(Judgment grade, HealthRuleAction action, int count)
        {
            if (!Enum.IsDefined(typeof(HealthRuleAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            count = Mathf.Clamp(count, 1, HardModeHealthRule.MaximumLossCount);
            switch (grade)
            {
                case Judgment.Perfect: data.perfectAction = (int)action; data.perfectCount = count; break;
                case Judgment.Good: data.goodAction = (int)action; data.goodCount = count; break;
                case Judgment.Miss: data.missAction = (int)action; data.missCount = count; break;
                default: throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }
        public HardModeHealthRules CreateRules() => new HardModeHealthRules(
            GetRule(Judgment.Perfect), GetRule(Judgment.Good), GetRule(Judgment.Miss));
        public void Save() { if (key == null) return; PlayerPrefs.SetString(key, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
    }
}
