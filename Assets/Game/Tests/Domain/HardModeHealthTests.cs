using System;
using NUnit.Framework;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class HardModeHealthTests
    {
        private static HardModeHealthRule Rule(HealthRuleAction action, int count = 10) => new HardModeHealthRule(action,count);
        private static RhythmSession Session(HardModeHealthRules rules, params NoteData[] notes) =>
            new RhythmSession(new ChartData(notes,10000),new JudgmentSettings(.05,.11),new GameModeRules(4),rules);
        [TestCase(Judgment.Perfect,HealthRuleAction.Preserve)]
        [TestCase(Judgment.Good,HealthRuleAction.Preserve)]
        [TestCase(Judgment.Miss,HealthRuleAction.Preserve)]
        [TestCase(Judgment.Perfect,HealthRuleAction.Damage)]
        [TestCase(Judgment.Good,HealthRuleAction.Damage)]
        [TestCase(Judgment.Miss,HealthRuleAction.Damage)]
        [TestCase(Judgment.Perfect,HealthRuleAction.GameOver)]
        [TestCase(Judgment.Good,HealthRuleAction.GameOver)]
        [TestCase(Judgment.Miss,HealthRuleAction.GameOver)]
        public void EveryGradeCanPreserveDamageOrFail(Judgment grade, HealthRuleAction action)
        {
            var rule=Rule(action,4); var s=Session(new HardModeHealthRules(rule,rule,rule),NoteData.Tap(0,1)); s.Start();
            if (grade==Judgment.Miss) s.Advance(1.12); else s.PressLane(0,grade==Judgment.Good ? 1.08 : 1);
            Assert.That(s.Health,Is.EqualTo(action==HealthRuleAction.Preserve ? 100 : action==HealthRuleAction.Damage ? 75 : 0));
            Assert.That(s.State,Is.EqualTo(action==HealthRuleAction.GameOver ? SessionState.GameOver : SessionState.Playing));
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(7)] [TestCase(10)] [TestCase(100)] [TestCase(1000)]
        public void FractionalDamageFailsExactlyAtConfiguredCount(int count)
        {
            var notes=new NoteData[count+1]; for (int i=0;i<notes.Length;i++) notes[i]=NoteData.Tap(0,i+1);
            var s=Session(new HardModeHealthRules(Rule(HealthRuleAction.Preserve),Rule(HealthRuleAction.Damage,count),Rule(HealthRuleAction.GameOver)),notes);
            s.Start();
            for(int i=0;i<count;i++)
            {
                s.PressLane(0,i+1.08); s.ReleaseLane(0,i+1.09);
                Assert.That(s.Snapshot.HealthRatio,Is.EqualTo(1-(i+1d)/count).Within(1e-8));
                Assert.That(s.State,Is.EqualTo(i+1==count ? SessionState.GameOver : SessionState.Playing));
            }
            Assert.That(s.Good,Is.EqualTo(count)); Assert.That(s.Miss,Is.Zero);
            Assert.That(s.GetState(count),Is.EqualTo(NoteState.Pending));
        }
        [Test]
        public void PreservedGradesNeverRecoverHealthAndResetRestoresTheBudget()
        {
            var s=Session(new HardModeHealthRules(Rule(HealthRuleAction.Preserve),Rule(HealthRuleAction.Damage,4),Rule(HealthRuleAction.Preserve)),
                NoteData.Tap(0,1),NoteData.Tap(0,2),NoteData.Tap(0,3)); s.Start();
            s.PressLane(0,1.08); s.ReleaseLane(0,1.09); s.PressLane(0,2); s.ReleaseLane(0,2.01); s.Advance(3.12);
            Assert.That(s.Health,Is.EqualTo(75)); Assert.That(s.Perfect,Is.EqualTo(1)); Assert.That(s.Miss,Is.EqualTo(1));
            s.Start(); Assert.That(s.Health,Is.EqualTo(100));
            s.PressLane(0,1.08); Assert.That(s.Health,Is.EqualTo(75));
        }
        [Test]
        public void MixedGradesAddTheirIndividualDamageFractions()
        {
            var s=Session(new HardModeHealthRules(Rule(HealthRuleAction.Damage,10),Rule(HealthRuleAction.Damage,5),Rule(HealthRuleAction.Preserve)),
                NoteData.Tap(0,1),NoteData.Tap(0,2),NoteData.Tap(0,3)); s.Start();
            s.PressLane(0,1); s.ReleaseLane(0,1.01); s.PressLane(0,2.08); s.ReleaseLane(0,2.09); s.Advance(3.12);
            Assert.That(s.Health,Is.EqualTo(70)); Assert.That(s.Snapshot.HealthRatio,Is.EqualTo(.7).Within(1e-8));
        }
        [Test]
        public void HoldsApplyTheWorseGradeOnlyOnceAtResolution()
        {
            var s=Session(new HardModeHealthRules(Rule(HealthRuleAction.Preserve),Rule(HealthRuleAction.Damage,3),Rule(HealthRuleAction.GameOver)),NoteData.Hold(0,1,2));
            s.Start(); s.PressLane(0,1.08); Assert.That(s.Health,Is.EqualTo(100));
            s.ReleaseLane(0,2); Assert.That(s.Snapshot.HealthRatio,Is.EqualTo(2d/3).Within(1e-8));
            s.ReleaseLane(0,2.1); s.Advance(4); Assert.That(s.Good,Is.EqualTo(1));
            Assert.That(s.Snapshot.HealthRatio,Is.EqualTo(2d/3).Within(1e-8));
            s.Start(); s.PressLane(0,1); s.ReleaseLane(0,1.5);
            Assert.That(s.State,Is.EqualTo(SessionState.GameOver)); Assert.That(s.Miss,Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(1001)]
        public void InvalidCountsAreRejectedAtTheDomainBoundary(int count) =>
            Assert.Throws<ArgumentOutOfRangeException>(()=>Rule(HealthRuleAction.Damage,count));
        [Test]
        public void InvalidActionsAndGradesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>Rule((HealthRuleAction)99));
            var rules=new HardModeHealthRules(Rule(HealthRuleAction.Preserve),Rule(HealthRuleAction.GameOver),Rule(HealthRuleAction.GameOver));
            Assert.Throws<ArgumentOutOfRangeException>(()=>rules.GetRule((Judgment)99));
        }
    }
}
