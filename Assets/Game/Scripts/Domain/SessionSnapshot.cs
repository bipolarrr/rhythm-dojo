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
        public int Health { get; }
        public double HealthRatio { get; }
        public SessionSnapshot(SessionState state, int perfect, int good, int miss, int combo, int totalNotes)
            : this(state, perfect, good, miss, combo, totalNotes, RhythmSession.MaximumHealth) { }
        public SessionSnapshot(SessionState state, int perfect, int good, int miss, int combo, int totalNotes, int health)
            : this(state, perfect, good, miss, combo, totalNotes, health, (double)health / RhythmSession.MaximumHealth) { }
        public SessionSnapshot(SessionState state, int perfect, int good, int miss, int combo, int totalNotes, int health, double healthRatio)
        {
            State = state; Perfect = perfect; Good = good; Miss = miss; Combo = combo; TotalNotes = totalNotes;
            Health = System.Math.Clamp(health, 0, RhythmSession.MaximumHealth);
            HealthRatio = double.IsFinite(healthRatio) ? System.Math.Clamp(healthRatio, 0, 1) : (double)Health / RhythmSession.MaximumHealth;
        }
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
