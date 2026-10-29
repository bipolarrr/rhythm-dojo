using System;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SessionState = UnityEditor.SessionState;

namespace RhythmDojo.EditorTools
{
    [InitializeOnLoad]
    public static class ResultsPreview
    {
        public const string ActiveKey = "RhythmDojo.ResultsPreview.Active";
        private const string PreviousStartKey = "RhythmDojo.ResultsPreview.PreviousStart";
        private const string FailedKey = "RhythmDojo.ResultsPreview.Failed";
        private const string PendingKey = "RhythmDojo.ResultsPreview.Pending";
        private const string MenuPath = "Game Tools/Preview/Results";
        private const string FailedMenuPath = "Game Tools/Preview/Results (Game Over)";
        private static int enteredFrame;
        private static double deadline;

        static ResultsPreview()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.update += Update;
        }

        [MenuItem(MenuPath)]
        private static void Preview() => Begin(false);

        [MenuItem(FailedMenuPath)]
        private static void PreviewFailure() => Begin(true);

        [MenuItem(MenuPath, true)]
        [MenuItem(FailedMenuPath, true)]
        private static bool CanPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return false;
            }

            return !SessionState.GetBool(FoundationVerification.ActiveKey, false);
        }

        public static void Begin(bool failed)
        {
            if (!CanPreview())
            {
                throw new InvalidOperationException("Exit Play Mode and finish verification before previewing results.");
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBuilder.ResultsPath);
            if (!scene)
            {
                throw new InvalidOperationException("Build the Results scene before previewing results.");
            }

            SessionState.SetString(PreviousStartKey,
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(FailedKey, failed);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PendingKey, false);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enteredFrame = Time.frameCount;
                deadline = EditorApplication.timeSinceStartup + 15;
                SessionState.SetBool(PendingKey, true);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Restore();
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            // Also restore after a canceled Play Mode entry or a script reload in Edit Mode.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Restore();
                return;
            }

            if (!EditorApplication.isPlaying || !SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            if (EditorApplication.timeSinceStartup > deadline)
            {
                Debug.LogError("Results preview could not find the Results screen.");
                EditorApplication.isPlaying = false;
                return;
            }

            // Let ScreenCompositionRoot.Start bind the real return/review actions first.
            if (Time.frameCount < enteredFrame + 2)
            {
                return;
            }

            var screen = UnityEngine.Object.FindFirstObjectByType<ResultsScreen>();
            if (!screen)
            {
                return;
            }

            screen.Display(CreateResult(SessionState.GetBool(FailedKey, false)));
            SessionState.SetBool(PendingKey, false);
        }

        private static void Restore()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousStartKey, ""));
            SessionState.EraseBool(PendingKey);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(FailedKey);
            SessionState.EraseString(PreviousStartKey);
        }

        public static PlayResult CreateResult(bool failed)
        {
            var chart = new ChartData(new[]
            {
                NoteData.Tap(0, 1),
                NoteData.Tap(1, 2),
                NoteData.Hold(2, 3, 4),
                NoteData.Tap(3, 5),
                NoteData.Hold(0, 6, 8),
                NoteData.Tap(1, 9),
            }, 10);
            var tempo = new TempoMap(new[] { new TempoPoint(0, 120) });
            var rules = new PlayRules { HoldCheckBeats = .5 };
            if (failed)
            {
                rules.StartingHealth = 10;
                rules.GaugeFailure = GaugeFailureMode.Stop;
                rules.HoldEnd = HoldEndMode.Release;
            }

            var session = new RhythmSession(chart, new GameModeRules(4), tempo, rules);
            int maxCombo = 0;
            session.Judged += judgment => maxCombo = Math.Max(maxCombo, session.Combo);
            session.Start();
            session.PressLane(0, 1);
            session.ReleaseLane(0, 1.01);
            session.PressLane(1, 2.075);
            session.ReleaseLane(1, 2.08);
            session.PressLane(2, 3);
            session.ReleaseLane(2, 3.5);
            session.PressLane(2, 3.8);
            session.ReleaseLane(2, 4);
            session.PressLane(0, 6);
            session.ReleaseLane(0, 7);
            session.PressLane(0, 7.6);
            session.ReleaseLane(0, 8);
            session.PressLane(1, 9);
            session.ReleaseLane(1, 9.01);
            session.Advance(10);

            string difficulty = "Automatic · 미리보기";
            if (failed)
            {
                difficulty = "Release · 미리보기";
            }

            return new PlayResult("결과 화면 미리보기", "샘플 채보 · 실제 기록에 저장되지 않습니다",
                difficulty, null, session.Snapshot, maxCombo, chart, tempo,
                new[] { "D", "F", "J", "K" }, session);
        }
    }
}
