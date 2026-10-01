using UnityEngine.TestTools;

namespace RhythmDojo.Tests
{
    // Let the Test Runner own startup, independent of any configured game entry scene.
    // Keep the shared session key compatible with the title branch startup guard.
    public sealed class TestRunStartup : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            const string key = "RhythmDojo.MainMenu.TestRun";
            if (UnityEditor.SessionState.GetBool(key, false)) return;
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
            if (!UnityEditor.SessionState.GetBool(key, false)) return;
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene =
                UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(
                    UnityEditor.SessionState.GetString(key + ".Previous", ""));
            UnityEditor.SessionState.SetBool(key, false);
#endif
        }
    }
}
