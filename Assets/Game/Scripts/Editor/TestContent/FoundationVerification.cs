using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Application;
using UnityEngine.UI;
using SessionState = UnityEditor.SessionState;

namespace RhythmDojo.EditorTools
{
    // Editor-only integration driver; no test behaviour is saved into either scene or player.
    [InitializeOnLoad]
    public static class FoundationVerification
    {
        private const string Active = "RhythmDojo.Verification.Active";
        private static RhythmGameController game;
        private static Keyboard keyboard;
        private static InputSettings originalInputSettings;
        private static InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        private static InputSettings.BackgroundBehavior priorBackgroundBehavior;
        private static readonly HashSet<Key> down = new HashSet<Key>();
        private static readonly List<(double time, Key key, bool press)> events = new List<(double,Key,bool)>();
        private static int nextEvent, run;
        private static bool started, captured, hitched;
        private static bool awaitingSong;
        private static double deadline, nextDiagnostic;
        static FoundationVerification()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
            if (SessionState.GetBool(Active,false)) EditorApplication.update += Tick;
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Verification failed: " + message); }

        [MenuItem("Game Tools/Verification/Verify Foundation (Play Mode and Player Build)")]
        public static void RunAll()
        {
            SessionState.SetBool("RhythmDojo.Verification.Failed",false);
            Directory.CreateDirectory("Logs");
            SceneBuilder.BuildAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Check(AssetDatabase.DeleteAsset(SceneBuilder.BootstrapPath),"delete Bootstrap");
            Check(AssetDatabase.DeleteAsset(SceneBuilder.GameplayPath),"delete Gameplay");
            Check(AssetDatabase.DeleteAsset(SceneBuilder.SelectionPath),"delete SongSelection");
            Check(AssetDatabase.DeleteAsset(SceneBuilder.SettingsPath),"delete Settings");
            SceneBuilder.BuildAll();
            Check(new[] { SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath, SceneBuilder.GameplayPath, SceneBuilder.SettingsPath }.All(File.Exists),"regeneration from absent scenes");
            string first = SceneSignature();
            SceneBuilder.BuildAll();
            Check(first == SceneSignature(),"repeated generation changed hierarchy/configuration");

            File.WriteAllText("Logs/verification.txt","PASS: four scenes recreated from scratch; repeated hierarchy and references stable.\n");
            EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
            SessionState.SetBool(Active,true);
            EditorApplication.EnterPlaymode();
        }
        private static string SceneSignature()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.GameplayPath);
            SceneBuilder.ValidateScene(scene);
            var all = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            Check(all.Count(t=>t.name.StartsWith("Lane ") && t.parent && t.parent.name == "Playfield") == 4,"four lanes");
            Check(all.Count(t=>t.name == "Judgement Line") == 1,"judgement line");
            Check(all.Count(t=>t.GetComponent<NoteView>()) == 18,"18 note views");
            Check(!all.Any(t=>t.GetComponent<Collider>() || t.GetComponent<Rigidbody>()),"physics-free presentation");
            string signature = string.Join("\n",all.Select(t=>t.name+"/"+t.position+"/"+t.localScale+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().Name))));
            foreach (var path in new[] { SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath, SceneBuilder.SettingsPath })
            {
                scene = EditorSceneManager.OpenScene(path); SceneBuilder.ValidateScene(scene);
                signature += "\n" + path + "\n" + string.Join("\n", scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Select(t => t.name + "/" + t.position + "/" + t.localScale + "/" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name))));
            }
            return signature;
        }
        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Active,false)) return;
            if (state==PlayModeStateChange.EnteredPlayMode)
            {
                started=false; captured=false; hitched=false; awaitingSong=false; game=null;
                deadline=EditorApplication.timeSinceStartup+150;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
            if (state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Active,false);
                if (SessionState.GetBool("RhythmDojo.Verification.Failed",false)) { if(UnityEngine.Application.isBatchMode) EditorApplication.Exit(1); return; }
                try { BuildPlayer(); File.AppendAllText("Logs/verification.txt","PASS: exited Play Mode and Windows standalone build succeeded.\n"); if(UnityEngine.Application.isBatchMode) EditorApplication.Exit(0); }
                catch(Exception e) { Debug.LogException(e); if(UnityEngine.Application.isBatchMode) EditorApplication.Exit(1); }
            }
        }
        private static void Add(double time,Key key,double duration=.025)
        { events.Add((time,key,true)); events.Add((time+duration,key,false)); }
        private static void SetupEvents(bool perfect)
        {
            events.Clear(); nextEvent=0;
            var keys=new[]{Key.D,Key.F,Key.J,Key.K};
            for(int i=0;i<game.Chart.Count;i++)
            {
                var n=game.Chart[i];
                if(!perfect && (i==3 || i==14)) continue;
                double t=n.StartTime;
                if(!perfect && i==0) t-=.08;
                if(!perfect && i==1) t+=.08;
                double duration=n.Kind==NoteKind.Hold ? n.EndTime-t : .025;
                if(!perfect && i==11) duration=.3;
                if(!perfect && i==15) { events.Add((t,keys[n.Lane],true)); continue; }
                Add(t,keys[n.Lane],duration);
            }
            events.Sort((a,b)=>a.time.CompareTo(b.time));
        }
        private static void Queue(Key key,bool press,double targetSong)
        {
            if(press) down.Add(key); else down.Remove(key);
            double stamp=Time.realtimeSinceStartupAsDouble-(game.Clock.SongTime-targetSong);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(down.ToArray()),stamp);
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Play Mode verification timed out.");
                if(!game || game.Session==null)
                {
                    game=UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
                    if(game && game.Session==null) { game=null; return; }
                    if(!game)
                    {
                        var selection = UnityEngine.Object.FindFirstObjectByType<RhythmDojo.UI.SongSelectionScreen>();
                        if(selection)
                        {
                            var flow=UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
                            if(awaitingSong)
                                flow.UpdateSelection(1,flow.Settings.defaultDifficulty,run==3 ? ScrollMode.Constant : ScrollMode.Bpm,1);
                            flow.PlaySelected();
                        }
                        return;
                    }
                }
                if(EditorApplication.timeSinceStartup>=nextDiagnostic)
                {
                    nextDiagnostic=EditorApplication.timeSinceStartup+5;
                    Debug.Log($"Verification heartbeat: state={game.Session?.State} song={game.Clock.SongTime:F3} dsp={AudioSettings.dspTime:F3} events={nextEvent} focused={UnityEngine.Application.isFocused}");
                }
                if(!started)
                {
                    Check(game.Session.State==Gameplay.SessionState.Ready,"Bootstrap loads Ready Gameplay");
                    // Temporarily change only in-memory focus behavior for batch input.
                    // Restore the same settings object before leaving Play Mode.
                    originalInputSettings=InputSystem.settings;
                    priorEditorBehavior=originalInputSettings.editorInputBehaviorInPlayMode;
                    priorBackgroundBehavior=originalInputSettings.backgroundBehavior;
                    InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                    started=true; run=0; down.Clear();
                    InputSystem.onBeforeUpdate+=FeedInput;
                    // Start via the actual Input Action, not a controller method.
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                    SetupEvents(false); return;
                }
                if(awaitingSong)
                {
                    Check(game.Chart.Count==8,"selected tempo song has eight notes");
                    down.Clear(); InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    SetupEvents(true); game.StartSession(); awaitingSong=false; return;
                }
                if(game.Session.State==Gameplay.SessionState.Ready) return;
                double time=game.Clock.SongTime;
                if(!captured && time>4.4)
                {
                    captured=true; CaptureView();
                    Check(UnityEngine.Object.FindFirstObjectByType<SongClock>().GetComponent<AudioSource>().isPlaying,"scheduled audio playing");
                    var view=UnityEngine.Object.FindObjectsByType<NoteView>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(v=>v.name.StartsWith("Note 04"));
                    Check(Math.Abs(view.transform.Find("Head").position.z)<.001,"holding head pinned to judgment line");
                }
                if(game.Session.State!=Gameplay.SessionState.Completed) return;
                if(run==0)
                {
                    Check(game.Session.Perfect==13 && game.Session.Good==2 && game.Session.Miss==3,
                        $"mixed run counts: {game.Session.Perfect}/{game.Session.Good}/{game.Session.Miss}");
                    Check(game.Session.Combo==3,"combo after miss and final hold/chord");
                    Check(!game.Session.IsHeld(1),"completed simulation clears held state");
                    File.AppendAllText("Logs/verification.txt","PASS: Play Mode through Bootstrap; timestamped D/F/J/K, early/late Good, Perfect, tap misses, successful hold, other-lane input during hold, early-release Miss, automatic hold completion, chords, close taps, counts 13/2/3, combo 3, 180ms hitch.\n");
                    run=1; game.StartSession(); Check(!game.Session.IsHeld(1) && game.Session.Resolved==0,"restart clears held input and counts");
                    down.Clear(); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); SetupEvents(true); return;
                }
                if(run==1)
                {
                    Check(game.Session.Perfect==18 && game.Session.Resolved==18 && game.Session.Combo==18,"restarted perfect run");
                    File.AppendAllText("Logs/verification.txt","PASS: restart while F held; reset; second full run 18 Perfect / combo 18.\n");
                    run=2; game.StartSession(); events.Clear(); nextEvent=0; return;
                }
                if(run==2)
                {
                    Check(game.Session.Miss==18 && game.Session.Combo==0,"unplayed taps and hold starts miss in Play Mode");
                    File.AppendAllText("Logs/verification.txt","PASS: third full run with no input; all 18 notes including hold starts Miss.\n");
                    run=3; awaitingSong=true; events.Clear(); nextEvent=0;
                    UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection(); game=null; return;
                }
                Check(game.Session.Perfect==8 && game.Session.Combo==8,"tempo song perfect results independent of scroll mode");
                Check(game.ReadModel.Snapshot.SongTime>=12,"completed HUD retains final song time");
                File.AppendAllText("Logs/verification.txt",$"PASS: Tempo Shift full run in {(run==3 ? "Constant" : "BPM")} scroll; 8 Perfect / combo 8; hold across 120→180 BPM boundary.\n");
                if(run==3)
                {
                    run=4; awaitingSong=true; events.Clear(); nextEvent=0;
                    UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().ReturnToSelection(); game=null; return;
                }
                InputSystem.RemoveDevice(keyboard);
                InputSystem.onBeforeUpdate-=FeedInput;
                originalInputSettings.editorInputBehaviorInPlayMode=priorEditorBehavior;
                originalInputSettings.backgroundBehavior=priorBackgroundBehavior;
                EditorApplication.update-=Tick; EditorApplication.ExitPlaymode();
            }
            catch(Exception e)
            {
                Debug.LogException(e); File.AppendAllText("Logs/verification.txt",e+"\n");
                InputSystem.onBeforeUpdate-=FeedInput;
                if(originalInputSettings)
                {
                    originalInputSettings.editorInputBehaviorInPlayMode=priorEditorBehavior;
                    originalInputSettings.backgroundBehavior=priorBackgroundBehavior;
                }
                SessionState.SetBool("RhythmDojo.Verification.Failed",true);
                EditorApplication.update-=Tick; EditorApplication.ExitPlaymode();
            }
        }
        private static void FeedInput()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic || !game ||
                game.Session.State!=Gameplay.SessionState.Playing) return;
            if(!hitched && game.Clock.SongTime>6.40)
            { hitched=true; System.Threading.Thread.Sleep(180); }
            // Deliver timestamped hardware-like events before the same dynamic input update
            // and timeout sweep, including all events accumulated during a blocked frame.
            double time=game.Clock.SongTime;
            while(nextEvent<events.Count && time>=events[nextEvent].time)
            { var e=events[nextEvent++]; Queue(e.key,e.press,e.time); }
        }
        private static void CaptureView()
        {
            var camera=Camera.main; var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var target=new RenderTexture(1280,720,24);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target; canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.worldCamera=camera; canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                File.WriteAllBytes("Logs/playmode.png",texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
            }
            finally
            {
                canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
                camera.targetTexture=null; RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target);
            }
        }
        public static void BuildPlayer()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{SceneBuilder.TitlePath,SceneBuilder.BootstrapPath,SceneBuilder.SelectionPath,SceneBuilder.GameplayPath,SceneBuilder.SettingsPath},
                locationPathName="Builds/Windows/RhythmDojo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            Check(report.summary.result==BuildResult.Succeeded,"standalone player build: "+report.summary.result);
        }
    }
}
