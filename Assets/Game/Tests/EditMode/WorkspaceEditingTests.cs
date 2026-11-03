using System;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class WorkspaceEditingTests
    {
        private EditorSession session;
        private Guid chartId;
        private WorkspaceNoteEditing editing;

        [SetUp]
        public void SetUp()
        {
            session = new EditorSession(new SongProject());
            session.Edit(edit =>
            {
                chartId = edit.AddChartSlot(ChartSlot.Standard, "FourLaneMode", 4, 12);
                edit.AddNote(chartId, NoteData.Tap(0, 1));
                edit.AddNote(chartId, NoteData.Hold(1, 2, 3));
            });
            editing = new WorkspaceNoteEditing();
        }

        [Test]
        public void MoveGroupKeepsSpacingIdsAndSelectionWithOneUndo()
        {
            editing.Select(session, chartId);
            var ids = session.GetNotes(chartId).Select(note => note.Id).ToArray();
            Assert.That(editing.MoveNotes(session, chartId, ids, 1, 13), Is.True);
            var notes = session.GetNotes(chartId);
            Assert.That(notes.Select(note => note.Id), Is.EqualTo(ids));
            Assert.That(notes[0].Data.Lane, Is.EqualTo(1));
            Assert.That(notes[0].Data.StartTime, Is.EqualTo(14));
            Assert.That(notes[1].Data.Lane, Is.EqualTo(2));
            Assert.That(notes[1].Data.StartTime, Is.EqualTo(15));
            Assert.That(notes[1].Data.EndTime, Is.EqualTo(16));
            Assert.That(editing.Selected, Is.EquivalentTo(ids));
            Assert.That(session.Snapshot.Charts[0].CompletionTimeSeconds, Is.GreaterThan(16));
            session.Undo();
            notes = session.GetNotes(chartId);
            Assert.That(notes[0].Data.StartTime, Is.EqualTo(1));
            Assert.That(notes[1].Data.StartTime, Is.EqualTo(2));
            Assert.That(session.Snapshot.Charts[0].CompletionTimeSeconds, Is.EqualTo(12));
            session.Redo();
            Assert.That(session.GetNotes(chartId)[1].Data.EndTime, Is.EqualTo(16));
        }

        [Test]
        public void MoveGroupRejectsCollisionAndOutOfBoundsWithoutPartialChanges()
        {
            var ids = session.GetNotes(chartId).Select(note => note.Id).ToArray();
            session.Edit(edit => edit.AddNote(chartId, NoteData.Tap(2, 4)));
            Assert.That(editing.MoveNotes(session, chartId, ids, 1, 1.5), Is.False);
            Assert.That(editing.MoveNotes(session, chartId, ids, -1, 1), Is.False);
            Assert.That(editing.MoveNotes(session, chartId, ids, 3, 1), Is.False);
            Assert.That(editing.MoveNotes(session, chartId, ids, 0, -2), Is.False);
            Assert.That(session.GetNotes(chartId)[0].Data.StartTime, Is.EqualTo(1));
            Assert.That(session.GetNotes(chartId)[1].Data.Lane, Is.EqualTo(1));
            session.Undo();
            Assert.That(session.GetNotes(chartId).Length, Is.EqualTo(2));
        }

        [Test]
        public void MoveGroupIgnoresOriginalPositionsOfOtherMovingNotes()
        {
            long id = 0;
            session.Edit(edit => id = edit.AddNote(chartId, NoteData.Tap(0, 2)));
            var first = session.GetNotes(chartId).First(note => note.Data.StartTime == 1);
            Assert.That(editing.MoveNotes(session, chartId, new[] { first.Id, id }, 0, 1), Is.True);
            Assert.That(session.GetNotes(chartId).Single(note => note.Id == first.Id).Data.StartTime, Is.EqualTo(2));
            Assert.That(session.GetNotes(chartId).Single(note => note.Id == id).Data.StartTime, Is.EqualTo(3));
        }

        [Test]
        public void MovePreservesNoteIdAndHoldDurationWithOneUndo()
        {
            var note = session.GetNotes(chartId)[1];
            Assert.That(editing.MoveNote(session, chartId, note.Id, 3, 14), Is.True);
            var moved = session.GetNotes(chartId).Single(item => item.Id == note.Id);
            Assert.That(moved.Data.Lane, Is.EqualTo(3));
            Assert.That(moved.Data.StartTime, Is.EqualTo(14));
            Assert.That(moved.Data.EndTime, Is.EqualTo(15));
            Assert.That(session.Snapshot.Charts[0].CompletionTimeSeconds, Is.GreaterThan(15));
            session.Undo();
            var restored = session.GetNotes(chartId).Single(item => item.Id == note.Id);
            Assert.That(restored.Data.Lane, Is.EqualTo(1));
            Assert.That(restored.Data.StartTime, Is.EqualTo(2));
            Assert.That(session.Snapshot.Charts[0].CompletionTimeSeconds, Is.EqualTo(12));
            session.Redo();
            Assert.That(session.GetNotes(chartId).Single(item => item.Id == note.Id).Data.StartTime, Is.EqualTo(14));
        }

        [Test]
        public void MoveRejectsOverlapsAndDoesNotCreateUndoForUnchangedNotes()
        {
            var note = session.GetNotes(chartId)[0];
            Assert.That(editing.MoveNote(session, chartId, note.Id, 1, 2.5), Is.False);
            Assert.That(editing.MoveNote(session, chartId, note.Id, 0, 1), Is.True);
            session.Undo();
            Assert.That(session.Snapshot.Charts, Is.Empty);
        }

        [Test]
        public void PastePreservesSpacingHoldDurationAndUndoBoundary()
        {
            editing.Select(session, chartId);
            editing.Copy(session, chartId);
            editing.Paste(session, chartId, 10);
            var notes = session.GetNotes(chartId);
            Assert.That(notes.Length, Is.EqualTo(4));
            Assert.That(notes[2].Data.StartTime, Is.EqualTo(10));
            Assert.That(notes[3].Data.StartTime, Is.EqualTo(11));
            Assert.That(notes[3].Data.EndTime, Is.EqualTo(12));
            Assert.That(session.Snapshot.Charts[0].CompletionTimeSeconds, Is.GreaterThan(12));
            Assert.That(editing.Selected.Count, Is.EqualTo(2));
            session.Undo();
            Assert.That(session.GetNotes(chartId).Length, Is.EqualTo(2));
        }

        [Test]
        public void MirrorOnlyChangesSelectedNotesAndCanBeUndone()
        {
            editing.Select(session, chartId, NoteKind.Hold);
            editing.Remap(session, chartId, new[] { 3, 2, 1, 0 });
            var notes = session.GetNotes(chartId);
            Assert.That(notes[0].Data.Lane, Is.Zero);
            Assert.That(notes[1].Data.Lane, Is.EqualTo(2));
            Assert.That(notes[1].Data.EndTime, Is.EqualTo(3));
            session.Undo();
            Assert.That(session.GetNotes(chartId)[1].Data.Lane, Is.EqualTo(1));
        }

        [Test]
        public void DeleteIgnoresStaleSelectionAfterUndoAndSlotChanges()
        {
            editing.Select(session, chartId);
            editing.Delete(session, chartId);
            Assert.That(session.GetNotes(chartId), Is.Empty);
            session.Undo();
            editing.Delete(session, chartId);
            Assert.That(session.GetNotes(chartId).Length, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => editing.Remap(session, chartId, new[] { 0, 0, 1, 2 }));
        }

        [TestCase(90)]
        [TestCase(120)]
        [TestCase(180)]
        public void BpmEstimatorFindsRegularPulsesAcrossStereoChannels(int bpm)
        {
            const int rate = 8000;
            var samples = new float[rate * 16 * 2];
            for (double seconds = 0; seconds < 16; seconds += 60.0 / bpm)
            {
                int frame = (int)(seconds * rate);
                for (int pulse = 0; pulse < 80 && frame + pulse < rate * 16; pulse++)
                {
                    samples[(frame + pulse) * 2] = .8f;
                    samples[(frame + pulse) * 2 + 1] = -.8f;
                }
            }
            Assert.That(AudioBpmEstimator.Estimate(samples, rate, 2, bpm), Is.EqualTo(bpm).Within(2));
        }

        [Test]
        public void BpmEstimatorRejectsSilenceAndShortClips()
        {
            Assert.Throws<InvalidOperationException>(() => AudioBpmEstimator.Estimate(new float[8000 * 5], 8000, 1));
            Assert.Throws<ArgumentException>(() => AudioBpmEstimator.Estimate(new float[8000], 8000, 1));
        }
    }
}
