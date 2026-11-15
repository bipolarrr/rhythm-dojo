using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SessionState = UnityEditor.SessionState;

namespace RhythmDojo.EditorTools
{
    // Only values used by the real screen. No song, clock, session or persistent preferences.
    [Serializable]
    public sealed class ResultsPreviewData
    {
        public string title = "결과 화면 미리보기";
        public string artist = "개발용 Mock · 실제 기록에 저장되지 않습니다";
        public string difficulty = "Mock";
        public int score = 925000;
        public double accuracy = 92.5;
        public int maxCombo = 380;
        public int perfect = 900;
        public int good = 50;
        public int miss = 50;
        public bool failed;
        public bool completed = true;
        public double failureTime = 42;

        public PlayResult CreateResult()
        {
            var state = RhythmDojo.Gameplay.SessionState.Completed;
            var failure = default(FailureSnapshot);
            int health = 100;
            if (failed)
            {
                health = 0;
                failure = new FailureSnapshot(Math.Max(0, failureTime), FailureReason.GaugeEmpty, completed);
                if (!completed)
                {
                    state = RhythmDojo.Gameplay.SessionState.GameOver;
                }
            }
            var session = new SessionSnapshot(state, perfect, good, miss, maxCombo,
                perfect + good + miss, health, health / 100.0, failure);
            return new PlayResult(title, artist, difficulty, null, session, maxCombo);
        }
    }

    [InitializeOnLoad]
    public static class ResultsPreview
    {
        public const string ActiveKey = "RhythmDojo.ResultsPreview.Active";
        private const string PreviousStartKey = "RhythmDojo.ResultsPreview.PreviousStart";
        private const string PendingKey = "RhythmDojo.ResultsPreview.Pending";
        private const string DataKey = "RhythmDojo.ResultsPreview.Data";
        private const string SceneKey = "RhythmDojo.ResultsPreview.Scene";
        private const string RequestedAtKey = "RhythmDojo.ResultsPreview.RequestedAt";
        private const string PreviousOptionsKey = "RhythmDojo.ResultsPreview.PreviousOptions";
        private const string PreviousOptionsEnabledKey = "RhythmDojo.ResultsPreview.PreviousOptionsEnabled";
        private const string MenuPath = "Game Tools/Preview/Results";
        private const string FailedMenuPath = "Game Tools/Preview/Results (Game Over)";
        private static double deadline;
        private static ResultsScreen screen;
        private static GameplayPauseMenu pauseMenu;
        private static double requestedAt;
        private static readonly Dictionary<int, GameObject[]> sceneRoots = new Dictionary<int, GameObject[]>();

        public static bool IsActive => SessionState.GetBool(ActiveKey, false);
        public static double LastDisplayMilliseconds { get; private set; }

        static ResultsPreview()
        {
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.update += Update;
            SceneManager.sceneLoaded += SceneLoaded;
            EditorApplication.quitting += () =>
            {
                if (IsActive)
                {
                    Restore();
                }
            };
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += _ =>
            {
                if (IsActive && EditorApplication.isPlaying)
                {
                    Stop();
                }
            };
        }

        [MenuItem(MenuPath)]
        private static void Preview() => Begin(false);

        [MenuItem(FailedMenuPath)]
        private static void PreviewFailure() => Begin(true);

        [MenuItem("Game Tools/Preview/Pause")]
        public static void BeginPause() => Begin(SceneBuilder.GameplayPath);

        [MenuItem(MenuPath, true)]
        [MenuItem(FailedMenuPath, true)]
        [MenuItem("Game Tools/Preview/Pause", true)]
        internal static bool CanPreview()
        {
            if (EditorApplication.isCompiling || SessionState.GetBool(FoundationVerification.ActiveKey, false))
            {
                return false;
            }
            if (SessionState.GetBool(MainMenuEditorStartup.TestRunKey, false))
            {
                return false;
            }
            if (SessionState.GetBool(PendingKey, false))
            {
                return false;
            }
            if (EditorApplication.isPlaying)
            {
                return IsActive;
            }
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static void Begin(bool failed)
        {
            Begin(new ResultsPreviewData { failed = failed, completed = !failed });
        }

        public static void Begin(ResultsPreviewData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            RequireAvailable();
            SessionState.SetString(DataKey, JsonUtility.ToJson(data));
            Begin(SceneBuilder.ResultsPath);
        }

        private static void RequireAvailable()
        {
            if (!CanPreview())
            {
                throw new InvalidOperationException("Finish normal Play Mode, compilation or verification before previewing.");
            }
        }

        private static void Begin(string path)
        {
            RequireAvailable();
            requestedAt = EditorApplication.timeSinceStartup;
            SessionState.SetString(RequestedAtKey, requestedAt.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetString(SceneKey, path);
            if (EditorApplication.isPlaying)
            {
                var loaded = SceneManager.GetSceneByPath(path);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    ShowScene(loaded);
                    return;
                }
                SessionState.SetBool(PendingKey, true);
                deadline = EditorApplication.timeSinceStartup + 30;
                EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (!scene)
            {
                throw new InvalidOperationException("Build the requested scene before previewing.");
            }
            SessionState.SetString(PreviousStartKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetInt(PreviousOptionsKey, (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(PreviousOptionsEnabledKey, EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(PendingKey, true);
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorSceneManager.playModeStartScene = scene;
            // This isolated UI host does not need a fresh game domain. Always reload
            // the authored scene, and restore the user's options on exit/cancellation.
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorApplication.isPlaying = true;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (!IsActive)
            {
                return;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Restore();
            }
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsActive || scene.path != SessionState.GetString(SceneKey, ""))
            {
                return;
            }
            try
            {
                requestedAt = double.Parse(SessionState.GetString(RequestedAtKey, "0"), CultureInfo.InvariantCulture);
                // sceneLoaded runs before Start: never construct AppFlow, load a song,
                // apply saved graphics/audio settings, or attach persistence callbacks.
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var composition in root.GetComponentsInChildren<ScreenCompositionRoot>(true))
                    {
                        composition.enabled = false;
                    }
                    foreach (var composition in root.GetComponentsInChildren<GameplayCompositionRoot>(true))
                    {
                        composition.enabled = false;
                    }
                }
                ShowScene(scene);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Stop();
            }
        }

        private static void ShowScene(Scene scene)
        {
            if (!sceneRoots.ContainsKey(scene.handle))
            {
                sceneRoots.Add(scene.handle, scene.GetRootGameObjects().Where(root => root.activeSelf).ToArray());
            }
            // Cache both authored scenes for this preview only; switching never reloads them.
            foreach (var loaded in sceneRoots)
            {
                foreach (var root in loaded.Value)
                {
                    root.SetActive(loaded.Key == scene.handle);
                }
            }
            SceneManager.SetActiveScene(scene);
            if (scene.path == SceneBuilder.ResultsPath)
            {
                var data = JsonUtility.FromJson<ResultsPreviewData>(SessionState.GetString(DataKey, "{}"));
                var result = data.CreateResult();
                if (!screen)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<ResultsScreen>();
                    if (!screen)
                    {
                        throw new InvalidOperationException("Results screen missing from the authored scene.");
                    }
                    screen.Initialize(result, Stop, data.score, data.accuracy);
                }
                else
                {
                    screen.Display(result, data.score, data.accuracy);
                }
            }
            else
            {
                if (!pauseMenu)
                {
                    pauseMenu = UnityEngine.Object.FindFirstObjectByType<GameplayPauseMenu>();
                    if (!pauseMenu)
                    {
                        throw new InvalidOperationException("Pause menu missing from the authored scene.");
                    }
                    pauseMenu.resumeButton.onClick.AddListener(() => pauseMenu.panel.SetActive(false));
                    pauseMenu.restartButton.onClick.AddListener(ShowPause);
                    pauseMenu.exitButton.onClick.AddListener(Stop);
                }
                ShowPause();
            }
            SessionState.SetBool(PendingKey, false);
            LastDisplayMilliseconds = (EditorApplication.timeSinceStartup - requestedAt) * 1000;
        }

        private static void ShowPause()
        {
            pauseMenu.panel.SetActive(false);
            pauseMenu.Display();
        }

        private static void Update()
        {
            if (!IsActive)
            {
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Restore();
                return;
            }
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.timeSinceStartup > deadline)
            {
                Debug.LogError("Mock preview scene did not finish loading.");
                Stop();
            }
        }

        public static void Stop()
        {
            if (IsActive)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void Restore()
        {
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(PreviousOptionsKey, 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(PreviousOptionsEnabledKey, false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SessionState.GetString(PreviousStartKey, ""));
            SessionState.EraseBool(PendingKey);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseString(PreviousStartKey);
            SessionState.EraseString(SceneKey);
            SessionState.EraseString(RequestedAtKey);
            SessionState.EraseInt(PreviousOptionsKey);
            SessionState.EraseBool(PreviousOptionsEnabledKey);
            sceneRoots.Clear();
            screen = null;
            pauseMenu = null;
        }

        // Retain the original chart-review samples without replaying a RhythmSession.
        public static PlayResult CreateResult(bool failed)
        {
            var chart = new ChartData(new[]
            {
                NoteData.Tap(0, 1), NoteData.Tap(1, 2), NoteData.Hold(2, 3, 4),
                NoteData.Tap(3, 5), NoteData.Hold(0, 6, 8), NoteData.Tap(1, 9),
            }, 10);
            var judgments = new List<JudgmentEvent>
            {
                new JudgmentEvent(0, chart[0], Judgment.Perfect, 0),
                new JudgmentEvent(1, chart[1], Judgment.Good, .075),
                new JudgmentEvent(2, chart[2], Judgment.Perfect, 0),
            };
            var states = new[] { NoteState.Completed, NoteState.Completed, NoteState.Completed,
                NoteState.Pending, NoteState.Pending, NoteState.Pending };
            var session = new SessionSnapshot(RhythmDojo.Gameplay.SessionState.Completed,
                13, 1, 4, 3, 18, 60);
            string difficulty = "Automatic · 미리보기";
            if (failed)
            {
                session = new SessionSnapshot(RhythmDojo.Gameplay.SessionState.GameOver,
                    4, 1, 1, 0, 18, 0, 0, new FailureSnapshot(3.5, FailureReason.GaugeEmpty, false));
                difficulty = "Release · 미리보기";
                judgments.Add(new JudgmentEvent(2, chart[2], Judgment.Miss, -.5));
                states[2] = NoteState.Missed;
            }
            else
            {
                judgments.Add(new JudgmentEvent(2, chart[2], Judgment.Perfect, 0));
                judgments.Add(new JudgmentEvent(3, chart[3], Judgment.Miss, .1));
                judgments.Add(new JudgmentEvent(4, chart[4], Judgment.Perfect, 0));
                judgments.Add(new JudgmentEvent(4, chart[4], Judgment.Perfect, 0));
                judgments.Add(new JudgmentEvent(5, chart[5], Judgment.Perfect, 0));
                states[3] = NoteState.Missed;
                states[4] = NoteState.Completed;
                states[5] = NoteState.Completed;
            }
            return new PlayResult("결과 화면 미리보기", "샘플 채보 · 실제 기록에 저장되지 않습니다", difficulty,
                null, session, 5, chart, new TempoMap(new[] { new TempoPoint(0, 120) }),
                new[] { "D", "F", "J", "K" }, judgments, states);
        }

        public static void ShowReviewSample(bool failed)
        {
            RequireAvailable();
            if (!screen || !screen.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException("Show Results first, then apply the chart-review sample.");
            }
            screen.Display(CreateResult(failed));
        }
    }
}
