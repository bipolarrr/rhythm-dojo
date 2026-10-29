using System.Linq;
using NUnit.Framework;
using RhythmDojo.EditorTools;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class ResultsPreviewTests
    {
        [Test]
        public void ClearPreviewHasMixedJudgmentsAndMaintenanceMisses()
        {
            var result = ResultsPreview.CreateResult(false);
            Assert.That(result.Session.State, Is.EqualTo(SessionState.Completed));
            Assert.That(result.Session.Failure.Failed, Is.False);
            Assert.That(result.Session.Perfect, Is.GreaterThan(0));
            Assert.That(result.Session.Good, Is.GreaterThan(0));
            Assert.That(result.Session.Miss, Is.GreaterThan(0));
            Assert.That(result.Notes[2].Start.Value.Grade, Is.EqualTo(Judgment.Perfect));
            Assert.That(result.Notes[2].End.Value.Grade, Is.EqualTo(Judgment.Perfect));
            Assert.That(result.Notes.Sum(note => note.Judgments.Count), Is.LessThan(result.Session.Resolved));
        }

        [Test]
        public void GameOverPreviewHasEarlyReleaseMissAndUnreachedNotes()
        {
            var result = ResultsPreview.CreateResult(true);
            Assert.That(result.Session.State, Is.EqualTo(SessionState.GameOver));
            Assert.That(result.Session.Failure.Failed, Is.True);
            Assert.That(result.Notes[2].End.Value.Grade, Is.EqualTo(Judgment.Miss));
            Assert.That(result.Notes[3].Start, Is.Null);
            Assert.That(result.Notes[4].End, Is.Null);
        }
    }
}
