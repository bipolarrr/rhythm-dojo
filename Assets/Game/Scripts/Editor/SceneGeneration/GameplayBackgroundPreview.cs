using System;
using System.IO;
using System.Globalization;
using System.Text;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.EditorTools
{
    // Offscreen art review, separate from the judgment-driven PlayMode evidence.
    // The preview scene and its render targets never enter a saved gameplay scene.
    public static class GameplayBackgroundPreview
    {
        [MenuItem("Game Tools/Preview/Capture Dojo Room Motion")]
        public static void CaptureMotion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before capturing the dojo art preview.");
            }
            var sourceScene = EditorSceneManager.OpenPreviewScene(SceneBuilder.GameplayPath);
            var scene = EditorSceneManager.NewPreviewScene();
            var previous = RenderTexture.active;
            var target = new RenderTexture(960, 540, 24);
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                DojoRoomPhaseView source = null;
                foreach (var root in sourceScene.GetRootGameObjects())
                {
                    source = root.GetComponentInChildren<DojoRoomPhaseView>(true);
                    if (source)
                    {
                        break;
                    }
                }
                if (!source)
                {
                    throw new InvalidOperationException("Generate Gameplay with Build Gameplay before capturing.");
                }
                // Isolate the stage from the saved scene's ExecuteAlways viewports,
                // whose canvas callbacks otherwise replace the capture render target.
                var preview = UnityEngine.Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(preview.gameObject, scene);
                preview.roomCamera.scene = scene;
                preview.roomCamera.enabled = false;
                preview.roomCamera.targetTexture = target;
                preview.roomCamera.aspect = 16f / 9;
                for (int scenario = 0; scenario < 2; scenario++)
                {
                    string path = "Logs/dojo-passage/motion";
                    if (scenario == 1)
                    {
                        path = "Logs/dojo-passage/timed-exit";
                    }
                    CaptureSequence(preview, target, texture, path, scenario == 1);
                }
                Debug.Log("Dojo passage: Logs/dojo-passage/motion and timed-exit, 480 frames each at 30 fps. " +
                    "Synthetic performance preview; judgment-driven captures are produced by DojoBackgroundFlowTests.");
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

        private static void CaptureSequence(DojoRoomPhaseView preview, RenderTexture target,
            Texture2D texture, string path, bool timedExit)
        {
            Directory.CreateDirectory(path);
            var samples = new StringBuilder("frame,time,progress,opening,cameraZ,readyForHandoff\n");
            preview.ResetPresentation();
            for (int frame = 0; frame < 480; frame++)
            {
                float time = frame / 30f;
                float progress = 0;
                float reaction = 0;
                if (!timedExit)
                {
                    if (time >= 1.25f && time < 3.25f)
                    {
                        progress = Mathf.Floor((time - 1.25f) * 10) * .04f;
                        if (frame % 3 == 0)
                        {
                            reaction = .5f;
                        }
                    }
                    else if (time >= 3.25f)
                    {
                        progress = .9f;
                    }
                }
                else if (time >= 3.25f)
                {
                    preview.BeginTimedExit();
                }
                preview.Step(1f / 60, progress, reaction, false);
                preview.Step(1f / 60, progress, 0, false);
                preview.roomCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                texture.Apply();
                File.WriteAllBytes($"{path}/{frame:D3}.png", texture.EncodeToPNG());
                samples.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F4},{2:F4},{3:F5},{4:F5},{5}", frame, (frame + 1) / 30f,
                    progress, preview.Opening, preview.Travel, preview.ReadyForHandoff));
            }
            File.WriteAllText(path + "/samples.csv", samples.ToString());
        }
    }
}
