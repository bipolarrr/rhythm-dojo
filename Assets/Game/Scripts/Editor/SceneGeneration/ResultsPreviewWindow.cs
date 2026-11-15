using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public sealed class ResultsPreviewWindow : EditorWindow
    {
        [SerializeField] private ResultsPreviewData data = new ResultsPreviewData();
        [SerializeField] private bool live = true;
        private Vector2 scroll;

        [MenuItem("Game Tools/Preview/Mock Controls")]
        public static void ShowWindow()
        {
            GetWindow<ResultsPreviewWindow>("UI Mock").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("실제 화면·연출을 사용합니다. 최초 한 번 Play Mode에 진입하며, 이후 값 변경은 같은 화면에 적용합니다. 기록과 설정은 저장하지 않습니다.", MessageType.Info);
            bool available = ResultsPreview.CanPreview();
            using (new EditorGUI.DisabledScope(!available))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("정상 클리어"))
                    {
                        data = new ResultsPreviewData();
                        ResultsPreview.Begin(data);
                    }
                    if (GUILayout.Button("실패"))
                    {
                        data = new ResultsPreviewData { failed = true, completed = false, score = 120000,
                            accuracy = 12, maxCombo = 12, perfect = 100, good = 40, miss = 860 };
                        ResultsPreview.Begin(data);
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("최고 점수"))
                    {
                        data = new ResultsPreviewData { score = (int)PlayRules.ScoreScale, accuracy = 100,
                            maxCombo = 1000, perfect = 1000, good = 0, miss = 0 };
                        ResultsPreview.Begin(data);
                    }
                    if (GUILayout.Button("전부 Miss"))
                    {
                        data = new ResultsPreviewData { score = 0, accuracy = 0, maxCombo = 0,
                            perfect = 0, good = 0, miss = 1000, failed = true };
                        ResultsPreview.Begin(data);
                    }
                }
                if (GUILayout.Button("일시정지창"))
                {
                    ResultsPreview.BeginPause();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            data.title = EditorGUILayout.TextField("곡명", data.title);
            data.artist = EditorGUILayout.TextField("아티스트", data.artist);
            data.difficulty = EditorGUILayout.TextField("난이도", data.difficulty);
            data.score = Mathf.Max(0, EditorGUILayout.IntField("점수", data.score));
            data.accuracy = System.Math.Clamp(EditorGUILayout.DoubleField("정확도 (%)", data.accuracy), 0, 100);
            data.maxCombo = Mathf.Clamp(EditorGUILayout.IntField("최대 콤보", data.maxCombo), 0, 1000000);
            data.perfect = Mathf.Clamp(EditorGUILayout.IntField("Perfect", data.perfect), 0, 1000000);
            data.good = Mathf.Clamp(EditorGUILayout.IntField("Good", data.good), 0, 1000000);
            data.miss = Mathf.Clamp(EditorGUILayout.IntField("Miss", data.miss), 0, 1000000);
            data.failed = EditorGUILayout.Toggle("실패", data.failed);
            if (data.failed)
            {
                data.completed = EditorGUILayout.Toggle("실패 후 완주", data.completed);
                data.failureTime = System.Math.Max(0, EditorGUILayout.DoubleField("실패 시점 (초)", data.failureTime));
            }
            bool changed = EditorGUI.EndChangeCheck();
            live = EditorGUILayout.Toggle("변경 즉시 적용", live);
            EditorGUILayout.EndScrollView();
            bool showingResults = ResultsPreview.IsActive && EditorApplication.isPlaying
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == SceneBuilder.ResultsPath;
            if (changed && live && available && showingResults)
            {
                ResultsPreview.Begin(data);
            }
            using (new EditorGUI.DisabledScope(!available))
            {
                if (GUILayout.Button("결과 적용 / 연출 다시 보기"))
                {
                    ResultsPreview.Begin(data);
                }
            }
            using (new EditorGUI.DisabledScope(!available || !showingResults))
            {
                if (GUILayout.Button("기존 채보 상세 샘플"))
                {
                    ResultsPreview.ShowReviewSample(data.failed);
                }
            }
            using (new EditorGUI.DisabledScope(!ResultsPreview.IsActive))
            {
                if (GUILayout.Button("미리보기 종료"))
                {
                    ResultsPreview.Stop();
                }
            }
            EditorGUILayout.LabelField("최근 적용", $"{ResultsPreview.LastDisplayMilliseconds:F2} ms (화면 표시 호출)");
        }
    }
}
