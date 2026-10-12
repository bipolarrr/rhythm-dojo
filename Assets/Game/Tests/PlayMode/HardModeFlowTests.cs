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
            var view=Object.FindFirstObjectByType<SettingsView>();
            var expected=new[] { "체력 유지", "체력 감소", "즉시 게임 오버" };
            for(int i=0;i<view.hardRuleLists.Length;i++)
            {
                var dropdown=view.hardRuleLists[i];
                EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
                yield return Press(Key.Enter); yield return new WaitForSecondsRealtime(.18f);
                Canvas.ForceUpdateCanvases();
                var items=dropdown.GetComponentsInChildren<Toggle>().Where(item=>item.isActiveAndEnabled).ToArray();
                Assert.That(items.Length,Is.EqualTo(3));
                var labels=items.Select(item=>item.GetComponentInChildren<Text>()).ToArray();
                Assert.That(labels.Select(label=>label.text),Is.EqualTo(expected));
                foreach(var label in labels)
                {
                    Assert.That(label.rectTransform.rect.height,Is.GreaterThanOrEqualTo(label.preferredHeight),"Dropdown label must fit its complete line.");
                    Assert.That(label.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),"The opened option must actually render text geometry.");
                    Assert.That(label.color.r,Is.GreaterThan(.8f));
                    Assert.That(label.font.HasCharacter('체'),Is.True);
                }
                Assert.That(dropdown.captionText.rectTransform.rect.height,Is.GreaterThanOrEqualTo(dropdown.captionText.preferredHeight));
                EventSystem.current.SetSelectedGameObject(items[1].gameObject);
                yield return Press(Key.Enter); yield return new WaitForSecondsRealtime(.18f);
                Assert.That(dropdown.value,Is.EqualTo((int)HealthRuleAction.Damage));
                Assert.That(Object.FindFirstObjectByType<AppFlowController>().HardMode.GetRule((Judgment)i).Action,Is.EqualTo(HealthRuleAction.Damage));
            }
        }

        [UnityTest]
        public IEnumerator HardRuleRowsSaveValidateCountsAndNormalModeIgnoresThem()
        {
            yield return OpenGameplaySettings();
            var view=Object.FindFirstObjectByType<SettingsView>();
            var flow=Object.FindFirstObjectByType<AppFlowController>();
            Assert.That(view.hardRuleLists.Length,Is.EqualTo(3));
            Assert.That(view.hardLossCounts.All(field=>!field.interactable),Is.True);
            view.hardRuleLists[0].value=(int)HealthRuleAction.Damage;
            Assert.That(view.hardLossCounts[0].interactable,Is.True);
            view.hardLossCounts[0].onEndEdit.Invoke("7");
            view.hardLossCounts[0].onEndEdit.Invoke(""); Assert.That(view.hardLossCounts[0].text,Is.EqualTo("7"));
            view.hardLossCounts[0].onEndEdit.Invoke("0"); Assert.That(view.hardLossCounts[0].text,Is.EqualTo("1"));
            view.hardLossCounts[0].onEndEdit.Invoke("9999"); Assert.That(view.hardLossCounts[0].text,Is.EqualTo("1000"));
            view.hardLossCounts[0].onEndEdit.Invoke("7");
            view.hardRuleLists[1].value=(int)HealthRuleAction.Damage; view.hardLossCounts[1].onEndEdit.Invoke("3");
            Assert.That(HardModePreferences.Load().GetRule(Judgment.Good).LossCount,Is.EqualTo(3));
            Assert.That(flow.HardMode.Enabled,Is.False);
            for(int i=0;i<3;i++)
            {
                var rect=(RectTransform)view.hardLossCounts[i].transform;
                Assert.That(rect.anchoredPosition.y-rect.rect.height,Is.GreaterThanOrEqualTo(-((RectTransform)view.gameplayPanel.transform).rect.height));
            }
            yield return Click(view.back); yield return Await(()=>!Object.FindFirstObjectByType<SettingsView>());
            yield return OpenGameplaySettings(); view=Object.FindFirstObjectByType<SettingsView>();
            Assert.That(view.hardRuleLists[0].value,Is.EqualTo((int)HealthRuleAction.Damage));
            Assert.That(view.hardLossCounts[0].text,Is.EqualTo("7")); Assert.That(view.hardLossCounts[1].text,Is.EqualTo("3"));
            yield return Click(view.back); yield return Await(()=>!Object.FindFirstObjectByType<SettingsView>());
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>Object.FindFirstObjectByType<RhythmGameController>());
            Assert.That(flow.CurrentRequest.IsHardMode,Is.False); Assert.That(flow.CurrentRequest.HardModeRules,Is.Null);
        }
        [UnityTest]
        public IEnumerator ConfiguredHardModeDoesNotRecoverAndPausesExactlyAtTheDamageLimit()
        {
            yield return OpenGameplaySettings(); var view=Object.FindFirstObjectByType<SettingsView>();
            view.hardRuleLists[1].value=(int)HealthRuleAction.Damage; view.hardLossCounts[1].onEndEdit.Invoke("3");
            yield return Click(view.back); yield return Await(()=>!Object.FindFirstObjectByType<SettingsView>());
            yield return SelectHardMode();
            Assert.That(HardModePreferences.Load().Enabled,Is.True);
            yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var flow=Object.FindFirstObjectByType<AppFlowController>(); var game=Object.FindFirstObjectByType<RhythmGameController>();
            Assert.That(flow.CurrentRequest.IsHardMode,Is.True);
            Assert.That(flow.CurrentRequest.HardModeRules.Good.LossCount,Is.EqualTo(3));
            var windows=flow.CurrentRequest.Difficulty.ToSettings(); double error=(windows.PerfectWindow+windows.GoodWindow)/2;
            var inputs=new List<(double time,int lane,bool press)>(); int goodTaps=0;
            for(int i=0;i<game.Chart.Count;i++)
            {
                var note=game.Chart[i]; bool good=note.Kind==NoteKind.Tap && goodTaps++<3;
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
            Assert.That(game.Session.Good,Is.EqualTo(3)); Assert.That(game.Session.Miss,Is.Zero);
            Assert.That(game.Session.Health,Is.Zero); Assert.That(game.Clock.Running,Is.False);
            Assert.That(Object.FindFirstObjectByType<GameplayHudView>().gameOverPanel.activeSelf,Is.True);
            yield return Press(Key.Space); yield return null;
            Assert.That(game.Session.Health,Is.EqualTo(100)); Assert.That(game.Session.State,Is.EqualTo(SessionState.Playing));
            yield return Press(Key.Escape); yield return Await(()=>Object.FindFirstObjectByType<SongSelectionScreen>());
            Assert.That(Object.FindFirstObjectByType<SongSelectionScreen>().HardModeSelected,Is.True);
        }
        [UnityTest]
        public IEnumerator StrictHardDefaultsFailOnTheFirstGoodJudgment()
        {
            yield return SelectHardMode(); yield return Click(Object.FindFirstObjectByType<SongSelectionView>().play);
            yield return Await(()=>Object.FindFirstObjectByType<RhythmGameController>()); yield return null;
            var game=Object.FindFirstObjectByType<RhythmGameController>(); var flow=Object.FindFirstObjectByType<AppFlowController>();
            var windows=flow.CurrentRequest.Difficulty.ToSettings(); var first=game.Chart[0];
            Assert.That(first.Kind,Is.EqualTo(NoteKind.Tap));
            game.StartSession(); game.Session.PressLane(first.Lane,first.StartTime+(windows.PerfectWindow+windows.GoodWindow)/2);
            yield return null; yield return null;
            Assert.That(game.Session.Good,Is.EqualTo(1)); Assert.That(game.Session.State,Is.EqualTo(SessionState.GameOver));
            Assert.That(game.Clock.Running,Is.False);
        }
    }
}
