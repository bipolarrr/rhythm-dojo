using System;
using RhythmDojo.Gameplay;
using UnityEngine;
using PlayMode = RhythmDojo.Gameplay.PlayMode;

namespace RhythmDojo.Services
{
    public interface IPlayRulesService
    {
        PlayRules Snapshot { get; }
        void Update(PlayRules rules);
    }
    public sealed class PlayRulesService : IPlayRulesService
    {
        public const string StorageKey = "RhythmDojo.PlayRules.v2";
        [Serializable]
        private sealed class Data
        {
            public JudgmentProfile profile;
            public PlayMode mode;
            public HoldEndMode holdEnd;
            public GaugeFailureMode failure;
            public int challenge = 1;
        }
        private PlayRules rules = new PlayRules();
        public PlayRules Snapshot => rules.Copy();
        public PlayRulesService()
        {
            try
            {
                var data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(StorageKey, ""));
                if (data == null) return;
                rules.Profile = data.profile; rules.Mode = data.mode; rules.HoldEnd = data.holdEnd;
                rules.GaugeFailure = data.failure; rules.SelectChallenge(data.challenge); rules.Validate();
            }
            catch (ArgumentException) { rules = new PlayRules(); }
        }
        public void Update(PlayRules rules)
        {
            rules.Validate(); this.rules = rules.Copy();
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(new Data { profile = rules.Profile, mode = rules.Mode,
                holdEnd = rules.HoldEnd, failure = rules.GaugeFailure, challenge = rules.ChallengePreset }));
            PlayerPrefs.Save();
        }
    }
}
