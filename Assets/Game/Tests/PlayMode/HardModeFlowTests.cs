using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    [Category("UI")]
    public sealed class HardModeFlowTests : GameplayTestContext
    {
        private IEnumerator OpenGameplaySettings()
        {
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().settingsButton);
            yield return Await(()=>Object.FindFirstObjectByType<SettingsView>()); yield return null;
            yield return Click(Object.FindFirstObjectByType<SettingsView>().gameplayTab);
        }
        private IEnumerator SelectHardMode()
        {
            var view=Object.FindFirstObjectByType<SongSelectionView>();
            yield return Click(view.modeButton); yield return Click(view.hardModeButton);
        }
        [UnityTest]
        public IEnumerator ExpandedRuleDropdownsRenderAllKoreanLabelsAndAllowSelection()
        {
            yield return OpenGameplaySettings();
            var view = Object.FindFirstObjectByType<SettingsView>();
            Assert.That(view.playRuleLists.Length, Is.EqualTo(5));
            for (int i = 0; i < view.playRuleLists.Length; i++)
            {
                var dropdown = view.playRuleLists[i];
                EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
                yield return Press(Key.Enter); yield return new WaitForSecondsRealtime(.18f);
                var items = dropdown.GetComponentsInChildren<Toggle>().Where(item => item.isActiveAndEnabled).ToArray();
                Assert.That(items.Length, Is.EqualTo(i == 4 ? 3 : 2));
                Assert.That(items.All(item => item.GetComponentInChildren<Text>().canvasRenderer.GetMesh().vertexCount > 0), Is.True);
                EventSystem.current.SetSelectedGameObject(items[1].gameObject);
                yield return Press(Key.Enter); yield return new WaitForSecondsRealtime(.18f);
                Assert.That(dropdown.value, Is.EqualTo(1));
            }
            CaptureSelection("play-rules-settings");
        }

        [UnityTest]
        public IEnumerator HardRuleRowsSaveValidateCountsAndNormalModeIgnoresThem()
        {
            yield return OpenGameplaySettings();
            var view = Object.FindFirstObjectByType<SettingsView>();
            var flow = Object.FindFirstObjectByType<AppFlowController>();
            view.playRuleLists[2].value = 1;
            view.playRuleLists[0].value = 1;
            Assert.That(flow.Rules.Snapshot.HoldEnd, Is.EqualTo(HoldEndMode.Release));
            view.playRuleLists[0].value = 0;
            Assert.That(flow.Rules.Snapshot.HoldEnd, Is.EqualTo(HoldEndMode.Release));
            view.playRuleLists[4].value = 1;
            Assert.That(flow.Rules.Snapshot.GoodLimit, Is.EqualTo(3));
            Assert.That(flow.Rules.Snapshot.MissLimit, Is.Zero);
            yield return Click(view.back); yield return Await(() => !Object.FindFirstObjectByType<SettingsView>());
            yield return OpenGameplaySettings(); view = Object.FindFirstObjectByType<SettingsView>();
            Assert.That(view.playRuleLists[2].value, Is.EqualTo(1));
            Assert.That(view.playRuleLists[4].value, Is.EqualTo(1));
            yield return Click(view.back); yield return Await(() => !Object.FindFirstObjectByType<SettingsView>());
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(() => Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.Rules.Mode, Is.EqualTo(RhythmDojo.Gameplay.PlayMode.Gauge));
            Assert.That(flow.CurrentRequest.Rules.HoldEnd, Is.EqualTo(HoldEndMode.Release));
        }
        [UnityTest]
        public IEnumerator ConfiguredHardModeDoesNotRecoverAndPausesExactlyAtTheDamageLimit()
        {
            yield return OpenGameplaySettings(); var view=Object.FindFirstObjectByType<SettingsView>();
            view.playRuleLists[4].value = 1;
            yield return Click(view.back); yield return Await(()=>!Object.FindFirstObjectByType<SettingsView>());
            yield return SelectHardMode();
            Assert.That(HardModePreferences.Load().Enabled,Is.True);
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var flow=Object.FindFirstObjectByType<AppFlowController>(); var game=Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(flow.CurrentRequest.IsHardMode,Is.True);
            Assert.That(flow.CurrentRequest.Rules.GoodLimit,Is.EqualTo(3));
            var windows=flow.CurrentRequest.Rules.Timing; double error=(windows.PerfectWindow+windows.GoodWindow)/2;
            var inputs=new List<(double time,int lane,bool press)>(); int goodTaps=0;
            for(int i=0;i<game.Chart.Count;i++)
            {
                var note=game.Chart[i]; bool good=note.Kind==NoteKind.Tap && goodTaps++<4;
                inputs.Add((note.StartTime+(good ? error : 0),note.Lane,true));
                inputs.Add((note.Kind==NoteKind.Hold ? note.EndTime : note.StartTime+(good ? error : 0)+.01,note.Lane,false));
            }
            game.StartSession();
            foreach(var input in inputs.OrderBy(item=>item.time).ThenBy(item=>item.press))
            {
                double health=game.Session.Snapshot.HealthRatio; int perfect=game.Session.Perfect;
                if(input.press) game.Session.PressLane(input.lane,input.time); else game.Session.ReleaseLane(input.lane,input.time);
                if(game.Session.Perfect>perfect) Assert.That(game.Session.Snapshot.HealthRatio,Is.EqualTo(health));
                if(game.Session.State==SessionState.GameOver) break;
            }
            yield return null; yield return null;
            Assert.That(game.Session.Good,Is.EqualTo(4)); Assert.That(game.Session.Miss,Is.Zero);
            Assert.That(game.Session.Health,Is.EqualTo(100)); Assert.That(game.Clock.Running,Is.False);
            Assert.That(Object.FindFirstObjectByType<GameplayHudView>().gameOverPanel.activeSelf,Is.True);
            yield return Press(Key.Space); yield return null;
            Assert.That(game.Session.Health,Is.EqualTo(100)); Assert.That(game.Session.State,Is.EqualTo(SessionState.Playing));
            yield return Press(Key.Escape); yield return Await(()=>Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(Object.FindFirstObjectByType<SongSelectionScreen>().HardModeSelected,Is.True);
        }
        [UnityTest]
        public IEnumerator StrictHardDefaultsFailOnTheFirstGoodJudgment()
        {
            var configured = Object.FindFirstObjectByType<AppFlowController>().Rules.Snapshot; configured.SelectChallenge(3);
            Object.FindFirstObjectByType<AppFlowController>().Rules.Update(configured);
            yield return SelectHardMode(); yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game=Object.FindFirstObjectByType<RhythmGameController>(); var flow=Object.FindFirstObjectByType<AppFlowController>();
            var windows=flow.CurrentRequest.Rules.Timing; var first=game.Chart[0];
            Assert.That(first.Kind,Is.EqualTo(NoteKind.Tap));
            game.StartSession(); game.Session.PressLane(first.Lane,first.StartTime+(windows.PerfectWindow+windows.GoodWindow)/2);
            yield return null; yield return null;
            Assert.That(game.Session.Good,Is.EqualTo(1)); Assert.That(game.Session.State,Is.EqualTo(SessionState.GameOver));
            Assert.That(game.Clock.Running,Is.False);
        }
    }
}
