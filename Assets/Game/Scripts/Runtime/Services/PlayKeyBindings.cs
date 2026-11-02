using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RhythmDojo.Services
{
    public static class PlayKeyBindings
    {
        public const string StorageKey = "RhythmDojo.PlayKeys.v1";
        public const string ModeStorageKey = "RhythmDojo.PlayKeys.v2";
        public const string DefaultModeId = "FourLaneMode";
        private static readonly Key[] defaults = { Key.D, Key.F, Key.J, Key.K };
        private static GameModeDefinition defaultMode;
        public static GameModeDefinition ConfiguredDefaultMode => defaultMode;
        public static event Action Changed;

        [Serializable]
        private sealed class Data
        {
            public Key[] keys;
        }

        [Serializable]
        private sealed class ModeData
        {
            public string modeId;
            public LaneOverride[] overrides;
        }

        [Serializable]
        private sealed class LaneOverride
        {
            public int lane;
            public Key key;
        }

        [Serializable]
        private sealed class SavedModes
        {
            public int version = 2;
            public bool migrated;
            public List<ModeData> modes = new List<ModeData>();
        }

        public static void ConfigureDefaultMode(GameModeDefinition mode)
        {
            defaultMode = null;
            if (mode && mode.name == DefaultModeId)
            {
                defaultMode = mode;
            }
        }

        public static Key GetKey(int lane)
        {
            if (defaultMode)
            {
                return GetKey(defaultMode, DefaultModeId, lane);
            }
            return Load()[lane];
        }

        public static string GetLabel(int lane) => KeyLabel(GetKey(lane));

        public static string GetLabel(GameModeDefinition mode, int lane) =>
            GetLabel(mode, mode.name, lane);

        public static string GetLabel(GameModeDefinition mode, string modeId, int lane)
        {
            Validate(mode, modeId, lane);
            var keys = Load(mode, modeId);
            if (keys[lane] != Key.None)
            {
                return KeyLabel(keys[lane]);
            }
            return mode.GetLane(lane).displayName;
        }

        private static string KeyLabel(Key key)
        {
            string name = key.ToString();
            if (Keyboard.current != null)
            {
                var displayName = Keyboard.current[key].displayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    name = displayName;
                }
            }
            return name.ToUpperInvariant();
        }

        public static string GetBinding(GameModeDefinition mode, int lane) =>
            GetBinding(mode, mode.name, lane);

        public static string GetBinding(GameModeDefinition mode, string modeId, int lane)
        {
            Validate(mode, modeId, lane);
            var keys = Load(mode, modeId);
            if (keys[lane] != Key.None)
            {
                return KeyBinding(keys[lane]);
            }
            return mode.GetLane(lane).binding;
        }

        private static string KeyBinding(Key key)
        {
            string name = key.ToString();
            if (Keyboard.current != null)
            {
                name = Keyboard.current[key].name;
            }
            else if (name.StartsWith("Digit", StringComparison.Ordinal))
            {
                name = name.Substring(5);
            }
            else
            {
                name = char.ToLowerInvariant(name[0]) + name.Substring(1);
            }
            return "<Keyboard>/" + name;
        }

        public static Key GetKey(GameModeDefinition mode, string modeId, int lane)
        {
            TryKeyboardKey(GetBinding(mode, modeId, lane), out var key);
            return key;
        }

        public static bool CanEdit(GameModeDefinition mode, int lane) =>
            TryKeyboardKey(mode.GetLane(lane).binding, out _);

        private static bool TryKeyboardKey(string binding, out Key key)
        {
            key = Key.None;
            const string prefix = "<Keyboard>/";
            if (binding == null || !binding.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (Keyboard.current != null)
            {
                var controls = InputControlPath.TryFindControls(Keyboard.current, binding);
                if (controls.Length == 1 && controls[0] is KeyControl control)
                {
                    key = control.keyCode;
                    return true;
                }
                return false;
            }
            string name = binding.Substring(prefix.Length);
            if (name.Length == 1 && char.IsDigit(name[0]))
            {
                name = "Digit" + name;
            }
            if (!Enum.TryParse(name, true, out key) || key == Key.None)
            {
                return false;
            }
            return Enum.IsDefined(typeof(Key), key);
        }

        public static bool TrySetKey(int lane, Key key, out string message)
        {
            if (defaultMode)
            {
                return TrySetKey(defaultMode, DefaultModeId, lane, key, out message);
            }
            // Compatibility callers can run before scene composition supplies the default asset.
            var mode = LegacyMode();
            try
            {
                return TrySetKey(mode, DefaultModeId, lane, key, out message);
            }
            finally
            {
                DestroyLegacyMode(mode);
            }
        }

        public static bool TrySetKey(GameModeDefinition mode, string modeId, int lane, Key key,
            out string message)
        {
            Validate(mode, modeId, lane);
            if (!CanEdit(mode, lane))
            {
                message = "이 레인은 키보드 바인딩이 아니므로 변경할 수 없습니다.";
                return false;
            }
            if (!Allowed(key) || ConflictsWithCommands(mode, key))
            {
                message = "이 키는 사용할 수 없습니다. Esc는 취소, 시작·복귀 키는 예약되어 있습니다.";
                return false;
            }
            var keys = Load(mode, modeId);
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == lane)
                {
                    continue;
                }
                var existing = keys[i];
                if (existing == Key.None)
                {
                    TryKeyboardKey(mode.GetLane(i).binding, out existing);
                }
                bool conflict = existing == key;
                if (existing == Key.None)
                {
                    conflict = MatchesKey(mode.GetLane(i).binding, key);
                }
                if (conflict)
                {
                    message = (i + 1) + "번 레인에서 사용 중인 키입니다. 다른 키를 눌러 주세요.";
                    return false;
                }
            }
            keys[lane] = key;
            if (TryKeyboardKey(mode.GetLane(lane).binding, out var original) && original == key)
            {
                keys[lane] = Key.None;
            }
            var saved = ReadModes();
            SaveMode(saved, modeId, keys);
            WriteModes(saved);
            Changed?.Invoke();
            message = "저장했습니다. 플레이 화면에도 바로 적용됩니다.";
            return true;
        }

        public static void Reset() => Reset(DefaultModeId);

        public static void Reset(string modeId)
        {
            if (string.IsNullOrWhiteSpace(modeId))
            {
                throw new ArgumentException("ModeId is required.", nameof(modeId));
            }
            var saved = ReadModes();
            saved.modes.RemoveAll(value => value != null && value.modeId == modeId);
            if (modeId == DefaultModeId)
            {
                saved.migrated = true;
            }
            WriteModes(saved);
            Changed?.Invoke();
        }

        private static bool Allowed(Key key)
        {
            if (!Enum.IsDefined(typeof(Key), key) || key == Key.None)
            {
                return false;
            }
            return key != Key.Escape && key != Key.IMESelected;
        }

        private static bool ConflictsWithCommands(GameModeDefinition mode, Key key)
        {
            return MatchesKey(mode.StartBinding, key) || MatchesKey(mode.ReturnBinding, key);
        }

        private static bool MatchesKey(string binding, Key key)
        {
            if (Keyboard.current == null)
            {
                if (TryKeyboardKey(binding, out var parsed))
                {
                    return parsed == key;
                }
                // Without a keyboard, an unresolved keyboard command cannot be safely rebound.
                return binding.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase);
            }
            var target = Keyboard.current[key].stateBlock;
            ulong start = (ulong)target.byteOffset * 8 + target.bitOffset;
            ulong end = start + target.sizeInBits;
            foreach (var control in InputControlPath.TryFindControls(Keyboard.current, binding))
            {
                var block = control.stateBlock;
                ulong first = (ulong)block.byteOffset * 8 + block.bitOffset;
                ulong last = first + block.sizeInBits;
                if (first < end && start < last)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Validate(GameModeDefinition mode, string modeId, int lane)
        {
            if (!mode)
            {
                throw new ArgumentNullException(nameof(mode));
            }
            if (string.IsNullOrWhiteSpace(modeId))
            {
                throw new ArgumentException("ModeId is required.", nameof(modeId));
            }
            if (lane < 0 || lane >= mode.LaneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }
        }

        private static SavedModes ReadModes()
        {
            bool exists = PlayerPrefs.HasKey(ModeStorageKey);
            try
            {
                var saved = JsonUtility.FromJson<SavedModes>(PlayerPrefs.GetString(ModeStorageKey, ""));
                if (saved != null && saved.version == 2)
                {
                    saved.modes ??= new List<ModeData>();
                    return saved;
                }
            }
            catch (ArgumentException)
            {
            }
            // A present but corrupt new store still takes precedence over legacy preferences.
            return new SavedModes { migrated = exists };
        }

        private static void WriteModes(SavedModes saved)
        {
            PlayerPrefs.SetString(ModeStorageKey, JsonUtility.ToJson(saved));
            PlayerPrefs.Save();
        }

        private static void SaveMode(SavedModes saved, string modeId, Key[] keys)
        {
            var changes = new List<LaneOverride>();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != Key.None)
                {
                    changes.Add(new LaneOverride { lane = i, key = keys[i] });
                }
            }
            saved.modes.RemoveAll(value => value != null && value.modeId == modeId);
            saved.modes.Add(new ModeData { modeId = modeId, overrides = changes.ToArray() });
        }

        private static Key[] Load(GameModeDefinition mode, string modeId)
        {
            var saved = ReadModes();
            MigrateLegacy(mode, modeId, saved);
            var keys = new Key[mode.LaneCount];
            if (saved.modes.FindAll(value => value != null && value.modeId == modeId).Count > 1)
            {
                return keys;
            }
            var entry = saved.modes.Find(value => value != null && value.modeId == modeId);
            if (entry?.overrides == null)
            {
                return keys;
            }
            var seen = new HashSet<int>();
            foreach (var change in entry.overrides)
            {
                if (change == null || change.lane < 0 || change.lane >= keys.Length)
                {
                    return new Key[mode.LaneCount];
                }
                if (!seen.Add(change.lane) || !CanEdit(mode, change.lane))
                {
                    return new Key[mode.LaneCount];
                }
                if (!Allowed(change.key) || ConflictsWithCommands(mode, change.key))
                {
                    return new Key[mode.LaneCount];
                }
                keys[change.lane] = change.key;
            }
            var used = new HashSet<Key>();
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (key == Key.None)
                {
                    TryKeyboardKey(mode.GetLane(i).binding, out key);
                }
                if (key != Key.None && !used.Add(key))
                {
                    return new Key[mode.LaneCount];
                }
                if (keys[i] != Key.None && ConflictsWithDefaultLane(mode, keys, i))
                {
                    return new Key[mode.LaneCount];
                }
            }
            return keys;
        }

        private static void MigrateLegacy(GameModeDefinition mode, string modeId, SavedModes saved)
        {
            if (modeId != DefaultModeId || saved.migrated)
            {
                return;
            }
            saved.migrated = true;
            var existing = saved.modes.Find(value => value != null && value.modeId == modeId);
            if (existing == null && mode.LaneCount == defaults.Length)
            {
                var keys = LegacyChanges(mode);
                if (keys != null)
                {
                    SaveMode(saved, modeId, keys);
                }
            }
            WriteModes(saved);
        }

        private static Key[] LegacyChanges(GameModeDefinition mode)
        {
            Data data = null;
            try
            {
                data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(StorageKey, ""));
            }
            catch (ArgumentException)
            {
            }
            if (data?.keys == null || data.keys.Length != mode.LaneCount)
            {
                return null;
            }
            var seen = new HashSet<Key>();
            foreach (var key in data.keys)
            {
                if (!Allowed(key) || ConflictsWithCommands(mode, key) || !seen.Add(key))
                {
                    return null;
                }
            }
            for (int i = 0; i < data.keys.Length; i++)
            {
                if (!CanEdit(mode, i))
                {
                    return null;
                }
                if (TryKeyboardKey(mode.GetLane(i).binding, out var original) && original == data.keys[i])
                {
                    data.keys[i] = Key.None;
                }
            }
            return data.keys;
        }

        private static bool ConflictsWithDefaultLane(GameModeDefinition mode, Key[] keys, int lane)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (i == lane || keys[i] != Key.None)
                {
                    continue;
                }
                if (MatchesKey(mode.GetLane(i).binding, keys[lane]))
                {
                    return true;
                }
            }
            return false;
        }

        private static Key[] Load()
        {
            var mode = LegacyMode();
            try
            {
                var keys = Load(mode, DefaultModeId);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == Key.None)
                    {
                        keys[i] = defaults[i];
                    }
                }
                return keys;
            }
            finally
            {
                DestroyLegacyMode(mode);
            }
        }

        private static GameModeDefinition LegacyMode()
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            var lanes = new LaneDefinition[defaults.Length];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = new LaneDefinition(KeyBinding(defaults[i]), KeyLabel(defaults[i]), Color.white);
            }
            mode.SetGeneratedDefaults(lanes);
            return mode;
        }

        private static void DestroyLegacyMode(GameModeDefinition mode)
        {
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(mode);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mode);
            }
        }
    }
}
