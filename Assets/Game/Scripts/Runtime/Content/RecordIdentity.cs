using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Content
{
    public static class RecordIdentity
    {
        public static string Chart(SongDocument document)
        {
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write(document.ModeId ?? ""); writer.Write(document.AudioId ?? "");
                writer.Write(document.AudioOffset); writer.Write(document.CompletionTime);
                writer.Write(document.TempoPoints.Length);
                foreach (var point in document.TempoPoints) { writer.Write(point.StartTimeSeconds); writer.Write(point.Bpm); }
                writer.Write(document.Notes.Length);
                foreach (var note in document.Notes) { writer.Write(note.Lane); writer.Write((int)note.Kind); writer.Write(note.StartTime); writer.Write(note.EndTime); }
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
        }
        public static string Rules(string chartId, string revision, PlayRules rules) =>
            string.Join("|", chartId, revision, PlayRules.CurrentVersion, rules.Profile, rules.HoldEnd,
                rules.Timing.PerfectWindow.ToString("R", CultureInfo.InvariantCulture), rules.Timing.GoodWindow.ToString("R", CultureInfo.InvariantCulture),
                rules.HoldCheckBeats.ToString("R", CultureInfo.InvariantCulture), rules.PerfectWeight.ToString("R", CultureInfo.InvariantCulture),
                rules.GoodWeight.ToString("R", CultureInfo.InvariantCulture), rules.MissWeight.ToString("R", CultureInfo.InvariantCulture));
    }
}
