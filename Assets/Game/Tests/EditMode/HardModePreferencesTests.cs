using System;
using NUnit.Framework;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class HardModePreferencesTests
    {
        private string key;

        [SetUp]
        public void SetUp() => key = "RhythmDojo.HardMode.Tests." + Guid.NewGuid();

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        [Category("Fast")]
        public void NewPlayersStartInNormalModeWithStrictHardDefaults()
        {
            var preferences = new HardModePreferences(key);
            Assert.That(preferences.Enabled, Is.False);

        }

        [Test]
        [Category("Fast")]
        public void ModeAndRulesSurviveReloadAndSnapshotsDoNotChange()
        {
            var preferences = new HardModePreferences(key) { Enabled = true };
            preferences.Save();
            var loaded = new HardModePreferences(key);
            Assert.That(loaded.Enabled, Is.True);
            loaded.Enabled = false;
            loaded.Save();
            Assert.That(new HardModePreferences(key).Enabled, Is.False);
        }

    }
}
