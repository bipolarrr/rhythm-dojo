using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Authoring
{
    // File DTO is deliberately separate from mutable editor snapshots and domain structs.
    public sealed class SongProjectJson
    {
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class FileDto
        {
            [JsonProperty("format", Order = 0)] public string Format = "rhythm-dojo-chart";
            [JsonProperty("version", Order = 1)] public int Version = 1;
            [JsonProperty("song", Order = 2)] public SongDto Song;
            [JsonProperty("timing", Order = 3)] public TimingDto Timing;
            [JsonProperty("charts", Order = 4)] public ChartDto[] Charts;
        }
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class SongDto
        {
            [JsonProperty("id", Order = 0)] public string Id;
            [JsonProperty("title", Order = 1)] public string Title;
            [JsonProperty("artist", Order = 2)] public string Artist;
            [JsonProperty("audio", Order = 3)] public string Audio;
        }
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class TimingDto
        {
            [JsonProperty("audioOffsetSeconds", Order = 0)] public double Offset;
            [JsonProperty("tempoPoints", Order = 1)] public TempoDto[] Points;
        }
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class TempoDto
        {
            [JsonProperty("timeSeconds", Order = 0)] public double Time;
            [JsonProperty("bpm", Order = 1)] public double Bpm;
        }
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class ChartDto
        {
            [JsonProperty("id", Order = 0)] public string Id;
            [JsonProperty("name", Order = 1)] public string Name;
            [JsonProperty("modeId", Order = 2)] public string Mode;
            [JsonProperty("laneCount", Order = 3)] public int Lanes;
            [JsonProperty("completionTimeSeconds", Order = 4)] public double Completion;
            [JsonProperty("notes", Order = 5)] public NoteDto[] Notes;
        }
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class NoteDto
        {
            [JsonProperty("kind", Order = 0)] public string Kind;
            [JsonProperty("lane", Order = 1)] public int Lane;
            [JsonProperty("timeSeconds", Order = 2)] public double Time;
            [JsonProperty("endTimeSeconds", Order = 3, NullValueHandling = NullValueHandling.Ignore)] public double? End;
        }
        public string Serialize(SongProject project)
        {
            var p = ProjectValidation.Normalize(project);
            var dto = new FileDto { Song = new SongDto { Id = p.Id.ToString("D"), Title = p.Title, Artist = p.Artist, Audio = p.Audio },
                Timing = new TimingDto { Offset = p.AudioOffsetSeconds, Points = p.TempoPoints.Select(t => new TempoDto { Time = t.StartTimeSeconds, Bpm = t.Bpm }).ToArray() },
                Charts = p.Charts.Select(c => new ChartDto { Id = c.Id.ToString("D"), Name = c.Name, Mode = c.ModeId,
                    Lanes = c.LaneCount, Completion = c.CompletionTimeSeconds,
                    Notes = c.Notes.Select(n => new NoteDto { Kind = n.Kind == NoteKind.Tap ? "tap" : "hold", Lane = n.Lane,
                        Time = n.StartTime, End = n.Kind == NoteKind.Hold ? n.EndTime : (double?)null }).ToArray() }).ToArray() };
            return JsonConvert.SerializeObject(dto, Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        public SongProject Deserialize(string json)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 };
                var root = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Load, LineInfoHandling = LineInfoHandling.Load });
                if (reader.Read()) Fail("$", "Unexpected trailing content.");
                Fields(root, "format", "version", "song", "timing", "charts");
                if (String(root["format"]) != "rhythm-dojo-chart") Fail("format", "Unsupported format.");
                if (Integer(root["version"]) != 1) Fail("version", "Unsupported version.");
                var song = root["song"]; Fields(song, "id", "title", "artist", "audio");
                var timing = root["timing"]; Fields(timing, "audioOffsetSeconds", "tempoPoints");
                var p = new SongProject { Id = Id(song["id"]), Title = String(song["title"]), Artist = String(song["artist"]),
                    Audio = song["audio"].Type == JTokenType.Null ? null : String(song["audio"]),
                    AudioOffsetSeconds = Number(timing["audioOffsetSeconds"]),
                    TempoPoints = Array(timing["tempoPoints"]).Select(t => { Fields(t, "timeSeconds", "bpm"); return new TempoPoint(Number(t["timeSeconds"]), Number(t["bpm"])); }).ToArray(),
                    Charts = Array(root["charts"]).Select(c => {
                        Fields(c, "id", "name", "modeId", "laneCount", "completionTimeSeconds", "notes");
                        return new ChartDocument { Id = Id(c["id"]), Name = String(c["name"]), ModeId = String(c["modeId"]),
                            LaneCount = Integer(c["laneCount"]), CompletionTimeSeconds = Number(c["completionTimeSeconds"]),
                            Notes = Array(c["notes"]).Select(n => {
                                if (n.Type != JTokenType.Object) Fail(n.Path, "Object required.");
                                var kind = String(n["kind"]);
                                if (kind != "tap" && kind != "hold") Fail(n.Path + ".kind", "Unknown note kind.");
                                Fields(n, kind == "tap" ? new[] { "kind", "lane", "timeSeconds" } : new[] { "kind", "lane", "timeSeconds", "endTimeSeconds" });
                                return kind == "tap" ? NoteData.Tap(Integer(n["lane"]), Number(n["timeSeconds"])) :
                                    NoteData.Hold(Integer(n["lane"]), Number(n["timeSeconds"]), Number(n["endTimeSeconds"]));
                            }).ToArray() };
                    }).ToArray() };
                return ProjectValidation.Normalize(p);
            }
            catch (JsonException e) { throw new ProjectValidationException("$", e.Message); }
        }
        private static void Fail(string path, string message) => throw new ProjectValidationException(path, message);
        private static void Fields(JToken token, params string[] names)
        {
            if (!(token is JObject obj)) { Fail(token?.Path ?? "$", "Object required."); return; }
            foreach (var property in obj.Properties()) if (!names.Contains(property.Name)) Fail(property.Path, "Unknown field.");
            foreach (var name in names) if (obj.Property(name) == null) Fail(obj.Path + "." + name, "Required field missing.");
        }
        private static JArray Array(JToken token)
        { if (!(token is JArray)) Fail(token.Path, "Array required."); return (JArray)token; }
        private static string String(JToken token)
        { if (token == null || token.Type != JTokenType.String) Fail(token?.Path ?? "$", "String required."); return (string)token; }
        private static Guid Id(JToken token)
        { if (!Guid.TryParseExact(String(token), "D", out var id) || id == Guid.Empty) Fail(token.Path, "Nonempty UUID required."); return id; }
        private static double Number(JToken token)
        {
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) Fail(token.Path, "Number required.");
            double number = (double)token;
            if (!double.IsFinite(number)) Fail(token.Path, "Finite number required."); return number;
        }
        private static int Integer(JToken token)
        {
            if (token.Type != JTokenType.Integer) Fail(token.Path, "Integer required.");
            var number = Number(token);
            if (number < int.MinValue || number > int.MaxValue) Fail(token.Path, "Integer out of range."); return (int)number;
        }
    }
}
