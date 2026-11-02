using NUnit.Framework;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    public sealed class PlayKeyBindingsTests
    {
        private string previous;

        [SetUp]
        public void SetUp()
        {
            previous = PlayerPrefs.GetString(PlayKeyBindings.StorageKey, "");
            PlayerPrefs.DeleteKey(PlayKeyBindings.StorageKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(previous))
            {
                PlayerPrefs.DeleteKey(PlayKeyBindings.StorageKey);
            }
            else
            {
                PlayerPrefs.SetString(PlayKeyBindings.StorageKey, previous);
            }
            PlayerPrefs.Save();
        }

        [TestCase("invalid json")]
        [TestCase("{\"keys\":[0,0,0,0]}")]
        [TestCase("{\"keys\":[15]}")]
        public void InvalidSavedKeysFallBackToDefaults(string saved)
        {
            PlayerPrefs.SetString(PlayKeyBindings.StorageKey, saved);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.D));
            Assert.That(PlayKeyBindings.GetKey(3), Is.EqualTo(Key.K));
        }

        [TestCase(Key.None)]
        [TestCase(Key.Escape)]
        [TestCase(Key.Space)]
        [TestCase(Key.F)]
        public void InvalidReservedAndDuplicateKeysLeaveSavedBindingsUnchanged(Key key)
        {
            Assert.That(PlayKeyBindings.TrySetKey(0, key, out var message), Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.D));
        }
    }
}
