using System;
using NUnit.Framework;
using UnityEngine;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;

namespace RhythmDojo.Tests
{
    public sealed class HardModePreferencesTests
    {
        private string key;
        [SetUp] public void SetUp() => key="RhythmDojo.HardMode.Tests."+Guid.NewGuid();
        [TearDown] public void TearDown() { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        [Test]
        public void NewPlayersStartInNormalModeWithStrictHardDefaults()
        {
            var preferences=new HardModePreferences(key);
            Assert.That(preferences.Enabled,Is.False);
            Assert.That(preferences.GetRule(Judgment.Perfect).Action,Is.EqualTo(HealthRuleAction.Preserve));
            Assert.That(preferences.GetRule(Judgment.Good).Action,Is.EqualTo(HealthRuleAction.GameOver));
            Assert.That(preferences.GetRule(Judgment.Miss).Action,Is.EqualTo(HealthRuleAction.GameOver));
        }
        [Test]
        public void ModeAndRulesSurviveReloadAndSnapshotsDoNotChange()
        {
            var preferences=new HardModePreferences(key) { Enabled=true };
            preferences.SetRule(Judgment.Perfect,HealthRuleAction.Damage,7);
            preferences.SetRule(Judgment.Good,HealthRuleAction.Damage,3);
            preferences.SetRule(Judgment.Miss,HealthRuleAction.GameOver,10); preferences.Save();
            var snapshot=preferences.CreateRules();
            preferences.SetRule(Judgment.Good,HealthRuleAction.Preserve,9);
            Assert.That(snapshot.Good.Action,Is.EqualTo(HealthRuleAction.Damage)); Assert.That(snapshot.Good.LossCount,Is.EqualTo(3));
            var loaded=new HardModePreferences(key);
            Assert.That(loaded.Enabled,Is.True); Assert.That(loaded.GetRule(Judgment.Perfect).LossCount,Is.EqualTo(7));
            Assert.That(loaded.GetRule(Judgment.Good).LossCount,Is.EqualTo(3));
            Assert.That(loaded.GetRule(Judgment.Good).Action,Is.EqualTo(HealthRuleAction.Damage));
        }
        [Test]
        public void InvalidStoredValuesAreClampedAndUnknownActionsUseSafeDefaults()
        {
            PlayerPrefs.SetString(key,"{\"enabled\":true,\"perfectAction\":99,\"goodAction\":99,\"missAction\":99,\"perfectCount\":-1,\"goodCount\":9999,\"missCount\":0}");
            var preferences=new HardModePreferences(key);
            Assert.That(preferences.GetRule(Judgment.Perfect).Action,Is.EqualTo(HealthRuleAction.Preserve));
            Assert.That(preferences.GetRule(Judgment.Good).Action,Is.EqualTo(HealthRuleAction.GameOver));
            Assert.That(preferences.GetRule(Judgment.Perfect).LossCount,Is.EqualTo(1));
            Assert.That(preferences.GetRule(Judgment.Good).LossCount,Is.EqualTo(1000));
            Assert.That(preferences.GetRule(Judgment.Miss).LossCount,Is.EqualTo(1));
        }
    }
}
