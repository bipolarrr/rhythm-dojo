using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [Serializable]
    public struct SerializedNote
    {
        [SerializeField] private int lane;
        [SerializeField] private NoteKind kind;
        [SerializeField] private double startTime;
        [SerializeField] private double endTime;
        public NoteData ToData() => new NoteData(lane, kind, startTime, kind == NoteKind.Tap ? startTime : endTime);
        public SerializedNote(NoteData note)
        { lane = note.Lane; kind = note.Kind; startTime = note.StartTime; endTime = note.EndTime; }
    }
    [CreateAssetMenu(menuName = "Rhythm Dojo/Chart")]
    public sealed class RhythmChart : ScriptableObject
    {
        [SerializeField] private SerializedNote[] notes;
        [SerializeField] private double completionTime = 12;
        public int Count => notes?.Length ?? 0;
        public NoteData this[int index] => notes[index].ToData();
        public double CompletionTime => completionTime;
        public ChartData ToChartData()
        {
            if (notes == null) throw new InvalidOperationException("Chart notes missing.");
            var data = new NoteData[notes.Length];
            for (int i = 0; i < data.Length; i++) data[i] = notes[i].ToData();
            return new ChartData(data, completionTime);
        }
        public void Validate(GameModeRules mode) => ToChartData().Validate(mode);
        public void SetGeneratedContent(NoteData[] data, double completion)
        {
            notes = new SerializedNote[data.Length];
            for (int i = 0; i < data.Length; i++) notes[i] = new SerializedNote(data[i]);
            completionTime = completion;
        }
    }
}
