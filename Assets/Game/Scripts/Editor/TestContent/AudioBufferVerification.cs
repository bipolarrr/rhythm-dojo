using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmDojo.Gameplay;

namespace RhythmDojo.EditorTools
{
    public static class AudioBufferVerification
    {
        public static void BuildPlayer()
        {
            const string scenePath = "Assets/Game/Scenes/AudioBufferProbe.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)) throw new InvalidOperationException("Diagnostic scene path already exists.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var type = Type.GetType("RhythmDojo.Tests.AudioBufferProbe, Game.PlayMode.Tests", true);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var probe = new GameObject("Audio Buffer Probe").AddComponent(type);
                SceneBuilder.Wire(probe, "settings", AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath));
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Cannot save diagnostic scene.");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { scenePath, SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath, SceneBuilder.GameplayPath, SceneBuilder.SettingsPath },
                    locationPathName = "Builds/AudioProbe/AudioProbe.exe", target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies | BuildOptions.StrictMode });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Diagnostic player build failed.");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
                if (Array.Exists(setup, s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
            }
        }
    }
}
