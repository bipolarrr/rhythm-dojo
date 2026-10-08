using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using SessionState = UnityEditor.SessionState;

namespace RhythmDojo.EditorTools
{
    // Orchestrates the same independently runnable tests shown in Unity's Test Runner.
    // SessionState survives the domain reloads between EditMode and PlayMode.
    [InitializeOnLoad]
    public static class FoundationVerification
    {
        public const string ActiveKey = "RhythmDojo.Verification.Active";
        private const string Prefix = "RhythmDojo.Verification.";
        private const string ReportPath = "Logs/verification.txt";
        // In the pinned Test Framework 1.6.0, RunFinished precedes scene/settings cleanup
        // and there is no public job-completed callback. Wait for the runner's job state,
        // rather than exiting/building early or guessing a number of editor frames.
        private static readonly MethodInfo isRunActive = typeof(TestRunnerApi).GetMethod(
            "IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);

        private static bool RunnerIsBusy()
        {
            if (isRunActive == null)
                throw new InvalidOperationException("Test Framework job-state API changed. Update the verification adapter before running tests.");
            return (bool)isRunActive.Invoke(null, null);
        }

        static FoundationVerification()
        {
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update += Continue;
        }

        [MenuItem("Game Tools/Verification/Verify Foundation (Play Mode and Player Build)")]
        public static void RunAll() => Begin("", true);

        public static void RunTestsOnly() => Begin("", false, true);

        [MenuItem("Game Tools/Verification/Verify UI Actions")]
        public static void RunUi() => Begin("UI", false);

        [MenuItem("Game Tools/Verification/Verify Gameplay Actions")]
        public static void RunGameplay() => Begin("Gameplay", false);

        [MenuItem("Game Tools/Verification/Verify Authoring Actions")]
        public static void RunAuthoring() => Begin("Authoring", false);

        private static void Begin(string category, bool build, bool includeEditMode = false)
        {
            if (SessionState.GetBool(ActiveKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || RunnerIsBusy())
                throw new InvalidOperationException("Finish the current verification and exit Play Mode first.");
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, $"Verification started: {DateTime.UtcNow:O}\n");
            SessionState.SetString(Prefix + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Prefix + "Category", category);
            SessionState.SetBool(Prefix + "Build", build);
            SessionState.SetBool(Prefix + "Failed", false);
            SessionState.SetInt(Prefix + "Phase", build || includeEditMode ? 0 : 1);
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(Prefix + "Pending", true);
        }

        private static void Continue()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !SessionState.GetBool(Prefix + "Pending", false) ||
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            try
            {
                if (RunnerIsBusy()) return;
                SessionState.SetBool(Prefix + "Pending", false);
                int phase = SessionState.GetInt(Prefix + "Phase", 0);
                if (SessionState.GetBool(Prefix + "Failed", false)) { Finish(false); return; }
                if (phase == 2)
                {
                    if (SessionState.GetBool(Prefix + "Build", false)) BuildPlayer();
                    Finish(true);
                    return;
                }

                string category = SessionState.GetString(Prefix + "Category", "");
                var filter = new Filter
                {
                    testMode = phase == 0 ? TestMode.EditMode : TestMode.PlayMode,
                    assemblyNames = phase == 0
                        ? new[] { "Game.Domain.Tests", "Game.EditMode.Tests" }
                        : new[] { "Game.PlayMode.Tests" },
                    categoryNames = string.IsNullOrEmpty(category) ? null : new[] { category }
                };
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                try { api.Execute(new ExecutionSettings(filter)); }
                finally { UnityEngine.Object.DestroyImmediate(api); }
            }
            catch (Exception error)
            {
                File.AppendAllText(ReportPath, $"FAIL: {error}\n");
                Debug.LogException(error);
                Finish(false);
            }
        }

        private static void Finish(bool passed)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(Prefix + "PreviousStart", ""));
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(Prefix + "Pending", false);
            SessionState.SetBool(Prefix + "Failed", !passed);
            File.AppendAllText(ReportPath, passed ? "PASS: verification completed.\n" : "FAIL: verification stopped; see XML results.\n");
            Debug.Log($"Rhythm Dojo verification {(passed ? "passed" : "failed")}: {ReportPath}");
            if (UnityEngine.Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }

        private sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(ActiveKey, false) || result.Test.IsSuite) return;
                File.AppendAllText(ReportPath, $"{result.ResultState}: {result.FullName}\n{result.Message}\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(ActiveKey, false)) return;
                int phase = SessionState.GetInt(Prefix + "Phase", 0);
                string category = SessionState.GetString(Prefix + "Category", "");
                string name = phase == 0 ? "editmode" : string.IsNullOrEmpty(category) ? "playmode" : category.ToLowerInvariant();
                TestRunnerApi.SaveResultToFile(result, $"Logs/verification-{name}.xml");
                bool passed = result.ResultState == "Passed" && result.PassCount > 0 &&
                    result.FailCount == 0 && result.SkipCount == 0 && result.InconclusiveCount == 0;
                File.AppendAllText(ReportPath,
                    $"{name}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped, {result.InconclusiveCount} inconclusive.\n");
                SessionState.SetBool(Prefix + "Failed", !passed);
                SessionState.SetInt(Prefix + "Phase", phase + 1);
                // Continue waits for all runner cleanup tasks, including after leaving Play Mode.
                SessionState.SetBool(Prefix + "Pending", true);
            }

            public void OnError(string message)
            {
                if (!SessionState.GetBool(ActiveKey, false)) return;
                File.AppendAllText(ReportPath, $"FAIL: Test Runner: {message}\n");
                SessionState.SetBool(Prefix + "Failed", true);
                SessionState.SetBool(Prefix + "Pending", true);
            }
        }

        [MenuItem("Game Tools/Verification/Build Player Only")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.TitlePath, SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath,
                    SceneBuilder.GameplayPath, SceneBuilder.SettingsPath, ChartEditorSceneBuilder.ScenePath },
                locationPathName = "Builds/Windows/RhythmDojo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Standalone player build: " + report.summary.result);
            Directory.CreateDirectory("Logs");
            File.AppendAllText(ReportPath, "PASS: Windows standalone build (Title entry point).\n");
        }
    }
}
