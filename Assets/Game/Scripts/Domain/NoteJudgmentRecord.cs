namespace RhythmDojo.Gameplay
{
    public readonly struct NoteJudgmentRecord
    {
        public JudgmentEvent? Start { get; }
        public JudgmentEvent? End { get; }

        public NoteJudgmentRecord(JudgmentEvent? start, JudgmentEvent? end)
        {
            Start = start;
            End = end;
        }
    }
}
