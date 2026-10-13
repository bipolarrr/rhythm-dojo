using System;
using UnityEngine;

namespace RhythmDojo.Services
{
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
        public static readonly int[] FrameRates = { 30, 60, 120, 144, 240, -1 };

        public static event Action Changed;
        public static event Action<Vector2Int> DisplaySizeRequested;

        public static PresentationPreferences Current { get; private set; }

        public static PresentationPreferences Load()
        {
            var saved = PlayerPrefs.GetString(StorageKey, "");

            try
            {
                if (string.IsNullOrEmpty(saved))
                {
                    Current = new PresentationPreferences();
                }
                else
                {
                    Current = JsonUtility.FromJson<PresentationPreferences>(saved);
                }
            }
            catch (ArgumentException)
            {
                Current = new PresentationPreferences();
            }

            Current ??= new PresentationPreferences();
            Current.masterVolume = Mathf.Clamp01(Current.masterVolume);
            Current.backgroundVolume = Mathf.Clamp01(Current.backgroundVolume);
            Current.gameplayVolume = Mathf.Clamp01(Current.gameplayVolume);
            Current.aspect = Mathf.Clamp(Current.aspect, 0, 1);
            Current.resolution = Mathf.Clamp(Current.resolution, 0, 1);
            Current.frameRate = Mathf.Clamp(Current.frameRate, 0, FrameRates.Length - 1);
            Current.quality = Mathf.Clamp(Current.quality, 0, 2);

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
            Save();
        }

        public void ApplyAudio()
        {
            AudioListener.volume = masterVolume;
            Changed?.Invoke();
        }

        public void SaveValues() => Save();

        public void ApplyGraphics(bool resize = true)
        {
            ApplyGraphicsValues(resize);
            Save();
        }

        public void ApplyGraphicsValues(bool resize = true)
        {
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
