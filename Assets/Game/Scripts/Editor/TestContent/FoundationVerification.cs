using System;
using System.IO;
using System.Globalization;
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
            "IsRunActive",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        private static bool RunnerIsBusy()
        {
            if (isRunActive == null)
            {
                throw new InvalidOperationException(
                    "Test Framework job-state API changed. Update the verification adapter before running tests."
                );
            }

            return (bool)isRunActive.Invoke(null, null);
        }

        static FoundationVerification()
        {
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update += Continue;
        }

        [MenuItem("Game Tools/Verification/Verify Foundation (Play Mode and Player Build)")]
        public static void RunAll() => Begin("All", true);

        [MenuItem("Game Tools/Verification/Verify All Tests (No Build)")]
        public static void RunTestsOnly() => Begin("All", false, true);

        [MenuItem("Game Tools/Verification/Verify Smoke Actions")]
        public static void RunSmoke() => Begin("Smoke", false);

        [MenuItem("Game Tools/Verification/Verify UI Actions")]
        public static void RunUi() => Begin("UI", false);

        [MenuItem("Game Tools/Verification/Verify Gameplay Actions")]
        public static void RunGameplay() => Begin("Gameplay", false);

        [MenuItem("Game Tools/Verification/Playtest Health and Results")]
        public static void RunHealthAndResults() => Begin("HealthPlaytest", false);

        [MenuItem("Game Tools/Verification/Verify Authoring Actions")]
        public static void RunAuthoring() => Begin("Authoring", false);

        [MenuItem("Game Tools/Verification/Verify Fast Tests")]
        public static void RunFast() => Begin("Fast", false);

        [MenuItem("Game Tools/Verification/Verify Asset Generation")]
        public static void RunAssetGeneration() => Begin("AssetGeneration", false);

        [MenuItem("Game Tools/Verification/Verify Hardware")]
        public static void RunHardware() => Begin("Hardware", false);

        [MenuItem("Game Tools/Verification/Verify Endurance")]
        public static void RunEndurance() => Begin("Endurance", false);

        // Batch: -executeMethod RhythmDojo.EditorTools.FoundationVerification.RunProfile
        // -verificationProfile UI. Menu methods and CLI use the same selector.
        public static void RunProfile()
        {
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-verificationProfile");
            if (index < 0 || index + 1 >= arguments.Length)
            {
                throw new ArgumentException("Specify -verificationProfile.");
            }

            Begin(arguments[index + 1], false);
        }

        private static void Begin(string category, bool build, bool includeEditMode = true)
        {
            VerificationProfile.Includes(category, new[] { "Fast" });
            if (
                SessionState.GetBool(ActiveKey, false)
                || EditorApplication.isPlayingOrWillChangePlaymode
            )
            {
                throw new InvalidOperationException(
                    "Finish the current verification and exit Play Mode first."
                );
            }

            if (RunnerIsBusy())
            {
                throw new InvalidOperationException(
                    "Finish the current verification and exit Play Mode first."
                );
            }

            if (
                !UnityEngine.Application.isBatchMode
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
            )
            {
                return;
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, $"Verification started: {DateTime.UtcNow:O}; profile={category}\n");
            SessionState.SetString(Prefix + "Started", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SessionState.SetInt(Prefix + "Count", 0);
            SessionState.SetString(
                Prefix + "PreviousStart",
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
            );
            SessionState.SetString(Prefix + "Category", category);
            SessionState.SetBool(Prefix + "Build", build);
            SessionState.SetBool(Prefix + "Failed", false);

            if (build || includeEditMode)
            {
                SessionState.SetInt(Prefix + "Phase", 0);
            }
            else
            {
                SessionState.SetInt(Prefix + "Phase", 1);
            }

            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(Prefix + "Pending", true);
        }

        private static void Continue()
        {
            if (
                !SessionState.GetBool(ActiveKey, false)
                || !SessionState.GetBool(Prefix + "Pending", false)
            )
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            try
            {
                if (RunnerIsBusy())
                {
                    return;
                }

                SessionState.SetBool(Prefix + "Pending", false);

                int phase = SessionState.GetInt(Prefix + "Phase", 0);

                if (SessionState.GetBool(Prefix + "Failed", false))
                {
                    Finish(false);

                    return;
                }

                if (phase == 2)
                {
                    if (SessionState.GetInt(Prefix + "Count", 0) == 0)
                    {
                        Finish(false);
                        return;
                    }

                    if (SessionState.GetBool(Prefix + "Build", false))
                    {
                        BuildPlayer();
                    }

                    Finish(true);

                    return;
                }

                string category = SessionState.GetString(Prefix + "Category", "");
                var filter = new Filter();

                if (phase == 0)
                {
                    filter.testMode = TestMode.EditMode;
                    filter.assemblyNames = new[] { "Game.Domain.Tests", "Game.EditMode.Tests" };
                }
                else
                {
                    filter.testMode = TestMode.PlayMode;
                    filter.assemblyNames = new[] { "Game.PlayMode.Tests" };
                }

                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RetrieveTestList(filter.testMode, root =>
                {
                    try
                    {
                        filter.testNames = VerificationProfile.Select(root, category, filter.assemblyNames);
                        SessionState.SetInt(Prefix + "Expected", filter.testNames.Length);
                        if (filter.testNames.Length == 0)
                        {
                            File.AppendAllText(ReportPath, $"{filter.testMode}: no matching cases; phase not run.\n");
                            SessionState.SetInt(Prefix + "Phase", phase + 1);
                            SessionState.SetBool(Prefix + "Pending", true);
                            return;
                        }

                        api.Execute(new ExecutionSettings(filter));
                    }
                    catch (Exception error)
                    {
                        File.AppendAllText(ReportPath, $"FAIL: {error}\n");
                        Debug.LogException(error);
                        Finish(false);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(api);
                    }
                });
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
                SessionState.GetString(Prefix + "PreviousStart", "")
            );
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(Prefix + "Pending", false);
            SessionState.SetBool(Prefix + "Failed", !passed);

            double seconds = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - long.Parse(
                SessionState.GetString(Prefix + "Started", "0"), CultureInfo.InvariantCulture)).TotalSeconds;
            File.AppendAllText(ReportPath,
                $"profile={SessionState.GetString(Prefix + "Category", "")}; unique cases={SessionState.GetInt(Prefix + "Count", 0)}; elapsed={seconds:F2}s\n");

            if (passed)
            {
                File.AppendAllText(ReportPath, "PASS: verification completed.\n");
            }
            else
            {
                File.AppendAllText(ReportPath, "FAIL: verification stopped; see XML results.\n");
            }

            if (passed)
            {
                Debug.Log($"Rhythm Dojo verification passed: {ReportPath}");
            }
            else
            {
                Debug.Log($"Rhythm Dojo verification failed: {ReportPath}");
            }

            if (UnityEngine.Application.isBatchMode)
            {
                if (passed)
                {
                    EditorApplication.Exit(0);
                }
                else
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private sealed class Results : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(ActiveKey, false) || result.Test.IsSuite)
                {
                    return;
                }

                if (result.ResultState != "Passed")
                {
                    File.AppendAllText(ReportPath,
                        $"{result.ResultState}: {result.FullName}\n{result.Message}\n{result.StackTrace}\n");
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(ActiveKey, false))
                {
                    return;
                }

                int phase = SessionState.GetInt(Prefix + "Phase", 0);
                string category = SessionState.GetString(Prefix + "Category", "");
                string name;

                if (phase == 0)
                {
                    name = "editmode";
                }
                else
                {
                    name = "playmode";
                }

                if (category != "All")
                {
                    name = category.ToLowerInvariant() + "-" + name;
                }

                TestRunnerApi.SaveResultToFile(result, $"Logs/verification-{name}.xml");

                bool passed = VerificationProfile.Passed(result.ResultState,
                    SessionState.GetInt(Prefix + "Expected", 0), result.PassCount,
                    result.FailCount, result.SkipCount, result.InconclusiveCount);
                SessionState.SetInt(Prefix + "Count", SessionState.GetInt(Prefix + "Count", 0)
                    + result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount);
                File.AppendAllText(
                    ReportPath,
                    $"{name}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped, {result.InconclusiveCount} inconclusive; duration={result.Duration:F2}s.\n"
                );
                SessionState.SetBool(Prefix + "Failed", !passed);
                SessionState.SetInt(Prefix + "Phase", phase + 1);

                // Continue waits for all runner cleanup tasks, including after leaving Play Mode.
                SessionState.SetBool(Prefix + "Pending", true);
            }

            public void OnError(string message)
            {
                if (!SessionState.GetBool(ActiveKey, false))
                {
                    return;
                }

                File.AppendAllText(ReportPath, $"FAIL: Test Runner: {message}\n");
                SessionState.SetBool(Prefix + "Failed", true);
                SessionState.SetBool(Prefix + "Pending", true);
            }
        }

        [MenuItem("Game Tools/Verification/Build Player Only")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("Builds/Windows");

            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        SceneBuilder.TitlePath,
                        SceneBuilder.ModeSelectionPath,
                        SceneBuilder.BootstrapPath,
                        SceneBuilder.SelectionPath,
                        SceneBuilder.GameplayPath,
                        SceneBuilder.SettingsPath,
                        SceneBuilder.ResultsPath,
                        ChartEditorSceneBuilder.ScenePath,
                        SceneBuilder.TutorialPath,
                    },
                    locationPathName = "Builds/Windows/RhythmDojo.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                }
            );

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Standalone player build: " + report.summary.result
                );
            }

            Directory.CreateDirectory("Logs");
            File.AppendAllText(ReportPath, "PASS: Windows standalone build (Title entry point).\n");
        }
    }
}
