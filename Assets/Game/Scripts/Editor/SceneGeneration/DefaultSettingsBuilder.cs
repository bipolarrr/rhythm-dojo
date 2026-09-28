using UnityEngine;
using RhythmDojo.Gameplay;
using static RhythmDojo.EditorTools.ContentAssets;
namespace RhythmDojo.EditorTools
{
    public sealed class DefaultSettingsContent
    {
        public GameModeDefinition Mode;
        public DifficultyProfile Easy, Standard, Hard;
        public ScrollSettings Scroll;
        public AudioPlaybackSettings Audio;
        public GameplayPresentationSettings Presentation;
    }
    public static class DefaultSettingsBuilder
    {
        public static DefaultSettingsContent Prepare(RhythmGameConfig legacy)
        {
            var mode = Asset<GameModeDefinition>("Assets/Game/Settings/FourLaneMode.asset", out bool newMode);
            if (newMode)
            {
                mode.SetGeneratedDefaults(new[] {
                    new LaneDefinition("<Keyboard>/d", "D", new Color(.2f,.8f,1)),
                    new LaneDefinition("<Keyboard>/f", "F", new Color(.35f,1,.65f)),
                    new LaneDefinition("<Keyboard>/j", "J", new Color(1,.7f,.22f)),
                    new LaneDefinition("<Keyboard>/k", "K", new Color(1,.35f,.65f)) });
                Dirty(mode);
            }
            var easy = Difficulty("Easy", .08, .16);
            var standard = Asset<DifficultyProfile>("Assets/Game/Settings/Standard.asset", out bool newStandard);
            if (newStandard) { standard.SetGeneratedDefaults("standard", "Standard", legacy ? legacy.PerfectWindow : .05, legacy ? legacy.GoodWindow : .11); Dirty(standard); }
            var hard = Difficulty("Hard", .035, .08);
            var scroll = Asset<ScrollSettings>("Assets/Game/Settings/ScrollSettings.asset", out bool newScroll);
            if (newScroll) { if (legacy) scroll.MigrateBaseSpeed(legacy.ScrollSpeed); Dirty(scroll); }
            var audio = Asset<AudioPlaybackSettings>("Assets/Game/Settings/AudioPlaybackSettings.asset", out bool newAudio);
            if (newAudio) { if (legacy) audio.MigrateLeadTime(legacy.ScheduleLeadTime); Dirty(audio); }
            var presentation = Asset<GameplayPresentationSettings>("Assets/Game/Settings/PresentationSettings.asset", out _);
            return new DefaultSettingsContent { Mode = mode, Easy = easy, Standard = standard, Hard = hard, Scroll = scroll, Audio = audio, Presentation = presentation };
        }
        private static DifficultyProfile Difficulty(string name, double perfect, double good)
        {
            var profile = Asset<DifficultyProfile>($"Assets/Game/Settings/{name}.asset", out bool created);
            if (created) { profile.SetGeneratedDefaults(name.ToLowerInvariant(), name, perfect, good); Dirty(profile); }
            return profile;
        }
    }
}
