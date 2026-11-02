using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmDojo.Services
{
    public static class PlayKeyBindings
    {
        public const string StorageKey = "RhythmDojo.PlayKeys.v1";
        private static readonly Key[] defaults = { Key.D, Key.F, Key.J, Key.K };
        public static event Action Changed;

        [Serializable]
        private sealed class Data
        {
            public Key[] keys;
        }

        public static Key GetKey(int lane) => Load()[lane];

        public static string GetLabel(int lane)
        {
            var key = GetKey(lane);
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

        public static string GetLabel(GameModeDefinition mode, int lane)
        {
            if (mode.LaneCount == defaults.Length)
            {
                return GetLabel(lane);
            }
            return mode.GetLane(lane).displayName;
        }

        public static string GetBinding(GameModeDefinition mode, int lane)
        {
            if (mode.LaneCount != defaults.Length)
            {
                return mode.GetLane(lane).binding;
            }
            var key = GetKey(lane);
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

        public static bool TrySetKey(int lane, Key key, out string message)
        {
            if (lane < 0 || lane >= defaults.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }
            if (!Allowed(key))
            {
                message = "이 키는 사용할 수 없습니다. Esc는 취소, Space는 재시작 키입니다.";
                return false;
            }
            var keys = Load();
            for (int i = 0; i < keys.Length; i++)
            {
                if (i != lane && keys[i] == key)
                {
                    message = (i + 1) + "번 레인에서 사용 중인 키입니다. 다른 키를 눌러 주세요.";
                    return false;
                }
            }
            keys[lane] = key;
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(new Data { keys = keys }));
            PlayerPrefs.Save();
            Changed?.Invoke();
            message = "저장했습니다. 플레이 화면에도 바로 적용됩니다.";
            return true;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static bool Allowed(Key key)
        {
            if (!Enum.IsDefined(typeof(Key), key) || key == Key.None)
            {
                return false;
            }
            return key != Key.Escape && key != Key.Space && key != Key.IMESelected;
        }

        private static Key[] Load()
        {
            Data data = null;
            try
            {
                data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(StorageKey, ""));
            }
            catch (ArgumentException)
            {
            }
            if (data?.keys == null || data.keys.Length != defaults.Length)
            {
                return (Key[])defaults.Clone();
            }
            var seen = new HashSet<Key>();
            foreach (var key in data.keys)
            {
                if (!Allowed(key) || !seen.Add(key))
                {
                    return (Key[])defaults.Clone();
                }
            }
            return data.keys;
        }
    }
}
