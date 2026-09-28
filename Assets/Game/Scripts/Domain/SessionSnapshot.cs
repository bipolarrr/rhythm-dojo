namespace RhythmDojo.Gameplay
{
    public readonly struct SessionSnapshot
    {
        public SessionState State { get; }
        public int Perfect { get; }
        public int Good { get; }
        public int Miss { get; }
        public int Combo { get; }
        public int Resolved => Perfect + Good + Miss;
        public int TotalNotes { get; }
        public SessionSnapshot(SessionState state, int perfect, int good, int miss, int combo, int totalNotes)
        { State = state; Perfect = perfect; Good = good; Miss = miss; Combo = combo; TotalNotes = totalNotes; }
    }
    public readonly struct JudgmentEvent
    {
        public int NoteIndex { get; }
        public int Lane { get; }
        public NoteKind Kind { get; }
        public Judgment Grade { get; }
        public double ErrorSeconds { get; }
        public JudgmentEvent(int noteIndex, NoteData note, Judgment grade, double errorSeconds)
        { NoteIndex = noteIndex; Lane = note.Lane; Kind = note.Kind; Grade = grade; ErrorSeconds = errorSeconds; }
    }
    public readonly struct HoldStartedEvent
    {
        public JudgmentEvent Head { get; }
        public HoldStartedEvent(JudgmentEvent head) { Head = head; }
    }
}
