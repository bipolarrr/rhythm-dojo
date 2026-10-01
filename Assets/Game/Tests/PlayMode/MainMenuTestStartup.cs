using UnityEngine.TestTools;




namespace RhythmDojo.Tests
{
    // Let the Unity Test Runner start its own scene, then restore the user's editor startup.
    public sealed class MainMenuTestStartup : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            const string key = "RhythmDojo.MainMenu.TestRun";
            var current = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
            UnityEditor.SessionState.SetString(key + ".Previous", UnityEditor.AssetDatabase.GetAssetPath(current));
            UnityEditor.SessionState.SetBool(key, true);
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            const string key = "RhythmDojo.MainMenu.TestRun";
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
                UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(
                    UnityEditor.SessionState.GetString(key + ".Previous", ""));
            UnityEditor.SessionState.SetBool(key, false);
#endif
        }
    }
}