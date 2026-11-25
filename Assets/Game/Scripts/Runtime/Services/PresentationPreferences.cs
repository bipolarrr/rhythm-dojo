using System;
using UnityEngine;

namespace RhythmDojo.Services
{
    public enum JudgmentPosition
    {
        Middle,
        Upper,
        Lower,
    }

    [Serializable]
    public sealed class PresentationPreferences
    {
        public const string StorageKey = "RhythmDojo.Presentation.v1";
        public float masterVolume = 1f,
            backgroundVolume = 1f,
            gameplayVolume = 1f;
        public int aspect,
            resolution,
            frameRate = 1,
            quality = 1;
        public bool focusHud;
        public float railOpacity = 1;
        public JudgmentPosition judgmentPosition;
        public static readonly int[] FrameRates = { 30, 60, 120, 144, 240, -1 };

        public static event Action Changed;
        public static event Action<Vector2Int> DisplaySizeRequested;

        public static PresentationPreferences Current { get; private set; }

        private static PresentationPreferences ReadSaved()
        {
            var saved = PlayerPrefs.GetString(StorageKey, "");

            try
            {
                if (!string.IsNullOrEmpty(saved))
                {
                    return JsonUtility.FromJson<PresentationPreferences>(saved) ?? new PresentationPreferences();
                }
            }
            catch (ArgumentException)
            {
                return new PresentationPreferences();
            }

            return new PresentationPreferences();
        }

        public static PresentationPreferences Load()
        {
            Current = ReadSaved();
            Current.masterVolume = Mathf.Clamp01(Current.masterVolume);
            Current.backgroundVolume = Mathf.Clamp01(Current.backgroundVolume);
            Current.gameplayVolume = Mathf.Clamp01(Current.gameplayVolume);
            Current.aspect = Mathf.Clamp(Current.aspect, 0, 1);
            Current.resolution = Mathf.Clamp(Current.resolution, 0, 1);
            Current.frameRate = Mathf.Clamp(Current.frameRate, 0, FrameRates.Length - 1);
            Current.quality = Mathf.Clamp(Current.quality, 0, 2);
            Current.railOpacity = Mathf.Clamp01(Current.railOpacity);
            if (!Enum.IsDefined(typeof(JudgmentPosition), Current.judgmentPosition))
            {
                Current.judgmentPosition = JudgmentPosition.Middle;
            }

            return Current;
        }

        public Vector2Int DisplaySize
        {
            get
            {
                int height;

                if (resolution == 0)
                {
                    height = 1080;
                }
                else
                {
                    height = 1440;
                }

                return new Vector2Int(
                    Mathf.RoundToInt(
                        height
                            * (
                                aspect switch
                                {
                                    0 => 16f / 9f,
                                    _ => 16f / 10f,
                                }
                            )
                    ),
                    height
                );
            }
        }

        public void SaveAudio()
        {
            ApplyAudio();
            SaveValues();
        }

        public void ApplyAudio()
        {
            if (!SettingsAccessPolicy.CanChange)
            {
                return;
            }

            AudioListener.volume = masterVolume;
            Changed?.Invoke();
        }

        public void SaveValues()
        {
            if (SettingsAccessPolicy.CanChange)
            {
                Save();
            }
        }

        internal void SaveRailOpacity(float value)
        {
            // During play only opacity can be committed, even if other values have pending edits.
            var saved = ReadSaved();
            railOpacity = Mathf.Clamp01(value);
            saved.railOpacity = railOpacity;
            saved.Save();
        }

        public void ApplyGraphics(bool resize = true)
        {
            ApplyGraphicsValues(resize);
            SaveValues();
        }

        public void ApplyGraphicsValues(bool resize = true)
        {
            if (!SettingsAccessPolicy.CanChange)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = FrameRates[frameRate];

            if (resize)
            {
                if (UnityEngine.Application.isEditor)
                {
                    DisplaySizeRequested?.Invoke(DisplaySize);
                }
                else
                {
                    Screen.SetResolution(DisplaySize.x, DisplaySize.y, FullScreenMode.Windowed);
                }
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
