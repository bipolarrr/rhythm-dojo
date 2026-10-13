using System;
using System.IO;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    public static class VerificationProjectBuilder
    {
        public static void Prepare()
        {
            var store = new FileSongProjectStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Songs"));
            var project = new SongProject { Id = Guid.Parse("56876aac-7c1c-4bda-9721-79eb8bd161db"), Title = "Three Chart Verification", Artist = "Rhythm Dojo",
                TempoPoints = new[] { new TempoPoint(0, 120), new TempoPoint(6, 180) },
                Charts = new[] {
                    new ChartDocument { Id = Guid.Parse("26876aac-7c1c-4bda-9721-79eb8bd161db"), Slot = ChartSlot.Easy, Name = "Easy", ModeId = "FourLaneMode", LaneCount = 4,
                        CompletionTimeSeconds = 12, Notes = new[] { NoteData.Tap(0, 1), NoteData.Tap(1, 3), NoteData.Tap(2, 5) } },
                    new ChartDocument { Id = Guid.Parse("36876aac-7c1c-4bda-9721-79eb8bd161db"), Slot = ChartSlot.Standard, Name = "Standard", ModeId = "FourLaneMode", LaneCount = 4,
                        CompletionTimeSeconds = 12, Notes = new[] { NoteData.Tap(0, 1), NoteData.Hold(1, 5, 7), NoteData.Tap(2, 8) } },
                    new ChartDocument { Id = Guid.Parse("46876aac-7c1c-4bda-9721-79eb8bd161db"), Slot = ChartSlot.Hard, Name = "Hard", ModeId = "FourLaneMode", LaneCount = 4,
                        CompletionTimeSeconds = 12, Notes = new[] { NoteData.Tap(0, 1), NoteData.Tap(1, 1), NoteData.Hold(2, 4, 8), NoteData.Hold(3, 5, 7), NoteData.Tap(0, 9) } }
                } };
            project.Audio = store.ImportAsync(project.Id, Path.GetFullPath("Assets/Game/Audio/TestPulse.wav"), default).GetAwaiter().GetResult();
            store.SaveAsync(project, default).GetAwaiter().GetResult();
            Debug.Log("Three chart verification project: " + store.GetSongFolder(project.Id));
        }
    }
}
