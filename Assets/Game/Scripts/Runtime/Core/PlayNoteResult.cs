using System;
using System.Collections.Generic;
using System.Collections;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Core
{
    public sealed class PlayNoteResult : IReadOnlyList<JudgmentEvent>
    {
        public int Index { get; }
        public NoteData Note { get; }
        public NoteState State { get; }
        public IReadOnlyList<JudgmentEvent> Judgments { get; }
        public Judgment? Grade { get; }
        public JudgmentEvent? Start { get; }
        public JudgmentEvent? End { get; }
        public int Count
        {
            get
            {
                int count = 0;
                if (Start.HasValue)
                {
                    count++;
                }

                if (End.HasValue)
                {
                    count++;
                }

                return count;
            }
        }

        public JudgmentEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (index == 0 && Start.HasValue)
                {
                    return Start.Value;
                }

                return End.Value;
            }
        }

        public IEnumerator<JudgmentEvent> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public PlayNoteResult(int index, NoteData note, NoteState state, NoteJudgmentRecord judgments)
        {
            Index = index;
            Note = note;
            State = state;
            Start = judgments.Start;
            End = judgments.End;
            Judgments = this;
            Grade = Start?.Grade;

            if (End.HasValue)
            {
                if (!Grade.HasValue || End.Value.Grade > Grade.Value)
                {
                    Grade = End.Value.Grade;
                }
            }
        }

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

            if (copy.Length > 0)
            {
                Start = copy[0];
            }

            if (note.Kind == NoteKind.Hold && copy.Length > 1)
            {
                End = copy[copy.Length - 1];
            }
        }
    }
}
