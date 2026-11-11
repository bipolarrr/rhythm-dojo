using System;
using System.IO;
using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class RemediationPlayerVerification
    {
        [Serializable]
        private sealed class Report
        {
            public string status, error, output, unity, startedUtc, finishedUtc;
            public string[] scenes;
            public ulong bytes;
        }

        public static void BuildPlayer()
        {
            const string scenePath = "Assets/Game/Scenes/RemediationPlayerProbe.unity";
            const string evidencePath = "Logs/pr-quality-remediation/player-probe-build.json";
            const string outputPath = "Builds/RemediationProbe/RemediationProbe.exe";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before building the diagnostic player.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
            {
                throw new InvalidOperationException("Diagnostic scene path already exists.");
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var evidence = new Report
            {
                status = "running",
                output = outputPath,
                unity = UnityEngine.Application.unityVersion,
                startedUtc = DateTime.UtcNow.ToString("O"),
                scenes = new[]
                {
                    scenePath,
                    SceneBuilder.TitlePath,
                    SceneBuilder.ModeSelectionPath,
                    SceneBuilder.BootstrapPath,
                    SceneBuilder.SelectionPath,
                    SceneBuilder.GameplayPath,
                    SceneBuilder.ResultsPath,
                    SceneBuilder.SettingsPath,
                    ChartEditorSceneBuilder.ScenePath,
                },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));
            File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
            try
            {
                var type = Type.GetType("RhythmDojo.Tests.RemediationPlayerProbe, Game.PlayMode.Tests", true);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var probe = new GameObject("Remediation Player Probe").AddComponent(type);
                SceneBuilder.Wire(probe, "settings",
                    AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath));
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new InvalidOperationException("Cannot save diagnostic scene.");
                }
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = evidence.scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies | BuildOptions.StrictMode,
                });
                evidence.bytes = report.summary.totalSize;
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("Diagnostic player build failed: " + report.summary.result);
                }
                evidence.status = "succeeded";
            }
            catch (Exception exception)
            {
                evidence.status = "failed";
                evidence.error = exception.ToString();
                throw;
            }
            finally
            {
                try
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.DeleteAsset(scenePath);
                    if (Array.Exists(setup, value => value.isLoaded && value.isActive))
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    }
                    else
                    {
                        EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
                    }
                }
                catch (Exception exception)
                {
                    evidence.status = "failed";
                    evidence.error += "\nScene restoration: " + exception;
                    throw;
                }
                finally
                {
                    evidence.finishedUtc = DateTime.UtcNow.ToString("O");
                    File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
                }
            }
        }
    }
}
