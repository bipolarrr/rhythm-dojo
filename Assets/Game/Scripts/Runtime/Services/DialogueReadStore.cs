using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using RhythmDojo.Application;
using UnityEngine;

namespace RhythmDojo.Services
{
    public sealed class DialogueReadStore : IDialogueReadHistory
    {
        public const string StorageKey = "RhythmDojo.DialogueRead.v1";

        [Serializable]
        private sealed class Data
        {
            public List<string> read = new List<string>();
        }

        private readonly Data data;
        private readonly HashSet<string> read;

        public DialogueReadStore()
        {
            try
            {
                data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(StorageKey, ""));
            }
            catch (ArgumentException)
            {
                data = null;
            }
            data ??= new Data();
            data.read ??= new List<string>();
            read = new HashSet<string>(data.read, StringComparer.Ordinal);
        }

        public bool IsRead(string key) => read.Contains(Hash(key));

        public void MarkRead(string key)
        {
            string hash = Hash(key);
            if (!read.Add(hash))
            {
                return;
            }

            data.read.Add(hash);
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private static string Hash(string key)
        {
            using (var algorithm = SHA256.Create())
            {
                return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(key)));
            }
        }
    }
}
