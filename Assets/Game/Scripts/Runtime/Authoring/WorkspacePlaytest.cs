using System.Collections;
using System.Collections.Generic;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmDojo.Authoring
{
    [DefaultExecutionOrder(-100)]
    public sealed class WorkspacePlaytest : MonoBehaviour
    {
        private static double startTime;
        private static bool automatic;
        private RhythmGameController controller;
        private readonly List<NoteData> holds = new List<NoteData>();
        private int next;
        private double previousTime = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Cancel();

        public static void Prepare(double time, bool auto)
        {
            Cancel();
            if (time <= 0 && !auto)
            {
                return;
            }

            startTime = time;
            automatic = auto;
            SceneManager.sceneLoaded += Loaded;
        }

        public static void Cancel() => SceneManager.sceneLoaded -= Loaded;

        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.path.EndsWith("/Gameplay.unity"))
            {
                return;
            }

            Cancel();
            var host = new GameObject("Workspace Playtest");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<WorkspacePlaytest>();
        }

        private IEnumerator Start()
        {
            controller = FindFirstObjectByType<RhythmGameController>();
            while (controller && controller.Clock == null)
            {
                yield return null;
            }

            if (!controller)
            {
                yield break;
            }

            ((SongClock)controller.Clock).SetEditorStartPosition(startTime);
            controller.StartSession();
        }

        private void Update()
        {
            if (!automatic || !controller || controller.Session == null)
            {
                return;
            }

            if (!controller.Clock.Running)
            {
                return;
            }

            double time = controller.Clock.SongTime;
            if (time < previousTime)
            {
                next = 0;
                holds.Clear();
            }

            previousTime = time;
            for (int i = holds.Count - 1; i >= 0; i--)
            {
                if (holds[i].EndTime <= time)
                {
                    controller.Session.ReleaseLane(holds[i].Lane, holds[i].EndTime);
                    holds.RemoveAt(i);
                }
            }

            while (next < controller.Chart.Count && controller.Chart[next].StartTime <= time)
            {
                var note = controller.Chart[next++];
                controller.Session.PressLane(note.Lane, note.StartTime);
                if (note.Kind == NoteKind.Hold)
                {
                    holds.Add(note);
                }
                else
                {
                    controller.Session.ReleaseLane(note.Lane, note.StartTime);
                }
            }
        }
    }
}
