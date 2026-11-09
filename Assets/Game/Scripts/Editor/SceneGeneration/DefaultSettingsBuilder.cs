using RhythmDojo.Gameplay;
using UnityEngine;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    public sealed class DefaultSettingsContent
    {
        public GameModeDefinition Mode;
        public DifficultyProfile Easy,
            Standard,
            Hard;
        public ScrollSettings Scroll;
        public AudioPlaybackSettings Audio;
        public GameplayPresentationSettings Presentation;
    }

    public static class DefaultSettingsBuilder
    {
        public static DefaultSettingsContent Prepare(RhythmGameConfig legacy)
        {
            var mode = Asset<GameModeDefinition>(
                "Assets/Game/Settings/FourLaneMode.asset",
                out bool newMode
            );

            if (newMode)
            {
                mode.SetGeneratedDefaults(
                    new[]
                    {
                        new LaneDefinition("<Keyboard>/d", "D", Color.white),
                        new LaneDefinition("<Keyboard>/f", "F", Color.white),
                        new LaneDefinition("<Keyboard>/j", "J", Color.white),
                        new LaneDefinition("<Keyboard>/k", "K", Color.white),
                    }
                );
                Dirty(mode);
            }

            var easy = Difficulty("Easy", .08, .16);
            var standard = Asset<DifficultyProfile>(
                "Assets/Game/Settings/Standard.asset",
                out bool newStandard
            );

            if (newStandard)
            {
                InitializeStandard(standard, legacy);

                Dirty(standard);
            }

            var hard = Difficulty("Hard", .035, .08);
            var scroll = Asset<ScrollSettings>(
                "Assets/Game/Settings/ScrollSettings.asset",
                out bool newScroll
            );

            if (newScroll)
            {
                InitializeScroll(scroll, legacy);

                Dirty(scroll);
            }

            var audio = Asset<AudioPlaybackSettings>(
                "Assets/Game/Settings/AudioPlaybackSettings.asset",
                out bool newAudio
            );

            if (newAudio)
            {
                InitializeAudio(audio, legacy);

                Dirty(audio);
            }

            var presentation = Asset<GameplayPresentationSettings>(
                "Assets/Game/Settings/PresentationSettings.asset",
                out _
            );

            return new DefaultSettingsContent
            {
                Mode = mode,
                Easy = easy,
                Standard = standard,
                Hard = hard,
                Scroll = scroll,
                Audio = audio,
                Presentation = presentation,
            };
        }

        internal static RhythmGameConfig MigrationSource(bool needed, bool exists, System.Func<RhythmGameConfig> load)
        {
            if (!needed || !exists)
            {
                return null;
            }

            var legacy = load();
            if (!legacy)
            {
                throw new System.InvalidOperationException(
                    "Cannot load legacy configuration. Repair its script reference.");
            }

            legacy.Validate();
            return legacy;
        }

        internal static void InitializeStandard(DifficultyProfile standard, RhythmGameConfig legacy)
        {
            if (legacy)
            {
                standard.SetGeneratedDefaults("standard", "Standard", legacy.PerfectWindow, legacy.GoodWindow);
            }
            else
            {
                standard.SetGeneratedDefaults("standard", "Standard", .05, .11);
            }
        }

        internal static void InitializeScroll(ScrollSettings scroll, RhythmGameConfig legacy)
        {
            if (legacy)
            {
                scroll.MigrateBaseSpeed(legacy.ScrollSpeed);
            }
        }

        internal static void InitializeAudio(AudioPlaybackSettings audio, RhythmGameConfig legacy)
        {
            if (legacy)
            {
                audio.MigrateLeadTime(legacy.ScheduleLeadTime);
            }
        }

        internal static void InitializeTiming(SongTiming fixedTiming, RhythmGameConfig legacy)
        {
            if (legacy)
            {
                fixedTiming.chartAudioOffsetSeconds = legacy.AudioOffset;
            }
        }

        private static DifficultyProfile Difficulty(string name, double perfect, double good)
        {
            var profile = Asset<DifficultyProfile>(
                $"Assets/Game/Settings/{name}.asset",
                out bool created
            );

            if (created)
            {
                profile.SetGeneratedDefaults(name.ToLowerInvariant(), name, perfect, good);
                Dirty(profile);
            }

            return profile;
        }
    }
}
