using System;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Tests
{
    public sealed class BeatGridEditingTests
    {
        [Test]
        public void GridOriginAndTempoChangesRoundTripWithoutAccumulatedDrift()
        {
            var timing = new BeatGridTiming(new[]
            {
                new TempoPoint(0, 123.456),
                new TempoPoint(17.31, 178.93)
            }, new BeatGridSettings { OriginSeconds = 0.347 });

            Assert.That(timing.TimeAtBeat(0), Is.EqualTo(0.347).Within(1e-10));
            foreach (double time in new[] { -1.0, 0, 0.347, 17.31, 1234.567, 7200 })
            {
                Assert.That(timing.TimeAtBeat(timing.BeatAtTime(time)), Is.EqualTo(time).Within(1e-9));
            }
        }

        [Test]
        public void CompoundMeterAndMidBeatMeterChangesEmitSharedMeasureLines()
        {
            var timing = new BeatGridTiming(new[] { new TempoPoint(0, 120) }, new BeatGridSettings
            {
                Meters = new[] { new MeterPoint(0, 3, 8), new MeterPoint(2.125, 5, 4) }
            });

            Assert.That(timing.Lines(0, 4, 1).Where(timing.IsMeasure), Is.EqualTo(new[] { 0.0, 1.5, 3 }));
            Assert.That(timing.Lines(4, 10, 1).Where(timing.IsMeasure), Is.EqualTo(new[] { 4.25, 9.25 }));
            timing.GetMusicalPosition(9.25, out int measure, out double position);
            Assert.That(measure, Is.EqualTo(5));
            Assert.That(position, Is.EqualTo(0).Within(1e-9));
            Assert.That(timing.Lines(0, 4, 1.0 / 3).Where(timing.IsMeasure),
                Is.EqualTo(new[] { 0.0, 1.5, 3 }));
            Assert.That(timing.SnapBeat(4.31, 0.25, 4, 10), Is.EqualTo(4.25).Within(1e-9));
        }

        [Test]
        public void MovingWholeGridShiftsEveryTempoAndMeterBySameMusicTime()
        {
            var project = new SongProject
            {
                TempoPoints = new[] { new TempoPoint(0, 120), new TempoPoint(4, 180) },
                Grid = new BeatGridSettings
                {
                    OriginSeconds = 0.1,
                    Meters = new[] { new MeterPoint(0, 4, 4), new MeterPoint(4, 3, 8) }
                }
            };
            var before = new BeatGridTiming(project.TempoPoints, project.Grid);
            var session = new EditorSession(project);
            BeatGridEditing.ApplyOrigin(session, project.TempoPoints, project.Grid, 0.3, false);
            var after = new BeatGridTiming(session.Snapshot.TempoPoints, session.Snapshot.Grid);
            foreach (double beat in new[] { 0.0, 4, 8, 16, 128 })
            {
                Assert.That(after.TimeAtBeat(beat), Is.EqualTo(before.TimeAtBeat(beat) + 0.2).Within(1e-9));
                Assert.That(after.IsMeasure(beat), Is.EqualTo(before.IsMeasure(beat)));
            }

            Assert.That(session.Snapshot.TempoPoints[1].StartTimeSeconds, Is.EqualTo(4.2));
            session.Undo();
            Assert.That(ProjectValidation.ContentEquals(project, session.Snapshot), Is.True);
        }

        [Test]
        public void LargeCompoundMeterOverviewKeepsGridLinesAtFarEnd()
        {
            var timing = new BeatGridTiming(new[] { new TempoPoint(0, 120) }, new BeatGridSettings
            {
                Meters = new[] { new MeterPoint(0, 3, 8) }
            });
            Assert.That(timing.Lines(0, 7200, 1).Last(), Is.EqualTo(7200));
            var sparse = timing.Lines(0, 7200, 0.25, 600).ToArray();
            Assert.That(sparse.Length, Is.LessThanOrEqualTo(600));
            Assert.That(sparse.Last(), Is.EqualTo(7200));
            Assert.That(sparse.All(timing.IsMeasure), Is.True);
        }

        [Test]
        public void FineMeterDoesNotRemoveTripletSubdivisionLines()
        {
            var timing = new BeatGridTiming(new[] { new TempoPoint(0, 120) }, new BeatGridSettings
            {
                Meters = new[] { new MeterPoint(0, 6, 32) }
            });
            Assert.That(timing.Lines(0, 4, 1.0 / 6)
                .Any(beat => Math.Abs(beat - 1.0 / 6) < 1e-8), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GridEditProtectsNotesAndUndoRestoresAllChartsAndHoldEndpoints(bool preserveBeats)
        {
            var session = new EditorSession(new SongProject { AudioOffsetSeconds = 0.07 });
            Guid first = Guid.Empty;
            Guid second = Guid.Empty;
            session.Edit(edit =>
            {
                first = edit.AddChart("First", "FourLaneMode", 4, 10);
                second = edit.AddChart("Second", "FourLaneMode", 4, 10);
                edit.AddNote(first, NoteData.Hold(0, 1, 2));
                edit.AddNote(second, NoteData.Tap(1, 3));
            });
            var before = session.Snapshot;
            long id = session.GetNotes(first)[0].Id;
            BeatGridEditing.Apply(session, new[] { new TempoPoint(0, 60) },
                new BeatGridSettings { OriginSeconds = 0.25 }, preserveBeats);

            double start = 1;
            double end = 2;
            if (preserveBeats)
            {
                start = 2.25;
                end = 4.25;
            }

            Assert.That(session.GetNotes(first)[0].Data.StartTime, Is.EqualTo(start));
            Assert.That(session.GetNotes(first)[0].Data.EndTime, Is.EqualTo(end));
            Assert.That(session.GetNotes(first)[0].Id, Is.EqualTo(id));
            Assert.That(session.Snapshot.AudioOffsetSeconds, Is.EqualTo(0.07));
            var after = session.Snapshot;
            session.Undo();
            Assert.That(ProjectValidation.ContentEquals(before, session.Snapshot), Is.True);
            session.Redo();
            Assert.That(ProjectValidation.ContentEquals(after, session.Snapshot), Is.True);
        }

        [Test]
        public void InvalidGridEditLeavesDocumentAndHistoryUntouched()
        {
            var session = new EditorSession(new SongProject());
            var before = session.Snapshot;
            Assert.Throws<ArgumentException>(() => BeatGridEditing.Apply(session,
                new[] { new TempoPoint(0, 120), new TempoPoint(1, 0) }, new BeatGridSettings(), true));
            Assert.That(ProjectValidation.ContentEquals(before, session.Snapshot), Is.True);
            Assert.That(session.CanUndo, Is.False);
            Assert.Throws<ArgumentException>(() => BeatGridEditing.ApplyOrigin(session,
                new[] { new TempoPoint(0, 120), new TempoPoint(1, 150) },
                new BeatGridSettings(), -2, false));
            Assert.That(ProjectValidation.ContentEquals(before, session.Snapshot), Is.True);
            Assert.Throws<ArgumentException>(() => session.Edit(edit => edit.SetGrid(new BeatGridSettings
            {
                Meters = new[] { new MeterPoint(0, 4, 3) }
            })));
            Assert.That(ProjectValidation.ContentEquals(before, session.Snapshot), Is.True);
        }

        [Test]
        public void JsonRestoresGridAndLoadsOldV2WithDefaultGrid()
        {
            var json = new SongProjectJson();
            var project = new SongProject();
            var legacy = Newtonsoft.Json.Linq.JObject.Parse(json.Serialize(project));
            legacy["version"] = 2;
            ((Newtonsoft.Json.Linq.JObject)legacy["timing"]).Remove("grid");
            string old = legacy.ToString();
            Assert.That(old, Does.Not.Contain("\"grid\""));
            Assert.That(ProjectValidation.ContentEquals(project, json.Deserialize(old)), Is.True);
            project.Grid = new BeatGridSettings
            {
                OriginSeconds = 0.237,
                Meters = new[] { new MeterPoint(0, 4, 4), new MeterPoint(12.3, 6, 8) }
            };
            project.TempoPoints = new[] { new TempoPoint(0, 127.25), new TempoPoint(12.3, 180) };
            string text = json.Serialize(project);
            Assert.That(ProjectValidation.ContentEquals(project, json.Deserialize(text)), Is.True);
            Assert.That(json.Serialize(json.Deserialize(text)), Is.EqualTo(text));
            Assert.Throws<ProjectValidationException>(() => json.Deserialize(text.Replace(
                "\"denominator\": 8", "\"denominator\": 3")));
            Assert.Throws<ProjectValidationException>(() => json.Deserialize(text.Replace(
                "\"originSeconds\":", "\"unknownGridField\":")));
        }

        [Test]
        public void MalformedTimingStillReportsOriginalValidationFieldPath()
        {
            var json = new SongProjectJson();
            string text = json.Serialize(new SongProject());
            int start = text.IndexOf("  \"timing\":", StringComparison.Ordinal);
            int end = text.IndexOf("  \"charts\":", StringComparison.Ordinal);
            foreach (string value in new[] { "5", "null", "\"bad\"", "[]" })
            {
                string broken = text.Substring(0, start) + "  \"timing\": " + value + ",\n"
                    + text.Substring(end);
                var error = Assert.Throws<ProjectValidationException>(() => json.Deserialize(broken));
                Assert.That(error.FieldPath, Is.EqualTo("timing"));
            }
        }
    }
}
