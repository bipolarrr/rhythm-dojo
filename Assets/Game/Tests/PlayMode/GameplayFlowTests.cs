using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using RhythmDojo.Content;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class GameplayFlowTests : GameplayTestContext
    {
        [UnityTest]
        public IEnumerator SixLaneDefinitionDrivesAllUnityAdapters()
        {
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>(); var originalCatalog = flow.Settings.catalog;
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            var chart = ScriptableObject.CreateInstance<RhythmChart>();
            var song = ScriptableObject.CreateInstance<SongDefinition>();
            var catalog = ScriptableObject.CreateInstance<SongCatalog>();
            try
            {
                string[] keys = { "a","s","d","f","j","k" };
                mode.SetGeneratedDefaults(keys.Select(k=>new LaneDefinition("<Keyboard>/"+k,k.ToUpperInvariant(),Color.cyan)).ToArray());
                chart.SetGeneratedContent(new[] { NoteData.Tap(5,1),NoteData.Hold(0,2,4) },6);
                var source = originalCatalog[0];
                song.SetGeneratedDefaults("six-lane-test","Six lane test",source.AudioClip,chart,mode,new SongTiming());
                mode.name = "six-lane-mode";
                catalog.SetGeneratedDefaults(new[] { song });
                flow.Library.Register(new BuiltInSongProvider(catalog));
                flow.Selection.UpdateSelection(song.SongId,flow.Settings.defaultDifficulty,ScrollMode.Constant,1); flow.PlaySelected();
                yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
                var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
                Assert.That(game.Input.LaneCount, Is.EqualTo(6)); Assert.That(game.ReadModel.LaneCount, Is.EqualTo(6));
                var field = UnityEngine.Object.FindFirstObjectByType<PlayfieldPresenter>().transform;
                Assert.That(field.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Lane ")), Is.EqualTo(6));
                Assert.That(field.Cast<Transform>().Count(t=>t.gameObject.activeSelf && t.name.StartsWith("Rail ")), Is.EqualTo(7));
                var hud = UnityEngine.Object.FindFirstObjectByType<RhythmHud>();
                Assert.That(hud.GetComponentsInChildren<Image>().Count(i=>i.name.StartsWith("Lane ")), Is.EqualTo(6));
                game.StartSession();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.K)); yield return null;
                Assert.That(game.Session.IsHeld(5), Is.True);
            }
            finally
            {
                flow.Settings.catalog=originalCatalog;
                UnityEngine.Object.Destroy(mode); UnityEngine.Object.Destroy(chart);
                UnityEngine.Object.Destroy(song); UnityEngine.Object.Destroy(catalog);
            }
        }

        [UnityTest]
        public IEnumerator KeyboardNavigationAndStartRestartFocusLossUseActualActions()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
            EventSystem.current.SetSelectedGameObject(view.Rows.First(r => r.Entry.Id == "test-pulse").gameObject);
            yield return Press(Key.DownArrow);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<AppFlowController>().Selection.SelectedSongId, Is.EqualTo("tempo-pulse"));
            var flow = UnityEngine.Object.FindFirstObjectByType<AppFlowController>();
            flow.UpdateSelection(0,flow.Settings.defaultDifficulty,ScrollMode.Constant,1); flow.PlaySelected();
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game = UnityEngine.Object.FindFirstObjectByType<RhythmGameController>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space)); yield return null;
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Playing)); Assert.That(game.Clock.Running, Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F)); yield return null;
            game.StartSession(); Assert.That(game.Session.IsHeld(1), Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F)); yield return null;
            Assert.That(game.Session.IsHeld(1), Is.False);
            game.SendMessage("OnApplicationFocus",false);
            Assert.That(game.Session.State, Is.EqualTo(SessionState.Ready)); Assert.That(game.Clock.Running, Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null;
            yield return Await(()=>UnityEngine.Object.FindFirstObjectByType<SongSelectionScreen>());
        }
    }
}
