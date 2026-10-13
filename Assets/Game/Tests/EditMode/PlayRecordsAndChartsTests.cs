using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using SessionState = RhythmDojo.Gameplay.SessionState;

namespace RhythmDojo.Tests
{
    public sealed class PlayRecordsAndChartsTests
    {
        [Test]
        public void RecordMigrationOnlyRemovesOldScores()
        {
            string key = "RhythmDojo.Tests." + Guid.NewGuid();
            try
            {
                PlayerPrefs.SetString(key, "{\"difficulty\":\"Hard\",\"multiplier\":2.5,\"timingOffsetMs\":27,\"favorites\":[\"song\"],\"records\":[{\"songId\":\"song\",\"difficulty\":\"Hard\",\"score\":1000000}]}");
                var preferences = new SelectionPreferences(key);
                Assert.That(preferences.FindRecord("song", "Hard"), Is.Null);
                Assert.That(preferences.IsFavorite("song"), Is.True);
                Assert.That(preferences.Multiplier, Is.EqualTo(2.5)); Assert.That(preferences.TimingOffsetMs, Is.EqualTo(27));
                var failure = new FailureSnapshot(1, FailureReason.GaugeEmpty, true);
                preferences.SaveCompleted("song", "rules", new SessionSnapshot(SessionState.Completed, 1, 0, 9, 1, 10, 0, 0, failure), 1);
                Assert.That(preferences.FindRecord("song", "rules"), Is.Null);
                Assert.That(preferences.FindFailedRecord("song", "rules"), Is.Not.Null);
                var rules = new PlayRules();
                preferences.SaveCompleted("song", "rules", new SessionSnapshot(SessionState.Completed, 9, 1, 0, 10, 10), 10, rules);
                rules.Mode = RhythmDojo.Gameplay.PlayMode.Challenge; rules.SelectChallenge(2);
                preferences.SaveCompleted("song", "rules", new SessionSnapshot(SessionState.Completed, 8, 2, 0, 10, 10), 10, rules);
                var loaded = new SelectionPreferences(key);
                Assert.That(loaded.FindRecord("song", "rules").score, Is.EqualTo(950000));
                Assert.That(loaded.FindRecord("song", "rules").gaugeClear, Is.True);
                Assert.That(loaded.FindRecord("song", "rules").challengeAchievements, Is.EqualTo(2));
                Assert.That(loaded.FindFailedRecord("song", "rules"), Is.Not.Null);
                preferences.SaveCompleted("song", "interrupted", new SessionSnapshot(SessionState.GameOver, 1, 0, 1, 0, 10), 1);
                Assert.That(preferences.FindRecord("song", "interrupted"), Is.Null);
            }
            finally { PlayerPrefs.DeleteKey(key); }
        }
        [Test]
        public void ComparisonSharesModesAndSeparatesTimingTailAndContent()
        {
            var rules = new PlayRules(); string original = RecordIdentity.Rules("chart", "revision", rules);
            rules.Mode = RhythmDojo.Gameplay.PlayMode.Challenge; rules.SelectChallenge(3);
            Assert.That(RecordIdentity.Rules("chart", "revision", rules), Is.EqualTo(original));
            rules.HoldEnd = HoldEndMode.Release;
            Assert.That(RecordIdentity.Rules("chart", "revision", rules), Is.Not.EqualTo(original));
            rules.HoldEnd = HoldEndMode.Automatic; rules.Profile = JudgmentProfile.Sparring;
            Assert.That(RecordIdentity.Rules("chart", "revision", rules), Is.Not.EqualTo(original));
            rules.Profile = JudgmentProfile.Warmup;
            Assert.That(RecordIdentity.Rules("chart", "changed", rules), Is.Not.EqualTo(original));
        }
        [Test]
        public async Task ThreeChartsPreserveSelectionEditsAndUndoAcrossSaveAndLoad()
        {
            string root = Path.Combine(Path.GetTempPath(), "RhythmDojo-Multi-" + Guid.NewGuid());
            try
            {
                var store = new FileSongProjectStore(root); var session = new EditorSession(new SongProject());
                var ids = new Guid[3];
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    session.Edit(edit => { ids[index] = edit.AddChartSlot((ChartSlot)index, "FourLaneMode", 4, 10 + index);
                        edit.AddNote(ids[index], NoteData.Tap(index, 1 + index)); });
                }
                session.SelectChart(ids[2]); session.SelectChart(ids[0]);
                session.Edit(edit => edit.AddNote(ids[0], NoteData.Hold(3, 5, 7)));
                session.SelectChart(ids[1]); session.Undo();
                Assert.That(session.GetNotes(ids[0]).Length, Is.EqualTo(1));
                session.Redo(); Assert.That(session.GetNotes(ids[0]).Length, Is.EqualTo(2));
                Assert.That(session.ActiveChartId, Is.EqualTo(ids[1]));
                await session.SaveAsync(store, default);
                var loaded = await store.LoadAsync(session.Snapshot.Id, default);
                Assert.That(ProjectValidation.ContentEquals(session.Snapshot, loaded), Is.True);
                Assert.That(loaded.Charts.Select(c => c.Slot), Is.EqualTo(new ChartSlot?[] { ChartSlot.Easy, ChartSlot.Standard, ChartSlot.Hard }));
                Assert.Throws<ProjectValidationException>(() => session.Edit(edit => edit.SetChartSlot(ids[0], ChartSlot.Hard)));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        [Test]
        public void LegacyImportIsExplicitAndKeepsIdsAndNotes()
        {
            string text = File.ReadAllText("Docs/examples/complete.rdchart.json"); var json = new SongProjectJson();
            Assert.Throws<ProjectValidationException>(() => json.Deserialize(text));
            var imported = json.ImportVersion1(text); var loaded = json.Deserialize(json.Serialize(imported));
            Assert.That(loaded.Id, Is.EqualTo(imported.Id));
            Assert.That(ProjectValidation.ContentEquals(imported, loaded), Is.True);
        }
    }
}
