using System;
using System.IO;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.EditorTools
{
    public static class TrainingJourneyPreview
    {
        [MenuItem("Game Tools/Preview/Capture Training Journey")]
        public static void CaptureStills()
        {
            Capture(false);
        }

        [MenuItem("Game Tools/Preview/Capture Training Journey Motion")]
        public static void CaptureMotion()
        {
            Capture(true);
        }

        private static void Capture(bool motion)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before capturing the journey preview.");
            }
            string path = "Logs/training-journey/preview";
            Directory.CreateDirectory(path);
            var sourceScene = EditorSceneManager.OpenPreviewScene(SceneBuilder.GameplayPath);
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            int width = 1280;
            int height = 720;
            if (motion)
            {
                width = 960;
                height = 540;
            }
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                foreach (var root in sourceScene.GetRootGameObjects())
                {
                    var source = root.GetComponent<TrainingJourneyView>();
                    if (!source)
                    {
                        continue;
                    }
                    var view = UnityEngine.Object.Instantiate(source);
                    SceneManager.MoveGameObjectToScene(view.gameObject, scene);
                    view.gameObject.SetActive(true);
                    view.sceneryCamera.scene = scene;
                    view.sceneryCamera.enabled = false;
                    view.sceneryCamera.targetTexture = target;
                    view.sceneryCamera.aspect = 16f / 9;
                    view.ResetPresentation();
                    for (int frame = 0; frame <= 450; frame++)
                    {
                        float progress = Mathf.Clamp01((frame - 30) / 270f);
                        float reaction = 0;
                        if (frame % 8 == 0 && frame > 30)
                        {
                            reaction = .8f;
                        }
                        for (int step = 0; step < 2; step++)
                        {
                            if (view is MountainPhaseView mountain)
                            {
                                mountain.Step(1f / 60, progress, reaction, false);
                            }
                            else if (view is RunPhaseView run)
                            {
                                run.Step(1f / 60, progress, reaction, frame == 160);
                            }
                            else if (view is DummyPhaseView dummy)
                            {
                                if (reaction > 0 && frame < 390)
                                {
                                    dummy.React(frame / 8 % 2, 0, reaction, false);
                                }
                                if (frame == 390)
                                {
                                    dummy.BeginFinish(true);
                                }
                                dummy.Step(1f / 60, progress);
                            }
                            reaction = 0;
                        }
                        bool sample = frame == 0 || frame == 180 || frame == 375;
                        bool finishSample = frame == 398 || frame == 415 || frame == 450;
                        if (motion || sample || finishSample)
                        {
                            view.sceneryCamera.Render();
                            RenderTexture.active = target;
                            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                            texture.Apply();
                            string directory = path + "/" + view.GetType().Name;
                            Directory.CreateDirectory(directory);
                            File.WriteAllBytes(directory + $"/{frame:D3}.png", texture.EncodeToPNG());
                        }
                    }
                    RenderTexture.active = previous;
                    view.sceneryCamera.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                }
                File.WriteAllText(path + "/completed.txt", "Unity rendered authored journey stages. Synthetic art preview. " +
                    "451 frames per stage at 30 fps when motion=true. " + DateTime.UtcNow.ToString("O"));
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                EditorSceneManager.ClosePreviewScene(sourceScene);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
