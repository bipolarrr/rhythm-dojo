using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    [InitializeOnLoad]
    public static class MainMenuEditorStartup
    {
        public const string TestRunKey = "RhythmDojo.MainMenu.TestRun";
        private const string MenuPath = "Game Tools/Scenes/Start Play Mode From Title";
        private static string PreferenceKey => "RhythmDojo.MainMenu.Start:" + UnityEngine.Application.dataPath;

        static MainMenuEditorStartup()
        {
            EditorApplication.delayCall += Apply;
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(TestRunKey, false)) return;
            EditorSceneManager.playModeStartScene = EditorPrefs.GetBool(PreferenceKey, true)
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBuilder.TitlePath) : null;
        }

        [MenuItem("Game Tools/Scenes/Open Title")]
        private static void OpenTitle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // Discard only an empty host scene left by our interrupted Test Runner.
            if (!active.name.StartsWith("InitTestScene", System.StringComparison.Ordinal) &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SceneBuilder.TitlePath);
            Apply();
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PreferenceKey, !EditorPrefs.GetBool(PreferenceKey, true));
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PreferenceKey, true));
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}