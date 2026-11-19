using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using UnityEngine;

namespace RhythmDojo.Services
{
    public sealed class TrainingProgressStore
    {
        public const string StorageKey = "RhythmDojo.Training.v1";

        [Serializable]
        private sealed class Entry
        {
            public string lessonId;
            public int definitionVersion;
        }

        [Serializable]
        private sealed class Data
        {
            public int version = 1;
            public List<Entry> completed = new List<Entry>();
        }

        private readonly string key;
        private readonly Data data;

        public TrainingProgressStore(string storageKey = StorageKey)
        {
            key = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
            try
            {
                data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(key, ""));
            }
            catch (ArgumentException)
            {
                data = null;
            }

            if (data == null || data.version != 1)
            {
                data = new Data();
            }
            data.completed ??= new List<Entry>();
            data.completed.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.lessonId)
                || entry.definitionVersion < 1);
        }

        public bool IsCompleted(string lessonId, int definitionVersion)
        {
            return data.completed.Exists(entry => entry.lessonId == lessonId
                && entry.definitionVersion == definitionVersion);
        }

        public void SaveCompleted(TrainingSession session)
        {
            if (session == null || session.Outcome != TrainingOutcome.Succeeded)
            {
                return;
            }
            if (IsCompleted(session.LessonId, session.DefinitionVersion))
            {
                return;
            }
            if (!session.Completed)
            {
                return;
            }
            if (session.Guided && session.Step != TrainingStep.Assessment)
            {
                return;
            }

            data.completed.Add(new Entry
            {
                lessonId = session.LessonId,
                definitionVersion = session.DefinitionVersion,
            });
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
