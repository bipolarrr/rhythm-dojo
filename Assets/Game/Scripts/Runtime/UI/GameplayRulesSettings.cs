using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine.UI;
using PlayMode = RhythmDojo.Gameplay.PlayMode;

namespace RhythmDojo.UI
{
    public sealed class GameplayRulesSettings : IDisposable
    {
        private readonly Dropdown[] controls;
        private readonly IPlayRulesService service;
        private readonly UnityEngine.Events.UnityAction<int>[] actions;

        public GameplayRulesSettings(Dropdown[] controls, IPlayRulesService service)
        {
            this.controls = controls;
            this.service = service;

            var rules = service.Snapshot;
            string[][] labels =
            {
                new[] { "준비운동", "대련" },
                new[] { "Gauge", "Challenge" },
                new[] { "자동 완료", "떼는 타이밍 판정" },
                new[] { "끝까지 진행", "즉시 종료" },
                new[] { "Challenge 1", "Challenge 2", "Challenge 3" },
            };
            int[] values =
            {
                (int)rules.Profile,
                (int)rules.Mode,
                (int)rules.HoldEnd,
                (int)rules.GaugeFailure,
                rules.ChallengePreset - 1,
            };
            actions = new UnityEngine.Events.UnityAction<int>[controls.Length];

            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].ClearOptions();
                controls[i].AddOptions(new List<string>(labels[i]));
                controls[i].SetValueWithoutNotify(values[i]);
                actions[i] = _ => Save();
                controls[i].onValueChanged.AddListener(actions[i]);

                var nav = controls[i].navigation;
                nav.mode = Navigation.Mode.Automatic;
                controls[i].navigation = nav;
            }
        }

        private void Save()
        {
            var rules = service.Snapshot;
            rules.Profile = (JudgmentProfile)controls[0].value;
            rules.Mode = (PlayMode)controls[1].value;
            rules.HoldEnd = (HoldEndMode)controls[2].value;
            rules.GaugeFailure = (GaugeFailureMode)controls[3].value;
            rules.SelectChallenge(controls[4].value + 1);
            service.Update(rules);
        }

        public void Dispose()
        {
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].onValueChanged.RemoveListener(actions[i]);
            }
        }
    }
}
