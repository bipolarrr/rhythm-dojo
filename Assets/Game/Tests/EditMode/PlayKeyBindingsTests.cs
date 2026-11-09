using NUnit.Framework;
using System.Collections.Generic;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.Tests
{
    public sealed class PlayKeyBindingsTests
    {
        private string previous;
        private string previousModes;
        private bool hadPreviousModes;
        private GameModeDefinition previousMode;
        private readonly List<GameModeDefinition> modes = new List<GameModeDefinition>();
        private Keyboard keyboard;
        private Keyboard previousKeyboard;

        [TestCase("<Keyboard>/a", "Left")]
        [TestCase("<Gamepad>/buttonSouth", "Pad South")]
        public void FourLaneModeKeepsItsOwnDefaultBindingAndLabel(string binding, string label)
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            mode.name = "IndependentFourLaneMode";
            mode.SetGeneratedDefaults(new[]
            {
                new LaneDefinition(binding, label, Color.white),
                new LaneDefinition("<Keyboard>/s", "Second", Color.white),
                new LaneDefinition("<Keyboard>/l", "Third", Color.white),
                new LaneDefinition("<Keyboard>/semicolon", "Fourth", Color.white),
            });

            try
            {
                Assert.That(PlayKeyBindings.GetBinding(mode, 0), Is.EqualTo(binding));
                Assert.That(PlayKeyBindings.GetLabel(mode, 0), Is.EqualTo(label));
            }
            finally
            {
                Object.DestroyImmediate(mode);
            }
        }

        [SetUp]
        public void SetUp()
        {
            previous = PlayerPrefs.GetString(PlayKeyBindings.StorageKey, "");
            previousModes = PlayerPrefs.GetString(PlayKeyBindings.ModeStorageKey, "");
            hadPreviousModes = PlayerPrefs.HasKey(PlayKeyBindings.ModeStorageKey);
            previousMode = PlayKeyBindings.ConfiguredDefaultMode;
            PlayKeyBindings.ConfigureDefaultMode(null);
            PlayerPrefs.DeleteKey(PlayKeyBindings.StorageKey);
            PlayerPrefs.DeleteKey(PlayKeyBindings.ModeStorageKey);
            previousKeyboard = Keyboard.current;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
            foreach (var mode in modes)
            {
                Object.DestroyImmediate(mode);
            }
            modes.Clear();
            PlayKeyBindings.ConfigureDefaultMode(previousMode);
            if (!hadPreviousModes)
            {
                PlayerPrefs.DeleteKey(PlayKeyBindings.ModeStorageKey);
            }
            else
            {
                PlayerPrefs.SetString(PlayKeyBindings.ModeStorageKey, previousModes);
            }
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

        [Test]
        public void LegacyImeDummyKeyLeavesSavedBindingsUnchanged()
        {
            var key = (Key)System.Enum.Parse(typeof(Key), "IMESelected");
            Assert.That(PlayKeyBindings.TrySetKey(0, key, out var message), Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(PlayKeyBindings.GetKey(0), Is.EqualTo(Key.D));
        }

        private GameModeDefinition Mode(string name, params string[] bindings)
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            mode.name = name;
            var lanes = new LaneDefinition[bindings.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = new LaneDefinition(bindings[i], name + " lane " + i, Color.white);
            }
            mode.SetGeneratedDefaults(lanes);
            modes.Add(mode);
            return mode;
        }

        private GameModeDefinition DefaultMode() => Mode(PlayKeyBindings.DefaultModeId,
            "<Keyboard>/d", "<Keyboard>/f", "<Keyboard>/j", "<Keyboard>/k");

        [Test]
        public void EqualLaneCountsKeepOverridesAndResetIsolatedByOriginalModeId()
        {
            var first = DefaultMode();
            var second = Mode("AlternateFour", "<Keyboard>/a", "<Keyboard>/s",
                "<Keyboard>/l", "<Keyboard>/semicolon");
            Assert.That(PlayKeyBindings.TrySetKey(first, first.name, 0, Key.Q, out _), Is.True);
            Assert.That(PlayKeyBindings.GetBinding(second, second.name, 0), Is.EqualTo("<Keyboard>/a"));
            Assert.That(PlayKeyBindings.TrySetKey(second, second.name, 0, Key.W, out _), Is.True);

            var clone = Object.Instantiate(second);
            modes.Add(clone);
            Assert.That(clone.name, Is.Not.EqualTo(second.name));
            Assert.That(PlayKeyBindings.GetBinding(clone, second.name, 0), Is.EqualTo("<Keyboard>/w"));
            PlayKeyBindings.Reset(first.name);
            Assert.That(PlayKeyBindings.GetBinding(first, first.name, 0), Is.EqualTo("<Keyboard>/d"));
            Assert.That(PlayKeyBindings.GetBinding(second, second.name, 0), Is.EqualTo("<Keyboard>/w"));
        }

        [Test]
        public void SixLaneOverridesUseDefinitionDefaultsAndPreserveGamepadLane()
        {
            var mode = Mode("SixKeys", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
                "<Keyboard>/j", "<Keyboard>/k", "<Gamepad>/buttonSouth");
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 4, Key.L, out _), Is.True);
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 4), Is.EqualTo("<Keyboard>/l"));
            Assert.That(PlayKeyBindings.GetLabel(mode, mode.name, 0), Is.EqualTo(mode.GetLane(0).displayName));
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 5, Key.O, out _), Is.False);
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 5), Is.EqualTo("<Gamepad>/buttonSouth"));
            PlayKeyBindings.Reset(mode.name);
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 4), Is.EqualTo("<Keyboard>/k"));
        }

        [TestCase("<Keyboard>/enter", Key.Enter)]
        [TestCase("<Keyboard>/ctrl", Key.LeftCtrl)]
        [TestCase("<Keyboard>/anyKey", Key.Q)]
        [TestCase("<Keyboard>/{Submit}", Key.Enter)]
        [TestCase("<Keyboard>/leftWindows", Key.LeftMeta)]
        public void ModeCommandBindingsReserveTheirPhysicalKeys(string binding, Key key)
        {
            var mode = DefaultMode();
            var serialized = new SerializedObject(mode);
            serialized.FindProperty("startBinding").stringValue = binding;
            serialized.FindProperty("returnBinding").stringValue = "<Keyboard>/backspace";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, key, out _), Is.False);
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, Key.Backspace, out _), Is.False);
        }

        [Test]
        public void SpaceIsAvailableWhenThisModeUsesDifferentCommands()
        {
            var mode = DefaultMode();
            var serialized = new SerializedObject(mode);
            serialized.FindProperty("startBinding").stringValue = "<Keyboard>/enter";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, Key.Space, out _), Is.True);
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 1, Key.Escape, out _), Is.False);
        }

        [Test]
        public void LegacyKeysMigrateOnlyToOriginalDefaultOnceAndRemainStored()
        {
            string saved = "{\"keys\":[" + (int)Key.A + "," + (int)Key.S + "," +
                (int)Key.L + "," + (int)Key.Semicolon + "]}";
            PlayerPrefs.SetString(PlayKeyBindings.StorageKey, saved);
            var other = Mode("OtherDefault", "<Keyboard>/q", "<Keyboard>/w",
                "<Keyboard>/e", "<Keyboard>/r");
            PlayKeyBindings.ConfigureDefaultMode(other);
            Assert.That(PlayKeyBindings.GetBinding(other, other.name, 0), Is.EqualTo("<Keyboard>/q"));
            var mode = DefaultMode();
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 0), Is.EqualTo("<Keyboard>/a"));
            Assert.That(PlayerPrefs.GetString(PlayKeyBindings.StorageKey), Is.EqualTo(saved));
            PlayKeyBindings.Reset(mode.name);
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 0), Is.EqualTo("<Keyboard>/d"));
            Assert.That(PlayerPrefs.GetString(PlayKeyBindings.StorageKey), Is.EqualTo(saved));
        }

        [TestCase("invalid json")]
        [TestCase("")]
        [TestCase("{\"version\":2,\"migrated\":true,\"modes\":[{\"modeId\":\"FourLaneMode\",\"overrides\":[{\"lane\":99,\"key\":15}]}]}")]
        public void CorruptNewStorageDoesNotResurrectLegacyKeys(string saved)
        {
            PlayerPrefs.SetString(PlayKeyBindings.StorageKey, "{\"keys\":[" + (int)Key.A + "," +
                (int)Key.S + "," + (int)Key.L + "," + (int)Key.Semicolon + "]}");
            PlayerPrefs.SetString(PlayKeyBindings.ModeStorageKey, saved);
            var mode = DefaultMode();
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 0), Is.EqualTo("<Keyboard>/d"));
        }

        [Test]
        public void ExistingModeRecordWinsOverLegacyEvenBeforeMigrationMarker()
        {
            PlayerPrefs.SetString(PlayKeyBindings.StorageKey, "{\"keys\":[" + (int)Key.A + "," +
                (int)Key.S + "," + (int)Key.L + "," + (int)Key.Semicolon + "]}");
            PlayerPrefs.SetString(PlayKeyBindings.ModeStorageKey,
                "{\"version\":2,\"migrated\":false,\"modes\":[{\"modeId\":\"FourLaneMode\",\"overrides\":[{\"lane\":0,\"key\":" +
                (int)Key.Q + "}]}]}");
            var mode = DefaultMode();
            Assert.That(PlayKeyBindings.GetBinding(mode, mode.name, 0), Is.EqualTo("<Keyboard>/q"));
        }

        [Test]
        public void SettingsBuilderUsesAssetDefaultsWithoutBakingSavedOverrides()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
            var mode = settings.catalog[0].Mode;
            Assert.That(PlayKeyBindings.TrySetKey(mode, mode.name, 0, Key.Q, out _), Is.True);
            var view = SettingsLayoutBuilder.Build();
            try
            {
                Assert.That(view.keyBindingButtons.Length, Is.EqualTo(mode.LaneCount));
                Assert.That(view.keyBindingButtons[0].GetComponentInChildren<UnityEngine.UI.Text>().text,
                    Is.EqualTo(mode.GetLane(0).displayName));
                Assert.That(view.keyRowTemplate, Is.Not.Null);
                Assert.That(view.keyRowsScroll.vertical, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
