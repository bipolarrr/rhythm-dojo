using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Core
{
    public sealed class PlayNoteResult
    {
        public int Index { get; }
        public NoteData Note { get; }
        public NoteState State { get; }
        public IReadOnlyList<JudgmentEvent> Judgments { get; }
        public Judgment? Grade { get; }

        public PlayNoteResult(int index, NoteData note, NoteState state, IList<JudgmentEvent> judgments)
        {
            Index = index;
            Note = note;
            State = state;
            var copy = new JudgmentEvent[judgments.Count];
            judgments.CopyTo(copy, 0);
            Judgments = Array.AsReadOnly(copy);
            Judgment? grade = null;

            foreach (var judgment in copy)
            {
                if (grade == null || judgment.Grade > grade.Value)
                {
                    grade = judgment.Grade;
                }
            }

            Grade = grade;
        }
    }
}
